// Swimming fish for the Built-in Render Pipeline.
// The mesh is bent along a procedural spine computed every frame by SwimSpine (C#):
// the spine follows the path the head has travelled, so the body curves through turns,
// and carries a carangiform body wave (small head sway, large tail sweep) and a tail twist.
// Each vertex is mapped from its rest position onto the spine, normals included, so any
// static fish mesh (e.g. from Meshy) swims without a skeleton.
Shader "FishBreeder/FishSwim"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _SmoothnessMap ("Smoothness Map (white = glossy)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.6
        [HideInInspector] _SpineEnabled ("Spine Enabled", Float) = 0
        [HideInInspector] _BodyAxis ("Body Axis (towards nose)", Vector) = (0,0,1,0)
        [HideInInspector] _SideAxis ("Side Axis", Vector) = (1,0,0,0)
        [HideInInspector] _UpAxis ("Up Axis", Vector) = (0,1,0,0)
        [HideInInspector] _RestCenter ("Rest Centre", Vector) = (0,0,0,0)
        [HideInInspector] _NoseOffset ("Nose Position Along Axis", Float) = 0.5
        [HideInInspector] _BodyLength ("Body Length Along Axis", Float) = 1
    }
    SubShader
    {
        // No batching: batched meshes arrive pre-transformed to world space, and the spine
        // bends object-space vertices, so small batched parts (fins) were flung across the room.
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        // Must match SwimSpine.SampleCount.
        #define SPINE_SAMPLES 16

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _SmoothnessMap;
        fixed4 _Color;
        half _Glossiness;

        float _SpineEnabled, _NoseOffset, _BodyLength;
        float4 _BodyAxis, _SideAxis, _UpAxis, _RestCenter;
        float4 _SpinePos[SPINE_SAMPLES];
        float4 _SpineFwd[SPINE_SAMPLES];
        float4 _SpineSide[SPINE_SAMPLES];
        float4 _SpineUp[SPINE_SAMPLES];

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            if (_SpineEnabled < 0.5) return;

            float3 axis = _BodyAxis.xyz;
            float3 side = _SideAxis.xyz;
            float3 up = _UpAxis.xyz;

            // Rest coordinates: distance along the body, and offset across it.
            float3 c = v.vertex.xyz - _RestCenter.xyz;
            float along = dot(c, axis);
            float acrossSide = dot(c, side);
            float acrossUp = dot(c, up);

            float t = saturate((_NoseOffset - along) / max(_BodyLength, 1e-5)); // 0 nose, 1 tail tip
            float f = t * (SPINE_SAMPLES - 1);
            int i0 = (int)min(floor(f), SPINE_SAMPLES - 2);
            float w = f - i0;

            float3 P = lerp(_SpinePos[i0].xyz, _SpinePos[i0 + 1].xyz, w);
            float3 F = normalize(lerp(_SpineFwd[i0].xyz, _SpineFwd[i0 + 1].xyz, w));
            float3 S = normalize(lerp(_SpineSide[i0].xyz, _SpineSide[i0 + 1].xyz, w));
            float3 U = normalize(lerp(_SpineUp[i0].xyz, _SpineUp[i0 + 1].xyz, w));

            v.vertex.xyz = P + S * acrossSide + U * acrossUp;

            float3 n = v.normal;
            v.normal = normalize(S * dot(n, side) + U * dot(n, up) + F * dot(n, axis));
            float3 tg = v.tangent.xyz;
            v.tangent.xyz = S * dot(tg, side) + U * dot(tg, up) + F * dot(tg, axis);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
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
