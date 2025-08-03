
Shader "Unlit/GlowFake"
{
    Properties
    {
        _Color ("Color", Color) = (0, 3, 5, 0.2)
        [HDR]_EmissionColor("Emission Color", Color) = (0, 3, 5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Color [_Color]
        }
    }
    FallBack "Unlit/Transparent"
}
