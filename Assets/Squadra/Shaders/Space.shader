// Full-view space backdrop: domain-warped nebula, three parallax star layers with twinkle, a few flared
// bright stars and the glow of the off-screen sun that lights the planets. Evaluated in world space so the
// picture does not stretch when the quad is resized to the camera.
Shader "Squadra/Space"
{
    Properties
    {
        _VoidColor ("Void", Color) = (0.02, 0.024, 0.06, 1)
        _NebulaA ("Nebula A", Color) = (0.1, 0.12, 0.3, 1)
        _NebulaB ("Nebula B", Color) = (0.23, 0.11, 0.36, 1)
        _NebulaC ("Nebula C", Color) = (0.05, 0.29, 0.36, 1)
        [NoScaleOffset] _NebulaMap ("Nebula gas and dust map", 2D) = "black" {}
        _NebulaMapStrength ("Nebula map strength", Range(0, 2)) = 0
        _NebulaStrength ("Nebula strength", Range(0, 3)) = 1
        _NebulaScale ("Nebula scale", Float) = 0.11
        _StarColor ("Star color", Color) = (0.86, 0.9, 1, 1)
        _StarWarm ("Warm star color", Color) = (1, 0.82, 0.62, 1)
        _StarDensity ("Star density", Range(0, 1)) = 0.5
        _StarBrightness ("Star brightness", Range(0, 4)) = 1.3
        _SunColor ("Sun glow color", Color) = (1, 0.86, 0.68, 1)
        _SunPosition ("Sun position (world xy)", Vector) = (-12, 7.5, 0, 0)
        _SunStrength ("Sun glow strength", Range(0, 3)) = 0.7
        _Drift ("Drift speed", Float) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SquadraNoise.hlsl"

            TEXTURE2D(_NebulaMap);
            SAMPLER(sampler_NebulaMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _VoidColor;
                float4 _NebulaA;
                float4 _NebulaB;
                float4 _NebulaC;
                float _NebulaMapStrength;
                float _NebulaStrength;
                float _NebulaScale;
                float4 _StarColor;
                float4 _StarWarm;
                float _StarDensity;
                float _StarBrightness;
                float4 _SunColor;
                float4 _SunPosition;
                float _SunStrength;
                float _Drift;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.world = positionWS.xy;
                return output;
            }

            // One layer of point stars on a jittered grid. cellsPerUnit sets density, radius the star size.
            float3 StarLayer(float2 world, float cellsPerUnit, float radius, float layerSeed)
            {
                float2 g = world * cellsPerUnit;
                float2 cell = floor(g);
                float2 f = frac(g) - 0.5;
                float2 rnd = Hash22(cell + layerSeed);
                float present = step(1.0 - _StarDensity * 0.55, Hash21(cell + layerSeed * 1.7 + 4.2));
                float2 offset = (rnd - 0.5) * 0.7;
                float d = length(f - offset) / cellsPerUnit;
                float size = radius * (0.5 + rnd.x);
                float pixelRadius = length(fwidth(world)) * 0.34;
                float resolvedSize = max(size, pixelRadius);
                float coverage = size * size / (resolvedSize * resolvedSize);
                float core = exp(-(d * d) / (resolvedSize * resolvedSize)) * coverage;
                float twinkle = 0.72 + 0.28 * sin(_Time.y * (0.8 + 2.4 * rnd.y) + rnd.x * 40.0);
                float brightness = (0.25 + 0.75 * rnd.y * rnd.y) * twinkle;
                float3 tint = lerp(_StarColor.rgb, _StarWarm.rgb, step(0.78, rnd.x));
                return tint * core * brightness * present;
            }

            // Sparse large stars with a soft cross flare.
            float3 FlareStars(float2 world)
            {
                float cellsPerUnit = 0.32;
                float2 g = world * cellsPerUnit;
                float2 cell = floor(g);
                float2 f = frac(g) - 0.5;
                float2 rnd = Hash22(cell + 91.7);
                float present = step(0.62, Hash21(cell + 13.9));
                float2 d = (f - (rnd - 0.5) * 0.6) / cellsPerUnit;
                float dist = length(d);
                float core = exp(-dist * dist / 0.0016);
                float glow = exp(-dist * 9.0) * 0.35;
                float spikes = exp(-abs(d.x) * 70.0) * exp(-abs(d.y) * 7.0) + exp(-abs(d.y) * 70.0) * exp(-abs(d.x) * 7.0);
                float twinkle = 0.8 + 0.2 * sin(_Time.y * (0.6 + rnd.x) + rnd.y * 30.0);
                float3 tint = lerp(_StarColor.rgb, _StarWarm.rgb, rnd.y);
                return tint * (core * 1.6 + glow + spikes * 0.5) * present * twinkle * (0.5 + 0.5 * rnd.x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 world = input.world;
                float t = _Time.y * _Drift;

                // ---- nebula
                float2 q = world * _NebulaScale + float2(t * 0.6, -t * 0.3);
                float2 warp = float2(Fbm2(q + float2(1.7, 9.2) + t), Fbm2(q + float2(8.3, 2.8) - t));
                float cloudA = Fbm2(q + 2.2 * warp);
                float cloudB = Fbm2(q * 1.6 + 3.1 * warp + 7.7);
                float dust = Fbm2(q * 2.7 - 1.4 * warp + 21.0);
                float fold = Fbm2(q * float2(3.5, 7.0) + 2.5 * warp + 14.3);
                float ribbon = q.y + 0.38 * q.x + (warp.x - 0.5) * 1.45;
                float cloudBank = exp(-abs(ribbon - 0.34) * 2.0);
                float filaments = smoothstep(0.46, 0.72, fold) * cloudBank;

                float3 nebula = _NebulaA.rgb * smoothstep(0.32, 0.78, cloudA) * (0.50 + 0.50 * cloudBank);
                nebula += _NebulaB.rgb * smoothstep(0.42, 0.85, cloudB) * cloudBank * 0.75;
                nebula += _NebulaC.rgb * filaments * 0.48;
                nebula *= 0.22 + 0.78 * smoothstep(0.29, 0.66, dust);
                float3 color = _VoidColor.rgb + nebula * _NebulaStrength;
                if (_NebulaMapStrength > 0.001)
                {
                    // Anchor the image to the map and fade its edges into procedural space on wide views.
                    float2 nebulaUv = (world + float2(t * 0.12, -t * 0.045)) / float2(21.5, 12.09) + 0.5;
                    float2 edge = min(nebulaUv, 1.0 - nebulaUv);
                    float coverage = smoothstep(0.0, 0.075, min(edge.x, edge.y));
                    float3 dustMap = SAMPLE_TEXTURE2D(_NebulaMap, sampler_NebulaMap, nebulaUv).rgb;
                    color += (dustMap * _NebulaMapStrength - nebula * _NebulaStrength * 0.56) * coverage;
                }

                // ---- stars: far layers drift slower than near ones
                float2 driftDir = float2(1.0, 0.25) * _Time.y * _Drift;
                float starOcclusion = 1.0 - 0.45 * smoothstep(0.5, 0.9, cloudA);
                float3 stars = StarLayer(world + driftDir * 0.4, 7.0, 0.009, 3.1) * 0.42;
                stars += StarLayer(world + driftDir * 0.9, 3.6, 0.014, 17.9) * 0.50;
                stars += StarLayer(world + driftDir * 1.8, 1.7, 0.022, 41.3) * 0.57;
                stars += FlareStars(world + driftDir * 2.4) * 0.48;
                color += stars * _StarBrightness * starOcclusion;

                // ---- the sun that lights the planets, just outside the frame
                float sunDistance = length(world - _SunPosition.xy);
                float sun = exp(-sunDistance * 0.30) * 0.44 + exp(-sunDistance * 0.75) * 1.4;
                color += _SunColor.rgb * sun * _SunStrength;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
