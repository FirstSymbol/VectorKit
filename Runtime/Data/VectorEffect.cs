using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public abstract class VectorEffect
    {
        public bool  Enabled = true;
        [Range(0f, 1f)] public float Opacity = 1f;

        public abstract EffectKind Kind { get; }
    }

    [Serializable]
    public sealed class DropShadowEffect : VectorEffect
    {
        public Vector2 Offset = new Vector2(0f, -4f);
        [Min(0f)] public float Blur   = 10f;
        public float   Spread = 0f;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill { Color = new Color(0f, 0f, 0f, 0.5f) };

        public override EffectKind Kind => EffectKind.DropShadow;
    }

    [Serializable]
    public sealed class InnerShadowEffect : VectorEffect
    {
        public Vector2 Offset = new Vector2(0f, -4f);
        [Min(0f)] public float Blur   = 10f;
        public float   Spread = 0f;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill { Color = new Color(0f, 0f, 0f, 0.5f) };

        public override EffectKind Kind => EffectKind.InnerShadow;
    }

    [Serializable]
    public sealed class OuterGlowEffect : VectorEffect
    {
        [Min(0f)] public float Blur   = 10f;
        public float   Spread = 0f;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill { Color = new Color(1f, 1f, 0f, 0.8f) };

        public override EffectKind Kind => EffectKind.OuterGlow;
    }

    [Serializable]
    public sealed class InnerGlowEffect : VectorEffect
    {
        [Min(0f)] public float Blur   = 10f;
        public float   Spread = 0f;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill { Color = new Color(1f, 1f, 1f, 0.5f) };

        public override EffectKind Kind => EffectKind.InnerGlow;
    }

    [Serializable]
    public sealed class GaussianBlurEffect : VectorEffect
    {
        [Range(0f, 100f)] public float Radius = 5f;

        public override EffectKind Kind => EffectKind.GaussianBlur;
    }

    [Serializable]
    public sealed class BevelEffect : VectorEffect
    {
        [Min(0f)] public float Distance       = 5f;
        [Range(0f, 360f)] public float Angle  = 135f;
        [Range(0f, 1f)] public float HighlightAlpha = 0.8f;
        [Range(0f, 1f)] public float ShadowAlpha    = 0.8f;

        public override EffectKind Kind => EffectKind.Bevel;
    }
}
