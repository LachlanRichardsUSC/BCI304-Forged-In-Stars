Shader "Debug/SimpleFoliagePoints"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 1)
        _Scale ("Scale", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // Foliage data structure matching compute shader
            struct FoliagePoint
            {
                float3 position;
                float3 normal;
                float scale;
                float rotation;
            };
            
            // Instancing data
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<FoliagePoint> _FoliagePoints;
            #endif
            
            float4 _Color;
            float _Scale;
            
            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // This is called before vertex shader for each instance
                #endif
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                float4 pos = v.vertex;
                
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    FoliagePoint data = _FoliagePoints[unity_InstanceID];
                    
                    // Apply scale
                    pos.xyz *= data.scale * _Scale;
                    
                    // Apply rotation (simplified - only Y rotation)
                    float s = sin(data.rotation);
                    float c = cos(data.rotation);
                    float3 rotatedPos;
                    rotatedPos.x = pos.x * c - pos.z * s;
                    rotatedPos.y = pos.y;
                    rotatedPos.z = pos.x * s + pos.z * c;
                    
                    // Apply position
                    pos.xyz = rotatedPos + data.position;
                #endif
                
                o.vertex = UnityWorldToClipPos(float4(pos.xyz, 1.0));
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return _Color;
            }
            ENDCG
        }
    }
}