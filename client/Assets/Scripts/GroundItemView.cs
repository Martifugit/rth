using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>An item lying on a tile: a small tinted block (placeholder art) that can be clicked.</summary>
    public sealed class GroundItemView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public int Id { get; private set; }
        public string ItemId { get; private set; }
        public int Count { get; private set; }
        public TileCoord Tile { get; private set; }

        public static GroundItemView Create(GroundItemAdded added, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ground item " + added.ItemId + " #" + added.Id;
            var view = go.AddComponent<GroundItemView>();
            view.Id = added.Id;
            view.ItemId = added.ItemId;
            view.Count = added.Count;
            view.Tile = added.Position;

            // Scatter piles a little within the tile so several items on one tile stay visible.
            float jx = ((added.Id * 37) % 7 - 3) * 0.06f;
            float jz = ((added.Id * 53) % 7 - 3) * 0.06f;
            go.transform.position = TileMath.Center(added.Position) + new Vector3(jx, 0.12f, jz);
            go.transform.localScale = new Vector3(0.3f, 0.24f, 0.3f);
            go.transform.rotation = Quaternion.Euler(0f, added.Id * 41 % 90, 0f);

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            // A slightly larger hitbox so small items are easy to click.
            go.GetComponent<BoxCollider>().size = new Vector3(1.6f, 2f, 1.6f);
            return view;
        }
    }
}
