Shader "GMTK/HDRAdditiveParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HdrIntensity ("HDR Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _HdrIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            // 软点贴图：RGB 全白，柔和度在 Alpha；Additive 需用 alpha 裁出圆点
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed a = tex.a * i.color.a;
                fixed3 rgb = i.color.rgb * tex.rgb * a * _HdrIntensity;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
    FallBack Off
}
