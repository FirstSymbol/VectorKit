using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Manages a pool of RenderTextures used by VectorGroup for blend mode isolation.
    public static class BlendCompositor
    {
        private static readonly Dictionary<long, Stack<RenderTexture>> s_Pool = new Dictionary<long, Stack<RenderTexture>>();

        private static long Key(int w, int h, RenderTextureFormat fmt)
            => ((long)w << 32) | ((long)h << 16) | (long)fmt;

        public static RenderTexture Acquire(int w, int h, RenderTextureFormat fmt = RenderTextureFormat.ARGB32)
        {
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            var key = Key(w, h, fmt);
            if (s_Pool.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                var rt = stack.Pop();
                if (rt != null && rt.IsCreated()) { RenderTexture.active = rt; GL.Clear(true, true, Color.clear); return rt; }
            }
            var newRT = new RenderTexture(w, h, 0, fmt)
            {
                hideFlags  = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };
            newRT.Create();
            RenderTexture.active = newRT;
            GL.Clear(true, true, Color.clear);
            return newRT;
        }

        public static void Release(RenderTexture rt)
        {
            if (rt == null) return;
            var key = Key(rt.width, rt.height, rt.format);
            if (!s_Pool.TryGetValue(key, out var stack)) { stack = new Stack<RenderTexture>(); s_Pool[key] = stack; }
            stack.Push(rt);
        }

        public static void ClearAll()
        {
            foreach (var stack in s_Pool.Values)
                while (stack.Count > 0) { var rt = stack.Pop(); if (rt != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(rt); else UnityEngine.Object.DestroyImmediate(rt); } }
            s_Pool.Clear();
        }
    }
}
