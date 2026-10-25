// Water (TileGround.BuildWater): one flat surface over a region's water and the ring of tiles round
// it. The banks dip under it, so the waterline is wherever they meet it. Cheap and chunky like the
// painted ground (texels a tile): deep to shallow colour (vertex r: 1 at the waterline, 0 a few
// tiles out), light glints that drift on lakes and run downstream on rivers (vertex gb: the flow's
// direction, a: its speed), broad slow patches, see-through in the shallows so the bank shows under
// it, lit by the sun and fogged like the ground.
Shader "Rth/Water"
{
    Properties
    {
        _Color ("Deep", Color) = (0.22, 0.36, 0.48, 1)
        _Shallow ("Shallow", Color) = (0.42, 0.56, 0.56, 1)
        _Glint ("Glints", Range(0, 1)) = 0.5
        _DeepAlpha ("Deep opacity", Range(0, 1)) = 0.93
        _ShallowAlpha ("Shallow opacity", Range(0, 1)) = 0.82
        _Texels ("Texels a tile", Float) = 8
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Shallow;
                half _Glint;
                half _DeepAlpha;
                half _ShallowAlpha;
                float _Texels;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                v.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS = TransformWorldToHClip(v.positionWS);
                v.color = a.color;
                v.fog = ComputeFogFactor(v.positionCS.z);
                return v;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings v) : SV_Target
            {
                float2 world = v.positionWS.xz;
                // Chunky, like the ground's texels.
                float2 p = (floor(world * _Texels) + 0.5) / _Texels;
                float t = _Time.y;
                float2 flow = v.color.gb * 2.0 - 1.0;
                float speed = v.color.a;
                // Lakes: a light breeze from the south-west; rivers: their current.
                float2 dir = dot(flow, flow) > 1e-4 ? normalize(flow) : float2(0.8, 0.6);
                float2 across = float2(-dir.y, dir.x);
                // In the flow's frame: glints stretched along it and carried by it.
                float along = dot(p, dir) - t * lerp(0.12, 1.1, speed);
                float side = dot(p, across);
                float2 q = float2(along * lerp(1.0, 0.35, speed), side * 1.7);
                float n = Noise(q * 1.4) * 0.6 + Noise(q * 3.1 + 17.0 + t * 0.05) * 0.4;
                float glint = step(0.74 - 0.06 * speed, n);
                float patches = Noise(world * 0.11 + t * 0.02);

                half shallow = v.color.r;
                half3 albedo = lerp(_Color.rgb, _Shallow.rgb, shallow);
                albedo *= 0.92 + 0.14 * patches;
                albedo = lerp(albedo, albedo * 1.35 + 0.06, glint * _Glint);

                Light sun = GetMainLight(TransformWorldToShadowCoord(v.positionWS));
                half sunlit = saturate(dot(half3(0, 1, 0), sun.direction)) * sun.shadowAttenuation;
                half3 lit = albedo * (sun.color * sunlit + SampleSH(half3(0, 1, 0)));
                lit = MixFog(lit, v.fog);
                return half4(lit, lerp(_DeepAlpha, _ShallowAlpha, shallow));
            }
            ENDHLSL
        }
    }
}
