// Marker (TileMarker): the tile a click sends the player to, drawn in the shader over a mesh
// draped on the ground there (uv 0..1 across the tile): a crisp rounded-square outline with a soft
// glow inside and out and a faint fill, a ripple that runs out from the middle when it is placed,
// breathing slowly while it waits. Anti-aliased at any distance; unlit, so it reads in sun and shade.
Shader "Rth/Marker"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.88, 0.55, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Age ("Seconds since placed", Float) = 10
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
                half _Alpha;
                float _Age;
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
                float2 p = v.uv - 0.5;
                float aa = max(fwidth(p.x), fwidth(p.y)) * 1.2;

                // The outline, a band a little inside the tile's edge, and its glow.
                float d = Box(p, 0.42, 0.1);
                float band = abs(d) - 0.022;
                half edge = 1 - smoothstep(-aa, aa, band);
                half glow = exp(-max(band, 0) * 28) * 0.45;
                half fill = d < 0 ? 0.10 : 0;

                // The ripple: a thin ring that runs out from the middle to the outline as it settles.
                float t = saturate(_Age / 0.45);
                float ring = abs(Box(p, lerp(0.05, 0.42, t), 0.1)) - 0.014;
                half ripple = (1 - smoothstep(-aa, aa, ring)) * (1 - t);

                half breathe = 0.85 + 0.15 * sin(_Time.y * 2.6);
                half a = saturate(max(edge, ripple) + glow + fill) * breathe * _Alpha * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
