Shader "Hidden/GMTK/Bloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _Threshold ("Threshold", Float) = 0.75
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BrightPass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBright
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Threshold;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBright(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float lum = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                float mask = saturate((lum - _Threshold) * 5.0);
                return fixed4(c.rgb * mask, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BlurHorizontal"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurH
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBlurH(v2f i) : SV_Target
            {
                float2 d = float2(_MainTex_TexelSize.x * 2.0, 0);
                fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
                c += tex2D(_MainTex, i.uv + d * 1.0) * 0.1945946;
                c += tex2D(_MainTex, i.uv - d * 1.0) * 0.1945946;
                c += tex2D(_MainTex, i.uv + d * 2.0) * 0.1216216;
                c += tex2D(_MainTex, i.uv - d * 2.0) * 0.1216216;
                c += tex2D(_MainTex, i.uv + d * 3.0) * 0.054054;
                c += tex2D(_MainTex, i.uv - d * 3.0) * 0.054054;
                c += tex2D(_MainTex, i.uv + d * 4.0) * 0.016216;
                c += tex2D(_MainTex, i.uv - d * 4.0) * 0.016216;
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "BlurVertical"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurV
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBlurV(v2f i) : SV_Target
            {
                float2 d = float2(0, _MainTex_TexelSize.y * 2.0);
                fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
                c += tex2D(_MainTex, i.uv + d * 1.0) * 0.1945946;
                c += tex2D(_MainTex, i.uv - d * 1.0) * 0.1945946;
                c += tex2D(_MainTex, i.uv + d * 2.0) * 0.1216216;
                c += tex2D(_MainTex, i.uv - d * 2.0) * 0.1216216;
                c += tex2D(_MainTex, i.uv + d * 3.0) * 0.054054;
                c += tex2D(_MainTex, i.uv - d * 3.0) * 0.054054;
                c += tex2D(_MainTex, i.uv + d * 4.0) * 0.016216;
                c += tex2D(_MainTex, i.uv - d * 4.0) * 0.016216;
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "Composite"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _Intensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragComposite(v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv);
                fixed3 bloom = tex2D(_BloomTex, i.uv).rgb;
                return fixed4(src.rgb + bloom * _Intensity, src.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
