// The see-through hole an Occluder opens round the player while it stands in the way (Rth/LitCutout,
// CameraOcclusion): a pixel is dithered away when it is nearer the camera than the player and lies
// within a circle round them on screen, fully near the middle and thinning to nothing at the rim, so
// the hole has a soft edge and only what actually covers the player opens (a wall's or a canopy's
// patch over them, not the whole thing).
//   _RthCutPoint  (global) the player's chest in world space.
//   _RthCutSize   (global) x: the circle's radius as a fraction of the screen's height.
//   _RthCut       (per renderer, a MaterialPropertyBlock) how open the hole is, 0-1 (it fades in and out).
//   _RthCutScale  (per renderer) a larger circle for things that guard an area round the player.
#ifndef RTH_OCCLUDER_CUTOUT_INCLUDED
#define RTH_OCCLUDER_CUTOUT_INCLUDED

float4 _RthCutPoint;
float4 _RthCutSize;
float _RthCut;
float _RthCutScale;

void RthCutout(float4 positionCS)
{
    if (_RthCut <= 0.001) return;

    // The player and this pixel in the same screen space, whichever way the platform flips it.
    float4 playerCS = mul(UNITY_MATRIX_VP, float4(_RthCutPoint.xyz, 1.0));
    float4 playerScreen = ComputeScreenPos(playerCS);
    float2 player = playerScreen.xy / playerScreen.w;
    float2 here = GetNormalizedScreenSpaceUV(positionCS);
    float2 d = here - player;
    d.x *= _ScreenParams.x / _ScreenParams.y;
    float radius = max(_RthCutSize.x * max(_RthCutScale, 1.0), 1e-4);
    float r = length(d) / radius;

    // Only what is in front of the player (a little margin, so their own feet's ground stays).
    float playerDepth = -mul(UNITY_MATRIX_V, float4(_RthCutPoint.xyz, 1.0)).z;
    float depth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    float front = saturate((playerDepth - depth - 0.4) / 0.8);

    // Full near the middle, thinning out over the outer 40% of the circle.
    float open = saturate((1.0 - r) / 0.4) * front * saturate(_RthCut);

    // An ordered 4x4 dither: steady (no shimmer) and as fine as the pixels.
    const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 p = uint2(positionCS.xy) % 4;
    float threshold = (bayer[p.y * 4 + p.x] + 0.5) / 16.0;
    clip(threshold - open);
}

#endif
