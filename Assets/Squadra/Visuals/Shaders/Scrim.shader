// Flat translucent cover with a darker rim, shown behind the match result.
// _Tint is a linear-space color; its alpha is the opacity at the centre.
Shader "Squadra/Scrim"
{
    Properties
    {
        _Tint ("Tint (linear rgb, a = opacity)", Vector) = (0.01, 0.012, 0.03, 0.7)
        _Rim ("Extra opacity at the edges", Range(0, 1)) = 0.25
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
            Blend SrcAlpha OneMinusSrcAlpha
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
                float _Rim;
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
                float alpha = saturate(_Tint.a + _Rim * smoothstep(0.3, 1.2, r));
                return half4(_Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
