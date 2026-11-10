// Antialiased route dashes with optional motion and a dark backing for bright backgrounds.
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
                float x = input.uv.x * _Length;
                float aa = max(fwidth(input.uv.y) * 2.0, 1e-3);
                float dx = max(fwidth(x), 1e-4);
                float nearestEnd = min(x, _Length - x);
                float ends = smoothstep(0.0, max(min(_EndFade, _Length * 0.35), 1e-3), nearestEnd);
                float spine = 1.0 - smoothstep(0.28 - aa * 0.5, 0.28 + aa * 0.5, across);
                float glow = (1.0 - smoothstep(0.18, 1.0, across)) * 0.18;

                float dash = 1.0;
                if (_Dash > 0.0001)
                {
                    float period = _Dash + _Gap;
                    float phase = frac((x - _Time.y * _Scroll) / period + 0.5) * period - period * 0.5;
                    float halfDash = _Dash * 0.5;
                    dash = 1.0 - smoothstep(halfDash - dx * 0.5, halfDash + dx * 0.5, abs(phase));
                }

                float opacity = dash * ends * _Tint.a;
                float backing = (1.0 - smoothstep(0.30, 0.91, across)) * 0.64;
                return half4(_Tint.rgb * (spine + glow) * opacity, backing * opacity);
            }
            ENDHLSL
        }
    }
}
