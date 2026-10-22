using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// A placeholder body from one Unity primitive and a face block (the shape the server names),
    /// for creatures without a model yet.
    /// </summary>
    public sealed class PrimitiveBody : ShapeBody
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

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

            // A face, so you can see which way it's heading.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, height * 0.75f, size.z * 0.45f);
            face.transform.localScale = new Vector3(0.3f, 0.15f, 0.12f) * Mathf.Max(0.6f, scale);
            block.SetColor(BaseColor, kind == EntityKind.Player ? new Color(0.95f, 0.85f, 0.7f) : Color.Lerp(color, Color.black, 0.5f));
            face.GetComponent<Renderer>().SetPropertyBlock(block);
            face.transform.SetParent(body.transform, true);

            SetShape(body.transform, height, Mathf.Max(size.x, size.z) * 0.5f);
        }
    }
}
