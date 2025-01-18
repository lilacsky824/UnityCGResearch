#include "./ShaderLibrary/ParallaxMappingFunctions.hlsl"

CBUFFER_START(UnityPerMaterial)
    sampler2D _AlbedoMap;
    float4 _AlbedoMap_ST;
    Texture2D _HeightMap;
    SamplerState sampler_HeightMap;
    sampler2D _NormalMap;
    float2 _UVObjectSpaceLength;
    float _Iteration;
    float _OffsetDistance;
CBUFFER_END

#ifdef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
float3 _LightDirection;
float3 _LightPosition;
#endif

struct appdata
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
    float3 viewDirWS : TEXCOORD5;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f POMVertex(appdata v)
{
    v2f o = (v2f)0;
    o.uv = TRANSFORM_TEX(v.texcoord,_AlbedoMap);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionWS = vertexInput.positionWS;
    o.positionCS = vertexInput.positionCS;

    VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS,v.tangentOS);
    o.normalWS = normalInput.normalWS;
    o.tangentWS = float4(normalInput.tangentWS,v.tangentOS.w);

    // If camera is Orthographic, return camera direction vector.
    #ifdef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
    float isOrtho = step(-1, -UNITY_MATRIX_VP[3][3]); // need extra handling in shadow pass
    o.viewDirWS = _LightPosition - o.positionWS;
    o.viewDirWS = lerp(o.viewDirWS, _LightDirection,isOrtho);
    #else
    o.viewDirWS = _WorldSpaceCameraPos - o.positionWS;
    o.viewDirWS = lerp(o.viewDirWS, UNITY_MATRIX_V[2],unity_OrthoParams.w);
    #endif
    return o;
}

float4 POMFragment(v2f i, out float outDepth : SV_Depth) : SV_Target
{
    #if defined(UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED) && ! defined(SHADOW_CASTER_DEPTH_OFFSET)
    outDepth = i.positionCS.z;
    return 0;
    #endif

    /// POM with depth offset calculation starts ///
    // Don't normalize vectors in vertex shader, since interpolation do not keep length.
    i.viewDirWS = normalize(i.viewDirWS);
    i.normalWS = normalize(i.normalWS);
    i.tangentWS.xyz = normalize(i.tangentWS.xyz);
    float3x3 worldToTangent = float3x3(i.tangentWS.xyz,cross(i.normalWS,i.tangentWS) * i.tangentWS.w,i.normalWS);
    #ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
    float3x3 tangentToWorld = transpose(worldToTangent);
    #endif

    float3 inverseObjectScale = GetInverseObjectScale();
    float3 viewDirTS = WorldToTangentParallaxVectorWithObjectScaling(worldToTangent,i.viewDirWS,inverseObjectScale);
    float2 uvScale = _OffsetDistance * _AlbedoMap_ST.xy / _UVObjectSpaceLength;
    float3 viewDirUVSpace = TangentToUVSpaceParallaxVector(viewDirTS,uvScale);

    int iteration = ComputeIterationsByViewAngle(_Iteration,viewDirTS.z);

    float rayHeight = 0;
    float3 selfShadow;
    POMParams params = PackPOMParameters(iteration,_AlbedoMap_ST,i.uv,_HeightMap,sampler_HeightMap,viewDirUVSpace);
    #ifdef REQUIRE_SELFSHADOW
    ParallaxOcclusionMappingWithSelfShadow_float(params, _Iteration, i.positionWS, worldToTangent, inverseObjectScale, uvScale, i.uv,rayHeight, selfShadow);
    #else
    ParallaxOcclusionMapping_float(params,i.uv,rayHeight);
    #endif

    // distance from surface to intersection
    float rayTravelDistance = (1 - rayHeight) * _OffsetDistance / max(viewDirTS.z,0.000001f); // prevent division by zero

    // World space position offset from POM
    #ifndef SHADOW_CASTER_DEPTH_OFFSET
    // If shadow caster pass does not have depth offset, the original world position should be used to sample the shadow.
    float3 originalPositionWS = i.positionWS;
    #endif
    float3 posOffsetWS = -i.viewDirWS * rayTravelDistance;
    i.positionWS += posOffsetWS;

    // Calculate Depth
    #ifdef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
    i.positionWS = ApplyShadowBias(i.positionWS,i.normalWS,_LightDirection);
    i.positionCS = TransformWorldToHClip(i.positionWS);
    i.positionCS = ApplyShadowClamping(i.positionCS);
    #else
    i.positionCS = TransformWorldToHClip(i.positionWS);
    #endif

    #if UNITY_REVERSED_Z
    i.positionCS.z = min(i.positionCS.z,i.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
    i.positionCS.z = max(i.positionCS.z, i.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    outDepth = i.positionCS.z / i.positionCS.w;

    #ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
    /// Lighting calculation starts ///
    half3 diffuse = 0;
    half3 specular = 0;

    // Calculate & Sample Color and Normal in world space
    half4 col = tex2D(_AlbedoMap,i.uv);
    half3 sampledNormal = UnpackNormal(tex2D(_NormalMap,i.uv)).rgb;
    sampledNormal = float3(sampledNormal.rg,lerp(1,sampledNormal.b,saturate(_OffsetDistance)));
    i.normalWS = normalize(mul(tangentToWorld,sampledNormal) + i.normalWS);

    #ifndef SHADOW_CASTER_DEPTH_OFFSET
    //Recover world position to sample shadow.
    i.positionWS = originalPositionWS;
    #endif
    // Main Lighting
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 mainLightAttenuatedColor = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    DiffuseSpecular_half(0.5f,0.9f,mainLight.direction,mainLightAttenuatedColor,i.normalWS,i.viewDirWS,diffuse,specular);
    half3 lighting = diffuse + specular;

    // Additional Lighting
    AdditionalLights_float(0.5f,0.9f,i.positionWS,i.normalWS,i.viewDirWS,1,diffuse,specular);
    lighting += diffuse + specular;
    #ifdef REQUIRE_SELFSHADOW
    lighting = min(selfShadow, lighting);
    #endif

    col *= float4(lighting,1);
    return col;
    #else
    return  0;
    #endif
}
