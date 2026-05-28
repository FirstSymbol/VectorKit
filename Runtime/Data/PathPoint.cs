using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public struct PathPoint
    {
        public Vector2      Position;
        public Vector2      ControlPoint1;
        public Vector2      ControlPoint2;
        public PathPointType Type;

        public PathPoint(Vector2 position)
        {
            Position      = position;
            ControlPoint1 = position;
            ControlPoint2 = position;
            Type          = PathPointType.Line;
        }

        public PathPoint(Vector2 position, Vector2 cp1, Vector2 cp2)
        {
            Position      = position;
            ControlPoint1 = cp1;
            ControlPoint2 = cp2;
            Type          = PathPointType.Bezier;
        }
    }
}
