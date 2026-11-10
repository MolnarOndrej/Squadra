// Soft radial glow on a quad, blended additively: ownership auras and flashes.
// _Tint is a linear-space HDR color set per renderer.
Shader "Squadra/Glow"
{
    Properties
    {
        _Tint ("Tint (linear rgb, a = opacity)", Vector) = (1, 1, 1, 1)
        _Power ("Falloff power", Range(0.2, 8)) = 2
        _Hole ("Inner hole radius", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Power;
                float _Hole;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv * 2.0 - 1.0);
                float falloff = pow(saturate(1.0 - r), _Power);
                float hole = smoothstep(_Hole * 0.6, _Hole, r);
                float alpha = falloff * lerp(1.0, hole, step(0.001, _Hole)) * _Tint.a;
                return half4(_Tint.rgb * alpha, 0.0);
            }
            ENDHLSL
        }
    }
}
