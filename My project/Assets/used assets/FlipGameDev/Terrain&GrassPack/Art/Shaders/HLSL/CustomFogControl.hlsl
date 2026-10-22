#ifndef CUSTOM_FOG_CONTROL_INCLUDED
#define CUSTOM_FOG_CONTROL_INCLUDED

#pragma warning(disable:4005)

static float g_CustomFogDensity = 1.0;

#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
    #define MixFogColor(color, fogColor, fogFactor) lerp(fogColor, color, lerp(1.0, fogFactor, g_CustomFogDensity))
    #define MixFog(color, fogFactor) MixFogColor(color, unity_FogColor.rgb, fogFactor)
#else
    #define MixFogColor(color, fogColor, fogFactor) color
    #define MixFog(color, fogFactor) color
#endif

void CustomFogControl_float(float3 In, float Density, out float3 Out)
{
    g_CustomFogDensity = clamp(Density, 0.0, 1.0);
    Out = In;
}

void CustomFogControl_half(half3 In, half Density, out half3 Out)
{
    g_CustomFogDensity = clamp(Density, 0.0, 1.0);
    Out = In;
}

#endif