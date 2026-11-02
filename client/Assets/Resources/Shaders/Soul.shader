// Soul: the stuff of the dead (a shade, a soul lantern's flame, the gate's mark). Unlit and partly see-through,
// brightest at the rim where the surface turns from the eye, fading out toward the model's foot (so a shade's
// wisp trails into nothing), with a slow breath of brightness. Colour from _BaseColor, set per renderer by
// property block (CreatureBody) or on the shared material (NatureImporter), for palette colours marked "soul".
Shader "Rth/Soul"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.62, 0.77, 0.75, 1)
        _Alpha ("Body alpha", Range(0, 1)) = 0.55
        _Rim ("Rim strength", Range(0, 4)) = 1.6
        _Fade ("Fade height (object space)", Float) = 0.6
        _Breath ("Breath", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alpha;
                half _Rim;
                float _Fade;
                half _Breath;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float height : TEXCOORD2; float seed : TEXCOORD3; float fog : TEXCOORD4; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                float3 positionWS = TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS = TransformWorldToHClip(positionWS);
                v.fog = ComputeFogFactor(v.positionCS.z);
                v.normalWS = TransformObjectToWorldNormal(a.normalOS);
                v.viewWS = GetWorldSpaceViewDir(positionWS);
                v.height = a.positionOS.y;
                // Each object breathes on its own beat.
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                v.seed = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                return v;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                float3 n = normalize(v.normalWS);
                float3 e = normalize(v.viewWS);
                half rim = pow(1 - saturate(abs(dot(n, e))), 2);
                half foot = _Fade > 0 ? saturate(v.height / _Fade) : 1;
                half breath = 1 + _Breath * sin(_Time.y * 1.7 + v.seed * 6.2831);
                half3 colour = _BaseColor.rgb * (0.7 + _Rim * rim) * breath;
                half alpha = saturate((_Alpha + rim * 0.6) * foot) * _BaseColor.a;
                // Additive: fog fades it to nothing (toward black), so souls and flames vanish into the
                // fog with everything else instead of glowing through it.
                colour = MixFogColor(colour, half3(0, 0, 0), v.fog);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
