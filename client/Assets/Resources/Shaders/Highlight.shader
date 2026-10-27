// Highlight (FocusHighlight): a soft glow laid over what the player is busy with (the door, tree or
// creature an action is queued on), breathing slowly. Two passes over the thing's own meshes: a
// halo (its back faces pushed out along their normals, so a band shows round its outline) and a
// faint fill over its faces with a brighter rim where they turn away; both added to what is under.
Shader "Rth/Highlight"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.84, 0.42, 1)
        _Halo ("Halo", Range(0, 1)) = 0.55
        _Width ("Halo width", Float) = 0.045
        _Fill ("Fill", Range(0, 1)) = 0.22
        _Rim ("Rim", Range(0, 2)) = 0.7
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Halo;
            float _Width;
            half _Fill;
            half _Rim;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 viewWS : TEXCOORD1;
        };

        Varyings Grown(Attributes a, float grow)
        {
            Varyings v;
            float3 normalWS = TransformObjectToWorldNormal(a.normalOS);
            float3 positionWS = TransformObjectToWorld(a.positionOS.xyz) + normalWS * grow;
            v.positionCS = TransformWorldToHClip(positionWS);
            v.normalWS = normalWS;
            v.viewWS = GetWorldSpaceViewDir(positionWS);
            return v;
        }

        half Breathe() { return 0.7 + 0.3 * sin(_Time.y * 3.2); }
        ENDHLSL

        // The halo: back faces, grown outwards, only where nothing nearer hides them.
        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            Varyings Vert(Attributes a) { return Grown(a, _Width); }
            half4 Frag(Varyings v) : SV_Target { return half4(_Color.rgb, _Halo * Breathe() * _Color.a); }
            ENDHLSL
        }

        // The fill: the faces themselves, a little brighter, more so at their edges.
        Pass
        {
            Name "Fill"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            Varyings Vert(Attributes a) { return Grown(a, 0.004); }
            half4 Frag(Varyings v, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(v.normalWS) * (front ? 1 : -1);
                half rim = pow(1 - saturate(dot(n, normalize(v.viewWS))), 2.5);
                return half4(_Color.rgb, saturate((_Fill + rim * _Rim) * Breathe() * _Color.a));
            }
            ENDHLSL
        }
    }
}
