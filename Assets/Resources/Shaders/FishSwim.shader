// Swimming fish for the Built-in Render Pipeline.
// Bends the body side to side with a travelling sine wave that grows towards the tail,
// so any static fish mesh (e.g. from Meshy) swims without a skeleton.
// FishVisual sets the axis/length values from the mesh and drives _SwimPhase from speed.
Shader "FishBreeder/FishSwim"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.6
        _SwimPhase ("Swim Phase", Float) = 0
        _SwimAmplitude ("Swim Amplitude (fraction of length)", Float) = 0.12
        _SwimWaves ("Waves Along Body", Float) = 0.9
        _HeadStiffness ("Head Stiffness", Range(0,0.9)) = 0.3
        _BodyAxis ("Body Axis (object space, towards nose)", Vector) = (0,0,1,0)
        _SideAxis ("Side Axis (object space)", Vector) = (1,0,0,0)
        _NoseOffset ("Nose Position Along Axis", Float) = 0.5
        _BodyLength ("Body Length Along Axis", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Glossiness;
        float _SwimPhase, _SwimAmplitude, _SwimWaves, _HeadStiffness, _NoseOffset, _BodyLength;
        float4 _BodyAxis, _SideAxis;

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            float along = dot(v.vertex.xyz, _BodyAxis.xyz);
            float t = saturate((_NoseOffset - along) / max(_BodyLength, 1e-4)); // 0 at nose, 1 at tail tip
            float bend = saturate((t - _HeadStiffness) / (1.0 - _HeadStiffness));
            bend *= bend;
            float wave = sin(_SwimPhase - t * _SwimWaves * 6.2831853);
            v.vertex.xyz += _SideAxis.xyz * (wave * bend * _SwimAmplitude * _BodyLength);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
