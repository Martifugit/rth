using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>
    /// The visible model of an entity and its animation. <see cref="EntityView"/> moves the
    /// entity across tiles; the body only animates in place, told how fast it is going and what
    /// just happened. New kinds of model (other creatures, equipment-built players) subclass this
    /// and get picked in <see cref="Create"/>.
    /// </summary>
    public abstract class EntityBody : MonoBehaviour
    {
        /// <summary>Top of the model, for labels and health bars.</summary>
        public abstract float Height { get; }
        /// <summary>Rough horizontal radius, for the click hitbox.</summary>
        public abstract float Radius { get; }

        /// <summary>Called every frame. <paramref name="speed"/> is in tiles per second.</summary>
        public abstract void Animate(float speed, bool running, float deltaTime);
        /// <summary>It attacked or used an ability.</summary>
        public abstract void PlayAction();
        /// <summary>It was hit.</summary>
        public abstract void PlayHit();
        /// <summary>It died; the server removes or respawns it shortly after.</summary>
        public abstract void PlayDeath();

        public static EntityBody Create(EntityAdded added, Color color, Transform parent)
        {
            var go = new GameObject("Body");
            go.transform.SetParent(parent, false);
            if (added.Kind == EntityKind.Player)
            {
                var humanoid = go.AddComponent<HumanoidBody>();
                humanoid.Build(HumanoidLook.For(added.Id, color), added.Scale / 10f);
                return humanoid;
            }
            var primitive = go.AddComponent<PrimitiveBody>();
            primitive.Build(added.Shape, color, added.Scale / 10f, added.Kind);
            return primitive;
        }

        private static Material _matte;

        /// <summary>One shared matte lit material; each part's colour comes from a property block.</summary>
        protected static Material Matte
        {
            get
            {
                if (_matte != null) return _matte;
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _matte = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                    Destroy(probe);
                }
                else _matte = new Material(shader);
                _matte.name = "Character (matte)";
                _matte.SetFloat("_Smoothness", 0f);
                _matte.SetFloat("_SpecularHighlights", 0f);
                _matte.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                return _matte;
            }
        }
    }
}
