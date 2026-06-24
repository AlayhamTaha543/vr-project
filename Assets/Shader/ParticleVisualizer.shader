Shader "BucketPaint/ParticleVisualizer"
{
    Properties
    {
        _size("Particle Size", Float) = 0.1
        _Color("Color", Color) = (0, 1, 0, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ConfigureProcedural assumeuniformscaling

            #include "UnityCG.cginc"

            struct ParticleData
            {
                float lambda;
                float density;
                float3 predictedPosition;
                float3 velocity;
                float3 position;
                float _pad;
            };

            StructuredBuffer<ParticleData> _particlesBuffer;

            float _size;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            void ConfigureProcedural()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float3 pos = _particlesBuffer[unity_InstanceID].position;
                    float s = max(_size, 0.0001);
                    float invS = 1.0 / s;

                    // Object-to-world matrix for this particle instance.
                    // Translation must be in the _14/_24/_34 column; putting it in the
                    // last row leaves the mesh at the wrong clip-space position.
                    unity_ObjectToWorld._11_21_31_41 = float4(s, 0, 0, 0);
                    unity_ObjectToWorld._12_22_32_42 = float4(0, s, 0, 0);
                    unity_ObjectToWorld._13_23_33_43 = float4(0, 0, s, 0);
                    unity_ObjectToWorld._14_24_34_44 = float4(pos, 1);

                    unity_WorldToObject._11_21_31_41 = float4(invS, 0, 0, 0);
                    unity_WorldToObject._12_22_32_42 = float4(0, invS, 0, 0);
                    unity_WorldToObject._13_23_33_43 = float4(0, 0, invS, 0);
                    unity_WorldToObject._14_24_34_44 = float4(-pos * invS, 1);
                #endif
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float diff = max(0, dot(N, L)) * 0.7 + 0.3;
                return float4(_Color.rgb * diff, _Color.a);
            }
            ENDCG
        }
    }
}
