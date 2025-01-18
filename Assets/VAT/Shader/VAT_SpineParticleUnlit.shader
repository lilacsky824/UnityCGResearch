//SRP BRP Compatible

Shader "VAT/SpineParticleUnlit" {
    Properties {
        _MainTex ("Main Texture", 2D) = "white" { }
        _VATTex ("Vertex Animation Texture", 2D) = "white" { }
        _TimeScale ("Time Scale", Float) = 1.0
    }
    SubShader {
        HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "VAT_Includes.hlsl"
        #include "UnityStandardParticleInstancing.cginc"
        
        #define UNITY_PARTICLE_INSTANCE_DATA ParticleRandomData
        #define UNITY_PARTICLE_INSTANCE_DATA_NO_ANIM_FRAME
        
        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        struct ParticleRandomData {
            float3x4 transform;
            uint color;
            float seed;
        };
        ENDHLSL

        Pass {
            Tags { "Queue" = "AlphaTest" "RenderType" = "Opaque" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup

            #include "VAT_Input.hlsl"
            #include "VAT_Pass.hlsl"

            float4 frag(v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _ClipThreshold);

                return col;
            }
            ENDHLSL
        }
        
        // ------------------------------------------------------------------
        //  Depth Only pass.
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma target 2.0
            
            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag
            
            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "VAT_Input.hlsl"
            #include "VAT_Pass.hlsl"

            half frag(v2f i) : SV_TARGET {
                float4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _ClipThreshold);

                return i.vertex.z;
            }
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture with the forward renderer or the depthNormal prepass with the deferred renderer.
        // Does not support normals currently.
        Pass {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            
            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma target 2.0
            
            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "VAT_Input.hlsl"
            #include "VAT_Pass.hlsl"

            float4 frag(v2f i) : SV_TARGET {
                float4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _ClipThreshold);
                
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Scene view outline pass.
        Pass {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            
            // -------------------------------------
            // Render State Commands
            BlendOp Add
            Blend One Zero
            ZWrite On
            Cull Off
            
            HLSLPROGRAM
            #define PARTICLES_EDITOR_META_PASS
            #pragma target 2.0
            
            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag
            
            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "VAT_Input.hlsl"
            #include "VAT_Pass.hlsl"

            float _ObjectId;
            float _PassValue;

            float4 frag(v2f i) : SV_TARGET {
                float4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _ClipThreshold);

                return float4(_ObjectId, _PassValue, 1, 1);
            }
            
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Scene picking buffer pass.
        Pass {
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }
            
            // -------------------------------------
            // Render State Commands
            BlendOp Add
            Blend One Zero
            ZWrite On
            Cull Off
            
            HLSLPROGRAM
            #define PARTICLES_EDITOR_META_PASS
            #pragma target 2.0
            
            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup
            //#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "VAT_Input.hlsl"
            #include "VAT_Pass.hlsl"

            float4 _SelectionID;

            float4 frag(v2f i) : SV_TARGET {
                float4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _ClipThreshold);

                return _SelectionID;
            }
            
            ENDHLSL
        }
    }
}