using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A body moved as one rigid shape: a lunge when it attacks, a squash when hit and a
    /// sink-and-shrink on death. Subclasses build the shape and call <see cref="SetShape"/>.
    /// </summary>
    public abstract class ShapeBody : EntityBody
    {
        private Transform _shape;
        private Vector3 _shapeScale;
        private Vector3 _shapePosition;
        private float _height;
        private float _radius;
        private float _actionTime = -10f;
        private float _hitTime = -10f;
        private float _deathTime = -1f;

        public override float Height => _height;
        public override float Radius => _radius;

        protected void SetShape(Transform shape, float height, float radius)
        {
            _shape = shape;
            _shapeScale = shape.localScale;
            _shapePosition = shape.localPosition;
            _height = height;
            _radius = radius;
        }

        public override void PlayAction(string animation) => _actionTime = Time.time;
        public override void PlayHit() => _hitTime = Time.time;
        public override void PlayDeath() => _deathTime = Time.time;

        public override void Animate(float speed, bool running, float deltaTime)
        {
            if (_shape == null) return;   // lost to a script reload during Play
            if (_deathTime >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - _deathTime) / 1.0f);
                _shape.localScale = Vector3.Lerp(_shapeScale, _shapeScale * 0.3f, k);
                _shape.localPosition = Vector3.Lerp(_shapePosition, _shapePosition * 0.3f, k);
                _shape.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 80f, k));
                return;
            }

            // Lunge forward and back over a third of a second.
            float lunge = Pulse(Time.time - _actionTime, 0.35f);
            // Squash down briefly when hit.
            float squash = Pulse(Time.time - _hitTime, 0.25f);
            _shape.localPosition = _shapePosition + new Vector3(0f, -squash * 0.04f, lunge * 0.2f);
            _shape.localScale = Vector3.Scale(_shapeScale, new Vector3(1f + squash * 0.08f, 1f - squash * 0.1f, 1f + squash * 0.08f));
        }

        /// <summary>0 → 1 → 0 over <paramref name="duration"/> seconds after an event, else 0.</summary>
        private static float Pulse(float since, float duration) =>
            since < 0f || since > duration ? 0f : Mathf.Sin(since / duration * Mathf.PI);
    }
}
