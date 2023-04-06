Shader "Unlit/Mul"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_LightMap ("LightMap", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
	

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _LightMap;
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				const float4 col = tex2D(_MainTex, i.uv)*tex2D(_LightMap, i.uv);
				//return saturate(col*1.2);
				return col;
			}
			ENDCG
		}
	}
}