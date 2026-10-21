using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rth.Client.Characters
{
    /// <summary>
    /// Plays humanoid animation clips on a character without an Animator Controller: a
    /// locomotion blend underneath (idle or combat idle, walk or combat walk, run) and one-shot clips (cast,
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

        /// <summary>
        /// One entry in animations.json, which lists every clip (file) and role: where it came
        /// from, what it is for, and how it is played. Every field is optional.
        /// </summary>
        public sealed class ClipTuning
        {
            /// <summary>Where the file came from (e.g. "Mixamo: Digging"). For people, not code.</summary>
            public string Source;
            /// <summary>What it is meant for. For people, not code.</summary>
            public string Use;
            /// <summary>The file loops (read by the importer).</summary>
            public bool Loop;
            /// <summary>Play this clip file instead (one download can serve several roles).</summary>
            public string Clip;
            /// <summary>Hold the clip still at this many seconds in (a stance taken from one frame).</summary>
            public float? Pose;
            /// <summary>0-1: how much of the clip shows over what's beneath it. Below 1 tones it down.</summary>
            public float Weight = 1f;
            /// <summary>Playback speed; negative plays it backwards (sheathing from a draw).</summary>
            public float Speed = 1f;
            /// <summary>Seconds into the clip to start from, and to stop at (0 = its end).</summary>
            public float Start, End;
            /// <summary>Only the spine, arms and head move; the legs keep doing what they were.</summary>
            public bool UpperBody;
            /// <summary>Only the hips and legs move; the arms keep their stance (a kick with any weapon).</summary>
            public bool LowerBody;
            /// <summary>
            /// 0-1: how much of the clip the hips and legs take, on top of <see cref="Weight"/>. Below
            /// 1 tames a lunge or a deep stance while the arms still play in full (a cast that throws
            /// a hand forward without dropping into a crouch).
            /// </summary>
            public float Legs = 1f;
            /// <summary>
            /// The clip's travel across the ground is taken out, so it plays on the spot (read by
            /// the importer). Big steps may make the feet slide a little.
            /// </summary>
            public bool InPlace;
        }

        private static readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        private static Dictionary<string, ClipTuning> _tuning;
        private static AvatarMask _upperBody, _lowerBody;

        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layers;
        private AnimationMixerPlayable _locomotion, _stand, _walking;
        private AnimationClipPlayable _walk, _walkCombat, _run;
        private float _walkSpeed, _walkCombatSpeed, _runSpeed;
        private float _walkCombatWeight;
        // A one-shot plays as two synced copies: the upper body on layer 1, hips and legs on layer 2,
        // so each half can have its own strength.
        private AnimationClipPlayable _oneShot, _oneShotLegs;
        private ClipTuning _oneShotTuning;
        private float _oneShotStart, _oneShotEnd;
        private bool _hold;
        private float _move, _runBlend, _combat;
        private float _combatWeight;

        // Domain reload is off in the Editor, so statics outlive Play sessions: start each one fresh,
        // so edits to animations.json and new clip files show the next time Play is pressed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaches()
        {
            Clips.Clear();
            _tuning = null;
        }

        /// <summary>The clip for this name (following its tuning's "clip"), or null if there isn't one.</summary>
        public static AnimationClip Clip(string name)
        {
            if (Clips.TryGetValue(name, out AnimationClip cached) && cached != null) return cached;
            string file = string.IsNullOrEmpty(Tuning(name).Clip) ? name : Tuning(name).Clip;
            AnimationClip found = null;
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(Folder + file))
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

        private static bool IsLower(AvatarMaskBodyPart part) =>
            part == AvatarMaskBodyPart.Root || part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg ||
            part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;

        private static AvatarMask LowerBodyMask
        {
            get
            {
                if (_lowerBody != null) return _lowerBody;
                _lowerBody = new AvatarMask();
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    _lowerBody.SetHumanoidBodyPartActive(part, IsLower(part));
                return _lowerBody;
            }
        }

        private static AvatarMask UpperBodyMask
        {
            get
            {
                if (_upperBody != null) return _upperBody;
                _upperBody = new AvatarMask();
                for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                    _upperBody.SetHumanoidBodyPartActive(part, !IsLower(part));
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

            // Walking: relaxed and combat walks, blended the same way.
            AnimationClip combatWalk = Clip("walk_combat") ?? walk;
            _walking = AnimationMixerPlayable.Create(_graph, 2);
            _walk = Looping(walk, "walk", false);
            _walkCombat = Looping(combatWalk, "walk_combat", false);
            Connect(_walk, _walking, 0);
            Connect(_walkCombat, _walking, 1);
            _walkCombatWeight = Tuning("walk_combat").Weight;
            _walkCombatSpeed = TravelSpeed(combatWalk, DefaultWalkSpeed);

            _locomotion = AnimationMixerPlayable.Create(_graph, 3);
            _run = Looping(run, "run", false);
            Connect(_stand, _locomotion, 0);
            Connect(_walking, _locomotion, 1);
            Connect(_run, _locomotion, 2);
            _walkSpeed = TravelSpeed(walk, DefaultWalkSpeed);
            _runSpeed = TravelSpeed(run, DefaultRunSpeed);

            _layers = AnimationLayerMixerPlayable.Create(_graph, 3);
            Connect(_locomotion, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            _layers.SetInputWeight(1, 0f);
            _layers.SetInputWeight(2, 0f);
            _layers.SetLayerMaskFromAvatarMask(1, UpperBodyMask);
            _layers.SetLayerMaskFromAvatarMask(2, LowerBodyMask);
            output.SetSourcePlayable(_layers);
            Update(0f, false, false, 0f);
            _graph.Play();
        }

        private AnimationClipPlayable Looping(AnimationClip clip, string name, bool randomStart)
        {
            var playable = AnimationClipPlayable.Create(_graph, clip);
            ClipTuning tuning = Tuning(name);
            if (tuning.Pose.HasValue)
            {
                playable.SetTime(Mathf.Clamp(tuning.Pose.Value, 0f, clip.length));
                playable.SetSpeed(0);
                return playable;
            }
            playable.SetSpeed(tuning.Speed);
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
            _walking.SetInputWeight(0, 1f - _combat * _walkCombatWeight);
            _walking.SetInputWeight(1, _combat * _walkCombatWeight);
            _locomotion.SetInputWeight(0, 1f - _move);
            _locomotion.SetInputWeight(1, _move * (1f - _runBlend));
            _locomotion.SetInputWeight(2, _move * _runBlend);

            // Step at the rate that covers the ground: feet don't slide or skate.
            if (moving)
            {
                _walk.SetSpeed(Mathf.Clamp(speed / _walkSpeed, 0.5f, 2f) * Tuning("walk").Speed);
                _walkCombat.SetSpeed(Mathf.Clamp(speed / _walkCombatSpeed, 0.5f, 2f) * Tuning("walk_combat").Speed);
                _run.SetSpeed(Mathf.Clamp(speed / _runSpeed, 0.5f, 2f) * Tuning("run").Speed);
            }

            if (_oneShot.IsValid())
            {
                float t = (float)_oneShot.GetTime();
                bool reverse = _oneShotTuning.Speed < 0f;
                float elapsed = reverse ? _oneShotEnd - t : t - _oneShotStart;
                float remaining = reverse ? t - _oneShotStart : _oneShotEnd - t;
                float weight = Mathf.Clamp01(elapsed / FadeIn);
                if (!_hold) weight = Mathf.Min(weight, Mathf.Clamp01(remaining / FadeOut));
                else if (remaining <= 0f)   // stay at the end (death)
                {
                    _oneShot.SetSpeed(0);
                    _oneShotLegs.SetSpeed(0);
                }
                weight *= _oneShotTuning.Weight;
                _layers.SetInputWeight(1, _oneShotTuning.LowerBody ? 0f : weight);
                _layers.SetInputWeight(2, _oneShotTuning.UpperBody ? 0f : weight * Mathf.Clamp01(_oneShotTuning.Legs));
                if (!_hold && remaining <= 0f) StopOneShot();
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
            _oneShotTuning = tuning;
            _oneShotStart = Mathf.Clamp(tuning.Start, 0f, clip.length);
            _oneShotEnd = tuning.End > tuning.Start ? Mathf.Min(tuning.End, clip.length) : clip.length;
            _oneShot = OneShotCopy(clip, tuning, 1);
            _oneShotLegs = OneShotCopy(clip, tuning, 2);
            _hold = hold;
            return true;
        }

        private AnimationClipPlayable OneShotCopy(AnimationClip clip, ClipTuning tuning, int layer)
        {
            var playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetTime(tuning.Speed < 0f ? _oneShotEnd : _oneShotStart);
            playable.SetSpeed(tuning.Speed);
            _graph.Connect(playable, 0, _layers, layer);
            _layers.SetInputWeight(layer, 0f);
            return playable;
        }

        private void StopOneShot()
        {
            if (!_oneShot.IsValid()) return;
            for (int layer = 1; layer <= 2; layer++)
            {
                _graph.Disconnect(_layers, layer);
                _layers.SetInputWeight(layer, 0f);
            }
            _oneShot.Destroy();
            _oneShotLegs.Destroy();
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
