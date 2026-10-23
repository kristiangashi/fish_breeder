// Screen filter for looking into a tank: a faint water wobble, a touch of the water
// colour and a soft vignette. Subtle on purpose, so it reads as "through water" without
// getting in the way of looking at the fish.
Shader "Hidden/FishBreeder/UnderwaterFilter"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.12, 0.35, 0.55, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.08
        _Distortion ("Distortion", Range(0, 0.01)) = 0.0012
        _Vignette ("Vignette", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            half _TintAmount;
            half _Distortion;
            half _Vignette;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                uv += float2(sin(uv.y * 22.0 + t * 1.3), cos(uv.x * 18.0 + t * 1.1)) * _Distortion;
                fixed4 col = tex2D(_MainTex, uv);
                col.rgb = lerp(col.rgb, col.rgb * (_Tint.rgb * 2.0), _TintAmount);
                float2 d = i.uv - 0.5;
                col.rgb *= 1.0 - dot(d, d) * _Vignette * 1.6;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
