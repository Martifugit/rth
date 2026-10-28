// Highlight (FocusHighlight): an outline round what the player points at (faint) or is busy with
// (brighter): the thing's own back faces pushed out a little and drawn only where nothing nearer
// hides them, so a thin band shows round its silhouette and nothing over its faces. The push follows
// the smoothed normals baked into uv3 at import (OutlineNormalsImporter), so the shell stays closed
// round a flat-shaded model's corners; skinned bodies (_Smooth 0) use their own normals, which move
// with the bones. Cut-out leaves keep their shapes (the source material's texture and alpha).
Shader "Rth/Highlight"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.86, 0.5, 1)
        _Halo ("Strength", Range(0, 1)) = 0.6
        _Width ("Width", Float) = 0.03
        _Breathe ("Breathing", Range(0, 1)) = 0
        _Smooth ("Use baked smooth normals", Float) = 1
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
            Blend SrcAlpha OneMinusSrcAlpha
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
                float _Smooth;
                float4 _BaseMap_ST;
                half _Cutoff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float3 smoothOS : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                float3 n = (_Smooth > 0.5 && dot(a.smoothOS, a.smoothOS) > 0.25) ? a.smoothOS : a.normalOS;
                float3 positionWS = TransformObjectToWorld(a.positionOS.xyz) + normalize(TransformObjectToWorldNormal(n)) * _Width;
                v.positionCS = TransformWorldToHClip(positionWS);
                v.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return v;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, v.uv).a - _Cutoff);
                half breathe = 1 - _Breathe * (0.5 + 0.5 * sin(_Time.y * 3.0));
                return half4(_Color.rgb, _Halo * breathe * _Color.a);
            }
            ENDHLSL
        }
    }
}
