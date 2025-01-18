struct POMParams
{
    int steps;
    float4 tilingOffset;
    float2 uv;
    Texture2D heightMap;
    SamplerState heightMapSampler;
    float3 viewDirUVSpace;
};

#define MIN_LAYER_COUNT 8

POMParams PackPOMParameters(int iteration, float4 tilingOffsetValue, float2 uv, Texture2D heightMap, SamplerState heightMapSampler,
                            float3 viewDirUVSpace)
{
    POMParams params;
    params.steps = iteration;
    params.uv = uv;
    params.tilingOffset = tilingOffsetValue;
    params.heightMap = heightMap;
    params.heightMapSampler = heightMapSampler;
    params.viewDirUVSpace = viewDirUVSpace;

    return params;
}

float3 GetInverseObjectScale()
{
    return float3(length(float3(UNITY_MATRIX_I_M[0].x, UNITY_MATRIX_I_M[1].x, UNITY_MATRIX_I_M[2].x)),
                  length(float3(UNITY_MATRIX_I_M[0].y, UNITY_MATRIX_I_M[1].y, UNITY_MATRIX_I_M[2].y)),
                  length(float3(UNITY_MATRIX_I_M[0].z, UNITY_MATRIX_I_M[1].z, UNITY_MATRIX_I_M[2].z)));
}

int ComputeIterationsByViewAngle(int maxIteration, float normalDotViewDir)
{
    float layerWeight = saturate(abs(normalDotViewDir));
    return lerp(maxIteration, MIN_LAYER_COUNT,layerWeight);
}

// Keep offset distance constant in object space
float3 WorldToTangentParallaxVectorWithObjectScaling(float3x3 worldToTangent, float3 vectorWS, float3 inverseObjectScale)
{
    // Don't normalize, otherwise it will make offset not constantly in objetc space
    return mul(worldToTangent,vectorWS) * inverseObjectScale.xzy;
}

float3 TangentToUVSpaceParallaxVector(float3 vectorTS, float2 uvScale)
{
    // Normalization is not needed because we will later divide the xy components by z, 
    // and the result will not be affected by normalization.
    return vectorTS * float3(uvScale,1.0f);
}

float3 WorldToUVSpaceParallaxVector(float3x3 worldToTangent, float3 vectorWS, float3 objectScale, float2 uvScale)
{
    return TangentToUVSpaceParallaxVector(WorldToTangentParallaxVectorWithObjectScaling(worldToTangent,vectorWS,objectScale),uvScale);
}

