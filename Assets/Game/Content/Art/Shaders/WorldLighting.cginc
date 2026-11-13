#ifndef SKETCHLAND_WORLD_LIGHTING_INCLUDED
#define SKETCHLAND_WORLD_LIGHTING_INCLUDED

sampler2D _WorldForegroundLightmap;
sampler2D _WorldBackgroundLightmap;
float4 _WorldLightmapBounds;
float4 _WorldLightmapTexelSize;
float _WorldLightingEnabled;

fixed3 SampleWorldLight(float2 worldPosition)
{
    if (_WorldLightingEnabled < 0.5) return fixed3(1, 1, 1);
    float2 uv = (worldPosition - _WorldLightmapBounds.xy) / _WorldLightmapBounds.zw;
    float2 maskUV = (floor(uv * _WorldLightmapTexelSize.zw) + 0.5) * _WorldLightmapTexelSize.xy;
    if (tex2D(_WorldForegroundLightmap, maskUV).a > 0.5)
    {
        float4 foreground = tex2D(_WorldForegroundLightmap, uv);
        return foreground.rgb / max(foreground.a, 0.0001);
    }
    return tex2D(_WorldBackgroundLightmap, uv).rgb;
}

#endif
