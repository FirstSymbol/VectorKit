using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public class StrokeLayer
    {
        public bool            Enabled   = true;
        public VectorBlendMode BlendMode = VectorBlendMode.Normal;
        [Range(0f, 1f)] public float Opacity   = 1f;
        [Min(0f)]       public float Width     = 2f;
        public StrokeAlignment Alignment = StrokeAlignment.Inside;
        [Min(0f)] public float Dash = 0f;
        [Min(0f)] public float Gap  = 0f;
        public LineCap Cap  = LineCap.Butt;
        public LineJoint Joint = LineJoint.Miter;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill();
    }
}
