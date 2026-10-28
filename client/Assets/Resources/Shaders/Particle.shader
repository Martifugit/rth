// Particle (SkillingEffects): a textured particle tinted by its vertex colour, either glowing (added
// to what is behind, for motes and sparks: _SrcBlend One, _DstBlend One) or solid-edged (blended, for
// leaves: SrcAlpha, OneMinusSrcAlpha). Fog applies, so effects sink into the distance like the world.
// Soft against the ground and the model it sits in: it fades where it nears what's behind it, so a
// mote never shows a hard line where it cuts into a trunk.
Shader "Rth/Particle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _SrcBlend ("Source blend", Float) = 5
        _DstBlend ("Destination blend", Float) = 10
        _Soft ("Soft edge against geometry (metres)", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _SrcBlend;
                float _DstBlend;
                float _Soft;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float4 screen : TEXCOORD2; };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                v.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                v.color = a.color;
                v.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                v.fog = ComputeFogFactor(v.positionCS.z);
                v.screen = ComputeScreenPos(v.positionCS);
                return v;
            }

            half4 Frag(Varyings v) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, v.uv) * v.color;
                // Soft particles: fade where the scene behind is close (needs the depth texture; without
                // it the scene depth reads as far away and nothing fades).
                float2 uv = v.screen.xy / v.screen.w;
                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float self = LinearEyeDepth(v.positionCS.z, _ZBufferParams);
                c.a *= saturate((scene - self) / max(_Soft, 0.001));
                bool additive = _DstBlend < 1.5 && _SrcBlend < 1.5;    // One, One
                if (additive)
                {
                    c.rgb *= c.a;                                        // premultiplied: alpha scales the glow
                    c.rgb = MixFogColor(c.rgb, half3(0, 0, 0), v.fog);   // glow fades to nothing in fog
                }
                else c.rgb = MixFog(c.rgb, v.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
