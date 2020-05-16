Shader "Demo/GPUSkinning"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            #include "UnityCG.cginc"

			float4 _PoseData[256];

			struct DualQuat
			{
				float4 dual;
				float4 quat;
			};

            float4 DualQuatTransformPoint(float4 dual, float4 quat, float3 p)
            {
                float len = length(quat);
                quat /= len;

                float3 result = p + 2.0f * cross(quat.xyz, cross(quat.xyz, p) + quat.w * p);
                float3 trans  = 2.0f * (quat.w * dual.xyz - dual.w * quat.xyz + cross(quat.xyz, dual.xyz));
                
                result += trans;

                return float4(result.x, result.y, result.z, 1.0);
            }

            float3 DualQuatTransformVector(float4 dual, float4 quat, float3 v)
            {
                return v + 2.0f * cross(quat.xyz, cross(quat.xyz, v) + quat.w * v);
            }

			DualQuat GetSkinDualQuat(int index0, int index1, float weight0, float weight1)
			{
				float4 dual0 = _PoseData[index0 * 2 + 0];
				float4 quat0 = _PoseData[index0 * 2 + 1];
				float4 dual1 = _PoseData[index1 * 2 + 0];
				float4 quat1 = _PoseData[index1 * 2 + 1];

				if (dot(quat0, quat1) < 0.0f) {
					dual1 *= -1.0f;
					quat1 *= -1.0f;
				}
				
				DualQuat dualQuat;
				dualQuat.dual = dual0 * weight0 + dual1 * weight1;
				dualQuat.quat = quat0 * weight0 + quat1 * weight1;

				return dualQuat;
			}
			
            struct appdata
            {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
				float3 normal  : NORMAL;
				float4 tangent : TANGENT;

				// SKIN
                float2 indices : TEXCOORD1;
                float2 weights : TEXCOORD2;
            };

            struct v2f
            {
                float2 uv     : TEXCOORD0;
				float3 normal : NORMAL;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

				DualQuat dualQuat = GetSkinDualQuat((int)(v.indices.x), (int)(v.indices.y), v.weights.x, v.weights.y);
                float4 vertex = DualQuatTransformPoint(dualQuat.dual, dualQuat.quat, v.vertex.xyz);
				float3 normal = DualQuatTransformVector(dualQuat.dual, dualQuat.quat, v.normal.xyz);

				o.normal = normal;
                o.vertex = UnityObjectToClipPos(vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
				col *= max(0.25, dot(i.normal, _WorldSpaceLightPos0.xyz));
                return col;
            }
			
            ENDCG
        }

		Pass
        {
			Tags{ "LightMode" = "ShadowCaster"}

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

			#include "UnityCG.cginc"

			float4 _PoseData[256];

			struct DualQuat
			{
				float4 dual;
				float4 quat;
			};

            float4 DualQuatTransformPoint(float4 dual, float4 quat, float3 p)
            {
                float len = length(quat);
                quat /= len;

                float3 result = p + 2.0f * cross(quat.xyz, cross(quat.xyz, p) + quat.w * p);
                float3 trans  = 2.0f * (quat.w * dual.xyz - dual.w * quat.xyz + cross(quat.xyz, dual.xyz));
                
                result += trans;

                return float4(result.x, result.y, result.z, 1.0);
            }

            float3 DualQuatTransformVector(float4 dual, float4 quat, float3 v)
            {
                return v + 2.0f * cross(quat.xyz, cross(quat.xyz, v) + quat.w * v);
            }

			DualQuat GetSkinDualQuat(int index0, int index1, float weight0, float weight1)
			{
				float4 dual0 = _PoseData[index0 * 2 + 0];
				float4 quat0 = _PoseData[index0 * 2 + 1];
				float4 dual1 = _PoseData[index1 * 2 + 0];
				float4 quat1 = _PoseData[index1 * 2 + 1];

				if (dot(quat0, quat1) < 0.0f) {
					dual1 *= -1.0f;
					quat1 *= -1.0f;
				}
				
				DualQuat dualQuat;
				dualQuat.dual = dual0 * weight0 + dual1 * weight1;
				dualQuat.quat = quat0 * weight0 + quat1 * weight1;

				return dualQuat;
			}
			
            struct appdata
            {
				float4 vertex   : POSITION;
				float3 normal   : NORMAL;
				float4 texcoord : TEXCOORD0;

                float2 indices  : TEXCOORD1;
                float2 weights  : TEXCOORD2;
            };

            struct v2f
            {
				V2F_SHADOW_CASTER;
            };

            v2f vert (appdata v)
            {
                v2f o;

				DualQuat dualQuat = GetSkinDualQuat((int)(v.indices.x), (int)(v.indices.y), v.weights.x, v.weights.y);
                float4 vertex = DualQuatTransformPoint(dualQuat.dual, dualQuat.quat, v.vertex.xyz);

				v.vertex = vertex;

				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				SHADOW_CASTER_FRAGMENT(i);
            }
			
            ENDCG
        }
    }
}
