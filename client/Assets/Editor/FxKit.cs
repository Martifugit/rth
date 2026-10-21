using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Effects for region builders: particle materials and systems (fire, embers, mist, dust,
    /// falling blood, drips), glowing materials, lights that flicker, light shafts, and a local
    /// post-processing volume. Everything it makes is saved under Assets/Art/Fx, so scenes keep
    /// working without the builder.
    /// </summary>
    public static class FxKit
    {
        private const string Dir = "Assets/Art/Fx";

        // ------------------------------------------------------------------ textures and materials

        /// <summary>A soft round dot (white, alpha falling off to the edge): the one particle texture.</summary>
        public static Texture2D SoftDot()
        {
            string path = Dir + "/SoftDot.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Dir);
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>An unlit particle material: additive (fire, glow, light) or alpha-blended (mist, blood).</summary>
        public static Material ParticleMat(string name, Color color, bool additive)
        {
            Material m = Asset(name, "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", SoftDot());
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f); // both sides: halos and beams are seen from anywhere
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A matte lit material that glows (eyes, embers in a bowl, blood that shouldn't be lit by anything).</summary>
        public static Material Glow(string name, Color color, float intensity)
        {
            Material m = Asset(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Asset(string name, string shader)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader.name != shader) m.shader = Shader.Find(shader);
            return m;
        }

        // ------------------------------------------------------------------ particles

        /// <summary>A looping particle system at a point, set up by the effect helpers below.</summary>
        private static ParticleSystem System(string name, Transform parent, Vector3 position, Material material, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static ParticleSystem.MinMaxGradient FadeInOut(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(c.a, 0.25f), new GradientAlphaKey(c.a, 0.6f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Flames in a brazier bowl: short-lived additive blobs rising and shrinking.</summary>
        public static void Fire(Transform parent, Vector3 at, float scale, Material flame)
        {
            ParticleSystem ps = System("Fire", parent, at, flame, 60);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.3f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.25f, 0.05f));
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.25f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
        }

        /// <summary>Sparks drifting up out of a fire into the dark, wandering as they go.</summary>
        public static void Embers(Transform parent, Vector3 at, float height, Material ember)
        {
            ParticleSystem ps = System("Embers", parent, at, ember, 80);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(height * 0.35f, height * 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.3f, 0.1f));
            var emission = ps.emission;
            emission.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
        }

        /// <summary>Low mist lying over a floor area: big, faint, slow blobs.</summary>
        public static void Mist(Transform parent, Vector3 center, Vector2 area, Material mist, Color tint)
        {
            ParticleSystem ps = System("Mist", parent, center, mist, 90);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, 0.2f, area.y);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.2f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
        }

        /// <summary>Motes hanging in a shaft of light, drifting very slowly.</summary>
        public static ParticleSystem Dust(Transform parent, Vector3 center, Vector3 box, Material mote)
        {
            ParticleSystem ps = System("Dust", parent, center, mote, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.8f);
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
            return ps;
        }

        /// <summary>A thin stream of blood falling from a point (a tipped chalice) and splashing below.</summary>
        public static void BloodStream(Transform parent, Vector3 from, float fall, Material blood)
        {
            ParticleSystem ps = System("Blood stream", parent, from, blood, 120);
            var main = ps.main;
            main.startLifetime = Mathf.Sqrt(2f * fall / 9.81f);
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
            main.startColor = new Color(0.45f, 0.02f, 0.04f, 0.95f);
            main.gravityModifier = 1f;
            var emission = ps.emission;
            emission.rateOverTime = 60f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 1.5f;

            // The splash where it lands.
            ParticleSystem splash = System("Splash", parent, from + Vector3.down * fall, blood, 60);
            var sm = splash.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            sm.startColor = new Color(0.45f, 0.02f, 0.04f, 0.9f);
            sm.gravityModifier = 1.5f;
            var se = splash.emission;
            se.rateOverTime = 25f;
            var ss = splash.shape;
            ss.shapeType = ParticleSystemShapeType.Hemisphere;
            ss.radius = 0.1f;
            ss.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>Now and then a drop falls back into a fount's bowl.</summary>
        public static ParticleSystem Drips(Transform parent, Vector3 at, Material blood)
        {
            ParticleSystem ps = System("Drips", parent, at, blood, 10);
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(0.5f, 0.03f, 0.05f, 1f);
            main.gravityModifier = 1f;
            var emission = ps.emission;
            emission.rateOverTime = 1.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            return ps;
        }

        /// <summary>A soft glow behind something (a symbol's halo): a quad facing south, drawn additively.</summary>
        public static Renderer Halo(Transform parent, Vector3 at, float size, Material glow)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Halo";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.position = at;
            quad.transform.localScale = new Vector3(size, size, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>A burst of sparks, emitted on demand (not looping): a symbol flaring.</summary>
        public static ParticleSystem Burst(Transform parent, Vector3 at, Material spark)
        {
            ParticleSystem ps = System("Burst", parent, at, spark, 120);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.gravityModifier = 0.15f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.radiusThickness = 0f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
            return ps;
        }

        /// <summary>Sparks streaming up from a ring on the floor; it starts at rest (rate 0) and the effect raises its rate.</summary>
        public static ParticleSystem RingSparks(Transform parent, Vector3 at, float radius, Material spark)
        {
            ParticleSystem ps = System("Ring sparks", parent, at, spark, 300);
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(1f, 0.9f, 0.65f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
            return ps;
        }

        /// <summary>Fire bursting up from everywhere in a floor area, emitted on demand (a judgement).</summary>
        public static ParticleSystem Eruptions(Transform parent, Vector3 center, Vector3 area, Material flame)
        {
            ParticleSystem ps = System("Eruptions", parent, center, flame, 1500);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = 0f; // a box throws along its z; they rise by velocity instead
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.15f), new Color(0.9f, 0.08f, 0.06f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(2f, 4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
            return ps;
        }

        /// <summary>A burst thrown upward, emitted on demand (a cleanse, a scorch).</summary>
        public static ParticleSystem Upburst(Transform parent, Vector3 at, Material spark, float radius, float speed)
        {
            ParticleSystem ps = System("Upburst", parent, at, spark, 250);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.gravityModifier = -0.05f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
            return ps;
        }

        /// <summary>Faint holy motes rising slowly around something sacred.</summary>
        public static void Motes(Transform parent, Vector3 at, float radius, Material mote, Color colour)
        {
            ParticleSystem ps = System("Motes", parent, at, mote, 30);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = colour;
            var emission = ps.emission;
            emission.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOut(Color.white);
        }

        // ------------------------------------------------------------------ light

        public static Light PointLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range, float flicker = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (flicker > 0f) go.AddComponent<LightFlicker>().strength = flicker;
            return light;
        }

        /// <summary>A shaft of light from high above: a spot light, and a faint column you can see in the dark.</summary>
        public static (Light Light, Renderer Beam) Shaft(Transform parent, Vector3 floor, float height, float radius, Color color, Material beam)
        {
            var go = new GameObject("Light shaft");
            go.transform.SetParent(parent, false);
            go.transform.position = floor + Vector3.up * height;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = 40f;
            light.range = height + 4f;
            light.spotAngle = Mathf.Atan(radius * 1.6f / height) * 2f * Mathf.Rad2Deg;
            light.innerSpotAngle = light.spotAngle * 0.6f;
            light.shadows = LightShadows.None;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Shaft";
            Object.DestroyImmediate(column.GetComponent<Collider>());
            column.transform.SetParent(go.transform.parent, false);
            column.transform.position = floor + Vector3.up * (height / 2f);
            column.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            var r = column.GetComponent<Renderer>();
            r.sharedMaterial = beam;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return (light, r);
        }

        /// <summary>Post-processing that applies only while the camera is inside the box (bloom so glows glow, a vignette, colour).</summary>
        public static void Volume(Transform parent, string name, Vector3 center, Vector3 size, float bloom, float vignette, float saturation)
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet(out Bloom b)) b = profile.Add<Bloom>(true);
            b.threshold.Override(0.85f);
            b.intensity.Override(bloom);
            b.scatter.Override(0.7f);
            if (!profile.TryGet(out Vignette v)) v = profile.Add<Vignette>(true);
            v.intensity.Override(vignette);
            v.smoothness.Override(0.5f);
            if (!profile.TryGet(out ColorAdjustments c)) c = profile.Add<ColorAdjustments>(true);
            c.saturation.Override(saturation);
            c.contrast.Override(12f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = 6f;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }
    }
}
