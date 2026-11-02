using Rth.MapTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Particle effects that show the state of a gathering node (drawn by <see cref="SkillingViews"/>):
    /// a tree that needs tending sheds withered leaves that tumble down out of its crown; one that has
    /// been tended glows with slow-rising pollen and a faint warm light; tending it gives a short
    /// burst of sparks and fresh leaves; a fishing spot bubbles, with rings spreading; water where
    /// fish are about to come shimmers (motes swirling up over it, a soft pulsing light). All drawn with Resources/Shaders/Particle and textures made here (a soft
    /// glow, a leaf, a ring), so they need no assets.
    /// </summary>
    public static class SkillingEffects
    {
        private static Texture2D _glow, _leaf, _ring;
        private static Material _additive, _blended, _rings;

        /// <summary>The crown of a tree: the upper part of what it draws (outlines and effects left out).</summary>
        public static Bounds Crown(MapObject tree)
        {
            var bounds = new Bounds(tree.transform.position + Vector3.up * 3f, new Vector3(2.5f, 2.5f, 2.5f));
            bool first = true;
            foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.name == "Outline") continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            float bottom = bounds.min.y + bounds.size.y * 0.45f;
            var crown = new Bounds();
            crown.SetMinMax(new Vector3(bounds.min.x, bottom, bounds.min.z), bounds.max);
            return crown;
        }

        /// <summary>Withered leaves, ochre and brown, coming loose from the crown and tumbling to the ground.</summary>
        public static GameObject Withering(MapObject tree)
        {
            Bounds crown = Crown(tree);
            GameObject go = Holder("Withering", tree, crown.center);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 5.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.19f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.44f, 0.18f), new Color(0.38f, 0.27f, 0.13f));
            main.gravityModifier = 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 6f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(crown.size.x * 0.5f, crown.size.y * 0.4f, crown.size.z * 0.5f);   // from the crown's middle, so they fall under it

            // They don't drop like stones: a slow terminal speed, a drifting flutter, a spin.
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.7f;
            limit.dampen = 0.3f;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.25f;
            noise.damping = true;
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            // Tumbling: the leaf narrows and widens as it turns over.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, Wave(5, 0.25f));
            size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.08f, 0.8f, 1f);

            Render(ps, Blended(), 0.25f);
            ps.Play();
            return go;
        }

        /// <summary>Soft green-gold pollen rising slowly through a tended crown, and a faint warm light in it.</summary>
        public static GameObject Thriving(MapObject tree)
        {
            Bounds crown = Crown(tree);
            GameObject go = Holder("Thriving", tree, crown.center);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.38f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 1f, 0.45f, 0.75f), new Color(1f, 0.86f, 0.42f, 0.75f));
            main.gravityModifier = -0.006f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 70;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 11f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            // From a shell round the crown, not inside it, where the leaves would hide them.
            shape.radius = Mathf.Max(0.8f, Mathf.Max(crown.extents.x, crown.extents.z) * 1.05f);
            shape.radiusThickness = 0.35f;
            shape.scale = new Vector3(1f, Mathf.Clamp(crown.extents.y / Mathf.Max(0.1f, shape.radius), 0.6f, 1.2f), 1f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            // Each mote swells in, twinkles a little and dims away.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            var swell = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(0.45f, 0.75f),
                new Keyframe(0.65f, 1f), new Keyframe(1f, 0.3f));
            size.size = new ParticleSystem.MinMaxCurve(1f, swell);
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.25f, 0.7f, 0.85f);

            Render(ps, Additive(), 0.4f);
            ps.Play();

            var light = new GameObject("Glow") { hideFlags = HideFlags.DontSave };
            light.transform.SetParent(go.transform, false);
            Light l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.85f, 1f, 0.55f);
            l.range = Mathf.Max(3f, crown.extents.magnitude * 1.6f);
            l.intensity = 0.55f;
            l.shadows = LightShadows.None;
            return go;
        }

        /// <summary>The moment of tending: a puff of sparks and fresh green leaves from the foot up into the crown.</summary>
        public static void TendedBurst(MapObject tree)
        {
            Bounds crown = Crown(tree);
            Vector3 foot = tree.transform.position + Vector3.up * 0.4f;
            GameObject go = Holder("Tended", tree, foot);

            ParticleSystem sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.5f), new Color(1f, 0.9f, 0.5f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36), new ParticleSystem.Burst(0.25f, 16) });
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);         // up the trunk
            ParticleSystem.LimitVelocityOverLifetimeModule limit = sparks.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.08f;
            ParticleSystem.NoiseModule noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.8f;
            ParticleSystem.ColorOverLifetimeModule colour = sparks.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.05f, 0.55f, 1f);
            Render(sparks, Additive(), 0.3f);
            sparks.Play();

            var leavesGo = new GameObject("Fresh leaves") { hideFlags = HideFlags.DontSave };
            leavesGo.transform.SetParent(go.transform, false);
            leavesGo.transform.position = new Vector3(crown.center.x, crown.min.y, crown.center.z);
            ParticleSystem leaves = leavesGo.AddComponent<ParticleSystem>();
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule lm = leaves.main;
            lm.loop = false;
            lm.duration = 0.4f;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            lm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            lm.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            lm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.62f, 0.22f), new Color(0.58f, 0.74f, 0.28f));
            lm.gravityModifier = 0.08f;
            lm.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule le = leaves.emission;
            le.rateOverTime = 0f;
            le.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 20) });
            ParticleSystem.ShapeModule ls = leaves.shape;
            ls.shapeType = ParticleSystemShapeType.Sphere;
            ls.radius = Mathf.Max(0.5f, crown.extents.x * 0.5f);
            ParticleSystem.RotationOverLifetimeModule spin = leaves.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            ParticleSystem.LimitVelocityOverLifetimeModule ll = leaves.limitVelocityOverLifetime;
            ll.enabled = true;
            ll.limit = 0.8f;
            ll.dampen = 0.15f;
            ParticleSystem.ColorOverLifetimeModule lc = leaves.colorOverLifetime;
            lc.enabled = true;
            lc.color = Fade(0.05f, 0.7f, 1f);
            Render(leaves, Blended(), 0.25f);
            leaves.Play();

            Object.Destroy(go, 3.5f);
        }

        /// <summary>
        /// Casting into shimmering water: a ring of light running out over the surface, a second
        /// behind it, motes leaping up and settling, and a flash that fades. It says: that took.
        /// </summary>
        public static void Cast(MapObject spot)
        {
            GameObject go = Holder("Cast", spot, spot.transform.position + Vector3.up * 0.06f);

            ParticleSystem rings = go.AddComponent<ParticleSystem>();
            rings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule rm = rings.main;
            rm.loop = false;
            rm.duration = 0.4f;
            rm.startLifetime = 1.1f;
            rm.startSpeed = 0f;
            rm.startSize = 0.5f;
            rm.startColor = new Color(0.6f, 0.95f, 1f, 1f);
            rm.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule re = rings.emission;
            re.rateOverTime = 0f;
            re.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.22f, 1) });
            ParticleSystem.ShapeModule rs = rings.shape;
            rs.enabled = false;
            ParticleSystem.SizeOverLifetimeModule rsize = rings.sizeOverLifetime;
            rsize.enabled = true;
            rsize.size = new ParticleSystem.MinMaxCurve(5f, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 2.5f), new Keyframe(1f, 1f, 0.2f, 0f)));
            ParticleSystem.ColorOverLifetimeModule rc = rings.colorOverLifetime;
            rc.enabled = true;
            rc.color = Fade(0.02f, 0.3f, 1f);
            Render(rings, Rings(), 0.05f);
            rings.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // running out over the water
            rings.Play();

            var motesGo = new GameObject("Motes") { hideFlags = HideFlags.DontSave };
            motesGo.transform.SetParent(go.transform, false);
            ParticleSystem motes = motesGo.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule mm = motes.main;
            mm.loop = false;
            mm.duration = 0.3f;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.7f);
            mm.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            mm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f), new Color(0.9f, 0.85f, 1f));
            mm.gravityModifier = 0.12f;
            mm.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule me = motes.emission;
            me.rateOverTime = 0f;
            me.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            ParticleSystem.ShapeModule ms = motes.shape;
            ms.shapeType = ParticleSystemShapeType.Cone;
            ms.angle = 30f;
            ms.radius = 0.3f;
            ms.rotation = new Vector3(-90f, 0f, 0f);             // up out of the water
            ParticleSystem.LimitVelocityOverLifetimeModule limit = motes.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.5f;
            limit.dampen = 0.1f;
            ParticleSystem.NoiseModule noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.8f;
            ParticleSystem.ColorOverLifetimeModule mc = motes.colorOverLifetime;
            mc.enabled = true;
            mc.color = Fade(0.02f, 0.6f, 1f);
            Render(motes, Additive(), 0.05f);
            motes.Play();

            var lightGo = new GameObject("Flash") { hideFlags = HideFlags.DontSave };
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.6f;
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.55f, 0.9f, 1f);
            l.range = 4.5f;
            l.shadows = LightShadows.None;
            lightGo.AddComponent<Flash>().Set(l, 1.6f, 0.8f);

            Object.Destroy(go, 2.5f);
        }

        /// <summary>A fishing spot: bubbles breaking the surface, rings spreading from them.</summary>
        public static GameObject Bubbles(MapObject spot)
        {
            GameObject go = Holder("Bubbles", spot, spot.transform.position + Vector3.up * 0.04f);

            ParticleSystem bubbles = go.AddComponent<ParticleSystem>();
            bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = bubbles.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.95f, 1f, 0.8f), new Color(0.65f, 0.85f, 0.95f, 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = bubbles.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = bubbles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            shape.rotation = new Vector3(-90f, 0f, 0f);            // flat on the water, bubbles rising
            ParticleSystem.SizeOverLifetimeModule size = bubbles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.8f, 1f), new Keyframe(1f, 1.4f)));
            ParticleSystem.ColorOverLifetimeModule colour = bubbles.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.1f, 0.75f, 1f);
            Render(bubbles, Additive(), 0.05f);
            bubbles.Play();

            var ringsGo = new GameObject("Rings") { hideFlags = HideFlags.DontSave };
            ringsGo.transform.SetParent(go.transform, false);
            ParticleSystem rings = ringsGo.AddComponent<ParticleSystem>();
            rings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule rm = rings.main;
            rm.loop = true;
            rm.prewarm = true;
            rm.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.6f);
            rm.startSpeed = 0f;
            rm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            rm.startColor = new Color(0.85f, 0.95f, 1f, 0.7f);
            rm.simulationSpace = ParticleSystemSimulationSpace.World;
            rm.maxParticles = 12;
            ParticleSystem.EmissionModule re = rings.emission;
            re.rateOverTime = 1.6f;
            ParticleSystem.ShapeModule rs = rings.shape;
            rs.shapeType = ParticleSystemShapeType.Circle;
            rs.radius = 0.5f;
            rs.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule rsize = rings.sizeOverLifetime;
            rsize.enabled = true;
            rsize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 2f), new Keyframe(1f, 1f, 0.3f, 0f)));
            ParticleSystem.ColorOverLifetimeModule rc = rings.colorOverLifetime;
            rc.enabled = true;
            rc.color = Fade(0.08f, 0.35f, 1f);
            Render(rings, Rings(), 0.05f);
            rings.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // lying on the water
            rings.Play();
            return go;
        }

        /// <summary>
        /// Water where fish are about to come: pale aqua and silver motes swirling in a slow ring just
        /// over it and drifting up, a few faint glows hanging on the surface, and a soft light that
        /// breathes. Something unseen gathering, the way the tended tree glows.
        /// </summary>
        public static GameObject Arriving(MapObject spot)
        {
            GameObject go = Holder("Arriving", spot, spot.transform.position + Vector3.up * 0.08f);

            ParticleSystem motes = go.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = motes.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f, 0.9f), new Color(0.85f, 0.8f, 1f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 60;
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.rateOverTime = 20f;
            ParticleSystem.ShapeModule shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.75f;
            shape.radiusThickness = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            // Round and round, drawing in a little, rising.
            ParticleSystem.VelocityOverLifetimeModule velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.6f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.15f);
            ParticleSystem.SizeOverLifetimeModule size = motes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0.7f),
                new Keyframe(0.7f, 1f), new Keyframe(1f, 0.2f)));
            ParticleSystem.ColorOverLifetimeModule colour = motes.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.2f, 0.65f, 1f);
            Render(motes, Additive(), 0.05f);
            motes.Play();

            var hazeGo = new GameObject("Haze") { hideFlags = HideFlags.DontSave };
            hazeGo.transform.SetParent(go.transform, false);
            ParticleSystem haze = hazeGo.AddComponent<ParticleSystem>();
            haze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule hm = haze.main;
            hm.loop = true;
            hm.prewarm = true;
            hm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            hm.startSpeed = 0f;
            hm.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            hm.startColor = new Color(0.55f, 0.85f, 1f, 0.22f);
            hm.simulationSpace = ParticleSystemSimulationSpace.Local;
            hm.maxParticles = 8;
            ParticleSystem.EmissionModule he = haze.emission;
            he.rateOverTime = 3f;
            ParticleSystem.ShapeModule hs = haze.shape;
            hs.shapeType = ParticleSystemShapeType.Circle;
            hs.radius = 0.3f;
            hs.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule hc = haze.colorOverLifetime;
            hc.enabled = true;
            hc.color = Fade(0.35f, 0.6f, 1f);
            Render(haze, Additive(), 0.3f);
            haze.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;   // a glow lying on the water
            haze.Play();

            var lightGo = new GameObject("Glow") { hideFlags = HideFlags.DontSave };
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.5f;
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.55f, 0.9f, 1f);
            l.range = 3.5f;
            l.shadows = LightShadows.None;
            lightGo.AddComponent<Breathe>().Set(l, 0.25f, 0.75f, 1.4f);
            return go;
        }

        // ------------------------------------------------------------------ pieces

        private static GameObject Holder(string name, MapObject tree, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(tree.transform, false);
            go.transform.position = at;
            return go;
        }

        private static void Render(ParticleSystem ps, Material material, float soft)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            if (material != null) renderer.SetPropertyBlock(Soft(soft));
        }

        private static MaterialPropertyBlock Soft(float metres)
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Soft", metres);
            return block;
        }

        /// <summary>Alpha in from nothing, held, out to nothing (fractions of the life).</summary>
        private static Gradient Fade(float inBy, float holdTo, float peak)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inBy), new GradientAlphaKey(peak, holdTo), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>A curve going between 1 and <paramref name="low"/> <paramref name="turns"/> times over the life.</summary>
        private static AnimationCurve Wave(int turns, float low)
        {
            var keys = new Keyframe[turns * 2 + 1];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(i / (float)(keys.Length - 1), i % 2 == 0 ? 1f : low);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        private static Material Additive()
        {
            if (_additive != null) return _additive;
            _additive = Make(Glow(), BlendMode.One, BlendMode.One);
            return _additive;
        }

        private static Material Blended()
        {
            if (_blended != null) return _blended;
            _blended = Make(Leaf(), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            return _blended;
        }

        private static Material Rings()
        {
            if (_rings != null) return _rings;
            _rings = Make(Ring(), BlendMode.One, BlendMode.One);
            return _rings;
        }

        private static Material Make(Texture2D texture, BlendMode src, BlendMode dst)
        {
            Shader shader = Resources.Load<Shader>("Shaders/Particle");
            if (shader == null) return null;
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            return m;
        }

        /// <summary>A soft round glow: a bright core falling off smoothly to nothing at the rim.</summary>
        private static Texture2D Glow()
        {
            if (_glow != null) return _glow;
            const int n = 64;
            _glow = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, name = "Glow" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Exp(-r * r * 5f) * Mathf.Clamp01(1f - r) + Mathf.Exp(-r * r * 40f) * 0.6f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            _glow.SetPixels(px);
            _glow.Apply(true);
            return _glow;
        }

        /// <summary>A thin soft ring, brightest on its rim: a ripple seen from above.</summary>
        private static Texture2D Ring()
        {
            if (_ring != null) return _ring;
            const int n = 64;
            _ring = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, name = "Ring" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Exp(-Mathf.Pow((r - 0.85f) / 0.06f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.7f) / 0.05f, 2f));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            _ring.SetPixels(px);
            _ring.Apply(true);
            return _ring;
        }

        /// <summary>A leaf: pointed at both ends, a little asymmetric, with a darker midrib and a stalk.</summary>
        private static Texture2D Leaf()
        {
            if (_leaf != null) return _leaf;
            const int n = 32;
            _leaf = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, name = "Leaf" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Along the leaf: v from 0 (stalk end) to 1 (tip); across: u from -1 to 1.
                    float v = (y + 0.5f) / n, u = ((x + 0.5f) / n * 2f - 1f);
                    float t = (v - 0.1f) / 0.85f;
                    float half = t <= 0f || t >= 1f ? 0f : Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f)), 0.9f) * 0.62f;
                    half *= u < 0f ? 1f : 0.88f;                                  // one side a little narrower
                    float edge = half - Mathf.Abs(u);
                    float a = Mathf.Clamp01(edge * n * 0.5f);                      // a soft pixel's width of edge
                    float shade = 0.82f + 0.18f * Mathf.Clamp01(Mathf.Abs(u) / Mathf.Max(0.05f, half));
                    if (Mathf.Abs(u) < 0.05f && t > 0f && t < 0.92f) shade *= 0.72f;   // the midrib
                    if (t <= 0f && Mathf.Abs(u) < 0.05f && v > 0.02f) { a = 1f; shade = 0.6f; }   // the stalk
                    px[y * n + x] = new Color(shade, shade, shade, a);
                }
            _leaf.SetPixels(px);
            _leaf.Apply(true);
            return _leaf;
        }
    }

    /// <summary>A light that starts bright and fades out.</summary>
    public sealed class Flash : MonoBehaviour
    {
        private Light _light;
        private float _from, _seconds, _start;

        public void Set(Light light, float intensity, float seconds)
        {
            _light = light;
            _from = intensity;
            _seconds = Mathf.Max(0.05f, seconds);
            _start = Time.time;
            light.intensity = intensity;
        }

        private void Update()
        {
            if (_light == null) return;
            float t = Mathf.Clamp01((Time.time - _start) / _seconds);
            _light.intensity = _from * (1f - t) * (1f - t);
        }
    }

    /// <summary>A light that swells and fades slowly between two strengths.</summary>
    public sealed class Breathe : MonoBehaviour
    {
        private Light _light;
        private float _low, _high, _seconds, _phase;

        public void Set(Light light, float low, float high, float seconds)
        {
            _light = light;
            _low = low;
            _high = high;
            _seconds = Mathf.Max(0.1f, seconds);
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (_light == null) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / _seconds + _phase);
            _light.intensity = Mathf.Lerp(_low, _high, t);
        }
    }
}
