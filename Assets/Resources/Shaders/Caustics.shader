// Moving light patterns on the sand, added on top of what is underneath.
// Procedural (no textures): a few iterations of a classic sine-warp caustic.
Shader "FishBreeder/Caustics"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.7, 0.9, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.35
        _Scale ("Scale", Float) = 3
        _Speed ("Speed", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            half _Strength;
            float _Scale;
            float _Speed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float Caustic(float2 uv, float time)
            {
                float2 p = fmod(uv * 6.28318 * _Scale, 6.28318) - 250.0;
                float2 i = p;
                float c = 1.0;
                const float inten = 0.005;
                for (int n = 0; n < 4; n++)
                {
                    float t = time * (1.0 - (3.5 / float(n + 1)));
                    i = p + float2(cos(t - i.x) + sin(t + i.y), sin(t - i.y) + cos(t + i.x));
                    c += 1.0 / length(float2(p.x / (sin(i.x + t) / inten), p.y / (cos(i.y + t) / inten)));
                }
                c /= 4.0;
                c = 1.17 - pow(c, 1.4);
                return pow(abs(c), 8.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float c = Caustic(i.uv, _Time.y * _Speed + 23.0);
                // Fade out at the edges of the tank floor.
                float2 edge = min(i.uv, 1.0 - i.uv);
                float fade = saturate(min(edge.x, edge.y) * 12.0);
                return fixed4(_Tint.rgb * saturate(c) * _Strength * fade, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
