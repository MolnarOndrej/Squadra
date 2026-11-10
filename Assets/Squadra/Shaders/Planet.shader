// Procedural lit sphere drawn on a quad: terrain, oceans, ice caps, clouds, craters, night-side glow
// and an atmosphere halo. Every planet and moon in the scene is a material of this shader.
Shader "Squadra/Planet"
{
    Properties
    {
        [Header(Shape)]
        _BodyRadius ("Body radius (fraction of quad)", Range(0.3, 1)) = 0.7
        _Seed ("Seed", Float) = 1
        _SpinSpeed ("Spin speed", Float) = 0.05
        _Tilt ("Axis tilt (radians)", Float) = 0.3

        [Header(Light)]
        _LightDir ("Light direction (xyz)", Vector) = (-0.55, 0.5, 0.67, 0)
        _Ambient ("Night side ambient", Range(0, 0.5)) = 0.04
        _Specular ("Ocean specular", Range(0, 2)) = 0.6

        [Header(Terrain)]
        _NoiseScale ("Terrain scale", Float) = 2.2
        _Warp ("Terrain warp", Range(0, 2)) = 0.6
        _Bands ("Latitude banding", Range(0, 1)) = 0
        _BandFrequency ("Band frequency", Float) = 9
        _SeaLevel ("Sea level", Range(0, 1)) = 0.5
        _ColorDeep ("Ocean deep", Color) = (0.02, 0.09, 0.25, 1)
        _ColorShallow ("Ocean shallow", Color) = (0.06, 0.35, 0.55, 1)
        _ColorLow ("Land low", Color) = (0.16, 0.36, 0.16, 1)
        _ColorMid ("Land mid", Color) = (0.45, 0.4, 0.24, 1)
        _ColorHigh ("Land high", Color) = (0.85, 0.83, 0.8, 1)

        [Header(Ice and clouds)]
        _IceColor ("Ice color", Color) = (0.9, 0.95, 1, 1)
        _IceAmount ("Ice caps", Range(0, 1)) = 0.15
        _CloudColor ("Cloud color", Color) = (1, 1, 1, 1)
        _CloudAmount ("Cloud cover", Range(0, 1)) = 0.45
        _CloudScale ("Cloud scale", Float) = 3
        _CloudSpeed ("Cloud drift", Float) = 0.02

        [Header(Craters)]
        _CraterAmount ("Craters", Range(0, 1)) = 0
        _CraterScale ("Crater scale", Float) = 4

        [Header(Glow)]
        _CityColor ("Night lights color", Color) = (1, 0.75, 0.4, 1)
        _CityAmount ("Night lights", Range(0, 4)) = 0
        _LavaColor ("Lava color", Color) = (1, 0.35, 0.08, 1)
        _LavaAmount ("Lava glow", Range(0, 6)) = 0
        _LavaLevel ("Lava level", Range(0, 1)) = 0.35

        [Header(Atmosphere)]
        _AtmoColor ("Atmosphere color", Color) = (0.35, 0.65, 1, 1)
        _AtmoRim ("Rim strength", Range(0, 3)) = 1
        _AtmoHalo ("Halo strength", Range(0, 3)) = 0.8
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
            #include "SquadraNoise.hlsl"

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
                float _BodyRadius;
                float _Seed;
                float _SpinSpeed;
                float _Tilt;
                float4 _LightDir;
                float _Ambient;
                float _Specular;
                float _NoiseScale;
                float _Warp;
                float _Bands;
                float _BandFrequency;
                float _SeaLevel;
                float4 _ColorDeep;
                float4 _ColorShallow;
                float4 _ColorLow;
                float4 _ColorMid;
                float4 _ColorHigh;
                float4 _IceColor;
                float _IceAmount;
                float4 _CloudColor;
                float _CloudAmount;
                float _CloudScale;
                float _CloudSpeed;
                float _CraterAmount;
                float _CraterScale;
                float4 _CityColor;
                float _CityAmount;
                float4 _LavaColor;
                float _LavaAmount;
                float _LavaLevel;
                float4 _AtmoColor;
                float _AtmoRim;
                float _AtmoHalo;
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
                float2 p = (input.uv * 2.0 - 1.0) / _BodyRadius; // the body is the unit disc
                float r = length(p);
                float aa = max(fwidth(r), 1e-4);
                float3 lightDir = normalize(_LightDir.xyz);

                // ---- atmosphere halo outside the limb, brighter toward the light
                float haloSpan = max(1.0 / _BodyRadius - 1.0, 1e-3);
                float haloT = saturate((r - 1.0) / haloSpan);
                float facing = saturate(dot(normalize(float3(p, 0.25)), lightDir) * 0.75 + 0.55);
                float halo = pow(1.0 - haloT, 4.0) * _AtmoHalo * facing * facing;
                float3 haloColor = _AtmoColor.rgb * halo;

                if (r > 1.0 + aa)
                    return half4(haloColor, 0.0);

                // ---- sphere
                float z = sqrt(saturate(1.0 - r * r));
                float3 normal = float3(p, z);
                float3 surface = RotateZ(normal, _Tilt);
                float3 spun = RotateY(surface, _Time.y * _SpinSpeed + _Seed);
                float latitude = spun.y;

                float3 sp = spun * _NoiseScale + _Seed * 17.31;
                float3 warp = float3(
                    ValueNoise3(sp * 1.3 + 11.3),
                    ValueNoise3(sp * 1.3 + 27.1),
                    ValueNoise3(sp * 1.3 + 5.7)) - 0.5;
                float height = Fbm3(sp + warp * _Warp);

                float bands = 0.5 + 0.5 * sin(latitude * _BandFrequency + warp.x * 2.5 + height * 3.0);
                height = lerp(height, lerp(height, bands, 0.75), _Bands);

                // ---- albedo
                float landT = saturate((height - _SeaLevel) / max(1.0 - _SeaLevel, 1e-3));
                float3 land = lerp(_ColorLow.rgb, _ColorMid.rgb, smoothstep(0.0, 0.35, landT));
                land = lerp(land, _ColorHigh.rgb, smoothstep(0.4, 0.85, landT));
                land *= 0.82 + 0.36 * height;

                float ocean = 1.0 - smoothstep(_SeaLevel - 0.012, _SeaLevel + 0.012, height);
                float3 sea = lerp(_ColorDeep.rgb, _ColorShallow.rgb, smoothstep(_SeaLevel - 0.22, _SeaLevel, height));
                float3 albedo = lerp(land, sea, ocean);

                if (_CraterAmount > 0.001)
                {
                    float2 big = Worley3(spun * _CraterScale + _Seed * 3.1);
                    float2 small = Worley3(spun * _CraterScale * 2.6 + _Seed * 9.7);
                    float bigRadius = 0.28 + 0.2 * big.y;
                    float smallRadius = 0.24 + 0.2 * small.y;
                    float bowl = (1.0 - smoothstep(0.0, bigRadius, big.x)) * step(0.35, big.y);
                    float rim = smoothstep(bigRadius - 0.07, bigRadius, big.x)
                        * (1.0 - smoothstep(bigRadius, bigRadius + 0.08, big.x)) * step(0.35, big.y);
                    float bowlSmall = (1.0 - smoothstep(0.0, smallRadius, small.x)) * step(0.5, small.y);
                    albedo *= 1.0 - _CraterAmount * (0.45 * bowl + 0.25 * bowlSmall);
                    albedo += _CraterAmount * rim * 0.12;
                }

                float iceEdge = 1.0 - _IceAmount;
                float ice = smoothstep(iceEdge, iceEdge + 0.07, abs(latitude) + (height - 0.5) * 0.3)
                    * step(0.001, _IceAmount);
                albedo = lerp(albedo, _IceColor.rgb, ice);

                float3 cloudPos = RotateY(surface, _Time.y * (_SpinSpeed + _CloudSpeed) + _Seed * 2.0);
                float cloudNoise = Fbm3(cloudPos * _CloudScale + warp * 0.8 + float3(0.0, _Time.y * 0.004, 0.0));
                float cloudEdge = lerp(0.8, 0.3, _CloudAmount);
                float cloud = smoothstep(cloudEdge, cloudEdge + 0.18, cloudNoise) * step(0.001, _CloudAmount);
                albedo = lerp(albedo, _CloudColor.rgb, cloud * 0.92);

                // ---- light
                float ndl = dot(normal, lightDir);
                float day = smoothstep(-0.18, 0.28, ndl);
                float diffuse = day * (0.22 + 0.82 * saturate(ndl));
                float3 color = albedo * (_Ambient + diffuse);

                float3 halfVector = normalize(lightDir + float3(0.0, 0.0, 1.0));
                float specular = pow(saturate(dot(normal, halfVector)), 48.0) * _Specular * ocean * (1.0 - cloud) * day;
                color += specular;

                // ---- self-lit detail
                float night = 1.0 - smoothstep(-0.3, 0.05, ndl);
                float cities = smoothstep(0.66, 0.74, ValueNoise3(spun * 58.0 + 3.0)) * smoothstep(0.42, 0.68, ValueNoise3(spun * 6.0 + 9.0));
                color += _CityColor.rgb * _CityAmount * cities * night * (1.0 - ocean) * (1.0 - ice) * (1.0 - cloud * 0.8);

                float lava = (1.0 - smoothstep(_LavaLevel - 0.09, _LavaLevel, height)) * step(0.001, _LavaAmount);
                float pulse = 0.75 + 0.25 * sin(_Time.y * 0.9 + height * 40.0);
                color += _LavaColor.rgb * _LavaAmount * lava * pulse * (0.35 + 0.65 * night);

                // ---- atmosphere over the surface, limb darkening
                color *= 0.72 + 0.28 * z;
                float fresnel = pow(1.0 - z, 2.6);
                color += _AtmoColor.rgb * fresnel * _AtmoRim * (0.12 + 0.88 * day);
                color = lerp(color, color + _AtmoColor.rgb * 0.06 * day, saturate(_AtmoRim));

                float body = 1.0 - smoothstep(1.0 - aa * 1.5, 1.0 + aa * 0.5, r);
                float3 rgb = color * body + haloColor * (1.0 - body);
                return half4(rgb, body);
            }
            ENDHLSL
        }
    }
}
