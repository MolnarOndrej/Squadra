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
        [NoScaleOffset] _SurfaceMap ("Surface color map", 2D) = "white" {}
        _SurfaceMapStrength ("Surface map blend", Range(0, 1)) = 0
        _SurfaceLongitude ("Surface map longitude offset", Float) = 0

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

            TEXTURE2D(_SurfaceMap);
            SAMPLER(sampler_SurfaceMap);

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
                float _SurfaceMapStrength;
                float _SurfaceLongitude;
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

            float3 SampleSurfaceMap(float3 direction)
            {
                float longitude = atan2(direction.x, direction.z) / TWO_PI + 0.5 + _SurfaceLongitude;
                float latitude = asin(clamp(direction.y, -1.0, 1.0)) / PI + 0.5;
                float2 uv = float2(frac(longitude), latitude);
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);

                float3 color = SAMPLE_TEXTURE2D_GRAD(_SurfaceMap, sampler_SurfaceMap, uv, dx, dy).rgb;
                float seam = min(uv.x, 1.0 - uv.x);
                if (seam < 0.024)
                {
                    // A narrow crossfade hides mismatched geology at the map's wrap point.
                    float2 opposite = float2(1.0 - uv.x, uv.y);
                    float2 mirror = float2(-1.0, 1.0);
                    float3 acrossSeam = SAMPLE_TEXTURE2D_GRAD(_SurfaceMap, sampler_SurfaceMap, opposite, dx * mirror, dy * mirror).rgb;
                    color = lerp(color, acrossSeam, (1.0 - smoothstep(0.0, 0.024, seam)) * 0.5);
                }
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv * 2.0 - 1.0) / _BodyRadius; // the body is the unit disc
                float r = length(p);
                float aa = max(fwidth(r), 1e-4);
                float planarFootprint = max(length(ddx(p)), length(ddy(p)));
                float3 lightDir = normalize(_LightDir.xyz);

                // ---- atmosphere halo outside the limb, brighter toward the light
                float haloSpan = max(1.0 / _BodyRadius - 1.0, 1e-3);
                float haloT = saturate((r - 1.0) / haloSpan);
                float facing = saturate(dot(normalize(float3(p, 0.25)), lightDir) * 0.75 + 0.55);
                float halo = (0.46 * pow(1.0 - haloT, 17.0) + 0.15 * pow(1.0 - haloT, 4.0))
                    * _AtmoHalo * facing * facing;
                float3 haloColor = _AtmoColor.rgb * halo;

                if (r > 1.0 + aa)
                    return half4(haloColor, 0.0);

                // ---- sphere
                float z = sqrt(saturate(1.0 - r * r));
                float3 normal = float3(p, z);
                float3 surface = RotateZ(normal, _Tilt);
                float spin = _Time.y * _SpinSpeed + _Seed;
                float3 spun = RotateY(surface, spin);
                float latitude = spun.y;
                float footprint = planarFootprint / max(z, 0.12);

                float3 sp = spun * _NoiseScale + _Seed * 17.31;
                float3 warp = float3(
                    ValueNoise3(sp * 1.3 + 11.3),
                    ValueNoise3(sp * 1.3 + 27.1),
                    ValueNoise3(sp * 1.3 + 5.7)) - 0.5;
                float height = Fbm3(sp + warp * _Warp);

                float bands = 0.5 + 0.5 * sin(latitude * _BandFrequency + warp.x * 2.5 + height * 3.0);
                height = lerp(height, lerp(height, bands, 0.75), _Bands);

                float textureFade = 1.0 - smoothstep(0.32, 0.85, footprint * 42.0);
                float surfaceGrain = (ValueNoise3(spun * 42.0 + _Seed * 4.3) - 0.5) * textureFade;

                // ---- albedo
                float landT = saturate((height - _SeaLevel) / max(1.0 - _SeaLevel, 1e-3));
                float3 land = lerp(_ColorLow.rgb, _ColorMid.rgb, smoothstep(0.0, 0.35, landT));
                land = lerp(land, _ColorHigh.rgb, smoothstep(0.4, 0.85, landT));
                float ridges = 1.0 - abs(2.0 * ValueNoise3(sp * 7.0 + warp * 2.0) - 1.0);
                land *= 0.76 + 0.36 * height + 0.24 * ridges + 0.22 * surfaceGrain;

                float coastAA = max(0.008, fwidth(height));
                float ocean = (1.0 - smoothstep(_SeaLevel - coastAA, _SeaLevel + coastAA, height)) * step(0.001, _SeaLevel);
                float shelf = smoothstep(_SeaLevel - 0.18, _SeaLevel, height);
                float3 sea = lerp(_ColorDeep.rgb, _ColorShallow.rgb, shelf) * (0.87 + 0.18 * shelf + 0.12 * surfaceGrain);
                float3 albedo = lerp(land, sea, ocean);
                float paintedHeat = 0.0;
                float paintedIce = 0.0;
                if (_SurfaceMapStrength > 0.001)
                {
                    float3 mapped = SampleSurfaceMap(spun);
                    float luminance = dot(mapped, float3(0.2126, 0.7152, 0.0722));
                    if (_SeaLevel > 0.001)
                    {
                        float blueBalance = (mapped.b - max(mapped.r, mapped.g * 0.92)) / max(mapped.b, 0.012);
                        float tealBalance = (mapped.b - mapped.r) / max(max(mapped.b, mapped.g), 0.012);
                        float water = max(smoothstep(0.035, 0.24, blueBalance), smoothstep(0.36, 0.64, tealBalance));
                        float dark = 1.0 - smoothstep(0.30, 0.57, luminance);
                        ocean = lerp(ocean, water * dark, _SurfaceMapStrength);
                        paintedIce = smoothstep(0.53, 0.79, luminance) * smoothstep(0.72, 0.92, abs(latitude));
                    }
                    // Saturated orange-red in the volcanic map marks hot fractures and cinder vents.
                    paintedHeat = smoothstep(0.09, 0.34, mapped.r - 3.7 * mapped.g)
                        * (1.0 - smoothstep(0.16, 0.36, luminance));
                    mapped = lerp(mapped, sqrt(mapped), 0.16) * (1.0 + 0.12 * surfaceGrain);
                    albedo = lerp(albedo, mapped, _SurfaceMapStrength);
                }

                float craterAmount = _CraterAmount * (1.0 - _SurfaceMapStrength);
                if (craterAmount > 0.001)
                {
                    float2 big = Worley3(spun * _CraterScale + _Seed * 3.1);
                    float2 small = Worley3(spun * _CraterScale * 2.6 + _Seed * 9.7);
                    float bigRadius = 0.28 + 0.2 * big.y;
                    float smallRadius = 0.24 + 0.2 * small.y;
                    float bowl = (1.0 - smoothstep(0.0, bigRadius, big.x)) * step(0.35, big.y);
                    float rim = smoothstep(bigRadius - 0.07, bigRadius, big.x)
                        * (1.0 - smoothstep(bigRadius, bigRadius + 0.08, big.x)) * step(0.35, big.y);
                    float bowlSmall = (1.0 - smoothstep(0.0, smallRadius, small.x)) * step(0.5, small.y);
                    albedo *= 1.0 - craterAmount * (0.45 * bowl + 0.25 * bowlSmall);
                    albedo += craterAmount * rim * 0.075;

                    // One large basin gives the moon a landmark at the scale of the map.
                    float3 basinCenter = normalize(float3(-0.93, 0.23, 0.28));
                    float3 radial = spun - basinCenter;
                    float basinDist = length(radial) + 0.025 * (height - 0.5);
                    float basin = 1.0 - smoothstep(0.16, 0.27, basinDist);
                    float basinRim = 1.0 - smoothstep(0.012, 0.065, abs(basinDist - 0.26));
                    float3 spunLight = RotateY(RotateZ(lightDir, _Tilt), spin);
                    float rimLight = dot(radial, spunLight) / max(basinDist, 0.02);
                    float innerShadow = basin * smoothstep(-0.35, 0.8, rimLight);
                    float3 east = normalize(cross(float3(0, 1, 0), basinCenter));
                    float azimuth = atan2(dot(radial, east), dot(radial, cross(basinCenter, east)));
                    float rays = pow(saturate(0.5 + 0.5 * sin(azimuth * 13.0 + height * 6.0)), 5.0);
                    rays *= smoothstep(0.24, 0.33, basinDist) * (1.0 - smoothstep(0.34, 0.61, basinDist));
                    albedo *= 1.0 - craterAmount * (0.16 * basin + 0.38 * innerShadow);
                    albedo += craterAmount * _ColorHigh.rgb * (basinRim * (0.12 + 0.26 * saturate(rimLight)) + 0.21 * rays);
                }

                float iceEdge = 1.0 - _IceAmount;
                float ice = smoothstep(iceEdge, iceEdge + 0.07, abs(latitude) + (height - 0.5) * 0.3)
                    * step(0.001, _IceAmount);
                albedo = lerp(albedo, _IceColor.rgb, ice * (1.0 - _SurfaceMapStrength));
                ice = lerp(ice, paintedIce, _SurfaceMapStrength);

                float cloud = 0.0;
                if (_CloudAmount > 0.001)
                {
                    float cloudSpin = spin + _Time.y * _CloudSpeed + _Seed;
                    float3 cloudSurface = RotateY(surface, cloudSpin);
                    float3 cloudPos = cloudSurface * _CloudScale;
                    cloudPos.y *= 1.65;
                    float3 cloudWarp = cloudPos + warp * 1.3 + float3(0.0, _Time.y * 0.004, 0.0);
                    float front = Fbm3(cloudWarp);
                    float wisps = ValueNoise3(cloudWarp * float3(6.0, 3.0, 5.0));
                    float threads = ValueNoise3(cloudWarp * float3(17.0, 7.0, 13.0) + 12.2);
                    float threadFade = 1.0 - smoothstep(0.30, 0.85, footprint * _CloudScale * 17.0);
                    float cloudNoise = front + (wisps - 0.5) * 0.17 + (threads - 0.5) * 0.10 * threadFade;
                    if (_SeaLevel > 0.001)
                    {
                        // A cloud system with a clear eye and two curved bands, drifting with the cloud layer.
                        float3 stormCenter = normalize(float3(0.85, 0.45, 0.22));
                        float3 radial = cloudSurface - stormCenter;
                        float stormRadius = length(radial);
                        float3 east = normalize(cross(float3(0, 1, 0), stormCenter));
                        float angle = atan2(dot(radial, east), dot(radial, cross(stormCenter, east)));
                        float spiral = 0.5 + 0.5 * cos(angle * 2.0 - stormRadius * 20.0 + (wisps - 0.5) * 1.8);
                        float envelope = smoothstep(0.035, 0.105, stormRadius) * (1.0 - smoothstep(0.19, 0.38, stormRadius));
                        cloudNoise += envelope * (smoothstep(0.32, 0.94, spiral) * 0.25 - 0.04);
                        cloudNoise -= 0.15 * (1.0 - smoothstep(0.03, 0.085, stormRadius));
                    }
                    float cloudEdge = lerp(0.77, 0.34, _CloudAmount);
                    cloud = smoothstep(cloudEdge, cloudEdge + 0.17, cloudNoise);
                    float3 shadowOffset = RotateY(RotateZ(lightDir, _Tilt), cloudSpin) * 0.19;
                    float shadow = smoothstep(cloudEdge - 0.02, cloudEdge + 0.13, Fbm3Lite(cloudWarp + shadowOffset));
                    albedo *= 1.0 - 0.23 * shadow;
                    float dust = saturate(_LavaAmount);
                    float3 cloudLight = _CloudColor.rgb * (0.67 + 0.33 * smoothstep(0.46, 0.72, cloudNoise));
                    albedo = lerp(albedo, cloudLight, cloud * lerp(0.78, 0.31, dust));
                }

                // ---- light
                float ndl = dot(normal, lightDir);
                float day = smoothstep(-0.18, 0.28, ndl);
                float diffuse = day * (0.22 + 0.82 * saturate(ndl));
                float3 color = albedo * (diffuse + _Ambient * float3(0.85, 0.98, 1.32));

                float3 halfVector = normalize(lightDir + float3(0.0, 0.0, 1.0));
                float specular = pow(saturate(dot(normal, halfVector)), 48.0) * _Specular * ocean * (1.0 - cloud) * day;
                color += specular;

                // ---- self-lit detail
                float night = 1.0 - smoothstep(-0.3, 0.05, ndl);
                if (_CityAmount > 0.001)
                {
                    float3 grid = spun * 96.0 + 3.0;
                    float3 cell = floor(grid);
                    float3 random = Hash33(cell);
                    float3 offset = frac(grid) - lerp(0.33, 0.67, random);
                    float radius = max(0.18, footprint * 96.0 * 0.56);
                    float settlement = exp(-dot(offset, offset) / (radius * radius)) * (0.0324 / (radius * radius));
                    settlement *= 0.45 + 0.55 * random.y;
                    float clusters = smoothstep(0.38, 0.66, ValueNoise3(spun * 7.0 + 9.0));
                    float metropolitanGlow = smoothstep(0.72, 0.94, ValueNoise3(spun * 42.0 + 18.0)) * 0.19;
                    float cities = (settlement * step(0.66, random.x) + metropolitanGlow) * clusters;
                    color += _CityColor.rgb * _CityAmount * cities * night * (1.0 - ocean)
                        * (1.0 - ice) * (1.0 - cloud * 0.8);
                }

                if (_LavaAmount > 0.001)
                {
                    // Keep smaller fractures near the main faults so the large rifts read from orbit.
                    float faults = ValueNoise3(sp * 1.55 + warp * 1.8);
                    float branch = ValueNoise3(sp * 3.6 - warp * 1.35);
                    float primaryDist = abs(faults - 0.52);
                    float branchDist = abs(branch - 0.55) + primaryDist * 0.42 + 0.006;
                    float faultDist = min(primaryDist, branchDist);
                    float faultAA = max(0.013, fwidth(faultDist));
                    float fissure = 1.0 - smoothstep(0.018, 0.018 + faultAA, faultDist);
                    float lavaField = 1.0 - smoothstep(_LavaLevel - 0.02, _LavaLevel + 0.24, height);
                    float pulse = 0.88 + 0.12 * sin(_Time.y * 0.9 + faults * 18.0);
                    float lava = lerp(lavaField * (0.10 + 0.90 * fissure), paintedHeat * 0.52, _SurfaceMapStrength);
                    color += _LavaColor.rgb * _LavaAmount * lava * pulse * (0.28 + 0.72 * night) * (1.0 - cloud * 0.38);
                }

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
