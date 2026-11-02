// The water inside a tank, drawn as a see-through box just inside the glass.
// Tinted by the tank's water colour, a little more opaque at grazing angles (fresnel),
// with soft moving highlights on the surface. Dirty water is a murk that thickens with
// how much water you look through (measured with the camera depth texture), so it only
// ever covers what is inside the tank: a fish at the front glass stays clearer than the
// plants at the back, and nothing outside the glass is touched.
// Cheap on purpose: one pass, no grab pass or refraction.
Shader "FishBreeder/WaterVolume"
{
    Properties
    {
        _Color ("Water Colour", Color) = (0.12, 0.35, 0.55, 1)
        _Clarity ("Clarity (1 = crystal clear)", Range(0, 1)) = 1
        _SurfaceHighlight ("Surface Highlight", Range(0, 1)) = 0.35
        _ClearDensity ("Murk per metre, clean water", Float) = 0.5
        _MurkyDensity ("Murk per metre, filthy water", Float) = 7
        _MaxThickness ("Deepest look through the tank (m)", Float) = 1.5
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
            float _ClearDensity;
            float _MurkyDensity;
            float _MaxThickness;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.eyeDepth);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 toCamera = _WorldSpaceCameraPos - i.worldPos;
                float3 v = normalize(toCamera);
                half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                half murk = 1.0 - _Clarity;

                // How far the view ray travels through water before it hits something solid.
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float rayPerEyeDepth = length(toCamera) / max(i.eyeDepth, 1e-4);
                float thickness = clamp((sceneDepth - i.eyeDepth) * rayPerEyeDepth, 0.0, _MaxThickness);
                float density = lerp(_ClearDensity, _MurkyDensity, murk * murk);
                half fog = 1.0 - exp(-density * thickness);

                fixed3 col = _Color.rgb * lerp(1.25, 0.9, murk);
                half alpha = 0.05 + fog * lerp(0.6, 0.97, murk) + fresnel * 0.25;

                // The top face gets slow, soft ripple highlights.
                half top = saturate(n.y * 2.0 - 1.0);
                float t = _Time.y;
                float2 p = i.worldPos.xz * 9.0;
                half ripple = sin(p.x + t * 1.3) * sin(p.y * 1.3 - t * 1.1) + sin((p.x + p.y) * 0.7 + t * 0.8);
                ripple = saturate(ripple * 0.5 - 0.35);
                col += top * ripple * _SurfaceHighlight * _Clarity;
                alpha += top * (0.08 + ripple * 0.2);

                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}
