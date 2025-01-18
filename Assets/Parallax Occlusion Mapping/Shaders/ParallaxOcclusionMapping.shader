Shader "Research/Parallax Occlusion Mapping"
{
	Properties
	{
		_AlbedoMap ("Albedo Map", 2D) = "white" {}
		[NoScaleOffset]_HeightMap ("Height Map", 2D) = "white" {}
		[NoScaleOffset]_NormalMap ("Normal Map", 2D) = "bump" {}
		_UVObjectSpaceLength("UV Vectors' Object Space Length", Vector) = (1, 1, 0, 0)
		_Iteration ("Iteration", Range(1, 128)) = 8
		_OffsetDistance ("Object Space Offset Distance(Meter)", Range (0, 1)) = 1
		[Toggle(REQUIRE_SELFSHADOW)]_UseSelfShadow ("Enable Self Shadow", Range (0, 1)) = 1
		[Toggle(SHADOW_CASTER_DEPTH_OFFSET)]_ShadowCasterDepthOffset ("Enable Shadow Caster with Depth Offset", Range (0, 1)) = 1
		// Blending state
		[HideInInspector] _Surface("__surface", Float) = 0.0
		[HideInInspector] _Blend("__blend", Float) = 0.0
		[HideInInspector] _AlphaClip("__clip", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _ZWrite("__zw", Float) = 1.0
		[HideInInspector] _Cull("__cull", Float) = 2.0
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
			"UniversalMaterialType" = "Lit"
			"IgnoreProjector" = "True"
		}

		Pass
		{
			Name "ForwardLit"
			Tags
			{
				"LightMode" = "UniversalForward"
			}
			Blend[_SrcBlend][_DstBlend]
			ZWrite[_ZWrite]
			Cull[_Cull]

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex POMVertex
			#pragma fragment POMFragment

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS

			#pragma multi_compile_fragment _ REQUIRE_SELFSHADOW
			#pragma multi_compile_fragment  _ SHADOW_CASTER_DEPTH_OFFSET

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer

			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
			#include "../../Utility/ShaderLibrary/LightingFunctions.hlsl"
			#include "./ShaderLibrary/ParallaxOcclusionMappingPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			// -------------------------------------
			// Render State Commands
			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull[_Cull]

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex POMVertex
			#pragma fragment POMFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			// This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#pragma multi_compile_fragment  _ SHADOW_CASTER_DEPTH_OFFSET

			#ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
			#define UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED

			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "../../Utility/ShaderLibrary/LightingFunctions.hlsl"
			#include "./ShaderLibrary/ParallaxOcclusionMappingPass.hlsl"
			#endif
			ENDHLSL
		}
	}
}