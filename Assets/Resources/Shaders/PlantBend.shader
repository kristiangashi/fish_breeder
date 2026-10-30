// Aquarium plants for the Built-in Render Pipeline: the Standard look of the decoration atlas,
// with the leaves bending in the vertex shader. The plant sways gently all the time, and fish
// swimming through it push the leaves aside: PlantBender (C#) keeps up to four springy "contacts"
// per plant, each a spot where a fish is (or just was), how far the leaves part around it and how
// far they're dragged along with it. Leaves bend from the base up (the base never moves) and keep
// roughly their length. Everything per plant is an instanced property, so plants still draw
// together with GPU instancing.
Shader "FishBreeder/PlantBend"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _GlossMapScale ("Smoothness Scale", Range(0,1)) = 1
        _SwayAmount ("Idle Sway (share of height)", Range(0,0.2)) = 0.025
        _SwaySpeed ("Idle Sway Speed", Range(0,5)) = 0.9
        // Per plant (instanced, set from a MaterialPropertyBlock):
        [HideInInspector] _BendBase ("Base (world), w = height", Vector) = (0,0,0,0)
        [HideInInspector] _Contact0 ("Contact centre, w = radius", Vector) = (0,0,0,0)
        [HideInInspector] _Contact1 ("Contact centre, w = radius", Vector) = (0,0,0,0)
        [HideInInspector] _Contact2 ("Contact centre, w = radius", Vector) = (0,0,0,0)
        [HideInInspector] _Contact3 ("Contact centre, w = radius", Vector) = (0,0,0,0)
        [HideInInspector] _Push0 ("Drag offset, w = parting", Vector) = (0,0,0,0)
        [HideInInspector] _Push1 ("Drag offset, w = parting", Vector) = (0,0,0,0)
        [HideInInspector] _Push2 ("Drag offset, w = parting", Vector) = (0,0,0,0)
        [HideInInspector] _Push3 ("Drag offset, w = parting", Vector) = (0,0,0,0)
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

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        fixed4 _Color;
        half _GlossMapScale;
        float _SwayAmount;
        float _SwaySpeed;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BendBase)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Contact0)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Contact1)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Contact2)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Contact3)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Push0)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Push1)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Push2)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Push3)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
        };

        // Share of a push that moves the whole plant, tip most.
        #define WholePlantLean 0.6

        // How far one contact moves a leaf at wp (up = height above the plant's base).
        float3 ContactOffset(float3 wp, float up, float heightShare, float3 baseWs, float4 contact, float4 push)
        {
            if (contact.w <= 0) return 0;
            float3 d = wp - contact.xyz;
            float horizontal = length(d.xz);
            // Stems carry the bend upwards, so leaves above the fish move too (less as they get further).
            float above = max(d.y, 0) * 0.35;
            float distance = length(float2(horizontal, above));
            // From the base up to the fish's height the stem curves in gradually.
            float stem = saturate(up / max(contact.y - baseWs.y, contact.w * 0.5));
            stem *= stem;
            // Leaves right round the fish part to let it through...
            float x = saturate(distance / (contact.w * 2.5));
            float part = (1 - x) * (1 - x) * stem;
            // ...and the stems it brushes are dragged along with it, further out than the parting.
            float y = saturate(distance / (contact.w * 5));
            float drag = (1 - y) * (1 - y) * stem;
            // The leaves share one stem, so the whole plant leans a little with every push (and
            // sways back with it when the fish has gone).
            drag = max(drag, WholePlantLean * heightShare * heightShare);
            float2 apart = horizontal > 1e-4 ? d.xz / horizontal : float2(0, 0);
            float3 offset = push.xyz * drag;
            offset.xz += apart * (push.w * contact.w * part);
            return offset;
        }

        void vert(inout appdata_full v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            float4 base = UNITY_ACCESS_INSTANCED_PROP(Props, _BendBase);
            if (base.w <= 0) return;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float up = max(wp.y - base.y, 0);
            float h = saturate(up / base.w);

            // A slow idle sway, out of step from plant to plant and from the bottom of a leaf to its tip.
            float phase = dot(base.xz, float2(7.3, 5.1)) + h * 1.2;
            float t = _Time.y * _SwaySpeed;
            float3 offset = float3(sin(t + phase), 0, sin(t * 0.77 + phase * 1.3)) * (_SwayAmount * base.w * h * h);

            offset += ContactOffset(wp, up, h, base.xyz, UNITY_ACCESS_INSTANCED_PROP(Props, _Contact0), UNITY_ACCESS_INSTANCED_PROP(Props, _Push0));
            offset += ContactOffset(wp, up, h, base.xyz, UNITY_ACCESS_INSTANCED_PROP(Props, _Contact1), UNITY_ACCESS_INSTANCED_PROP(Props, _Push1));
            offset += ContactOffset(wp, up, h, base.xyz, UNITY_ACCESS_INSTANCED_PROP(Props, _Contact2), UNITY_ACCESS_INSTANCED_PROP(Props, _Push2));
            offset += ContactOffset(wp, up, h, base.xyz, UNITY_ACCESS_INSTANCED_PROP(Props, _Contact3), UNITY_ACCESS_INSTANCED_PROP(Props, _Push3));

            // A bent leaf keeps its length, so its tip comes down a little instead of stretching.
            float sideways = dot(offset.xz, offset.xz);
            offset.y -= min(sideways / (2 * max(up, 1e-3)), up * 0.6);

            v.vertex.xyz = mul(unity_WorldToObject, float4(wp + offset, 1)).xyz;
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
