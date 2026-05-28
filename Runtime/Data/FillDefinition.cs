using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public abstract class FillDefinition
    {
        public abstract FillKind Kind { get; }
    }

    [Serializable]
    public sealed class SolidFill : FillDefinition
    {
        public Color Color = Color.white;

        public override FillKind Kind => FillKind.Solid;
    }

    [Serializable]
    public sealed class LinearGradientFill : FillDefinition
    {
        public Gradient Gradient = CreateDefaultGradient();
        [Range(-360f, 360f)] public float   Angle  = 0f;
        public Vector2  Offset = Vector2.zero;
        [Min(0.001f)] public float Scale  = 1f;

        public override FillKind Kind => FillKind.LinearGradient;

        static Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }

    [Serializable]
    public sealed class RadialGradientFill : FillDefinition
    {
        public Gradient Gradient = CreateDefaultGradient();
        public Vector2  Center   = Vector2.zero;
        [Min(0.001f)] public float   Radius   = 1f;

        public override FillKind Kind => FillKind.RadialGradient;

        static Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }

    [Serializable]
    public sealed class ConicGradientFill : FillDefinition
    {
        public Gradient Gradient    = CreateDefaultGradient();
        public Vector2  Center      = Vector2.zero;
        [Range(-360f, 360f)] public float StartAngle = 0f;

        public override FillKind Kind => FillKind.ConicGradient;

        static Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }

    [Serializable]
    public sealed class ImageFill : FillDefinition
    {
        public Texture2D  Texture = null;
        public Vector2    Tiling  = Vector2.one;
        public Vector2    Offset  = Vector2.zero;
        public ImageFitMode FitMode = ImageFitMode.Tile;

        public override FillKind Kind => FillKind.Image;
    }
}
