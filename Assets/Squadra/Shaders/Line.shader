// A soft line on a stretched quad, optionally dashed and scrolling: space lanes and the order preview.
// uv.x runs along the line, uv.y across it. _Tint is a linear-space HDR color; _Length is in world units.
Shader "Squadra/Line"
{
    Properties
    {
        _Tint ("Tint (linear rgb, a = opacity)", Vector) = (1, 1, 1, 1)
        _Length ("Length (world units)", Float) = 1
        _Dash ("Dash length (0 = solid)", Float) = 0
        _Gap ("Gap length", Float) = 0.2
        _Scroll ("Scroll speed", Float) = 0
        _EndFade ("End fade (world units)", Float) = 0.3
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
                float _Length;
                float _Dash;
                float _Gap;
                float _Scroll;
                float _EndFade;
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
                float across = abs(input.uv.y * 2.0 - 1.0);
                float body = pow(saturate(1.0 - across), 1.5);

                float x = input.uv.x * _Length;
                float ends = smoothstep(0.0, max(_EndFade, 1e-3), x) * smoothstep(0.0, max(_EndFade, 1e-3), _Length - x);

                float dash = 1.0;
                if (_Dash > 0.0001)
                {
                    float period = _Dash + _Gap;
                    float t = frac((x - _Time.y * _Scroll) / period) * period;
                    float edge = max(fwidth(x), 1e-4);
                    dash = smoothstep(0.0, edge, t) * (1.0 - smoothstep(_Dash - edge, _Dash, t));
                }

                float alpha = body * ends * dash * _Tint.a;
                return half4(_Tint.rgb * alpha, 0.0);
            }
            ENDHLSL
        }
    }
}
