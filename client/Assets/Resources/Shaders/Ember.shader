// Ember (MiningView): fire-setting on a rock, drawn over the rock's own faces. A network of cracks
// (the edges of 3D cells in the rock's own space, so they sit still on it) that glow as the miner's
// side of the stone (round _FireAt, the logs) heats (_Heat 0..1: dull red, orange, and at the peak a pulsing near-white, the
// moment to douse), and once doused show dark and open (_Crack 0..1), fading as the crack is worked.
// Unlit, so the glow reads in shade; only the miner sees their own.
Shader "Rth/Ember"
{
    Properties
    {
        _Heat ("Heat", Range(0, 1)) = 0
        _Crack ("Crack", Range(0, 1)) = 0
        _Cell ("Cell size", Float) = 0.3
        _FireAt ("Fire (world)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+40" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Heat;
                half _Crack;
                float _Cell;
                float4 _FireAt;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 cellPos : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; float footing : TEXCOORD3; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                float3 positionWS = TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS = TransformWorldToHClip(positionWS);
                v.cellPos = a.positionOS.xyz / _Cell;
                // Nearer the fire, hotter: 1 at the logs, nothing beyond about a metre and a quarter, so
                // only the face the fire was laid against glows.
                float near = saturate(1 - distance(positionWS, _FireAt.xyz) / 1.25);
                v.footing = near;
                v.normalWS = TransformObjectToWorldNormal(a.normalOS);
                v.viewWS = GetWorldSpaceViewDir(positionWS);
                return v;
            }

            float3 Hash3(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            // Distance to the nearest edge between 3D cells (two-pass Voronoi).
            float CellEdge(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                float3 nearest = 0; float best = 8;
                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float3 g = float3(x, y, z);
                    float3 r = g + Hash3(i + g) - f;
                    float d = dot(r, r);
                    if (d < best) { best = d; nearest = r; }
                }
                float edge = 8;
                for (int z2 = -1; z2 <= 1; z2++)
                for (int y2 = -1; y2 <= 1; y2++)
                for (int x2 = -1; x2 <= 1; x2++)
                {
                    float3 g = float3(x2, y2, z2);
                    float3 r = g + Hash3(i + g) - f;
                    float3 diff = r - nearest;
                    if (dot(diff, diff) > 0.0001) edge = min(edge, dot(0.5 * (nearest + r), normalize(diff)));
                }
                return edge;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                float e = CellEdge(v.cellPos);
                float w = fwidth(e) * 2.0 + 0.06;
                half seam = 1 - smoothstep(0.0, w, e);
                half heat = saturate(_Heat);
                half peak = smoothstep(0.92, 1.0, heat);
                half pulse = 1 + peak * 0.35 * sin(_Time.y * 9.0);

                // Glowing cracks: dull red to orange to near-white at the peak, with a faint warmth round them.
                // The colour follows the local warmth: cooler red at the edge of the heated patch, orange
                // and, at the peak, yellow-white nearest the logs.
                half warm = heat * v.footing * v.footing;
                half3 ember = warm < 0.5 ? lerp(half3(0.45, 0.06, 0.02), half3(1.0, 0.38, 0.08), warm / 0.5)
                                         : lerp(half3(1.0, 0.38, 0.08), half3(1.0, 0.86, 0.55), (warm - 0.5) / 0.5);
                ember = lerp(ember, half3(1, 0.97, 0.88), peak * v.footing * 0.5);
                half glow = saturate(seam * smoothstep(0.02, 0.35, warm) * 1.3 * pulse + (1 - smoothstep(0.0, w * 4, e)) * warm * 0.35);

                // Doused: the same cracks dark and open.
                half dark = seam * saturate(_Crack) * 0.75 * smoothstep(0.0, 0.25, v.footing);

                half3 rgb = glow > dark ? ember : half3(0.05, 0.04, 0.03);
                return half4(rgb, saturate(max(glow, dark)));
            }
            ENDHLSL
        }
    }
}
