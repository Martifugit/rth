using UnityEngine;

namespace Rth.Client.Characters
{
    /// <summary>The colours of a humanoid's parts. Later, worn equipment can override these.</summary>
    public struct HumanoidLook
    {
        public Color Skin;
        public Color Hair;
        public Color Top;
        public Color Legs;
        public Color Boots;
        public Color Belt;
        public Color Eyes;

        private static readonly Color[] Skins =
        {
            new Color(0.93f, 0.78f, 0.62f), new Color(0.85f, 0.66f, 0.49f),
            new Color(0.70f, 0.50f, 0.34f), new Color(0.48f, 0.33f, 0.22f),
        };
        private static readonly Color[] Hairs =
        {
            new Color(0.22f, 0.15f, 0.09f), new Color(0.45f, 0.30f, 0.15f), new Color(0.08f, 0.07f, 0.06f),
            new Color(0.70f, 0.55f, 0.30f), new Color(0.55f, 0.22f, 0.10f), new Color(0.55f, 0.53f, 0.50f),
        };
        private static readonly Color[] Trousers =
        {
            new Color(0.30f, 0.24f, 0.17f), new Color(0.22f, 0.22f, 0.24f), new Color(0.35f, 0.30f, 0.22f),
        };

        /// <summary>A look picked from the entity id, so each player stays the same while seen.</summary>
        public static HumanoidLook For(int seed, Color top)
        {
            uint h = (uint)seed * 2654435761u;
            return new HumanoidLook
            {
                Skin = Skins[(h >> 4) % Skins.Length],
                Hair = Hairs[(h >> 9) % Hairs.Length],
                Top = top,
                Legs = Trousers[(h >> 15) % Trousers.Length],
                Boots = new Color(0.20f, 0.13f, 0.08f),
                Belt = new Color(0.35f, 0.22f, 0.12f),
                Eyes = new Color(0.08f, 0.07f, 0.07f),
            };
        }
    }

    /// <summary>
    /// A low-poly person built from rigid, flat-shaded parts on a joint hierarchy (the way Old
    /// School models are put together), animated procedurally: idle breathing, a walk and run
    /// cycle whose stride follows the ground covered, a cast, a flinch and a fall on death.
    /// One unit is one tile; it stands about 1.8 tall and faces +Z.
    /// </summary>
    public sealed class HumanoidBody : EntityBody
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static Meshes _meshes;

        private const float CastSeconds = 0.7f;
        private const float HitSeconds = 0.35f;
        private const float DeathSeconds = 0.9f;

        private Transform _pivot, _hips, _spine, _neck, _head;
        private Limb _armL, _armR, _legL, _legR;
        private float _scale = 1f;
        private float _move, _run, _phase;
        private float _idleOffset;
        private float _actionTime = -10f, _hitTime = -10f, _deathTime = -1f;
        private bool _castRight = true;

        public override float Height => 1.8f * _scale;
        public override float Radius => 0.3f * _scale;

        private struct Limb
        {
            public Transform Upper, Lower, End;
        }

        /// <summary>The joint angles of one moment, in degrees, so poses can be blended.</summary>
        private struct Pose
        {
            public float Bob, Lean, Twist, HeadPitch;
            public float ArmFwdL, ArmFwdR, ArmOutL, ArmOutR, ElbowL, ElbowR;
            public float LegFwdL, LegFwdR, KneeL, KneeR;

