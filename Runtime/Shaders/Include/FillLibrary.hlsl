#ifndef VECTORKIT_FILL_LIBRARY_INCLUDED
#define VECTORKIT_FILL_LIBRARY_INCLUDED

// Evaluates a fill for a given pixel position.
// atlasRow  : row in the gradient atlas (row 0 = white/solid)
// fillKind  : FillKind enum value (0=Solid,1=Linear,2=Radial,3=Conic,4=Image)
// fillParam : x=atlasRow y=fillKind z=gradAngle/startAngle w=gradScale
// fillOffset: xy=gradOffset zw=unused
// atlasHeight: height in pixels of the gradient atlas texture
float4 EvaluateFill(
    float2 localPos,
    float2 halfSize,
    sampler2D atlas,
    float  atlasRow,
    float  fillKind,
    float  gradAngle,
    float  gradScale,
    float2 gradOffset,
    sampler2D patternTex,
    float2 patternTiling,
    float2 patternOffset,
    float  atlasHeightInv)   // 1.0 / atlasTextureHeight
{
    if (fillKind > 3.5) // Image / Pattern
    {
        float2 uv = (localPos / halfSize * 0.5 + 0.5) * patternTiling + patternOffset;
        return tex2D(patternTex, uv);
    }

    float t = 0.5;

    if (fillKind > 0.5) // Gradient types
    {
        float2 gp = localPos - (halfSize * gradOffset);
        gp /= max(gradScale, 0.001);

        if (fillKind < 1.5) // Linear
        {
            float rad  = gradAngle * 0.0174533;
            float2 dir = float2(cos(rad), sin(rad));
            t = (dot(gp, dir) / max(abs(dir.x * halfSize.x) + abs(dir.y * halfSize.y), 0.001)) * 0.5 + 0.5;
        }
        else if (fillKind < 2.5) // Radial
        {
            t = length(gp) / max(max(halfSize.x, halfSize.y), 0.001);
        }
        else if (fillKind < 3.5) // Conic (Angular)
        {
            t = frac((atan2(gp.y, gp.x) - gradAngle * 0.0174533) / 6.28318 + 0.5);
        }
    }

    // Atlas row: each gradient occupies 3 rows; sample the middle one for bilinear safety
    float vCoord = (atlasRow * 3.0 + 1.5) * atlasHeightInv;
    return tex2D(atlas, float2(saturate(t), vCoord));
}

#endif // VECTORKIT_FILL_LIBRARY_INCLUDED
