using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A placeholder body from one Unity primitive and a face block (the shape the server names),
    /// with a lunge when it attacks, a squash when hit and a sink-and-shrink on death.
    /// </summary>
    public sealed class PrimitiveBody : EntityBody
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

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

        public void Build(EntityShape shape, Color color, float scale, EntityKind kind)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);

            PrimitiveType type = shape switch
            {
                EntityShape.Box => PrimitiveType.Cube,
                EntityShape.Cylinder => PrimitiveType.Cylinder,
                EntityShape.Sphere => PrimitiveType.Sphere,
                _ => PrimitiveType.Capsule,
            };
            GameObject body = GameObject.CreatePrimitive(type);
            Destroy(body.GetComponent<Collider>());
            body.name = "Shape";
            body.transform.SetParent(transform, false);

            // Sizes chosen so a scale-1 entity is about player-sized.
            Vector3 size = shape switch
            {
                EntityShape.Box => new Vector3(0.8f, 0.6f, 1.0f),
                EntityShape.Sphere => new Vector3(0.9f, 0.9f, 0.9f),
                EntityShape.Cylinder => new Vector3(0.5f, 0.9f, 0.5f), // a cylinder primitive is 2 tall
                _ => new Vector3(0.6f, 0.85f, 0.6f),                   // so is a capsule
            } * scale;
            float height = shape == EntityShape.Capsule || shape == EntityShape.Cylinder ? size.y * 2f : size.y;
            body.transform.localScale = size;
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            body.GetComponent<Renderer>().SetPropertyBlock(block);
            _shape = body.transform;
            _shapeScale = body.transform.localScale;
            _shapePosition = body.transform.localPosition;
            _height = height;
            _radius = Mathf.Max(size.x, size.z) * 0.5f;

            // A face, so you can see which way it's heading.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, height * 0.75f, size.z * 0.45f);
            face.transform.localScale = new Vector3(0.3f, 0.15f, 0.12f) * Mathf.Max(0.6f, scale);
            block.SetColor(BaseColor, kind == EntityKind.Player ? new Color(0.95f, 0.85f, 0.7f) : Color.Lerp(color, Color.black, 0.5f));
            face.GetComponent<Renderer>().SetPropertyBlock(block);
            face.transform.SetParent(_shape, true);
        }

        public override void PlayAction() => _actionTime = Time.time;
        public override void PlayHit() => _hitTime = Time.time;
        public override void PlayDeath() => _deathTime = Time.time;

        public override void Animate(float speed, bool running, float deltaTime)
        {
            if (_shape == null) return;   // lost to a script reload during Play
            if (_deathTime >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - _deathTime) / 1.0f);
                _shape.localScale = Vector3.Lerp(_shapeScale, _shapeScale * 0.3f, k);
                _shape.localPosition = Vector3.Lerp(_shapePosition, new Vector3(0f, _shapeScale.y * 0.15f, 0f), k);
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