/// Similar with normal parallax mapping but backward, toward to light source(camera position or light position)
float ParallaxMappingSelfShadow(POMParams params, float2 currentOffsetUV, float rayHeight)
{
    int steps = params.steps;
    float4 tilingOffset = params.tilingOffset;
    float2 uv = params.uv;
    Texture2D heightMap = params.heightMap;
    SamplerState heightMapSampler = params.heightMapSampler;
    float3 viewDirUVSpace = params.viewDirUVSpace;
    float backwardRayHeight = rayHeight;

    float2 uvDDX = ddx(uv);
    float2 uvDDY = ddy(uv);

    float stepsReciprocal = 1.0f / steps;
    float step = (1.0f - rayHeight) * stepsReciprocal;
    float2 layerUVOffset = -(viewDirUVSpace.xy / viewDirUVSpace.z) * stepsReciprocal;

    // First step
    backwardRayHeight += step;
    currentOffsetUV -= layerUVOffset;
    float sampleHeight = heightMap.Sample(heightMapSampler,currentOffsetUV).r;

    for (int i = 0; i < steps; ++i)
    {
        if (sampleHeight > backwardRayHeight)
        {
            break;
        }
        currentOffsetUV -= layerUVOffset;
        backwardRayHeight += step;
        sampleHeight = heightMap.SampleGrad(heightMapSampler,currentOffsetUV,uvDDX,uvDDY).r;
    }

    if (currentOffsetUV.x > tilingOffset.x + tilingOffset.z || currentOffsetUV.y > tilingOffset.y + tilingOffset.w || currentOffsetUV.x < tilingOffset
        .z || currentOffsetUV.y <
        tilingOffset.w) // Remove artifact on edge.
    {
        return float3(1,1,1);
    }

    float shadowMultiplier = backwardRayHeight;
    return shadowMultiplier;
}
/// With Offset Limiting
void ParallaxOcclusionMapping_float(POMParams params, out float2 offsetUV, out float rayHeight)
{
    int steps = params.steps;
    float4 tilingOffset = params.tilingOffset;
    float2 uv = params.uv;
    Texture2D heightMap = params.heightMap;
    SamplerState heightMapSampler = params.heightMapSampler;
    float3 viewDirUVSpace = params.viewDirUVSpace;

    float step = 1.0f / steps;
    // After dividing by the z component, we no longer need to normalize viewDirUVSpace
    // Please see TangentToUVSpaceParallaxVector function
    float2 layerUVOffset = -(viewDirUVSpace.xy / viewDirUVSpace.z) * step;

    // First step
    rayHeight = 1.0f - step;
    offsetUV = uv + layerUVOffset;
    float sampleHeight = heightMap.Sample(heightMapSampler,offsetUV).r;

    // Start Steep Parallax Mapping
    float2 uvDDX = ddx(uv);
    float2 uvDDY = ddy(uv);
    for (int i = 0; i < steps; ++i)
    {
        if (sampleHeight > rayHeight)
        {
            break;
        }
        offsetUV += layerUVOffset;
        rayHeight -= step;
        sampleHeight = heightMap.SampleGrad(heightMapSampler,offsetUV,uvDDX,uvDDY).r;
    }

    // Start Parallax Occlusion Mapping
    float2 prevOffsetUV = offsetUV - layerUVOffset;
    float prevStep = rayHeight + step;

    float afterHeight = sampleHeight - rayHeight;
    float beforeHeight = heightMap.Sample(heightMapSampler,prevOffsetUV).r - prevStep;
    float weight = afterHeight / (afterHeight - beforeHeight);
    offsetUV = lerp(offsetUV,prevOffsetUV,weight);
    rayHeight = lerp(rayHeight,prevStep,weight);

    if (offsetUV.x > tilingOffset.x + tilingOffset.z || offsetUV.y > tilingOffset.y + tilingOffset.w || offsetUV.x < tilingOffset.z || offsetUV.y <
        tilingOffset.w) // Remove artifact on edge.
    {
        discard;
    }
}

/// When use depth offset, this implementation is not required.
// Since each light requires iterations to be computed, we need to pass the maximum number of iterations.
void ParallaxOcclusionMappingWithSelfShadow_float(POMParams params, int maxIteration, float3 positionWS, float3x3 worldToTangentMatrix,
                                                  float3 objectScale, float2 uvScale, out float2 offsetUV, out float height, out float3 selfShadow)
{
    selfShadow = 1;

    ParallaxOcclusionMapping_float(params,offsetUV,height);

    // Calculate Self Shadow of Multiple Lights.
    #ifdef REQUIRE_SELFSHADOW
    #if !defined(SHADERGRAPH_PREVIEW)
    float3 lightDirWS = 0;
    float3 lightColor = 0;
    float lightDistanceAtten = 0;
    MainLightNoShadow_half(lightDirWS, lightColor, lightDistanceAtten);
    float3 lightDirTS = WorldToTangentParallaxVectorWithObjectScaling(worldToTangentMatrix,lightDirWS,objectScale);
    params.steps = ComputeIterationsByViewAngle(maxIteration, lightDirTS.z);
    params.viewDirUVSpace = TangentToUVSpaceParallaxVector(lightDirTS,uvScale);
    selfShadow = ParallaxMappingSelfShadow(params, offsetUV, height) * lightColor;
        
    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(positionWS));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = positionWS;
    uint meshRenderingLayers = GetMeshRenderingLayer();
    #if defined(_ADDITIONAL_LIGHTS)
        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, positionWS, 1);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            lightDirWS = light.direction;
            lightColor = light.color;
            lightDistanceAtten = light.distanceAttenuation;
            float3 lightDirTS = WorldToTangentParallaxVectorWithObjectScaling(worldToTangentMatrix,lightDirWS,objectScale);
            params.steps = ComputeIterationsByViewAngle(maxIteration, lightDirTS.z);
            params.viewDirUVSpace = TangentToUVSpaceParallaxVector(lightDirTS,uvScale);
            selfShadow += ParallaxMappingSelfShadow(params, offsetUV, height) * lightColor * lightDistanceAtten;
        }
        LIGHT_LOOP_END
    #endif
    #endif
    #endif
}
