Shader "Squadra/Mapped Planet"
{
    Properties
    {
        [Header(Globe)]
        _BodyRadius ("Body radius (fraction of quad)", Range(0.3, 1)) = 0.7
        [MainTexture] [NoScaleOffset] _SurfaceMap ("Surface color (RGB), cloud cover or height (A)", 2D) = "white" {}
        _Longitude ("Starting longitude (radians)", Float) = 0
        _Tilt ("Axis tilt (radians)", Float) = 0
        _SpinSpeed ("Spin speed (radians per second)", Float) = 0.035
        _CloudSpeed ("Cloud drift (radians per second)", Float) = 0.006
        _CloudOpacity ("Cloud opacity", Range(0, 1)) = 0
        _CloudColor ("Cloud color", Color) = (1, 1, 1, 1)
        _ReliefHeight ("Height map range (globe radii)", Range(0, 0.2)) = 0

        [Header(Light)]
        _LightDir ("Light direction (xyz)", Vector) = (-0.62, 0.45, 0.5, 0)
        _Ambient ("Night side ambient", Range(0, 0.5)) = 0.12
        _OceanShine ("Ocean sun glint", Range(0, 0.3)) = 0
        _AtmoColor ("Atmosphere color", Color) = (0.35, 0.65, 1, 1)
        _AtmoRim ("Rim strength", Range(0, 1)) = 0.26
        _AtmoHalo ("Halo strength", Range(0, 1)) = 0.24

        [Header(Rings)]
        _RingColor ("Ring color", Color) = (1, 1, 1, 1)
        _RingOpacity ("Ring opacity", Range(0, 1)) = 0
        _RingRoll ("Ring position angle (radians)", Float) = 0
        _RingOpening ("Projected minor/major axis", Range(0.1, 1)) = 0.4
        _RingInner ("Inner radius (globe radii)", Float) = 1.18
        _RingOuter ("Outer radius (globe radii)", Float) = 1.75
        _RingSoftness ("Edge feather (globe radii)", Float) = 0.025
        _RingGap ("Gap center and half-width (globe radii)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PreviewTime ("Fixed catalog time", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SquadraNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BodyRadius, _Longitude, _Tilt, _SpinSpeed, _CloudSpeed, _CloudOpacity;
                float4 _CloudColor, _LightDir, _AtmoColor;
                float _Ambient, _AtmoRim, _AtmoHalo, _ReliefHeight, _OceanShine;
                float4 _RingColor, _RingGap;
                float _RingOpacity, _RingRoll, _RingOpening, _RingInner, _RingOuter, _RingSoftness;
                float _PreviewTime;
            CBUFFER_END

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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float2 RingPlane(float2 p, float2 minor)
            {
                return float2(dot(p, float2(minor.y, -minor.x)), dot(p, minor) / _RingOpening);
            }

            float RingCoverage(float radius, float aa, float softness)
            {
                float edge = max(aa, softness);
                float coverage = smoothstep(_RingInner - aa, _RingInner + edge, radius)
                    * (1.0 - smoothstep(_RingOuter - edge, _RingOuter + aa, radius));
                if (_RingGap.y > 0.0)
                    coverage *= 1.0 - smoothstep(-aa, aa, _RingGap.y - abs(radius - _RingGap.x));
                return coverage;
            }
        ENDHLSL

        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_SurfaceMap);
            SAMPLER(sampler_SurfaceMap);

            float4 SampleMap(float2 uv)
            {
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                // Longitude wraps once around the globe. Keep mip selection continuous at that seam.
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);
                return SAMPLE_TEXTURE2D_GRAD(_SurfaceMap, sampler_SurfaceMap, uv, dx, dy);
            }

            float3 ReliefNormal(float3 normal, float height)
            {
                // Reconstruct the height gradient in the globe's local tangent plane, so crater walls
                // catch the same light as the globe as they rotate. Fade where the disc grazes the view.
                float3 dx = ddx(normal);
                float3 dy = ddy(normal);
                float3 tangentX = cross(dy, normal);
                float3 tangentY = cross(normal, dx);
                float area = dot(dx, tangentX);
                float3 gradient = (ddx(height) * tangentX + ddy(height) * tangentY)
                    * (sign(area) / max(abs(area), 1e-8));
                return normalize(normal - gradient * (_ReliefHeight * smoothstep(0.08, 0.28, normal.z)));
            }

            float CloudShadow(float2 uv, float3 position, float3 lightDir)
            {
                // Convert the sun to map tangents to find the cloud above the surface along the light ray.
                float latitudeScale = max(length(position.xz), 0.18);
                float3 east = float3(position.z, 0.0, -position.x) / latitudeScale;
                float3 north = cross(position, east);
                float3 sun = RotateZ(lightDir, _Tilt);
                float2 offset = float2(dot(sun, east) / latitudeScale, 2.0 * dot(sun, north)) * 0.008;
                return SampleMap(uv + offset).a;
            }

            float4 PaintRing(float2 p, float3 lightDir, float spin, float2 minor, float3 ringNormal, out float nearSide)
            {
                float2 plane = RingPlane(p, minor);
                nearSide = step(plane.y, 0.0);
                float radius = length(plane);
                float aa = max(fwidth(radius) * 0.65, 0.002);
                float coverage = RingCoverage(radius, aa, _RingSoftness);
                if (coverage <= 0.0) return 0.0;

                float across = saturate((radius - _RingInner) / max(_RingOuter - _RingInner, 0.001));
                float strands = (0.09 * sin(radius * 49.0 + 0.8 * sin(radius * 25.0)) + 0.05 * sin(radius * 111.0))
                    * (1.0 - smoothstep(0.012, 0.042, aa)) * (1.0 - smoothstep(0.04, 0.12, _RingSoftness));
                // Dust turns in the ring plane with the globe. The gaps and sun shadow stay in place.
                float angle = atan2(plane.x, -plane.y) + spin;
                float dust = 0.10 * sin(2.0 * angle + radius * 8.0)
                    + 0.11 * sin(3.0 * angle + radius * 13.0) * sin(5.0 * angle - radius * 19.0);
                float bands = saturate(0.80 - 0.25 * across + strands + dust);
                float3 ringPoint = float3(p, -dot(p, ringNormal.xy) / ringNormal.z);
                float alongLight = dot(ringPoint, lightDir);
                float shadowDistance = length(ringPoint - lightDir * alongLight);
                float lit = lerp(smoothstep(0.85, 1.06, shadowDistance), 1.0, smoothstep(-0.08, 0.08, alongLight));
                float alpha = coverage * bands * _RingOpacity;
                return float4(_RingColor.rgb * lerp(0.28, 1.0, lit) * alpha, alpha);
            }

            float ReceiveRingShadow(float3 surfacePoint, float3 lightDir, float3 ringNormal)
            {
                float incidence = dot(ringNormal, lightDir);
                if (_RingOpacity <= 0.0 || abs(incidence) < 0.02) return 0.0;

                float travel = -dot(ringNormal, surfacePoint) / incidence;
                float radius = length(surfacePoint + lightDir * travel);
                float aa = max(fwidth(radius) * 0.65, 0.005);
                return step(0.0, travel) * RingCoverage(radius, aa, 0.06) * _RingOpacity * 0.36;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv * 2.0 - 1.0) / _BodyRadius;
                float r = length(p);
                float aa = max(fwidth(r) * 0.65, 0.0001);
                float3 lightDir = normalize(_LightDir.xyz);
                float time = _PreviewTime >= 0.0 ? _PreviewTime : _Time.y;
                float spin = _Longitude + time * _SpinSpeed;
                float nearSide = 0.0;
                float4 rings = 0.0;
                float3 ringNormal = 0.0;
                if (_RingOpacity > 0.0)
                {
                    float2 minor = RotateZ(float3(0.0, 1.0, 0.0), _RingRoll).xy;
                    ringNormal = float3(minor * sqrt(1.0 - _RingOpening * _RingOpening), _RingOpening);
                    rings = PaintRing(p, lightDir, spin, minor, ringNormal, nearSide);
                }

                float halo = exp2(-max(r - 1.0, 0.0) * 23.0);
                float facing = saturate(dot(normalize(float3(p, 0.25)), lightDir) * 0.65 + 0.48);
                float3 haloColor = _AtmoColor.rgb * halo * facing * facing * _AtmoHalo;
                float4 globe = 0.0;
                if (r < 1.0 + aa)
                {
                    float2 disc = p / max(r, 1.0);
                    float3 normal = float3(disc, sqrt(saturate(1.0 - dot(disc, disc))));
                    float3 tilted = RotateZ(normal, _Tilt);
                    float longitude = (atan2(tilted.x, tilted.z) + spin) / TWO_PI + 0.5;
                    float latitude = asin(clamp(tilted.y, -1.0, 1.0)) / PI + 0.5;
                    float2 uv = float2(longitude, latitude);
                    float4 mapped = SampleMap(uv);
                    float3 albedo = mapped.rgb;
                    float cloudCover = 0.0;
                    if (_CloudOpacity > 0.0)
                    {
                        float2 cloudUv = uv + float2(time * _CloudSpeed / TWO_PI, 0.0);
                        float density = SampleMap(cloudUv).a;
                        float shadow = CloudShadow(cloudUv, tilted, lightDir);
                        // Thin clouds shade the ground softly. Dense, sunward cloud tops catch more light.
                        float brightness = 0.69 + 0.31 * density + 0.15 * (density - shadow);
                        cloudCover = density * _CloudOpacity;
                        albedo = lerp(albedo * (1.0 - 0.28 * shadow * _CloudOpacity),
                            _CloudColor.rgb * brightness, cloudCover);
                    }

                    float ndl = dot(normal, lightDir);
                    float day = smoothstep(-0.18, 0.28, ndl);
                    float surfaceLight = _ReliefHeight > 0.0 ? dot(ReliefNormal(normal, mapped.a), lightDir) : ndl;
                    float diffuse = day * (0.22 + 0.82 * saturate(surfaceLight))
                        * (1.0 - ReceiveRingShadow(normal, lightDir, ringNormal));
                    float3 color = albedo * (diffuse + _Ambient * float3(0.85, 0.98, 1.32));
                    if (_OceanShine > 0.0)
                    {
                        float3 halfVector = normalize(lightDir + float3(0.0, 0.0, 1.0));
                        float clear = 1.0 - cloudCover;
                        float glint = pow(saturate(dot(normal, halfVector)), 39.0) * _OceanShine * day * clear * clear;
                        color += glint * float3(0.54, 0.74, 0.9);
                    }
                    color += _AtmoColor.rgb * pow(1.0 - normal.z, 3.0) * _AtmoRim * day;
                    float coverage = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, r);
                    globe = float4(color * coverage, coverage);
                }

                // Composite inside the surface draw, below the gameplay rings, Darts and labels.
                float behind = 1.0 - globe.a;
                globe.rgb += haloColor * behind;
                return nearSide > 0.0 ? rings + globe * (1.0 - rings.a) : globe + rings * behind;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SelectSurface
            #pragma editor_sync_compilation

            int _ObjectId;
            int _PassValue;

            float4 SelectSurface(Varyings input) : SV_Target
            {
                // The surface texture's alpha holds clouds or relief. Outline the projected shape instead.
                float2 p = (input.uv * 2.0 - 1.0) / _BodyRadius;
                float edge = 1.0 - length(p);
                if (_RingOpacity > 0.0)
                {
                    float2 minor = RotateZ(float3(0.0, 1.0, 0.0), _RingRoll).xy;
                    float ring = RingCoverage(length(RingPlane(p, minor)), 0.002, _RingSoftness);
                    edge = max(edge, ring * _RingOpacity - 0.18);
                }
                clip(edge);
                return float4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}
