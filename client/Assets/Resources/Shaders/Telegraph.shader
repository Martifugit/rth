// Telegraph (TelegraphViews): where a creature's special attack will land, drawn over a mesh draped
// on the ground (uv 0..1 across the whole square): a crisp outline and a faint floor from the first
// moment, and a fill that grows from the middle to the edge as the blow comes, so the time left
// reads at a glance. The outline flashes in the last moments. Line widths are in tiles (_Size is the
// square's side in tiles), so a big area and a single tile look like the same family.
// Unlit, so it reads in sun and shade; drawn over the ground, never through walls.
Shader "Rth/Telegraph"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.8, 0.2, 1)
        _Progress ("Wind-up done (0..1)", Range(0, 1)) = 0
        _Size ("Side in tiles", Float) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -2, -2
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Progress;
                float _Size;
                half _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                v.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                v.uv = a.uv;
                return v;
            }

            // Signed distance to a rounded square of half-size b and corner radius r, centred.
            float Box(float2 p, float b, float r)
            {
                float2 q = abs(p) - (b - r);
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                // In tiles from the middle, so widths don't scale with the area.
                float2 p = (v.uv - 0.5) * _Size;
                float aa = max(fwidth(p.x), fwidth(p.y)) * 1.2;
                float half_ = _Size * 0.5 - 0.06;
                float corner = 0.18;

                float d = Box(p, half_, corner);
                half inside = 1 - smoothstep(-aa, aa, d);
                // The outline: a band just inside the edge, with a soft glow round it.
                float band = abs(d + 0.04) - 0.04;
                half edge = 1 - smoothstep(-aa, aa, band);
                half glow = exp(-max(band, 0) * 10) * 0.35;

                // The fill: grows from the middle to the outline as the blow comes.
                float grown = Box(p, max(half_ * _Progress, 0.001), corner * _Progress);
                half fill = (1 - smoothstep(-aa, aa, grown)) * inside;
                // Its leading edge, a brighter line, so the growth reads as movement.
                half front = (1 - smoothstep(-aa, aa, abs(grown) - 0.025)) * inside * step(0.02, _Progress);

                // The last fifth: the outline flashes.
                half urgent = saturate((_Progress - 0.8) * 5);
                half flash = lerp(1, 0.65 + 0.35 * sin(_Time.y * 28), urgent);

                half a = saturate(edge * flash + glow + inside * 0.16 + fill * 0.32 + front * 0.6);
                half3 rgb = lerp(_Color.rgb, 1, (edge * urgent + front) * 0.35);
                return half4(rgb, a * _Alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
