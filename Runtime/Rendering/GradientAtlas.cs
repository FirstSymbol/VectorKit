using System;
using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Manages a dynamic texture atlas that stores gradients as rows of pixels.
    // Each gradient occupies 3 rows (256 pixels wide) for safe bilinear sampling.
    // Row 0 is always white (solid fill sentinel).
    // Atlas height grows dynamically: 256 → 512 → 1024 → 2048.
    public static class GradientAtlas
    {
        private const int Width       = 256;
        private const int RowsPerGrad = 3;
        private const int MaxHeight   = 2048;
        private static  int s_Height  = 256;

        private static Texture2D s_Texture;
        private static int       s_NextFreeRow = 1; // row 0 = white
        private static readonly Stack<int>         s_FreeRows    = new Stack<int>();
        private static readonly Dictionary<int, Entry> s_HashToEntry = new Dictionary<int, Entry>();
        private static readonly Dictionary<int, int>   s_RowToHash   = new Dictionary<int, int>();

        private class Entry
        {
            public int Row;
            public int RefCount;
            public int Hash;
        }

        public static Texture2D Texture
        {
            get
            {
                EnsureCreated();
                return s_Texture;
            }
        }

        public static float HeightInverse
        {
            get
            {
                EnsureCreated();
                return 1f / s_Height;
            }
        }

        private static void EnsureCreated()
        {
            if (s_Texture != null) return;
            s_Texture = new Texture2D(Width, s_Height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };
            // Row 0 = white
            var white = new Color32[Width * RowsPerGrad];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            s_Texture.SetPixels32(0, 0, Width, RowsPerGrad, white);
            s_Texture.Apply(false);
        }

        // Returns the atlas row index for a fill definition.
        // Increments RefCount if already cached.
        public static int Acquire(FillDefinition fill)
        {
            EnsureCreated();

            if (fill == null || fill is SolidFill sf && sf.Color == Color.white) return 0;
            if (fill is SolidFill) return 0; // Solid fills use vertex color; row 0 = white

            int hash = ComputeHash(fill);

            if (s_HashToEntry.TryGetValue(hash, out var entry))
            {
                entry.RefCount++;
                return entry.Row;
            }

            int row = AllocRow();
            BakeRow(row, fill);

            var newEntry = new Entry { Row = row, RefCount = 1, Hash = hash };
            s_HashToEntry[hash] = newEntry;
            s_RowToHash[row]    = hash;
            return row;
        }

        public static void Release(int row)
        {
            if (row <= 0) return;
            if (!s_RowToHash.TryGetValue(row, out int hash)) return;
            if (!s_HashToEntry.TryGetValue(hash, out var entry)) return;

            entry.RefCount--;
            if (entry.RefCount <= 0)
            {
                s_HashToEntry.Remove(hash);
                s_RowToHash.Remove(row);
                s_FreeRows.Push(row);
            }
        }

        private static int AllocRow()
        {
            if (s_FreeRows.Count > 0) return s_FreeRows.Pop();

            int row = s_NextFreeRow++;
            if ((row + 1) * RowsPerGrad >= s_Height) Grow();
            return row;
        }

        private static void Grow()
        {
            int newH = Mathf.Min(s_Height * 2, MaxHeight);
            if (newH == s_Height)
            {
                Debug.LogWarning("[VectorKit] GradientAtlas at maximum capacity (2048). Consider releasing unused gradients.");
                return;
            }

            var newTex = new Texture2D(Width, newH, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };
            // Copy existing pixel data
            var pixels = s_Texture.GetPixels32();
            var expanded = new Color32[Width * newH];
            Array.Copy(pixels, expanded, pixels.Length);
            newTex.SetPixels32(expanded);
            newTex.Apply(false);

            if (Application.isPlaying) UnityEngine.Object.Destroy(s_Texture);
            else                       UnityEngine.Object.DestroyImmediate(s_Texture);

            s_Texture = newTex;
            s_Height  = newH;
        }

        private static void BakeRow(int row, FillDefinition fill)
        {
            var pixels = new Color32[Width * RowsPerGrad];
            Gradient gradient = null;

            switch (fill)
            {
                case LinearGradientFill lgf: gradient = lgf.Gradient; break;
                case RadialGradientFill rgf: gradient = rgf.Gradient; break;
                case ConicGradientFill  cgf: gradient = cgf.Gradient; break;
                case ImageFill imf when imf.Texture != null:
                    BakeImageRow(row, imf.Texture);
                    return;
                default:
                    // Solid or unknown: fill with white
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                    break;
            }

            if (gradient != null)
            {
                for (int x = 0; x < Width; x++)
                {
                    float t  = x / (float)(Width - 1);
                    Color c  = gradient.Evaluate(t);
                    Color32 c32 = c;
                    for (int r = 0; r < RowsPerGrad; r++)
                        pixels[r * Width + x] = c32;
                }
            }

            s_Texture.SetPixels32(0, row * RowsPerGrad, Width, RowsPerGrad, pixels);
            s_Texture.Apply(false);
        }

        private static void BakeImageRow(int row, Texture2D src)
        {
            var pixels = new Color32[Width * RowsPerGrad];
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1);
                Color c = src.GetPixelBilinear(u, 0.5f);
                Color32 c32 = c;
                for (int r = 0; r < RowsPerGrad; r++)
                    pixels[r * Width + x] = c32;
            }
            s_Texture.SetPixels32(0, row * RowsPerGrad, Width, RowsPerGrad, pixels);
            s_Texture.Apply(false);
        }

        private static int ComputeHash(FillDefinition fill)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)fill.Kind;
                switch (fill)
                {
                    case LinearGradientFill lgf:
                        h = HashGradient(h, lgf.Gradient);
                        h = h * 31 + lgf.Angle.GetHashCode();
                        h = h * 31 + lgf.Scale.GetHashCode();
                        break;
                    case RadialGradientFill rgf:
                        h = HashGradient(h, rgf.Gradient);
                        h = h * 31 + rgf.Center.GetHashCode();
                        h = h * 31 + rgf.Radius.GetHashCode();
                        break;
                    case ConicGradientFill cgf:
                        h = HashGradient(h, cgf.Gradient);
                        h = h * 31 + cgf.StartAngle.GetHashCode();
                        break;
                    case ImageFill imf:
                        h = h * 31 + (imf.Texture != null ? imf.Texture.GetInstanceID() : 0);
                        h = h * 31 + imf.FitMode.GetHashCode();
                        break;
                }
                return h;
            }
        }

        private static int HashGradient(int seed, Gradient g)
        {
            unchecked
            {
                int h = seed;
                foreach (var ck in g.colorKeys)
                {
                    h = h * 31 ^ ck.color.GetHashCode();
                    h = h * 31 ^ ck.time.GetHashCode();
                }
                foreach (var ak in g.alphaKeys)
                {
                    h = h * 31 ^ ak.alpha.GetHashCode();
                    h = h * 31 ^ ak.time.GetHashCode();
                }
                return h;
            }
        }

        public static void Clear()
        {
            if (s_Texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(s_Texture);
                else                       UnityEngine.Object.DestroyImmediate(s_Texture);
                s_Texture = null;
            }
            s_HashToEntry.Clear();
            s_RowToHash.Clear();
            s_FreeRows.Clear();
            s_NextFreeRow = 1;
            s_Height      = 256;
        }
    }
}
