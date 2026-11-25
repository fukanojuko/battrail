#ifndef ADDITIONAL_LIGHT_INCLUDED
#define ADDITIONAL_LIGHT_INCLUDED

void GetMainLight_float(out float3 Direction, out float3 LightColor, out float LightStrength)
{
#ifdef  SHADERGRAPH_PREVIEW
   Direction = half3(0.5,0.5,0);
   LightColor = 0.0f;
   LightStrength = 1.0f;

    
#else
   Light MainLight = GetMainLight();
   Direction = MainLight.direction;
   LightColor = MainLight.color;
   LightStrength = length(MainLight.color);
#endif
}

void AllAdditionalLights_float(float3 WorldPosition,float3 WorldNormal,float2 CutoffThreshold, out float3 LightColor)
{
    LightColor = 0.0f;
    
 #ifndef SHADERGRAPH_PREVIEW
    
    int lightCount = GetAdditionalLightsCount();
    
    for (int i = 0; i < lightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPosition);
        
        float3 color = dot(light.direction, WorldNormal);
        color = smoothstep(CutoffThreshold.x, CutoffThreshold.y, color);
        color *= light.color;
        color *= light.distanceAttenuation;
        
        LightColor += color;

    }
#endif
}

#endif
   
