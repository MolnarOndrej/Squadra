#ifndef SQUADRA_NOISE_INCLUDED
#define SQUADRA_NOISE_INCLUDED

// Hash and value-noise helpers shared by the Squadra procedural shaders.

float Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 Hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float ValueNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = Hash31(i);
    float n100 = Hash31(i + float3(1, 0, 0));
    float n010 = Hash31(i + float3(0, 1, 0));
    float n110 = Hash31(i + float3(1, 1, 0));
    float n001 = Hash31(i + float3(0, 0, 1));
    float n101 = Hash31(i + float3(1, 0, 1));
    float n011 = Hash31(i + float3(0, 1, 1));
    float n111 = Hash31(i + float3(1, 1, 1));
    return lerp(
        lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
        lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
        f.z);
}

float Fbm2(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 5; i++)
    {
        sum += amp * ValueNoise2(p);
        p = p * 2.03 + float2(17.3, 9.1);
        amp *= 0.5;
    }
    return sum / 0.96875;
}

float Fbm3(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 5; i++)
    {
        sum += amp * ValueNoise3(p);
        p = p * 2.02 + float3(13.1, 7.7, 21.3);
        amp *= 0.5;
    }
    return sum / 0.96875;
}

float Fbm3Lite(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 3; i++)
    {
        sum += amp * ValueNoise3(p);
        p = p * 2.02 + float3(13.1, 7.7, 21.3);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Distance to the nearest feature point of a 3D cell grid, plus that cell's random value.
float2 Worley3(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    float best = 8.0;
    float id = 0.0;
    for (int z = -1; z <= 1; z++)
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float3 offset = float3(x, y, z);
        float3 h = Hash33(cell + offset);
        float3 d = offset + h - f;
        float dist = dot(d, d);
        if (dist < best)
        {
            best = dist;
            id = h.x;
        }
    }
    return float2(sqrt(best), id);
}

float3 RotateY(float3 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
}

float3 RotateZ(float3 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float3(c * v.x - s * v.y, s * v.x + c * v.y, v.z);
}

#endif
