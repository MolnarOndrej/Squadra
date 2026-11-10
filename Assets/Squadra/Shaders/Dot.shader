// Fleet dots and sparks: a soft disc tinted by a linear-space HDR vertex color.
Shader "Squadra/Dot"
{
    Properties
    {
        _Core ("Core radius", Range(0.05, 1)) = 0.5
        _Halo ("Halo strength", Range(0, 2)) = 0.5
        _Additive ("Additive blend", Range(0, 1)) = 0.5
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
            Blend One OneMinusSrcAlpha
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Core;
                float _Halo;
                float _Additive;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv * 2.0 - 1.0);
                float aa = max(fwidth(r), 1e-4);
                float core = 1.0 - smoothstep(_Core - aa, _Core + aa, r);
                float halo = pow(saturate(1.0 - r), 2.0) * _Halo;
                float alpha = saturate(core + halo) * input.color.a;
                return half4(input.color.rgb * alpha, alpha * (1.0 - _Additive * (1.0 - core)));
            }
            ENDHLSL
        }
    }
}
