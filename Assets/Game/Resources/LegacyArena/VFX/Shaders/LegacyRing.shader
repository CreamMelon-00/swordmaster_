// Reuses Cartoon FX's original procedural ring implementation and particle streams.
// The single unlit pass works in the active built-in pipeline and URP without its editor importer.
Shader "TurnLimbo/Legacy/Procedural Ring"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RingTopOffset ("Ring Offset", Float) = 0.07
        _HdrMultiply ("HDR Multiplier", Float) = 2.39
        _SrcBlend ("Blend Source", Float) = 5
        _DstBlend ("Blend Destination", Float) = 10
        _DissolveSmooth ("Dissolve Smooth", Float) = 0.1
        _SoftParticlesFadeDistanceNear ("Near Fade", Float) = 0
        _SoftParticlesFadeDistanceFar ("Far Fade", Float) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vertex_program
            #pragma fragment fragment_program
            #pragma target 3.0
            #pragma multi_compile_fog
            #define CFXR_PROCEDURAL_RING_SHADER
            #define GLOBAL_DISABLE_SOFT_PARTICLES
            #define _CFXR_HDR_BOOST 1
            #include "CFXR_PASSES.cginc"
            ENDCG
        }
    }
}
