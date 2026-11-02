// Swimming fish for the Built-in Render Pipeline.
// The mesh is bent along a procedural spine computed every frame by SwimSpine (C#):
// the spine follows the path the head has travelled, so the body curves through turns,
// and carries a carangiform body wave (small head sway, large tail sweep) and a tail twist.
// Each vertex is mapped from its rest position onto the spine, normals included, so any
// static fish mesh (e.g. from Meshy) swims without a skeleton.
// Spines live in one float texture (SpineAtlas), a row per fish, and everything that differs
// between fish is an instanced property, so all fish sharing a mesh and material are drawn
// together with GPU instancing.
Shader "FishBreeder/FishSwim"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _SmoothnessMap ("Smoothness Map (white = glossy)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.6
        _TintStrength ("Gene Colour Strength", Range(0,1)) = 0
        // Per fish (instanced, set from a MaterialPropertyBlock):
        [HideInInspector] _GeneColor ("Gene Colour", Color) = (1,1,1,1)
        [HideInInspector] _Tint ("Illness Tint", Color) = (1,1,1,1)
        [HideInInspector] _SwimAxis ("Body Axis (towards nose), w = nose position", Vector) = (0,0,1,0.5)
        [HideInInspector] _SwimSide ("Side Axis, w = body length", Vector) = (1,0,0,1)
        [HideInInspector] _SwimUp ("Up Axis, w = spine row (-1 = rest pose)", Vector) = (0,1,0,-1)
        [HideInInspector] _RestCenter ("Rest Centre", Vector) = (0,0,0,0)
        [HideInInspector] _FinMotion ("Mouth open, pectoral phase, pectoral swing, fin phase", Vector) = (0,0,0,0)
        [HideInInspector] _FinMotion2 ("Dorsal/ventral swing, tail flex", Vector) = (0,0,0,0)
    }
    SubShader
    {
        // No batching: batched meshes arrive pre-transformed to world space, and the spine
        // bends object-space vertices, so small batched parts (fins) were flung across the room.
        // GPU instancing keeps each fish's own object space, so it is fine.
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling

        // Must match SwimSpine.SampleCount.
        #define SPINE_SAMPLES 16

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _SmoothnessMap;
        fixed4 _Color;
        half _Glossiness;
        half _TintStrength;

        sampler2D _SpineTex;
        float4 _SpineTexSize; // 1/width, 1/height, width, height

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _GeneColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_DEFINE_INSTANCED_PROP(float4, _SwimAxis)
            UNITY_DEFINE_INSTANCED_PROP(float4, _SwimSide)
            UNITY_DEFINE_INSTANCED_PROP(float4, _SwimUp)
            UNITY_DEFINE_INSTANCED_PROP(float4, _RestCenter)
            UNITY_DEFINE_INSTANCED_PROP(float4, _FinMotion)
            UNITY_DEFINE_INSTANCED_PROP(float4, _FinMotion2)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
        };

        // Texel 4 * sample + k of the fish's row: k = 0 position, 1 forward, 2 side, 3 up.
        float4 Spine(float row, float texel)
        {
            return tex2Dlod(_SpineTex, float4((texel + 0.5) * _SpineTexSize.x, (row + 0.5) * _SpineTexSize.y, 0, 0));
        }

        // Mouth and fins of the big fish (FishFinMotion): uv3.xyz is how far a vertex moves at full
        // swing, uv3.w its motion (1 mouth, 2 pectorals, 3 dorsal/ventral, 4 tail) plus 0.9 * its lag.
        // Fish without the channel, or whose motion is all zero, are left as they are.
        void FinMotion(inout appdata_full v)
        {
            float4 m = v.texcoord3;
            if (m.w < 0.5) return;
            float4 a = UNITY_ACCESS_INSTANCED_PROP(Props, _FinMotion);
            float4 b = UNITY_ACCESS_INSTANCED_PROP(Props, _FinMotion2);
            float channel = floor(m.w);
            float lag = frac(m.w) / 0.9 * 6.2831853;
            float k = channel < 1.5 ? a.x
                : channel < 2.5 ? a.z * (0.5 + 0.5 * sin(a.y - lag))
                : channel < 3.5 ? b.x * sin(a.w - lag)
                : b.y * sin(a.w - lag);
            v.vertex.xyz += m.xyz * k;
        }

        void vert(inout appdata_full v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            FinMotion(v);
            float4 axis4 = UNITY_ACCESS_INSTANCED_PROP(Props, _SwimAxis);
            float4 side4 = UNITY_ACCESS_INSTANCED_PROP(Props, _SwimSide);
            float4 up4 = UNITY_ACCESS_INSTANCED_PROP(Props, _SwimUp);
            float row = up4.w;
            if (row < 0) return;

            float3 axis = axis4.xyz;
            float3 side = side4.xyz;
            float3 up = up4.xyz;

            // Rest coordinates: distance along the body, and offset across it.
            float3 c = v.vertex.xyz - UNITY_ACCESS_INSTANCED_PROP(Props, _RestCenter).xyz;
            float along = dot(c, axis);
            float acrossSide = dot(c, side);
            float acrossUp = dot(c, up);

            float t = saturate((axis4.w - along) / max(side4.w, 1e-5)); // 0 nose, 1 tail tip
            float f = t * (SPINE_SAMPLES - 1);
            float i0 = min(floor(f), SPINE_SAMPLES - 2);
            float w = f - i0;
            float a = i0 * 4;
            float b = a + 4;

            float3 P = lerp(Spine(row, a).xyz, Spine(row, b).xyz, w);
            float3 F = normalize(lerp(Spine(row, a + 1).xyz, Spine(row, b + 1).xyz, w));
            float3 S = normalize(lerp(Spine(row, a + 2).xyz, Spine(row, b + 2).xyz, w));
            float3 U = normalize(lerp(Spine(row, a + 3).xyz, Spine(row, b + 3).xyz, w));

            v.vertex.xyz = P + S * acrossSide + U * acrossUp;

            float3 n = v.normal;
            v.normal = normalize(S * dot(n, side) + U * dot(n, up) + F * dot(n, axis));
            float3 tg = v.tangent.xyz;
            v.tangent.xyz = S * dot(tg, side) + U * dot(tg, up) + F * dot(tg, axis);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 gene = UNITY_ACCESS_INSTANCED_PROP(Props, _GeneColor);
            fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * lerp(fixed4(1,1,1,1), gene, _TintStrength) * tint;
            o.Albedo = c.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Metallic = 0;
            o.Smoothness = _Glossiness * tex2D(_SmoothnessMap, IN.uv_MainTex).r;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
