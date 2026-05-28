using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    [Serializable]
    public class FillLayer
    {
        public bool          Enabled   = true;
        public VectorBlendMode BlendMode = VectorBlendMode.Normal;
        [Range(0f, 1f)] public float Opacity = 1f;

        [SerializeReference]
        public FillDefinition Fill = new SolidFill();
    }
}
