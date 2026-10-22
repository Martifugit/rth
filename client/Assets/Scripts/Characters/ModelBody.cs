using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A body made from character art (<see cref="CharacterModel"/>) and animated with clips
    /// (<see cref="ClipAnimator"/>). Falls back to nothing: <see cref="Build"/> returns false when
    /// the art or the basic clips are missing, and the caller uses another body.
    ///
    /// Being hit is a small stagger, as in Old School: unless there is a "hit" clip, the chest and
    /// head rock back a few degrees on top of whatever is playing.
    /// </summary>
    public sealed class ModelBody : EntityBody
    {
        private const float StaggerSeconds = 0.3f;
        private const float StaggerDegrees = 7f;

        private CharacterModel _model;
        private ClipAnimator _animator;
        private Transform _chest, _head;
        private float _scale = 1f;
        private float _hitTime = -10f;
        private float _fallTime = -1f;

        public CharacterModel Model => _model;
        public override float Height => _model.Height * _scale;
        public override float Radius => 0.3f * _scale;

        public bool Build(string bodyType, HumanoidLook look, float scale)
        {
            if (!ClipAnimator.HasLocomotion) return false;
            _model = CharacterModel.Load(bodyType, transform);
            if (_model == null || _model.Animator == null || _model.Animator.avatar == null) return false;

            _scale = scale;
            _model.transform.localScale = Vector3.one * scale;
            _model.SetColor("skin", look.Skin);
            _model.SetColor("hair", look.Hair);
            _model.SetColor("primary", look.Top);
            _model.SetColor("secondary", look.Legs);
            _model.SetColor("leather", look.Boots);
            _model.SetColor("eyes", Palette.TryParse("Sable", out Color eyes) ? eyes : Color.black);
            _model.SetHair("short");
            _animator = new ClipAnimator(_model.Animator, name);
            _chest = _model.Animator.GetBoneTransform(HumanBodyBones.Chest) ?? _model.Animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = _model.Animator.GetBoneTransform(HumanBodyBones.Head);
            return true;
        }

        public override void Animate(float speed, bool running, float deltaTime) =>
            _animator?.Update(speed, running, InCombat, deltaTime);

        public override void PlayAction(string animation)
        {
            if (_animator != null && !_animator.Play(animation)) _animator.Play("cast");
        }

        private GameObject _held;
        private string _heldId;

        public override void SetHeld(Rth.Shared.Items.ItemDef item)
        {
            if ((item?.Id ?? "") == (_heldId ?? "")) return;
            _heldId = item?.Id;
            if (_held != null) _model.Detach(_held);
            _held = null;
            GameObject art = HeldItemArt.Build(item);
            if (art == null) return;
            _held = _model.Attach(art, socket: "grip.R", hold: HeldItemArt.Hold);
            Destroy(art);
            HeldItemArt.Tint(_held, item, _model);
        }

        public override void PlayHit()
        {
            if (_animator == null || !_animator.Play("hit")) _hitTime = Time.time;
        }

        public override void PlayDeath()
        {
            // Without a death clip, topple backwards.
            if (_animator == null || !_animator.Play("death", hold: true)) _fallTime = Time.time;
        }

        // After the Animator has posed the bones this frame.
        private void LateUpdate()
        {
            if (_fallTime >= 0f && _model != null)
            {
                float f = Mathf.Clamp01((Time.time - _fallTime) / 0.8f);
                f = f * f * (3f - 2f * f);
                _model.transform.localRotation = Quaternion.Euler(-85f * f, 0f, 0f);
                _model.transform.localPosition = new Vector3(0f, 0.12f * f * _scale, -0.1f * f * _scale);
            }

            float since = Time.time - _hitTime;
            if (since < 0f || since > StaggerSeconds || _chest == null) return;
            // Quick back, slower return.
            float t = since / StaggerSeconds;
            float k = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
            k = k * k * (3f - 2f * k);
            Vector3 right = transform.right;
            _chest.rotation = Quaternion.AngleAxis(-StaggerDegrees * k, right) * _chest.rotation;
            if (_head != null) _head.rotation = Quaternion.AngleAxis(-StaggerDegrees * 0.6f * k, right) * _head.rotation;
        }

        private void OnDestroy() => _animator?.Destroy();
    }
}
