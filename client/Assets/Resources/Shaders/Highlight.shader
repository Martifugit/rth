// Highlight (FocusHighlight): an outline round what the player points at (faint) or is busy with
// (brighter, breathing): the thing's own back faces pushed out along their normals, drawn only where
// nothing nearer hides them, so a band shows round its silhouette and nothing over its faces. Cut-out
// leaves keep their shapes (the source material's texture and alpha, set per renderer).
Shader "Rth/Highlight"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.84, 0.42, 1)
        _Halo ("Strength", Range(0, 1)) = 0.6
        _Width ("Width", Float) = 0.045
        _Breathe ("Breathing", Range(0, 1)) = 0.3
        _BaseMap ("Cut-out (from the source)", 2D) = "white" {}
        _Cutoff ("Cut-out threshold", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Halo;
                float _Width;
                half _Breathe;
                float4 _BaseMap_ST;
                half _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                float3 positionWS = TransformObjectToWorld(a.positionOS.xyz) + TransformObjectToWorldNormal(a.normalOS) * _Width;
                v.positionCS = TransformWorldToHClip(positionWS);
                v.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return v;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, v.uv).a - _Cutoff);
                half breathe = 1 - _Breathe * (0.5 + 0.5 * sin(_Time.y * 3.2));
                return half4(_Color.rgb, _Halo * breathe * _Color.a);
            }
            ENDHLSL
        }
    }
}
