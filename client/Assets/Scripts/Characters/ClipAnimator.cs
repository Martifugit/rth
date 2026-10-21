using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rth.Client.Characters
{
    /// <summary>
    /// Plays humanoid animation clips on a character without an Animator Controller: a
    /// locomotion blend underneath (idle or combat idle, walk, run) and one-shot clips (cast,
    /// death, or any clip by name) blended over the top. Clips are files in
    /// Resources/Characters/Animations named after what they are, so content can ask for an
    /// animation by name; Resources/Characters/animations.json tunes each one (see
    /// <see cref="ClipTuning"/>), which is how a too-big download is made Old School-small.
    /// </summary>
    public sealed class ClipAnimator
    {
        private const string Folder = "Characters/Animations/";
        private const string TuningFile = "Characters/animations";
        private const float FadeIn = 0.12f;
        private const float FadeOut = 0.25f;
        // Used when a clip has no forward travel to measure (an "in place" download).
        private const float DefaultWalkSpeed = 1.5f, DefaultRunSpeed = 3.5f;

        /// <summary>How one clip is played; every field is optional in animations.json.</summary>
        public sealed class ClipTuning
        {
            /// <summary>0-1: how much of the clip shows over what's beneath it. Below 1 tones it down.</summary>
            public float Weight = 1f;
            /// <summary>Playback speed.</summary>
            public float Speed = 1f;
            /// <summary>Seconds into the clip to start from, and to stop at (0 = its end).</summary>
            public float Start, End;
            /// <summary>Only the spine, arms and head move; the legs keep doing what they were.</summary>
            public bool UpperBody;
        }

        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static Dictionary<string, ClipTuning> _tuning;
        private static AvatarMask _upperBody, _fullBody;

        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layers;
        private AnimationMixerPlayable _locomotion, _stand;
        private AnimationClipPlayable _walk, _run;
        private float _walkSpeed, _runSpeed;
        private AnimationClipPlayable _oneShot;
        private ClipTuning _oneShotTuning;
        private float _oneShotEnd;
        private bool _hold;
        private float _move, _runBlend, _combat;
        private float _combatWeight;

        /// <summary>The clip with this name, or null if there isn't one.</summary>
        public static AnimationClip Clip(string name)
        {
            if (Clips.TryGetValue(name, out AnimationClip cached) && cached != null) return cached;
            AnimationClip found = null;
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(Folder + name))
                if (!clip.name.StartsWith("__preview__")) { found = clip; break; }
            if (found != null) Clips[name] = found;
            return found;
        }

        public static ClipTuning Tuning(string name)
        {
            if (_tuning == null)
            {
                var file = Resources.Load<TextAsset>(TuningFile);
                try
                {
                    _tuning = file != null ? JsonConvert.DeserializeObject<Dictionary<string, ClipTuning>>(file.text) : null;
                }
                catch (JsonException e)
                {
                    Debug.LogWarning("[R'th] Bad Resources/" + TuningFile + ".json: " + e.Message);
                }
                _tuning ??= new Dictionary<string, ClipTuning>();
            }
            return _tuning.TryGetValue(name, out ClipTuning t) ? t : new ClipTuning();
        }

        /// <summary>Whether the clips locomotion needs exist; without them, use another body.</summary>
        public static bool HasLocomotion => Clip("idle") != null && Clip("walk") != null;

        public static bool Has(string name) => Clip(name) != null;

        private static AvatarMask FullBodyMask => _fullBody != null ? _fullBody : _fullBody = new AvatarMask();

        private static AvatarMask UpperBodyMask
        {
            get
            {
                if (_upperBody != null) return _upperBody;
                _upperBody = new AvatarMask();
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    _upperBody.SetHumanoidBodyPartActive(part, part != AvatarMaskBodyPart.Root &&
                        part != AvatarMaskBodyPart.LeftLeg && part != AvatarMaskBodyPart.RightLeg &&
                        part != AvatarMaskBodyPart.LeftFootIK && part != AvatarMaskBodyPart.RightFootIK);
                return _upperBody;
            }
        }

        public ClipAnimator(Animator animator, string name)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            _graph = PlayableGraph.Create(name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);

            AnimationClip idle = Clip("idle"), walk = Clip("walk");
            AnimationClip combatIdle = Clip("idle_combat") ?? idle, run = Clip("run") ?? walk;

            // Standing: relaxed and combat idles, blended by how recently it fought.
            _stand = AnimationMixerPlayable.Create(_graph, 2);
            Connect(Looping(idle, "idle", true), _stand, 0);
            Connect(Looping(combatIdle, "idle_combat", true), _stand, 1);
            _combatWeight = Tuning("idle_combat").Weight;

            _locomotion = AnimationMixerPlayable.Create(_graph, 3);
            _walk = Looping(walk, "walk", false);
            _run = Looping(run, "run", false);
            Connect(_stand, _locomotion, 0);
            Connect(_walk, _locomotion, 1);
            Connect(_run, _locomotion, 2);
            _walkSpeed = TravelSpeed(walk, DefaultWalkSpeed);
            _runSpeed = TravelSpeed(run, DefaultRunSpeed);

            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            Connect(_locomotion, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);
            output.SetSourcePlayable(_layers);
            Update(0f, false, false, 0f);
            _graph.Play();
        }

        private AnimationClipPlayable Looping(AnimationClip clip, string name, bool randomStart)
        {
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetSpeed(Tuning(name).Speed);
            // Start somewhere different in an idle, so a crowd doesn't breathe in step.
            if (randomStart) playable.SetTime(Random.value * clip.length);
            return playable;
        }

        private void Connect(Playable source, Playable target, int input)
        {
            _graph.Connect(source, 0, target, input);
        }

        private static float TravelSpeed(AnimationClip clip, float fallback)
        {
            float speed = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;
            return speed > 0.2f ? speed : fallback;
        }

        /// <summary>
        /// Called every frame with the ground speed in tiles (metres) per second and whether it
        /// is in combat (stands in its combat stance).
        /// </summary>
        public void Update(float speed, bool running, bool inCombat, float deltaTime)
        {
            if (!_graph.IsValid()) return;
            bool moving = speed > 0.2f;
            _move = Mathf.MoveTowards(_move, moving ? 1f : 0f, deltaTime * 6f);
            // Hold the gait while stopping, so a run doesn't turn into a walk as it slows.
            _runBlend = Mathf.MoveTowards(_runBlend, moving ? (running ? 1f : 0f) : _runBlend, deltaTime * 4f);
            _combat = Mathf.MoveTowards(_combat, inCombat ? 1f : 0f, deltaTime * 3f);
            _stand.SetInputWeight(0, 1f - _combat * _combatWeight);
            _stand.SetInputWeight(1, _combat * _combatWeight);
            _locomotion.SetInputWeight(0, 1f - _move);
            _locomotion.SetInputWeight(1, _move * (1f - _runBlend));
            _locomotion.SetInputWeight(2, _move * _runBlend);

            // Step at the rate that covers the ground: feet don't slide or skate.
            if (moving)
            {
                _walk.SetSpeed(Mathf.Clamp(speed / _walkSpeed, 0.5f, 2f) * Tuning("walk").Speed);
                _run.SetSpeed(Mathf.Clamp(speed / _runSpeed, 0.5f, 2f) * Tuning("run").Speed);
            }

            if (_oneShot.IsValid())
            {
                float t = (float)_oneShot.GetTime();
                float start = _oneShotTuning.Start;
                float weight = Mathf.Clamp01((t - start) / FadeIn);
                if (!_hold) weight = Mathf.Min(weight, Mathf.Clamp01((_oneShotEnd - t) / FadeOut));
                else if (t >= _oneShotEnd) _oneShot.SetSpeed(0);   // lie still at the end
                _layers.SetInputWeight(1, weight * _oneShotTuning.Weight);
                if (!_hold && t >= _oneShotEnd) StopOneShot();
            }
        }

        /// <summary>
        /// Plays a clip once over the locomotion; <paramref name="hold"/> keeps its last frame
        /// (death). Returns false if there is no such clip. A held clip isn't interrupted.
        /// </summary>
        public bool Play(string name, bool hold = false)
        {
            if (!_graph.IsValid() || (_hold && _oneShot.IsValid())) return false;
            AnimationClip clip = Clip(name);
            if (clip == null) return false;
            StopOneShot();
            ClipTuning tuning = Tuning(name);
            _oneShot = AnimationClipPlayable.Create(_graph, clip);
            _oneShot.SetApplyFootIK(true);
            _oneShot.SetTime(tuning.Start);
            _oneShot.SetSpeed(tuning.Speed);
            _oneShotTuning = tuning;
            _oneShotEnd = tuning.End > tuning.Start ? Mathf.Min(tuning.End, clip.length) : clip.length;
            _hold = hold;
            _graph.Connect(_oneShot, 0, _layers, 1);
            _layers.SetLayerMaskFromAvatarMask(1, tuning.UpperBody ? UpperBodyMask : FullBodyMask);
            _layers.SetInputWeight(1, 0f);
            return true;
        }

        private void StopOneShot()
        {
            if (!_oneShot.IsValid()) return;
            _graph.Disconnect(_layers, 1);
            _oneShot.Destroy();
            _layers.SetInputWeight(1, 0f);
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
