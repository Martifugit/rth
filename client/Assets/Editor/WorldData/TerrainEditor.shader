// Edit Terrain's look (TerrainEditor.cs): the ground's colours from a map texture (a pixel a tile),
// flat-shaded facets, a faint tile grid, region borders, cliffs tinted red, see-through by _Opacity.
// Editor only. The terrain is drawn without instancing, so the mesh carries real positions and its
// UVs span the whole terrain. Map alpha: 0 no ground (cut away), about 0.75 locked ground (hatched),
// 1 ground you can sculpt.
Shader "Hidden/Rth/TerrainEditor"
{
    Properties
    {
        _MainTex ("Ground map", 2D) = "white" {}
        _Opacity ("Opacity", Range(0.1, 1)) = 0.85
        _Grid ("Tile grid", Range(0, 1)) = 0.3
        _CliffStep ("Cliff: rise across a tile", Float) = 1
        _RegionTiles ("Region size in tiles", Float) = 96
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Opacity;
                float _Grid;
                float _CliffStep;
                float _RegionTiles;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes a)
            {
                Varyings v;
                v.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS = TransformWorldToHClip(v.positionWS);
                v.uv = a.uv;
                return v;
            }

            // How close to a line every `spacing` tiles, 1 on it, fading over about a pixel.
            float Lines(float2 p, float spacing, float width)
            {
                float2 t = p / spacing;
                float2 d = abs(frac(t - 0.5) - 0.5) / max(fwidth(t), 1e-5);
                return 1 - saturate(min(d.x, d.y) / width);
            }

            half4 Frag(Varyings v) : SV_Target
            {
                half4 map = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, v.uv);
                clip(map.a - 0.25);

                // The facet's own normal: low-poly shading, and its slope for cliffs.
                float3 n = normalize(cross(ddy(v.positionWS), ddx(v.positionWS)));
                n *= sign(n.y);
                Light sun = GetMainLight();
                float light = 0.5 + 0.6 * saturate(dot(n, sun.direction));
                float3 c = map.rgb * light;

                float rise = sqrt(saturate(1 - n.y * n.y)) / max(n.y, 1e-3); // across one tile
                if (rise > _CliffStep) c = lerp(c, float3(0.9, 0.15, 0.1), 0.55);

                float2 p = v.positionWS.xz;
                float near = saturate(1.5 - max(fwidth(p).x, fwidth(p).y) * 3); // the grid fades out far away
                c *= 1 - Lines(p, 1, 1) * _Grid * near;
                c = lerp(c, float3(1, 0.85, 0.35), Lines(p, _RegionTiles, 1.5) * 0.6);

                if (map.a < 0.9) // locked: diagonal hatching
                {
                    float hatch = step(0.5, frac((p.x + p.y) / 4));
                    c = lerp(c, c * 0.6, hatch);
                }
                return half4(c, _Opacity);
            }
            ENDHLSL
        }
    }
}
