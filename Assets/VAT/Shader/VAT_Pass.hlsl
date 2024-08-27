#ifndef VAT_PASS_INCLUDED
#define VAT_PASS_INCLUDED

v2f vert(appdata v, uint id : SV_VertexID) {
    UNITY_SETUP_INSTANCE_ID(v);

    float seed = 0;
    #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
        UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
        seed = data.seed;
    #endif
    float2 resolution = _VATTex_TexelSize.zw;
    int frameCount = resolution.y;
    int randomOffset = seed * frameCount;
    int frame = (frac(_Time.y) * frameCount) * _TimeScale + randomOffset;

    float4 vatOffset = 0;
    SampleVAT_float(_VATTex, sampler_VATTex, resolution, id, frame, vatOffset);
    
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex + float4(vatOffset.xyz, 0));
    o.uv = v.uv;

    return o;
}

#endif