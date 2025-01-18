#ifndef VAT_INCLUDED
#define VAT_INCLUDED

#define HALFPIXELOFFSET float2(0.5f, 0.5f)

float hash(float n) {
    return frac(sin(n) * 43.5f);
}

float rand(float2 co) {
    return hash(dot(co.xy, float2(12.98f, 78.2f)));
}

float3 UnpackObjectSpaceNormalFromFloat_float(uint packed) {
    uint xInt = packed & 0x3FF;
    uint yInt = (packed >> 10) & 0x3FF;
    uint zInt = (packed >> 20) & 0x3FF;

    //Swizzle normal axis from Blender to Unity
    float3 normal = float3((float)xInt, (float)zInt, (float)yInt) / 1023.0f;
    normal = normal * 2.0f - 1.0f;
    return normal;
}

void SampleVAT_float(Texture2D vat, SamplerState vat_Sampler, float2 vat_Resolution, uint index, float frame, out float4 vatOffset) {
    float2 vatUV = float2(index, frame) + HALFPIXELOFFSET;
    vatUV /= vat_Resolution;
    vatOffset = vat.SampleLevel(vat_Sampler, vatUV, 0);
}

void UnpackVAT_float(float4 raw, out float3 positionOffset, out float3 objectSpaceNormal) {
    positionOffset = float3(raw.x, raw.y, -raw.z);
    objectSpaceNormal = UnpackObjectSpaceNormalFromFloat_float(asuint(raw.w));

    float tempY = objectSpaceNormal.y;
    objectSpaceNormal.y = objectSpaceNormal.z;
    objectSpaceNormal.z = -tempY;
}
#endif