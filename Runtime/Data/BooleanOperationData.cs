using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public class BooleanOperationData
    {
        public BoolOp Operation = BoolOp.Union;

        [Range(0f, 200f)]
        public float Smoothness = 0f;

        [SerializeReference]
        public ShapeDefinition Shape = new RectangleShape();
    }
}
