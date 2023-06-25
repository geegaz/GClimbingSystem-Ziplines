Shader "Unlit/Billboard"
{
	Properties
	{
	   _MainTex("Texture Image", 2D) = "white" {}
	   _Color("Color", color) = (1, 1, 1, 1)

	   _ScaleX("Scale X", Float) = 1.0
	   _ScaleY("Scale Y", Float) = 1.0

	   _FadeStart("Camera Fade Start", Range(0, 100)) = 2
	   _FadeEnd("Camera Fade End", Range(0, 100)) = 12
	}
	SubShader
	{
		Tags {
			"Queue" = "Transparent" 
			"IgnoreProjector" = "True" 
			"RenderType" = "Transparent"
			"DisableBatching" = "True"
			}
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM

			#pragma vertex vert  
			#pragma fragment frag

			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			// User-specified uniforms            
			sampler2D _MainTex;
			float4 _Color;
			float _ScaleX;
			float _ScaleY;
			float _FadeStart;
			float _FadeEnd;

			float invLerp(float start, float end, float value) {
				return (value - start) / (end - start);
			}

			struct vertexInput
			{
				float4 vertex : POSITION;
				float4 uv : TEXCOORD0;
			};
			struct vertexOutput
			{
				float4 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 worldPos : TEXCOORD2;
				float4 pos : SV_POSITION;
			};

			vertexOutput vert(vertexInput input)
			{
				vertexOutput output;

				output.worldPos = mul(unity_ObjectToWorld, input.vertex);
				output.pos = mul(UNITY_MATRIX_P,
				mul(UNITY_MATRIX_MV, float4(0.0, 0.0, 0.0, 1.0))
				+ float4(input.vertex.x, input.vertex.y, 0.0, 0.0)
				* float4(_ScaleX, _ScaleY, 1.0, 1.0));

				UNITY_TRANSFER_FOG(output, output.pos);

				output.uv = input.uv;

				return output;
			}

			float4 frag(vertexOutput input) : COLOR
			{
				float camera_distance = distance(input.worldPos, _WorldSpaceCameraPos);
				
				fixed4 col = tex2D(_MainTex, float2(input.uv.xy)) * _Color;
				col.a = lerp(0.0, col.a, saturate(invLerp(_FadeStart, _FadeEnd, camera_distance)));
				
				UNITY_APPLY_FOG(input.fogCoord, col);
				return col;
			}

			ENDCG
		}
	}
}