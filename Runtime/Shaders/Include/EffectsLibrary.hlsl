#ifndef VECTORKIT_EFFECTS_LIBRARY_INCLUDED
#define VECTORKIT_EFFECTS_LIBRARY_INCLUDED

// Computes a mask value [0,1] for a given layer type.
//
// effectType:
//   0 = Main Fill / Blur
//   1 = Drop Shadow / Outer Glow
//   2 = Stroke
//   3 = Inner Shadow / Inner Glow
//   5 = Bevel (handled inline, returns 1)
//
// d       : SDF distance at shadow-offset position (for effectType 1 and 3)
// d_orig  : SDF distance at original position (for effectType 0, 2, 3)
// blur    : blur radius
// aa      : anti-aliasing width (edge softness)
// spread  : spread / stroke width
// alignment: stroke alignment (0=inside, 1=center, 2=outside)
// dashData : xy = dashSize, dashGap (0 = no dash)
// localPos : current pixel local position
// halfSize : shape half size
// perimCoord: precomputed perimeter coordinate (for dashes)

float ComputeLayerMask(
    float effectType,
    float d,
    float d_orig,
    float blur,
    float aa,
    float spread,
    float alignment,
    float dashSize,
    float dashGap)
{
    float mask = 0.0;

    if (effectType < 0.5 || (effectType > 3.5 && effectType < 4.5)) // Main Fill / Blur
    {
        mask = smoothstep(max(blur, aa), -max(blur, aa), d_orig);
    }
    else if (effectType < 1.5) // Drop Shadow / Outer Glow
    {
        float dd = d - spread;
        mask = smoothstep(max(blur, aa), -max(blur, aa), dd);
    }
    else if (effectType < 2.5) // Stroke
    {
        float strokeOffset = (alignment < 0.5)  ? -spread * 0.5   // inside
                           : (alignment > 1.5)  ?  spread * 0.5   // outside
                           : 0.0;                                   // center
        float strokeD = abs(d_orig - strokeOffset) - spread * 0.5;

        if (dashSize > 0.001 && (dashSize + dashGap) > 0.001)
        {
            // Dash check is done externally via discard; pass through here
        }
        mask = smoothstep(aa, -aa, strokeD);
    }
    else if (effectType < 3.5) // Inner Shadow / Inner Glow
    {
        float baseD = d + spread;
        mask = saturate(smoothstep(-max(blur, 0.001), 0.0, baseD));
        mask = min(mask, smoothstep(aa, -aa, d_orig));
    }
    else if (effectType < 5.5) // Bevel
    {
        mask = 1.0; // Bevel mask is computed inline in fragment shader
    }

    return mask;
}

#endif // VECTORKIT_EFFECTS_LIBRARY_INCLUDED