            public static Pose Lerp(Pose a, Pose b, float t) => new Pose
            {
                Bob = Mathf.Lerp(a.Bob, b.Bob, t),
                Lean = Mathf.Lerp(a.Lean, b.Lean, t),
                Twist = Mathf.Lerp(a.Twist, b.Twist, t),
                HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t),
                ArmFwdL = Mathf.Lerp(a.ArmFwdL, b.ArmFwdL, t),
                ArmFwdR = Mathf.Lerp(a.ArmFwdR, b.ArmFwdR, t),
                ArmOutL = Mathf.Lerp(a.ArmOutL, b.ArmOutL, t),
                ArmOutR = Mathf.Lerp(a.ArmOutR, b.ArmOutR, t),
                ElbowL = Mathf.Lerp(a.ElbowL, b.ElbowL, t),
                ElbowR = Mathf.Lerp(a.ElbowR, b.ElbowR, t),
                LegFwdL = Mathf.Lerp(a.LegFwdL, b.LegFwdL, t),
                LegFwdR = Mathf.Lerp(a.LegFwdR, b.LegFwdR, t),
                KneeL = Mathf.Lerp(a.KneeL, b.KneeL, t),
                KneeR = Mathf.Lerp(a.KneeR, b.KneeR, t),
            };
        }

        // ---- building ----

        public void Build(HumanoidLook look, float scale)
        {
            // Statics outlive Play sessions (no domain reload) but the meshes don't; rebuild them.
            if (_meshes == null || _meshes.Pelvis == null) _meshes = new Meshes();
            _scale = scale;
            _idleOffset = Random.value * 10f;

            _pivot = Joint("Pivot", transform, Vector3.zero);
            _pivot.localScale = Vector3.one * scale;

            _hips = Joint("Hips", _pivot, new Vector3(0f, 0.9f, 0f));
            Part(_hips, _meshes.Pelvis, look.Legs);
            Part(_hips, _meshes.Hem, look.Top);

            _spine = Joint("Spine", _hips, new Vector3(0f, 0.06f, 0f));
            Part(_spine, _meshes.Torso, look.Top);
            Part(_spine, _meshes.Belt, look.Belt);

            _neck = Joint("Neck", _spine, new Vector3(0f, 0.5f, 0f));
            Part(_neck, _meshes.Neck, look.Skin);
            _head = Joint("Head", _neck, new Vector3(0f, 0.06f, 0f));
            Part(_head, _meshes.Head, look.Skin);
            Part(_head, _meshes.Hair, look.Hair);
            Part(_head, _meshes.Eyes, look.Eyes);

            _armL = Arm("L", -1f, look);
            _armR = Arm("R", 1f, look);
            _legL = Leg("L", -1f, look);
            _legR = Leg("R", 1f, look);
        }

        private Limb Arm(string side, float x, HumanoidLook look)
        {
            Transform shoulder = Joint("Shoulder" + side, _spine, new Vector3(0.25f * x, 0.43f, 0f));
            Part(shoulder, _meshes.UpperArm, look.Top);
            Transform elbow = Joint("Elbow" + side, shoulder, new Vector3(0f, -0.29f, 0f));
            Part(elbow, _meshes.Forearm, look.Top);
            Transform wrist = Joint("Wrist" + side, elbow, new Vector3(0f, -0.26f, 0f));
            Part(wrist, _meshes.Hand, look.Skin);
            return new Limb { Upper = shoulder, Lower = elbow, End = wrist };
        }

        private Limb Leg(string side, float x, HumanoidLook look)
        {
            Transform hip = Joint("Hip" + side, _hips, new Vector3(0.1f * x, 0f, 0f));
            Part(hip, _meshes.Thigh, look.Legs);
            Transform knee = Joint("Knee" + side, hip, new Vector3(0f, -0.4f, 0f));
            Part(knee, _meshes.Shin, look.Legs);
            Transform ankle = Joint("Ankle" + side, knee, new Vector3(0f, -0.4f, 0f));
            Part(ankle, _meshes.Foot, look.Boots);
            return new Limb { Upper = hip, Lower = knee, End = ankle };
        }

        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        private static void Part(Transform joint, Mesh mesh, Color color)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(joint, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Matte;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(block);
        }

        // ---- animating ----

        public override void PlayAction()
        {
            _actionTime = Time.time;
            _castRight = Random.value < 0.75f;   // mostly the right hand
        }

        public override void PlayHit() => _hitTime = Time.time;
        public override void PlayDeath() => _deathTime = Time.time;

        public override void Animate(float speed, bool running, float deltaTime)
        {
            if (_hips == null) return;   // lost to a script reload during Play
            float moving = speed > 0.2f ? 1f : 0f;
            _move = Mathf.MoveTowards(_move, moving, deltaTime * 7f);
            // Hold the gait while stopping, so a run doesn't turn into a walk as it slows.
            _run = Mathf.MoveTowards(_run, moving > 0f ? (running ? 1f : 0f) : _run, deltaTime * 4f);

            // One cycle is two steps; its length follows the gait so feet don't slide.
            float stride = Mathf.Lerp(1.5f, 2.3f, _run) * _scale;
            _phase = Mathf.Repeat(_phase + speed * deltaTime / stride, 1f);

            Pose pose = Pose.Lerp(Idle(), Pose.Lerp(Walk(), Run(), _run), _move);
            pose = Cast(pose, Time.time - _actionTime);
            pose = Flinch(pose, Time.time - _hitTime);
            Apply(pose);
            Fall(Time.time - _deathTime);
        }

        private Pose Idle()
        {
            float breath = Mathf.Sin((Time.time + _idleOffset) * Mathf.PI * 2f * 0.28f);
            return new Pose
            {
                Bob = breath * 0.004f,
                Lean = 1f + breath * 1.2f,
                HeadPitch = -breath * 1.5f,
                ArmFwdL = 3f, ArmFwdR = 3f,
                ArmOutL = 5f + breath, ArmOutR = 5f + breath,
                ElbowL = 10f, ElbowR = 10f,
                KneeL = 2f, KneeR = 2f,
            };
        }

        private Pose Walk()
        {
            float a = _phase * Mathf.PI * 2f;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new Pose
            {
                Bob = -Mathf.Abs(s) * 0.035f,
                Lean = 3f,
                Twist = s * 5f,
                HeadPitch = -2f,
                LegFwdL = s * 27f, LegFwdR = -s * 27f,
                // A knee bends as its leg swings through, forward.
                KneeL = 4f + Mathf.Max(0f, c) * 42f, KneeR = 4f + Mathf.Max(0f, -c) * 42f,
                ArmFwdL = -s * 22f, ArmFwdR = s * 22f,
                ArmOutL = 6f, ArmOutR = 6f,
                ElbowL = 14f + Mathf.Max(0f, -s) * 16f, ElbowR = 14f + Mathf.Max(0f, s) * 16f,
            };
        }

        private Pose Run()
        {
            float a = _phase * Mathf.PI * 2f;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new Pose
            {
                Bob = 0.03f - Mathf.Abs(s) * 0.07f,
                Lean = 13f,
                Twist = s * 9f,
                HeadPitch = -10f,
                LegFwdL = 8f + s * 46f, LegFwdR = 8f - s * 46f,
                KneeL = 18f + Mathf.Max(0f, c) * 85f, KneeR = 18f + Mathf.Max(0f, -c) * 85f,
                ArmFwdL = -s * 42f, ArmFwdR = s * 42f,
                ArmOutL = 9f, ArmOutR = 9f,
                ElbowL = 80f, ElbowR = 80f,
            };
        }

        /// <summary>Throw a hand out towards the target, the other drawn to the chest.</summary>
        private Pose Cast(Pose pose, float since)
        {
            if (since < 0f || since > CastSeconds) return pose;
            float t = since / CastSeconds;
            float e = t < 0.22f ? Smooth(t / 0.22f) : t < 0.45f ? 1f : 1f - Smooth((t - 0.45f) / 0.55f);
            float sign = _castRight ? 1f : -1f;
            pose.Lean = Mathf.Lerp(pose.Lean, 7f, e);
            pose.Twist = Mathf.Lerp(pose.Twist, -14f * sign, e);
            pose.HeadPitch = Mathf.Lerp(pose.HeadPitch, -3f, e);
            if (_castRight)
            {
                pose.ArmFwdR = Mathf.Lerp(pose.ArmFwdR, 88f, e);
                pose.ArmOutR = Mathf.Lerp(pose.ArmOutR, 6f, e);
                pose.ElbowR = Mathf.Lerp(pose.ElbowR, 4f, e);
                pose.ArmFwdL = Mathf.Lerp(pose.ArmFwdL, 35f, e);
                pose.ElbowL = Mathf.Lerp(pose.ElbowL, 95f, e);
            }
            else
            {
                pose.ArmFwdL = Mathf.Lerp(pose.ArmFwdL, 88f, e);
                pose.ArmOutL = Mathf.Lerp(pose.ArmOutL, 6f, e);
                pose.ElbowL = Mathf.Lerp(pose.ElbowL, 4f, e);
                pose.ArmFwdR = Mathf.Lerp(pose.ArmFwdR, 35f, e);
                pose.ElbowR = Mathf.Lerp(pose.ElbowR, 95f, e);
            }
            return pose;
        }

        private static Pose Flinch(Pose pose, float since)
        {
            if (since < 0f || since > HitSeconds) return pose;
            float e = Mathf.Sin(since / HitSeconds * Mathf.PI);
            pose.Lean -= 11f * e;
            pose.HeadPitch -= 10f * e;
            pose.ArmOutL += 12f * e;
            pose.ArmOutR += 12f * e;
            pose.KneeL += 10f * e;
            pose.KneeR += 10f * e;
            pose.Bob -= 0.03f * e;
            return pose;
        }

        /// <summary>Knees give, then it topples backwards and lies still.</summary>
        private void Fall(float since)
        {
            if (_deathTime < 0f) return;
            float k = Mathf.Clamp01(since / DeathSeconds);
            float tip = Smooth(Mathf.Clamp01((k - 0.15f) / 0.85f));
            _pivot.localRotation = Quaternion.Euler(-86f * tip, 0f, 0f);
            _pivot.localPosition = new Vector3(0f, 0.12f * tip * _scale, -0.1f * tip * _scale);
            _legL.Lower.localRotation = Quaternion.Euler(30f * (1f - tip) * Smooth(k * 3f), 0f, 0f);
            _legR.Lower.localRotation = _legL.Lower.localRotation;
            _armL.Upper.localRotation = Quaternion.Euler(-20f * tip, 0f, -50f * tip);
            _armR.Upper.localRotation = Quaternion.Euler(-35f * tip, 0f, 45f * tip);
        }

        private void Apply(Pose p)
        {
            _hips.localPosition = new Vector3(0f, 0.9f + p.Bob, 0f);
            _hips.localRotation = Quaternion.Euler(0f, -p.Twist * 0.5f, 0f);
            _spine.localRotation = Quaternion.Euler(p.Lean, p.Twist, 0f);
            _head.localRotation = Quaternion.Euler(p.HeadPitch, -p.Twist * 0.6f, 0f);

            // Hanging limbs: +X swings them back, so forward is negative.
            _armL.Upper.localRotation = Quaternion.Euler(-p.ArmFwdL, 0f, -p.ArmOutL);
            _armR.Upper.localRotation = Quaternion.Euler(-p.ArmFwdR, 0f, p.ArmOutR);
            _armL.Lower.localRotation = Quaternion.Euler(-p.ElbowL, 0f, 0f);
            _armR.Lower.localRotation = Quaternion.Euler(-p.ElbowR, 0f, 0f);

            _legL.Upper.localRotation = Quaternion.Euler(-p.LegFwdL, 0f, 0f);
            _legR.Upper.localRotation = Quaternion.Euler(-p.LegFwdR, 0f, 0f);
            _legL.Lower.localRotation = Quaternion.Euler(p.KneeL, 0f, 0f);
            _legR.Lower.localRotation = Quaternion.Euler(p.KneeR, 0f, 0f);
            // Feet stay roughly level with the ground, toes lifting a little through the swing.
            _legL.End.localRotation = Quaternion.Euler((p.LegFwdL - p.KneeL) * 0.75f, 0f, 0f);
            _legR.End.localRotation = Quaternion.Euler((p.LegFwdR - p.KneeR) * 0.75f, 0f, 0f);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The part meshes, built once and shared by every humanoid.</summary>
        private sealed class Meshes
        {
            public readonly Mesh Pelvis, Hem, Torso, Belt, Neck, Head, Hair, Eyes;
            public readonly Mesh UpperArm, Forearm, Hand, Thigh, Shin, Foot;

            public Meshes()
            {
                Pelvis = new LowPolyMesh()
                    .Prism(8, -0.12f, 0.07f, new Vector2(0.15f, 0.092f), new Vector2(0.158f, 0.098f))
                    .Build("Pelvis");
                // The tunic's skirt, flaring over the top of the legs.
                Hem = new LowPolyMesh()
                    .Prism(8, -0.17f, 0.03f, new Vector2(0.215f, 0.14f), new Vector2(0.178f, 0.112f))
                    .Build("Hem");
                Torso = new LowPolyMesh()
                    .Prism(8, 0f, 0.4f, new Vector2(0.17f, 0.105f), new Vector2(0.215f, 0.125f), capTop: false)
                    .Prism(8, 0.4f, 0.5f, new Vector2(0.215f, 0.125f), new Vector2(0.14f, 0.095f), capBottom: false)
                    .Build("Torso");
                Belt = new LowPolyMesh()
                    .Prism(8, -0.02f, 0.045f, new Vector2(0.178f, 0.113f), new Vector2(0.178f, 0.113f))
                    .Box(new Vector3(-0.035f, -0.025f, 0.1f), new Vector3(0.035f, 0.05f, 0.125f))
                    .Build("Belt");
                Neck = new LowPolyMesh()
                    .Prism(6, -0.02f, 0.08f, new Vector2(0.055f, 0.05f), new Vector2(0.05f, 0.047f))
                    .Build("Neck");
                Head = new LowPolyMesh()
                    .Prism(8, 0f, 0.12f, new Vector2(0.08f, 0.09f), new Vector2(0.112f, 0.122f), capTop: false)
                    .Prism(8, 0.12f, 0.25f, new Vector2(0.112f, 0.122f), new Vector2(0.085f, 0.095f), capBottom: false)
                    .Box(new Vector3(-0.018f, 0.065f, 0.11f), new Vector3(0.018f, 0.12f, 0.15f))   // nose
                    .Build("Head");
                Hair = new LowPolyMesh()
                    .Prism(8, 0.155f, 0.285f, new Vector2(0.122f, 0.134f), new Vector2(0.075f, 0.085f), new Vector2(0f, -0.008f))
                    .Box(new Vector3(-0.118f, 0.04f, -0.13f), new Vector3(0.118f, 0.2f, -0.055f))  // back
                    .Box(new Vector3(-0.122f, 0.1f, -0.06f), new Vector3(-0.1f, 0.19f, 0.06f))      // sides
                    .Box(new Vector3(0.1f, 0.1f, -0.06f), new Vector3(0.122f, 0.19f, 0.06f))
                    .Build("Hair");
                Eyes = new LowPolyMesh()
                    .Box(new Vector3(-0.065f, 0.125f, 0.104f), new Vector3(-0.03f, 0.15f, 0.12f))
                    .Box(new Vector3(0.03f, 0.125f, 0.104f), new Vector3(0.065f, 0.15f, 0.12f))
                    .Build("Eyes");
                UpperArm = new LowPolyMesh()
                    .Prism(6, -0.3f, -0.02f, new Vector2(0.058f, 0.058f), new Vector2(0.068f, 0.068f), capTop: false)
                    .Prism(6, -0.02f, 0.03f, new Vector2(0.068f, 0.068f), new Vector2(0.045f, 0.05f), capBottom: false)
                    .Build("UpperArm");
                Forearm = new LowPolyMesh()
                    .Prism(6, -0.26f, 0.02f, new Vector2(0.05f, 0.05f), new Vector2(0.06f, 0.06f))
                    .Prism(6, -0.27f, -0.2f, new Vector2(0.058f, 0.058f), new Vector2(0.058f, 0.058f))  // cuff
                    .Build("Forearm");
                Hand = new LowPolyMesh()
                    .Prism(4, -0.11f, 0f, new Vector2(0.038f, 0.05f), new Vector2(0.045f, 0.05f), new Vector2(0f, 0.005f))
                    .Build("Hand");
                Thigh = new LowPolyMesh()
                    .Prism(6, -0.42f, -0.03f, new Vector2(0.072f, 0.077f), new Vector2(0.08f, 0.084f))
                    .Build("Thigh");
                Shin = new LowPolyMesh()
                    .Prism(6, -0.4f, 0.02f, new Vector2(0.06f, 0.065f), new Vector2(0.075f, 0.08f))
                    .Build("Shin");
                Foot = new LowPolyMesh()
                    .Prism(6, -0.06f, 0.12f, new Vector2(0.068f, 0.075f), new Vector2(0.066f, 0.07f))    // boot top
                    .Box(new Vector3(-0.065f, -0.1f, -0.08f), new Vector3(0.065f, 0f, 0.19f))           // sole and toe
                    .Build("Foot");
            }
        }
    }
}
