using System;

namespace VectorKit.Runtime
{
    public enum ShapeKind
    {
        Rectangle = 0,
        Ellipse   = 1,
        Polygon   = 2,
        Star      = 3,
        Line      = 4,
        Arc       = 5,
        Path      = 6,
        Boolean   = 7,
        Capsule   = 8,
        Triangle  = 9,
        Heart     = 10,
    }

    public enum FillKind
    {
        Solid           = 0,
        LinearGradient  = 1,
        RadialGradient  = 2,
        ConicGradient   = 3,
        Image           = 4,
    }

    public enum VectorBlendMode
    {
        Normal     = 0,
        Multiply   = 1,
        Screen     = 2,
        Overlay    = 3,
        Darken     = 4,
        Lighten    = 5,
        ColorDodge = 6,
        ColorBurn  = 7,
        HardLight  = 8,
        SoftLight  = 9,
        Difference = 10,
        Exclusion  = 11,
        Hue        = 12,
        Saturation = 13,
        Color      = 14,
        Luminosity = 15,
    }

    public enum StrokeAlignment
    {
        Inside  = 0,
        Center  = 1,
        Outside = 2,
    }

    public enum LineCap
    {
        Butt   = 0,
        Round  = 1,
        Square = 2,
    }

    public enum LineJoint
    {
        Miter = 0,
        Round = 1,
        Bevel = 2,
    }

    public enum PathPointType
    {
        Line   = 0,
        Bezier = 1,
    }

    public enum ImageFitMode
    {
        Tile    = 0,
        Fill    = 1,
        Fit     = 2,
        Stretch = 3,
    }

    public enum BoolOp
    {
        None         = 0,
        Union        = 1,
        Subtraction  = 2,
        Intersection = 3,
        Xor          = 4,
    }

    public enum EffectKind
    {
        DropShadow   = 0,
        InnerShadow  = 1,
        OuterGlow    = 2,
        InnerGlow    = 3,
        GaussianBlur = 4,
        Bevel        = 5,
    }
}
