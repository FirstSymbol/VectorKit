#if DOTWEEN
using DG.Tweening;
using UnityEngine;

namespace VectorKit.Runtime
{
    public static class DOTweenExtensions
    {
        public static Tweener DOShapeOpacity(this VectorShapeUI shape, float to, float duration)
            => DOTween.To(() => shape.ShapeOpacity, v => { shape.ShapeOpacity = v; shape.SetVerticesDirty(); }, to, duration);

        public static Tweener DOFillColor(this VectorShapeUI shape, Color to, float duration, int layerIndex = 0)
        {
            if (layerIndex >= shape.Fills.Count || !(shape.Fills[layerIndex].Fill is SolidFill sf))
                return null;
            return DOTween.To(() => sf.Color, v => { sf.Color = v; shape.SetVerticesDirty(); }, to, duration);
        }

        public static Tweener DOStrokeWidth(this VectorShapeUI shape, float to, float duration, int layerIndex = 0)
        {
            if (layerIndex >= shape.Strokes.Count) return null;
            var s = shape.Strokes[layerIndex];
            return DOTween.To(() => s.Width, v => { s.Width = v; shape.SetVerticesDirty(); }, to, duration);
        }

        public static Tweener DOCornerRadius(this VectorShapeUI shape, Vector4 to, float duration)
        {
            if (!(shape.Shape is RectangleShape r)) return null;
            return DOTween.To(() => r.CornerRadius, v => { r.CornerRadius = v; shape.SetVerticesDirty(); }, to, duration);
        }
    }
}
#endif
