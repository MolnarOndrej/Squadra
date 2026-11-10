// A glowing ring on a quad: ownership rings, selection outlines, capture progress arcs and shockwaves.
// Driven per renderer through a MaterialPropertyBlock. _Tint is a linear-space HDR color.
Shader "Squadra/Ring"
{
    Properties
    {
        _Tint ("Tint (linear rgb, a = opacity)", Vector) = (1, 1, 1, 1)
        _Radius ("Radius (fraction of half quad)", Range(0, 1)) = 0.8
        _Thickness ("Thickness", Range(0, 0.5)) = 0.02
        _Softness ("Glow width", Range(0.001, 0.5)) = 0.06
        _Glow ("Glow strength", Range(0, 2)) = 0.4
        _Dashes ("Dash count (0 = solid)", Float) = 0
        _DashFill ("Dash fill", Range(0, 1)) = 0.5
        _Spin ("Dash spin (turns per second)", Float) = 0
        _Arc ("Filled arc (0..1, clockwise from top)", Range(0, 1)) = 1
        _Track ("Opacity of the unfilled arc", Range(0, 1)) = 0
        _Additive ("Additive blend", Range(0, 1)) = 0.6
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
                float _Radius;
                float _Thickness;
                float _Softness;
                float _Glow;
                float _Dashes;
                float _DashFill;
                float _Spin;
                float _Arc;
                float _Track;
                float _Additive;
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
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float aa = max(fwidth(r), 1e-4);
                float d = abs(r - _Radius);

                float stroke = 1.0 - smoothstep(_Thickness * 0.5 - aa, _Thickness * 0.5 + aa, d);
                float glow = exp(-(d * d) / (_Softness * _Softness)) * _Glow;
                float shape = saturate(stroke + glow);

                // Angle as 0..1, clockwise from the top. Anti-alias width expressed in the same unit.
                float turn = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                float turnAA = aa / (6.2831853 * max(r, 1e-3));

                float mask = 1.0;
                if (_Dashes > 0.5)
                {
                    float t = frac(turn * _Dashes - _Time.y * _Spin * _Dashes);
                    float edge = turnAA * _Dashes;
                    mask = smoothstep(0.0, edge, t) * (1.0 - smoothstep(_DashFill, _DashFill + edge, t));
                }

                if (_Arc < 0.999)
                {
                    float filled = (1.0 - smoothstep(_Arc - turnAA, _Arc + turnAA, turn)) * step(0.0005, _Arc);
                    mask *= lerp(_Track, 1.0, filled);
                }

                float fade = 1.0 - smoothstep(0.92, 1.0, r); // never clip against the quad edge
                float alpha = shape * mask * fade * _Tint.a;
                return half4(_Tint.rgb * alpha, alpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
}
