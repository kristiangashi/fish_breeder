// The water inside a tank, drawn as a see-through box just inside the glass.
// Tinted by the tank's water colour, a little more opaque at grazing angles (fresnel),
// murkier as water quality drops, with soft moving highlights on the surface.
// Cheap on purpose: one pass, no grab pass or refraction.
Shader "FishBreeder/WaterVolume"
{
    Properties
    {
        _Color ("Water Colour", Color) = (0.12, 0.35, 0.55, 1)
        _Clarity ("Clarity (1 = crystal clear)", Range(0, 1)) = 1
        _SurfaceHighlight ("Surface Highlight", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Clarity;
            half _SurfaceHighlight;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                half murk = 1.0 - _Clarity;

                fixed3 col = _Color.rgb * 1.25;
                half alpha = lerp(0.07, 0.55, murk) + fresnel * 0.3;

                // The top face gets slow, soft ripple highlights.
                half top = saturate(n.y * 2.0 - 1.0);
                float t = _Time.y;
                float2 p = i.worldPos.xz * 9.0;
                half ripple = sin(p.x + t * 1.3) * sin(p.y * 1.3 - t * 1.1) + sin((p.x + p.y) * 0.7 + t * 0.8);
                ripple = saturate(ripple * 0.5 - 0.35);
                col += top * ripple * _SurfaceHighlight;
                alpha += top * (0.12 + ripple * 0.2);

                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}
