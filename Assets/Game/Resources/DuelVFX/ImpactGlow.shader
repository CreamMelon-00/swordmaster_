Shader "TurnLimbo/Duel Impact Glow"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 0.28, 0.06, 1)
        _Intensity ("Intensity", Float) = 3
        _Progress ("Lifetime Progress", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+50"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Pass
        {
            Name "ImpactGlow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Intensity;
                float _Progress;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float progress = saturate(_Progress);
                float life = 1.0 - progress;

                float ringRadius = lerp(0.2, 0.7, progress);
                float ring = 1.0 - smoothstep(0.045, 0.14, abs(radius - ringRadius));
                float halo = (1.0 - smoothstep(0.12, 0.98, radius)) * 0.18 + ring;
                float core = pow(saturate(1.0 - radius / 0.28), 2.0);

                float horizontal = (1.0 - smoothstep(0.0, 0.045, abs(p.y))) *
                    (1.0 - smoothstep(0.12, 0.95, abs(p.x)));
                float vertical = (1.0 - smoothstep(0.0, 0.055, abs(p.x))) *
                    (1.0 - smoothstep(0.08, 0.62, abs(p.y)));
                const float sine = 0.5735764;
                const float cosine = 0.8191520;
                float2 slashPosition = float2(p.x * cosine - p.y * sine, p.x * sine + p.y * cosine);
                float slash = (1.0 - smoothstep(0.0, 0.035, abs(slashPosition.y))) *
                    (1.0 - smoothstep(0.1, 0.88, abs(slashPosition.x)));
                float rayEnvelope = 1.0 - smoothstep(0.68, 1.0, radius);
                float rays = saturate(horizontal * 0.7 + vertical * 0.5 + slash) * rayEnvelope;

                float fade = life * life;
                float3 coloredLight = max(_Tint.rgb, 0.0) * (halo * 1.1 + rays * 0.65);
                float whiteFlash = core * lerp(2.0, 0.6, progress) + rays * 0.55;
                float3 whiteLight = float3(whiteFlash, whiteFlash, whiteFlash);
                float3 outputColor = (coloredLight + whiteLight) * max(_Intensity, 0.0) * fade;
                return half4(min(outputColor, float3(64.0, 64.0, 64.0)),
                    saturate((halo + core + rays) * fade));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
