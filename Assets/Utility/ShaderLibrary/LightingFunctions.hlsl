#if !defined(CUSTOM_LIGHTING_FUNCTIONS)
#define CUSTOM_LIGHTING_FUNCTIONS

void MainLight_half(float3 WorldPos, out half3 Direction, out half3 Color, out half DistanceAtten, out half ShadowAtten)
{
    InputData inputData = (InputData)0;
    {
        inputData.positionWS = WorldPos;
        // 不支援ScreenSpaceShadow
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    }

#if defined(SHADERGRAPH_PREVIEW)
    Direction = half3(0.5, 0.5, 0);
    Color = 1;
    DistanceAtten = 1;
    ShadowAtten = 1;
#else

#if defined(SHADOWS_SCREEN)
    half4 clipPos = TransformWorldToHClip(WorldPos);
    half4 shadowCoord = ComputeScreenPos(clipPos);
    ShadowAtten = SampleScreenSpaceShadowmap(shadowCoord);
#else
    half4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    ShadowAtten = SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams, true);
#endif
#if defined(_LIGHT_LAYERS)
    uint meshRenderingLayers = GetMeshRenderingLayer();
    if (IsMatchingLightLayer(GetMainLight().layerMask, meshRenderingLayers))
#endif
    {
        Direction = _MainLightPosition.xyz;
        Color = _MainLightColor.rgb;
        DistanceAtten = unity_LightData.z;
    }
#endif
}

void MainLightNoShadow_half(out half3 Direction, out half3 Color, out half DistanceAtten)
{
#if defined(SHADERGRAPH_PREVIEW)
    Direction = half3(0.5, 0.5, 0);
    Color = 1;
    DistanceAtten = 1;
#else
#if defined(_LIGHT_LAYERS)
    uint meshRenderingLayers = GetMeshRenderingLayer();
    if (IsMatchingLightLayer(GetMainLight().layerMask, meshRenderingLayers))
#endif
    {
        Direction = _MainLightPosition.xyz;
        Color = _MainLightColor.rgb;
        DistanceAtten = unity_LightData.z;
    }
#endif
}

void AdditionalLights_float(float3 SpecColor, float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, half4 Shadowmask,
							out float3 Diffuse, out float3 Specular) {
	float3 diffuseColor = 0;
	float3 specularColor = 0;
#ifndef SHADERGRAPH_PREVIEW
	Smoothness = exp2(10 * Smoothness + 1);
	uint pixelLightCount = GetAdditionalLightsCount();
	uint meshRenderingLayers = GetMeshRenderingLayer();

	#if USE_FORWARD_PLUS
	for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
		FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
		Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
	#ifdef _LIGHT_LAYERS
		if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
	#endif
		{
			// Blinn-Phong
			float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
			diffuseColor += LightingLambert(attenuatedLightColor, light.direction, WorldNormal);
			specularColor += LightingSpecular(attenuatedLightColor, light.direction, WorldNormal, WorldView, float4(SpecColor, 0), Smoothness);
		}
	}
	#endif

	// For Foward+ the LIGHT_LOOP_BEGIN macro will use inputData.normalizedScreenSpaceUV, inputData.positionWS, so create that:
	InputData inputData = (InputData)0;
	float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
	inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
	inputData.positionWS = WorldPosition;

	LIGHT_LOOP_BEGIN(pixelLightCount)
		Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
	#ifdef _LIGHT_LAYERS
		if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
	#endif
		{
			// Blinn-Phong
			float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
			diffuseColor += LightingLambert(attenuatedLightColor, light.direction, WorldNormal);
			specularColor += LightingSpecular(attenuatedLightColor, light.direction, WorldNormal, WorldView, float4(SpecColor, 0), Smoothness);
		}
	LIGHT_LOOP_END
#endif

	Diffuse = diffuseColor;
	Specular = specularColor;
}

void DiffuseSpecular_half(half3 Specular, half Smoothness, half3 Direction, half3 LightColor, half3 WorldNormal, half3 WorldView, out half3 Diffuse, out half3 Spec)
{
#if defined(SHADERGRAPH_PREVIEW)
    Diffuse = 0;
    Spec = 0;
#else
    Smoothness = exp2(10 * Smoothness + 1);
    WorldNormal = normalize(WorldNormal);
    WorldView = SafeNormalize(WorldView);
    Diffuse = LightingLambert(LightColor, Direction, WorldNormal);
    Spec = LightingSpecular(LightColor, Direction, WorldNormal, WorldView, half4(Specular, 1.0f), Smoothness);
#endif
}
#endif