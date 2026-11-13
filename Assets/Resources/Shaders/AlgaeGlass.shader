// Algae on the inside of a tank's glass, drawn on a quad just inside each pane.
// It grows in from the edges and corners (fastest along the bottom, where the substrate
// is) towards the middle: _Cover is how far it has got (0 = clean glass, 1 = all of it).
// The front edge is lacy and patchy like a real green film, with a faint dusting and hard
// green spots just ahead of it, after reference photos of green dust, green spot and
// green film algae on aquarium glass. Everything is worked out in metres on the pane, so
// a long wall aquarium gets the same size of patches as a small tank.
Shader "FishBreeder/AlgaeGlass"
{
    Properties
    {
        _Cover ("Cover (0..1)", Range(0, 1)) = 0
        _Size ("Pane size (m)", Vector) = (1.6, 0.85, 0, 0)
        _Seed ("Seed", Float) = 0
        _Light ("Light green", Color) = (0.50, 0.68, 0.20, 1)
        _Dark ("Dark green", Color) = (0.20, 0.36, 0.10, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half _Cover;
            float4 _Size;
            float _Seed;
            fixed4 _Light, _Dark;

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

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                for (int k = 0; k < 5; k++) { v += amp * noise(p); p = p * 2.03 + 17.1; amp *= 0.5; }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Cover <= 0.001) return fixed4(0, 0, 0, 0);
                float2 size = max(_Size.xy, 0.05);
                float2 p = i.uv * size;                       // metres on the pane
                float2 q = p + _Seed * 7.31;
                float half_ = 0.5 * min(size.x, size.y);

                // How far in from the nearest edge, 0 at the frame and 1 at the middle of the short side.
                // The bottom grows fastest, then the corners and the waterline at the top.
                float dx = min(p.x, size.x - p.x);
                float dy = min(p.y * 0.65, (size.y - p.y) * 0.9);
                float edge = saturate(min(dx, dy) / half_);
                // Corners get ahead of the sides.
                float corner = saturate(length(float2(dx, dy)) / (half_ * 1.4));
                float grow = min(edge, corner * 0.85 + 0.1);

                // Patchy, lacy front: big blotches and fine fringes.
                float big = fbm(q * 2.2);
                float fine = fbm(q * 11.0 + 3.7);
                float g = grow + (big - 0.5) * 0.55 + (fine - 0.5) * 0.18;

                float cover = _Cover * 1.08;
                float film = smoothstep(cover + 0.025, cover - 0.025, g);

                // A faint green dust ahead of the film, and hard green spots scattered in it.
                float dust = smoothstep(cover + 0.28, cover, g) * 0.1 * saturate(_Cover * 4.0);
                float2 cell = q * 70.0;
                float2 id = floor(cell);
                float2 spotAt = float2(hash21(id + 1.3), hash21(id + 7.9));
                float spotR = 0.18 + 0.12 * hash21(id + 4.4);
                float spotOn = step(hash21(id + 2.2), 0.22) * smoothstep(cover + 0.22, cover + 0.02, g);
                float spot = spotOn * smoothstep(spotR, spotR - 0.06, length(frac(cell) - spotAt));

                // Thicker (darker, more opaque) further inside the film.
                float depth = saturate((cover - g) * 3.0);
                float shade = fbm(q * 5.0 + 9.2);
                fixed3 col = lerp(_Light.rgb, _Dark.rgb, saturate(depth * 0.6 + (shade - 0.5) * 0.9));
                // Thin bright edge where the film is young.
                col = lerp(col, _Light.rgb * 1.15, (1.0 - depth) * film * 0.5);

                half alpha = film * lerp(0.32, 0.9, depth) * lerp(0.85, 1.0, fine);
                alpha = max(alpha, dust);
                alpha = max(alpha, spot * 0.85);
                fixed3 spotCol = _Dark.rgb * 1.1;
                col = lerp(col, spotCol, spot * (1.0 - film));
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
