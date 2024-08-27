#ifndef VAT_INPUT_INCLUDED
#define VAT_INPUT_INCLUDED

CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
float4 _MainTex_ST;

Texture2D _VATTex;
SamplerState sampler_VATTex;
float4 _VATTex_TexelSize;
float _TimeScale;
static const float _ClipThreshold = 0.5f;
CBUFFER_END

#endif