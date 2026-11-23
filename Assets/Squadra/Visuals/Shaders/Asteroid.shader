// Rocky asteroid drawn on a quad. A small rock is sphere-traced inside the quad from the camera's
// direction, so its silhouette, lobes, ledges and facets are real shape that turns and self-shadows
// under the battlefield's light. The form keyword picks the rock and its large surface feature; the
// margin of the quad around the body holds the drifting dust plume and the tumbling shards some forms
// carry. The body always stays inside the unit disc, which the world's body radius maps to.
Shader "Squadra/Asteroid"
{
    Properties
    {
        [Header(Shape)]
        _BodyRadius ("Body radius (fraction of quad)", Range(0.3, 1)) = 0.7
        [KeywordEnum(Blade, Wedge, Pair, Crest, Mantle)] _Form ("Form", Float) = 0
        _Seed ("Seed", Float) = 1
        _Roll ("Roll in the view (radians)", Float) = 0
        _Yaw ("Turn about the vertical (radians)", Float) = 0
        _SpinAxis ("Spin axis (rock space)", Vector) = (1, 0, 0, 0)
        _SpinSpeed ("Spin speed (radians per second)", Float) = 0.05
        _Sway ("Sway amplitude (radians; 0 turns all the way)", Range(0, 1.5)) = 0
        _Lumps ("Lumpiness", Range(0, 0.12)) = 0.05
        _Roughness ("Roughness", Range(0, 0.06)) = 0.025

        [Header(Light)]
        _LightDir ("Light direction (xyz)", Vector) = (-0.62, 0.45, 0.5, 0)
        _Ambient ("Night side ambient", Range(0, 0.5)) = 0.035
        _RimColor ("Rim color", Color) = (0.5, 0.5, 0.5, 1)
        _Rim ("Rim strength", Range(0, 1)) = 0.12

        [Header(Rock)]
        _NoiseScale ("Rock pattern scale", Float) = 3
        _ColorLow ("Rock dark", Color) = (0.12, 0.11, 0.1, 1)
        _ColorMid ("Rock mid", Color) = (0.3, 0.28, 0.26, 1)
        _ColorHigh ("Rock light", Color) = (0.5, 0.47, 0.44, 1)
        _CraterAmount ("Craters", Range(0, 1)) = 0.5
        _CraterScale ("Crater scale", Float) = 6

        [Header(Feature)]
        _FeatureColor ("Feature color", Color) = (0.8, 0.8, 0.8, 1)
        _DetailColor ("Detail color", Color) = (0.5, 0.5, 0.5, 1)
        _FeatureWidth ("Feature width", Range(0, 0.5)) = 0.1
        _FeatureScale ("Feature detail scale", Float) = 12
        _Shine ("Metal shine", Range(0, 3)) = 0

        [Header(Dust plume)]
        _DustColor ("Dust color", Color) = (0.7, 0.66, 0.62, 1)
        _DustAmount ("Dust opacity", Range(0, 1)) = 0
        _DustAngle ("Plume direction (radians, 0 = right)", Float) = 0
        _DustSource ("Plume source (body radii from the middle)", Range(0, 1)) = 0.8
        _DustReach ("Plume reach (body radii from the middle)", Range(1, 2)) = 1.6
        _DustSpeed ("Dust drift speed", Float) = 0.05

        [Header(Shards)]
        _ShardCount ("Shards", Range(0, 6)) = 0
        _ShardSize ("Shard size", Range(0.02, 0.2)) = 0.09
        _ShardAnchor ("Shard cloud direction (view)", Vector) = (-0.4, 0.55, 0.7, 0)
        _ShardDistance ("Shard distance", Range(0.9, 1.4)) = 1.1
        _ShardSpread ("Shard spread", Range(0, 1)) = 0.45
        _ShardDrift ("Shard drift", Range(0, 0.3)) = 0.1
        _ShardSpeed ("Shard speed", Float) = 0.12

        [HideInInspector] _PreviewTime ("Fixed catalog time", Float) = -1
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
            #pragma shader_feature_local _FORM_BLADE _FORM_WEDGE _FORM_PAIR _FORM_CREST _FORM_MANTLE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SquadraNoise.hlsl"

            #if defined(_FORM_WEDGE)
                #define HAS_SHARDS 1
                #define MAX_SHARDS 6
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                // View-to-rock rotation, the same for the whole quad.
                float3 rockX : TEXCOORD1;
                float3 rockY : TEXCOORD2;
                float3 rockZ : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BodyRadius;
                float _Seed;
                float _Roll;
                float _Yaw;
                float4 _SpinAxis;
                float _SpinSpeed;
                float _Sway;
                float _Lumps;
                float _Roughness;
                float4 _LightDir;
                float _Ambient;
                float4 _RimColor;
                float _Rim;
                float _NoiseScale;
                float4 _ColorLow;
                float4 _ColorMid;
                float4 _ColorHigh;
                float _CraterAmount;
                float _CraterScale;
                float4 _FeatureColor;
                float4 _DetailColor;
                float _FeatureWidth;
                float _FeatureScale;
                float _Shine;
                float4 _DustColor;
                float _DustAmount;
                float _DustAngle;
                float _DustSource;
                float _DustReach;
                float _DustSpeed;
                float _ShardCount;
                float _ShardSize;
                float4 _ShardAnchor;
                float _ShardDistance;
                float _ShardSpread;
                float _ShardDrift;
                float _ShardSpeed;
                float _PreviewTime;
            CBUFFER_END

            // The body never reaches past this radius, so it stays inside the world's body radius.
            #define BODY_LIMIT 0.99

            static float3x3 gToRock;
            static float gTime;

            float Now()
            {
                return _PreviewTime >= 0.0 ? _PreviewTime : _Time.y;
            }

            float3 RotateAxis(float3 v, float3 axis, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
            }

            float3 ViewToRock(float3 v, float spin)
            {
                v = RotateZ(v, -_Roll);
                v = RotateY(v, -_Yaw);
                return RotateAxis(v, normalize(_SpinAxis.xyz + float3(1e-5, 0, 0)), -spin);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                // A rock either turns all the way round its spin axis or, with a sway, rocks to and fro
                // about it, so a feature that must stay in view never turns away.
                float t = Now() * _SpinSpeed + _Seed;
                float spin = _Sway > 0.0001 ? _Sway * sin(t) : t;
                output.rockX = ViewToRock(float3(1, 0, 0), spin);
                output.rockY = ViewToRock(float3(0, 1, 0), spin);
                output.rockZ = ViewToRock(float3(0, 0, 1), spin);
                return output;
            }

            // ------------------------------------------------------------------ shapes

            float SdEllipsoid(float3 q, float3 radii)
            {
                float k0 = length(q / radii);
                float k1 = length(q / (radii * radii));
                return k0 * (k0 - 1.0) / max(k1, 1e-5);
            }

            float SMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float Plane(float3 q, float3 n, float d)
            {
                return dot(q, normalize(n)) - d;
            }

            #if defined(_FORM_BLADE)
                // The bedding planes of the slate: ledges follow them and the pale stripe is one of them.
                static const float3 BladeLayer = normalize(float3(0.62, 0.78, 0.1));
            #endif

            #if defined(_FORM_WEDGE)
                // The fresh break, lit by the sun, and the weathered faces of the wedge around it.
                static const float3 WedgeBreak = normalize(float3(-0.38, 0.42, 0.82));
                static const float WedgeBreakDepth = 0.30;

                float WedgeWeathered(float3 q)
                {
                    float d = Plane(q, float3(0.3, 1.0, 0.25), 0.40);        // top, sloping to the tip
                    d = max(d, Plane(q, float3(0.24, -1.0, 0.1), 0.44));     // underside
                    d = max(d, Plane(q, float3(-1.0, 0.1, 0.05), 0.70));     // blunt end
                    d = max(d, Plane(q, float3(1.0, -0.25, 0.15), 0.88));    // tip
                    d = max(d, Plane(q, float3(0.1, 0.15, -1.0), 0.36));     // back
                    d = max(d, Plane(q, float3(0.42, -0.45, 0.8), 0.30));    // old face below the break
                    d = max(d, Plane(q, float3(-0.7, 0.72, 0.25), 0.60));    // chipped corner
                    d = max(d, Plane(q, float3(0.45, 0.75, -0.45), 0.52));   // chipped back edge
                    return d;
                }
            #endif

            #if defined(_FORM_PAIR)
                static const float3 PairCenterA = float3(-0.42, 0.02, 0.0);
                static const float3 PairCenterB = float3(0.5, -0.03, 0.02);
                static const float PairNeck = 0.06;
            #endif

            #if defined(_FORM_CREST)
                // The metal ridge follows the plane across this direction, along the arc facing CrestFront.
                static const float3 CrestRidge = normalize(float3(0.55, 0.8, 0.45));
                static const float3 CrestFront = normalize(float3(-0.35, -0.3, 0.9));

                float CrestArc(float3 q)
                {
                    return smoothstep(-0.55, 0.1, dot(normalize(q + float3(0, 0, 1e-4)), CrestFront));
                }
            #endif

            // The rock's large shape, before lumps and roughness. Mask: how much of a smooth, fresh
            // surface the point is on (the wedge's break), which roughness spares.
            float RockShape(float3 q, out float smooth)
            {
                smooth = 0.0;
                #if defined(_FORM_BLADE)
                    float d = SdEllipsoid(q, float3(0.98, 0.36, 0.44));
                    d = max(d, Plane(q, float3(1.0, 0.45, 0.25), 0.84));
                    d = max(d, Plane(q, float3(1.0, -0.55, -0.3), 0.86));
                    d = max(d, Plane(q, float3(-1.0, -0.4, 0.3), 0.82));
                    d = max(d, Plane(q, float3(-1.0, 0.5, -0.2), 0.86));
                    float layer = frac(dot(q, BladeLayer) * _FeatureScale);
                    d += 0.012 * smoothstep(0.7, 1.0, layer);
                    return d;
                #elif defined(_FORM_WEDGE)
                    float weathered = WedgeWeathered(q);
                    float fracture = dot(q, WedgeBreak) - WedgeBreakDepth;
                    smooth = smoothstep(-0.04, 0.0, fracture - weathered);
                    return max(weathered, fracture) - 0.02;
                #elif defined(_FORM_PAIR)
                    float a = SdEllipsoid(q - PairCenterA, float3(0.53, 0.5, 0.47));
                    float b = SdEllipsoid(q - PairCenterB, float3(0.44, 0.4, 0.39));
                    return SMin(a, b, 0.09);
                #elif defined(_FORM_CREST)
                    float d = SdEllipsoid(q, float3(0.86, 0.74, 0.78));
                    d = max(d, Plane(q, float3(1.0, 0.15, 0.2), 0.56));
                    d = max(d, Plane(q, float3(-1.0, 0.3, 0.1), 0.55));
                    d = max(d, Plane(q, float3(0.3, 0.2, 1.0), 0.52));
                    d = max(d, Plane(q, float3(-0.2, -0.3, -1.0), 0.5));
                    d = max(d, Plane(q, float3(0.6, -1.0, 0.3), 0.52));
                    d = max(d, Plane(q, float3(-0.5, 1.0, -0.4), 0.52));
                    d = max(d, Plane(q, float3(0.7, 0.7, -0.6), 0.56));
                    d = max(d, Plane(q, float3(-0.7, -0.6, 0.7), 0.54));
                    d = max(d, Plane(q, float3(0.8, -0.4, -0.8), 0.58));
                    d = max(d, Plane(q, float3(-0.6, 0.6, 0.8), 0.55));
                    // The crest: a ragged ridge of ore along one arc of the rock, raised above the facets.
                    float across = dot(q, CrestRidge);
                    float height = (0.05 + 0.04 * ValueNoise3(q * 3.0 + _Seed)) * CrestArc(q);
                    return d - height * exp(-across * across / 0.006);
                #else // _FORM_MANTLE
                    float d = SdEllipsoid(q, float3(0.97, 0.47, 0.74));
                    return SMin(d, -(q.y + 0.33), -0.08); // a flattened underside
                #endif
            }

            float RockDistance(float3 q)
            {
                float smooth;
                float d = RockShape(q, smooth);
                d = max(d, length(q) - BODY_LIMIT);
                // Lumps and roughness never move the surface by more than this; far away they are skipped.
                float relief = _Lumps + _Roughness;
                if (d > relief + 0.05) return d - relief;

                float3 sp = q + _Seed * 7.13;
                float lumps = (ValueNoise3(sp * 2.1) - 0.5) * 2.0 * _Lumps;
                float rough = (Fbm3Lite(sp * _NoiseScale * 2.0) - 0.5) * 2.0 * _Roughness;
                d += (lumps + rough) * (1.0 - smooth);
                return max(d, length(q) - BODY_LIMIT);
            }

            // ------------------------------------------------------------------ shards

            #if defined(HAS_SHARDS)
                static float3 gShardAt[MAX_SHARDS];
                static float3x3 gShardTurn[MAX_SHARDS];
                static float3 gShardScale[MAX_SHARDS];

                // Each shard sits in a cloud off the break and drifts round a small closed path while it
                // tumbles, so it never leaves the rock and its motion repeats without a jump.
                void PlaceShards(float time)
                {
                    float3 anchor = normalize(_ShardAnchor.xyz);
                    float3 side = normalize(cross(anchor, float3(0, 0, 1)) + float3(0, 1e-4, 0));
                    float3 up = cross(side, anchor);
                    [unroll]
                    for (int i = 0; i < MAX_SHARDS; i++)
                    {
                        float3 h = Hash33(float3(i * 3.17 + 1.3, _Seed * 1.91, 5.71));
                        float3 g = Hash33(float3(i * 1.37 + 9.1, _Seed * 0.73, 2.33));
                        float angle = (i + h.x * 0.6) * (TWO_PI / MAX_SHARDS);
                        float ring = _ShardSpread * (0.35 + 0.65 * h.y);
                        float3 dir = normalize(anchor + (side * cos(angle) + up * sin(angle)) * ring);
                        float speed = _ShardSpeed * (0.6 + 0.8 * h.z);
                        float3 drift = float3(
                            sin(time * speed + g.x * TWO_PI),
                            sin(time * speed * 0.73 + g.y * TWO_PI),
                            sin(time * speed * 1.21 + g.z * TWO_PI)) * _ShardDrift;
                        float away = _ShardDistance + 0.18 * (h.y - 0.5);
                        gShardAt[i] = dir * max(away, 1.0) + drift;

                        float3 axis = normalize(g - 0.5 + float3(1e-3, 0, 0));
                        float tumble = time * _ShardSpeed * (2.0 + 4.0 * g.x) * (h.x > 0.5 ? 1.0 : -1.0) + h.z * TWO_PI;
                        gShardTurn[i] = float3x3(
                            RotateAxis(float3(1, 0, 0), axis, -tumble),
                            RotateAxis(float3(0, 1, 0), axis, -tumble),
                            RotateAxis(float3(0, 0, 1), axis, -tumble));
                        gShardTurn[i] = transpose(gShardTurn[i]);
                        gShardScale[i] = _ShardSize * (0.55 + 0.7 * h.z) * float3(1.0, 0.45 + 0.5 * g.y, 0.3 + 0.45 * g.z);
                    }
                }

                // An irregular splinter: a squashed tetrahedron with one corner knocked off.
                float ShardFaces(float3 p, out int face)
                {
                    float4 planes = float4(-p.x - p.y - p.z, p.x + p.y - p.z, -p.x + p.y + p.z, p.x - p.y + p.z);
                    face = 0;
                    float best = planes.x;
                    if (planes.y > best) { best = planes.y; face = 1; }
                    if (planes.z > best) { best = planes.z; face = 2; }
                    if (planes.w > best) { best = planes.w; face = 3; }
                    float chip = dot(p, float3(0.577, 0.577, 0.577)) - 0.55;
                    if (chip > best) { best = chip; face = 4; }
                    return best;
                }

                float ShardDistance(int i, float3 pos, out int face)
                {
                    float3 local = mul(gShardTurn[i], pos - gShardAt[i]) / gShardScale[i];
                    float d = (ShardFaces(local, face) - 0.6) * 0.577;
                    return d * min(gShardScale[i].x, min(gShardScale[i].y, gShardScale[i].z));
                }
            #endif

            // ------------------------------------------------------------------ scene

            // Distance to the nearest solid, in body radii. Id 0 is the rock, 1 + i is shard i.
            float Scene(float3 pos, out int id)
            {
                id = 0;
                float d = RockDistance(mul(gToRock, pos));
                #if defined(HAS_SHARDS)
                    int count = (int)_ShardCount;
                    [loop]
                    for (int i = 0; i < count; i++)
                    {
                        int face;
                        float s = ShardDistance(i, pos, face);
                        if (s < d)
                        {
                            d = s;
                            id = 1 + i;
                        }
                    }
                #endif
                return d;
            }

            float SceneDistance(float3 pos)
            {
                int id;
                return Scene(pos, id);
            }

            float3 SceneNormal(float3 pos, float eps)
            {
                const float2 k = float2(1, -1);
                return normalize(
                    k.xyy * SceneDistance(pos + k.xyy * eps) +
                    k.yyx * SceneDistance(pos + k.yyx * eps) +
                    k.yxy * SceneDistance(pos + k.yxy * eps) +
                    k.xxx * SceneDistance(pos + k.xxx * eps));
            }

            float SoftShadow(float3 origin, float3 lightDir)
            {
                float result = 1.0;
                float t = 0.03;
                [loop]
                for (int i = 0; i < 16; i++)
                {
                    float h = SceneDistance(origin + lightDir * t);
                    result = min(result, 7.0 * h / t);
                    t += clamp(h, 0.03, 0.2);
                    if (result < 0.02 || t > 2.2) break;
                }
                return saturate(result);
            }

            // ------------------------------------------------------------------ surface

            struct Surface
            {
                float3 albedo;
                float shine;    // metallic highlight strength
                float sparkle;  // frost glints
            };

            float3 Craters(float3 albedo, float3 q, float detailFade)
            {
                float amount = _CraterAmount * detailFade;
                if (amount < 0.001) return albedo;
                float2 big = Worley3(q * _CraterScale + _Seed * 3.1);
                float radius = 0.28 + 0.2 * big.y;
                float bowl = (1.0 - smoothstep(0.0, radius, big.x)) * step(0.4, big.y);
                float rim = smoothstep(radius - 0.07, radius, big.x)
                    * (1.0 - smoothstep(radius, radius + 0.08, big.x)) * step(0.4, big.y);
                albedo *= 1.0 - amount * 0.4 * bowl;
                return albedo + amount * rim * 0.03;
            }

            Surface RockSurface(float3 q, float pix)
            {
                Surface s;
                s.shine = 0.0;
                s.sparkle = 0.0;

                // Fine detail fades out before it is smaller than a pixel.
                float detailFade = 1.0 - smoothstep(0.02, 0.06, pix);
                float grainFade = 1.0 - smoothstep(0.008, 0.025, pix);

                float3 sp = q * _NoiseScale + _Seed * 17.31;
                float h = Fbm3(sp);
                float grain = (ValueNoise3(q * 46.0 + _Seed) - 0.5) * grainFade;
                float3 rock = lerp(_ColorLow.rgb, _ColorMid.rgb, smoothstep(0.25, 0.7, h));
                rock = lerp(rock, _ColorHigh.rgb, smoothstep(0.62, 0.9, h) * 0.7);
                rock *= 0.9 + 0.25 * grain;
                rock = Craters(rock, q, detailFade);

                #if defined(_FORM_BLADE)
                    // Slate bedding: alternating layers, and one pale bed that runs right through.
                    float along = dot(q, BladeLayer);
                    float wobble = (ValueNoise3(q * 5.0 + _Seed) - 0.5) * 0.05;
                    float bed = frac(along * _FeatureScale + wobble * 4.0);
                    rock *= lerp(1.0, 0.86 + 0.24 * smoothstep(0.1, 0.9, bed), detailFade);
                    float stripeDist = abs(along - 0.05 + wobble);
                    float aa = max(pix * 0.7, 0.01);
                    float stripe = 1.0 - smoothstep(_FeatureWidth - aa, _FeatureWidth + aa, stripeDist);
                    float3 pale = _FeatureColor.rgb * (0.9 + 0.2 * grain + 0.1 * (h - 0.5));
                    // Chipped ends show fresher, lighter rock.
                    float chip = smoothstep(0.55, 0.9, abs(q.x));
                    rock = lerp(rock, rock * 1.35 + _DetailColor.rgb * 0.08, chip * 0.6);
                    s.albedo = lerp(rock, pale, stripe);
                #elif defined(_FORM_WEDGE)
                    float weathered = WedgeWeathered(q);
                    float fracture = dot(q, WedgeBreak) - WedgeBreakDepth;
                    float aa = max(pix * 0.8, 0.008);
                    float broken = smoothstep(-aa, aa, fracture - weathered + 0.006);
                    // Conchoidal ripples spread from the point of impact across the break.
                    float3 impact = float3(-0.42, 0.46, 0.0);
                    float ripple = sin(length(q - impact) * 38.0 + ValueNoise3(q * 6.0) * 3.0);
                    float3 chalk = _FeatureColor.rgb * (0.94 + 0.06 * ripple * detailFade + 0.08 * grain);
                    chalk = lerp(chalk, _DetailColor.rgb, smoothstep(0.55, 0.8, ValueNoise3(q * 7.0 + _Seed)) * 0.12);
                    s.albedo = lerp(rock, chalk, broken);
                #elif defined(_FORM_PAIR)
                    // Frost gathers in the neck between the lobes; the outer faces are bare dark rock.
                    float neck = abs(q.x - PairNeck);
                    float edge = (Fbm3Lite(q * 6.0 + _Seed) - 0.5) * 0.22;
                    float frost = 1.0 - smoothstep(_FeatureWidth - 0.05, _FeatureWidth + 0.05, neck + edge);
                    float3 rime = _FeatureColor.rgb * (0.88 + 0.22 * Fbm3Lite(q * 22.0) + 0.1 * grain);
                    s.albedo = lerp(rock, rime, frost);
                    float glint = smoothstep(0.82, 0.95, ValueNoise3(q * 90.0 + _Seed));
                    s.sparkle = frost * glint * grainFade;
                #elif defined(_FORM_CREST)
                    float across = abs(dot(q, CrestRidge));
                    float aa = max(pix * 0.7, 0.01);
                    float arc = CrestArc(q);
                    float ridge = (1.0 - smoothstep(_FeatureWidth * arc - aa, _FeatureWidth * arc + aa, across)) * arc;
                    // Pitted ore: tight pits over the whole rock, dull oxide in patches.
                    float2 pits = Worley3(q * _FeatureScale + _Seed);
                    float pit = (1.0 - smoothstep(0.0, 0.28, pits.x)) * step(0.55, pits.y) * detailFade;
                    rock *= 1.0 - 0.45 * pit;
                    float oxide = smoothstep(0.55, 0.72, Fbm3Lite(q * 2.6 + _Seed * 2.0));
                    rock = lerp(rock, _DetailColor.rgb * (0.85 + 0.3 * h), oxide * 0.75);
                    // Thin metal veins branch off the ridge.
                    float3 veinWarp = q * 4.0 + (Fbm3Lite(q * 3.0 + _Seed) - 0.5) * 1.6;
                    float vein = abs(Fbm3Lite(veinWarp + _Seed * 1.3) - 0.5);
                    // Only up close: at gameplay scale the veins would blur into a pale scribble.
                    float veinFade = 1.0 - smoothstep(0.008, 0.02, pix);
                    float veins = (1.0 - smoothstep(0.006, 0.014, vein)) * veinFade * (1.0 - oxide);
                    float metal = max(ridge, veins * 0.55);
                    float3 steel = _FeatureColor.rgb * (0.85 + 0.25 * grain + 0.15 * h);
                    s.albedo = lerp(rock, steel, metal);
                    s.shine = metal * _Shine;
                #else // _FORM_MANTLE
                    // A pale apron of loose ash round the base, studded with half-buried stones.
                    float edge = (Fbm3Lite(q * 4.0 + _Seed) - 0.5) * 0.3;
                    float apronLine = _FeatureWidth - 0.33;
                    float apron = 1.0 - smoothstep(apronLine - 0.05, apronLine + 0.05, q.y + edge);
                    float2 stones = Worley3(q * _FeatureScale + _Seed * 2.7);
                    float stone = (1.0 - smoothstep(0.2, 0.32, stones.x)) * step(0.6, stones.y);
                    float3 ash = _FeatureColor.rgb * (0.92 + 0.12 * Fbm3Lite(q * 18.0) + 0.06 * grain);
                    ash = lerp(ash, _ColorMid.rgb * (0.8 + 0.4 * h), stone * detailFade);
                    s.albedo = lerp(rock, ash, apron);
                #endif
                return s;
            }

            // ------------------------------------------------------------------ dust

            #if defined(_FORM_MANTLE)
                // A soft plume leaving one side of the rock. The noise scrolls outward along the plume, so
                // the dust drifts away for ever without repeating or jumping, and thins out by its reach.
                float4 Dust(float2 p, float time, float3 lightDir)
                {
                    if (_DustAmount < 0.001) return 0;
                    float2 dir = float2(cos(_DustAngle), sin(_DustAngle));
                    float2 perp = float2(-dir.y, dir.x);
                    float2 rel = p - dir * _DustSource;
                    float along = dot(rel, dir);
                    float across = dot(rel, perp);
                    float plumeLength = max(_DustReach - _DustSource, 0.1);
                    float t = saturate(along / plumeLength);
                    float spread = 0.12 + 0.34 * t;
                    float flow = time * _DustSpeed;
                    float bend = (ValueNoise2(float2(along * 1.5 - flow * 1.5, _Seed)) - 0.5) * 0.25 * t;
                    float side = (across - bend) / spread;
                    float envelope = exp(-side * side) * smoothstep(-0.3, 0.0, along) * (1.0 - smoothstep(0.3, 1.0, t));
                    if (envelope < 0.002) return 0;
                    float2 uv = float2((along - flow) * 3.0, (across - bend) * 3.6) + _Seed * 3.3;
                    float billow = Fbm2(uv) * 0.75 + Fbm2(uv * 2.3 + 5.1) * 0.35;
                    // Thick and even where it leaves the ground, broken into billows further out.
                    float density = envelope * smoothstep(0.1 + 0.3 * t, 0.7 + 0.1 * t, billow);
                    float lit = 0.72 + 0.28 * saturate(dot(normalize(float3(perp * (across - bend), 0.6)), lightDir) + 0.3);
                    float alpha = density * _DustAmount;
                    return float4(_DustColor.rgb * lit * alpha, alpha);
                }
            #endif

            // ------------------------------------------------------------------ fragment

            half4 Frag(Varyings input) : SV_Target
            {
                gToRock = float3x3(input.rockX, input.rockY, input.rockZ);
                gToRock = transpose(gToRock);
                gTime = Now();
                float3 lightDir = normalize(_LightDir.xyz);

                float2 p = (input.uv * 2.0 - 1.0) / _BodyRadius; // the body lies inside the unit disc
                float pix = max(length(ddx(p)), length(ddy(p)));

                float reach = 1.0;
                #if defined(HAS_SHARDS)
                    PlaceShards(gTime);
                    if (_ShardCount >= 1.0) reach = _ShardDistance + 0.25 + _ShardDrift * 1.8 + _ShardSize;
                #endif

                float4 result = 0;
                float rr = dot(p, p);
                if (rr < reach * reach)
                {
                    // March from the front of the bounding sphere straight away from the camera.
                    float zTop = sqrt(reach * reach - rr);
                    float3 origin = float3(p, zTop);
                    float tMax = 2.0 * zTop;
                    float t = 0.0;
                    float closest = 1e5;
                    float tClosest = 0.0;
                    bool hit = false;
                    float hitEps = max(0.0006, pix * 0.2);
                    [loop]
                    for (int i = 0; i < 72; i++)
                    {
                        float d = SceneDistance(origin - float3(0, 0, t));
                        if (d < closest)
                        {
                            closest = d;
                            tClosest = t;
                        }
                        if (d < hitEps)
                        {
                            hit = true;
                            break;
                        }
                        t += max(d * 0.8, 0.002);
                        if (t > tMax) break;
                    }

                    // A ray that just misses still covers part of its pixel: soft, unjagged edges.
                    float coverage = hit ? 1.0 : 1.0 - smoothstep(0.0, pix * 0.9, closest);
                    if (coverage > 0.001)
                    {
                        float3 pos = origin - float3(0, 0, tClosest);
                        int id;
                        Scene(pos, id);
                        float3 normal = SceneNormal(pos, max(0.0015, pix * 0.5));
                        float3 q = mul(gToRock, pos);

                        Surface surface;
                        #if defined(HAS_SHARDS)
                            if (id > 0)
                            {
                                // Shards: fresh chalky facets and dark weathered ones.
                                int face;
                                ShardDistance(id - 1, pos, face);
                                float pick = Hash21(float2(face, id * 1.7 + _Seed));
                                surface.albedo = pick > 0.55 ? _FeatureColor.rgb * 0.92 : _ColorMid.rgb * 0.9;
                                surface.shine = 0.0;
                                surface.sparkle = 0.0;
                            }
                            else
                        #endif
                        {
                            surface = RockSurface(q, pix);
                        }

                        float ndl = dot(normal, lightDir);
                        float day = smoothstep(-0.12, 0.25, ndl);
                        float shadow = ndl > -0.1 ? SoftShadow(pos + normal * 0.01, lightDir) : 0.0;
                        float diffuse = day * (0.2 + 0.85 * saturate(ndl)) * lerp(0.25, 1.0, shadow);
                        float occlusion = saturate(0.45 + 0.55 * SceneDistance(pos + normal * 0.12) / 0.12);
                        float3 color = surface.albedo * (diffuse + _Ambient * occlusion * float3(0.85, 0.98, 1.32));

                        float3 halfVector = normalize(lightDir + float3(0, 0, 1));
                        float nh = saturate(dot(normal, halfVector));
                        color += _FeatureColor.rgb * surface.shine * pow(nh, 36.0) * shadow * day;
                        color += _FeatureColor.rgb * surface.shine * 0.12 * pow(nh, 6.0) * day;
                        color += surface.sparkle * 0.9 * shadow * day;

                        // A faint rim keeps the dark side of the outline against the sky.
                        float rim = pow(saturate(1.0 - normal.z), 3.0);
                        color += _RimColor.rgb * _Rim * rim * (0.35 + 0.65 * day);
                        color *= occlusion * 0.35 + 0.65;

                        result = float4(color * coverage, coverage);
                    }
                }

                #if defined(_FORM_MANTLE)
                    float4 dust = Dust(p, gTime, lightDir);
                    result = dust + result * (1.0 - dust.a);
                #endif

                return half4(result);
            }
            ENDHLSL
        }
    }
}
