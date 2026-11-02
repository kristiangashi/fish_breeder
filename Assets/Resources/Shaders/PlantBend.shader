// Aquarium plants for the Built-in Render Pipeline: the Standard look of the decoration atlas,
// with the whole plant bending like a stem in the vertex shader. The plant is treated as one
// flexible stem standing on its base: the stem's tilt grows from nothing at the base to the tip,
// each vertex rides on the stem at its own height, and its offset from the stem (a leaf sticking
// out) is turned rigidly with the stem there. So nothing stretches or squashes: the stem keeps
// its length and every leaf keeps its shape, it only bends and waves.
// The tilt is a slow wave (always on, out of step from plant to plant) plus the push from fish,
// which PlantBender (C#) keeps as one springy tilt per plant. Everything per plant is an
// instanced property, so plants still draw together with GPU instancing.
Shader "FishBreeder/PlantBend"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _GlossMapScale ("Smoothness Scale", Range(0,1)) = 1
        _WaveTilt ("Wave (tip tilt, radians)", Range(0,0.4)) = 0.07
        _WaveSpeed ("Wave Speed", Range(0,5)) = 0.9
        // Per plant (instanced, set from a MaterialPropertyBlock):
        [HideInInspector] _BendBase ("Base (world), w = height", Vector) = (0,0,0,0)
        [HideInInspector] _BendTilt ("Tip tilt from fish (xz radians)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        // Bends in world space from each plant's own base, so static batching must not merge plants.
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        // Steps along the stem when working out where a height ends up.
        #define STEM_STEPS 6
        // The tilt never goes past this (radians), however hard it's pushed.
        #define MAX_TILT 0.9

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        fixed4 _Color;
        half _GlossMapScale;
        float _WaveTilt;
        float _WaveSpeed;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BendBase)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BendTilt)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
        };

        // The stem's tilt (xz, radians) at share s of its height: none at the base, growing to the
        // tip, so the stem is stiff at the bottom. The wave runs up the stem, so it ripples a little.
        float2 Tilt(float s, float2 push, float2 wave, float t, float phase)
        {
            float2 ripple = float2(sin(t + phase - 2.2 * s), sin(t * 0.77 + phase * 1.3 - 2.2 * s));
            float2 tilt = (push * s + wave * ripple) * s;
            float size = length(tilt);
            return size > MAX_TILT ? tilt * (MAX_TILT / size) : tilt;
        }

        // Turns v the way the upright stem turns when it tilts by this much (Rodrigues).
        float3 Turn(float3 v, float2 tilt)
        {
            float angle = length(tilt);
            if (angle < 1e-5) return v;
            float3 axis = normalize(float3(tilt.y, 0, -tilt.x)); // up x tilt direction
            float c = cos(angle), s = sin(angle);
            return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1 - c);
        }

        void vert(inout appdata_full v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            float4 base = UNITY_ACCESS_INSTANCED_PROP(Props, _BendBase);
            if (base.w <= 0) return;
            float height = base.w;
            float2 push = UNITY_ACCESS_INSTANCED_PROP(Props, _BendTilt).xz;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 wn = UnityObjectToWorldNormal(v.normal);
            float3 wt = UnityObjectToWorldDir(v.tangent.xyz);

            float up = clamp(wp.y - base.y, 0, height * 1.5);
            float s = up / height;
            float t = _Time.y * _WaveSpeed;
            float phase = dot(base.xz, float2(7.3, 5.1));
            float2 wave = float2(_WaveTilt, _WaveTilt * 0.7);

            // Walk up the stem to this height: each step goes along the stem's direction there,
            // so the stem keeps its length as it bends.
            float3 stem = 0;
            float step = up / STEM_STEPS;
            for (int i = 0; i < STEM_STEPS; i++)
            {
                float2 tilt = Tilt((i + 0.5) * step / height, push, wave, t, phase);
                float angle = length(tilt);
                float2 dir = angle > 1e-5 ? tilt / angle : float2(0, 0);
                stem += float3(dir.x * sin(angle), cos(angle), dir.y * sin(angle)) * step;
            }

            // Whatever sticks out from the stem at this height turns with it, rigidly.
            float2 here = Tilt(s, push, wave, t, phase);
            float3 offset = wp - (base.xyz + float3(0, up, 0));
            wp = base.xyz + stem + Turn(offset, here);

            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
            v.normal = normalize(mul((float3x3)unity_WorldToObject, Turn(wn, here)));
            v.tangent.xyz = mul((float3x3)unity_WorldToObject, Turn(wt, here));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Albedo = c.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Metallic = mg.r;
            o.Smoothness = mg.a * _GlossMapScale;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
