Shader "URP/WindAnimatedVertex2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        _wind_dir ("Wind Direction", Vector) = (0.5,0.05,0.5,0)
        _wind_size ("Wind Wave Size", Range(5,50)) = 12
        _sway_stutter_influence ("Sway Stutter Influence", Range(0,1)) = 0.2
        _sway_stutter ("Sway Stutter", Range(0,10)) = 1.5
        _sway_speed ("Sway Speed", Range(0,10)) = 1
        _sway_disp ("Sway Displacement", Range(0,1)) = 0.3
        _wiggle_disp ("Wiggle Displacement", Range(0,1)) = 0.07
        _wiggle_speed ("Wiggle Speed", Range(0,25)) = 0.01
        _b_influence ("Wiggle Influence", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float _Cutoff;

            float4 _wind_dir;
            float _wind_size;
            float _sway_speed;
            float _sway_disp;
            float _wiggle_disp;
            float _wiggle_speed;
            float _sway_stutter;
            float _sway_stutter_influence;
            float _b_influence;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS).xyz;

                float sway = (cos(_Time.y * _sway_speed + (worldPos.x / _wind_size) +
                         sin(_Time.y * _sway_stutter * _sway_speed + (worldPos.x / _wind_size)) * _sway_stutter_influence) + 1) * 0.5;

                float wiggle = cos(_Time.y * IN.positionOS.x * _wiggle_speed + (worldPos.x / _wind_size));

                float3 offset;
                offset.x = sway * _sway_disp * _wind_dir.x * (IN.positionOS.y / 10) +
                           wiggle * _wiggle_disp * _wind_dir.x * IN.color.b * _b_influence;

                offset.z = sway * _sway_disp * _wind_dir.z * (IN.positionOS.y / 10) +
                           wiggle * _wiggle_disp * _wind_dir.z * IN.color.b * _b_influence;

                offset.y = 0;

                float3 displaced = IN.positionOS.xyz + offset;
                OUT.positionHCS = TransformObjectToHClip(float4(displaced, 1.0));
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.uv) * _Tint;
                clip(tex.a - _Cutoff);
                return tex;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
