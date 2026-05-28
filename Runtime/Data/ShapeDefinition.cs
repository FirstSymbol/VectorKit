using System;
using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public abstract class ShapeDefinition
    {
        public Vector2 Scale     = Vector2.one;
        public Vector2 Pivot     = new Vector2(0.5f, 0.5f);
        public float   EdgeSoftness    = 1f;
        public float   InternalPadding = 0f;

        public abstract ShapeKind Kind { get; }

        // Returns primary shape params packed into Vector4 for UV1 channel
        public abstract Vector4 PackShaderParams();

        // Returns secondary params packed for extra channel
        public virtual Vector4 PackShaderParams2() => Vector4.zero;

        // Geometric center offset from RectTransform center (for custom pivots)
        public virtual Vector2 GetGeometricCenterOffset(float hw, float hh) => Vector2.zero;
    }

    [Serializable]
    public sealed class RectangleShape : ShapeDefinition
    {
        public Vector4 CornerRadius   = Vector4.zero;
        public float   CornerSmoothing = 0f;

        public override ShapeKind Kind => ShapeKind.Rectangle;

        public override Vector4 PackShaderParams() => CornerRadius;

        public override Vector2 GetGeometricCenterOffset(float hw, float hh)
        {
            float pivotX = (Pivot.x - 0.5f) * hw * 2f;
            float pivotY = (Pivot.y - 0.5f) * hh * 2f;
            return new Vector2(pivotX, pivotY);
        }
    }

    [Serializable]
    public sealed class EllipseShape : ShapeDefinition
    {
        public override ShapeKind Kind => ShapeKind.Ellipse;
        public override Vector4 PackShaderParams() => Vector4.zero;
    }

    [Serializable]
    public sealed class PolygonShape : ShapeDefinition
    {
        [Range(3, 128)] public int   Sides    = 6;
        [Range(0f, 1f)] public float Rounding = 0f;
        public float Rotation = 0f;

        public override ShapeKind Kind => ShapeKind.Polygon;
        public override Vector4 PackShaderParams() => new Vector4(Sides, Rounding, Rotation * Mathf.Deg2Rad, 0f);
    }

    [Serializable]
    public sealed class StarShape : ShapeDefinition
    {
        [Range(3, 128)] public int   Points       = 5;
        [Range(0.01f, 1f)] public float Ratio      = 0.5f;
        [Range(0f, 1f)]    public float OuterRounding = 0f;
        [Range(0f, 1f)]    public float InnerRounding = 0f;
        public float Rotation = 0f;

        public override ShapeKind Kind => ShapeKind.Star;
        public override Vector4 PackShaderParams() => new Vector4(Points, Ratio, OuterRounding, InnerRounding);
        public override Vector4 PackShaderParams2() => new Vector4(Rotation * Mathf.Deg2Rad, 0f, 0f, 0f);
    }

    [Serializable]
    public sealed class LineShape : ShapeDefinition
    {
        public Vector2 Start = new Vector2(-50f, 0f);
        public Vector2 End   = new Vector2( 50f, 0f);
        public float   Width = 4f;
        public LineCap Cap   = LineCap.Round;

        public override ShapeKind Kind => ShapeKind.Line;
        public override Vector4 PackShaderParams() => new Vector4(Start.x, Start.y, End.x, End.y);
        public override Vector4 PackShaderParams2() => new Vector4(Width, (float)Cap, 0f, 0f);
    }

    [Serializable]
    public sealed class ArcShape : ShapeDefinition
    {
        [Range(0f, 1f)]   public float InnerRadius = 0.5f;
        [Range(-360f, 360f)] public float StartAngle = 0f;
        [Range(-360f, 360f)] public float EndAngle   = 360f;

        public override ShapeKind Kind => ShapeKind.Arc;
        public override Vector4 PackShaderParams() =>
            new Vector4(InnerRadius, StartAngle * Mathf.Deg2Rad, EndAngle * Mathf.Deg2Rad, 0f);
    }

    [Serializable]
    public sealed class CapsuleShape : ShapeDefinition
    {
        [Range(0f, 1f)] public float Rounding = 1f;

        public override ShapeKind Kind => ShapeKind.Capsule;
        public override Vector4 PackShaderParams() => new Vector4(Rounding, 0f, 0f, 0f);
    }

    [Serializable]
    public sealed class TriangleShape : ShapeDefinition
    {
        public override ShapeKind Kind => ShapeKind.Triangle;
        public override Vector4 PackShaderParams() => Vector4.zero;
    }

    [Serializable]
    public sealed class HeartShape : ShapeDefinition
    {
        public override ShapeKind Kind => ShapeKind.Heart;
        public override Vector4 PackShaderParams() => Vector4.zero;
    }

    [Serializable]
    public sealed class PathShape : ShapeDefinition
    {
        public List<PathPoint> Points    = new List<PathPoint>();
        public bool            Closed    = false;
        public float           Thickness = 4f;

        public override ShapeKind Kind => ShapeKind.Path;
        public override Vector4 PackShaderParams() => new Vector4(Closed ? 1f : 0f, Thickness, 0f, 0f);
    }

    [Serializable]
    public sealed class BooleanShape : ShapeDefinition
    {
        public List<BooleanOperationData> Operations = new List<BooleanOperationData>();

        public override ShapeKind Kind => ShapeKind.Boolean;
        public override Vector4 PackShaderParams() => Vector4.zero;
    }
}
