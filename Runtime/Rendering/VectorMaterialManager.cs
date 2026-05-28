using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Material pool keyed by VectorShaderState.
    // Shared between all VectorShapeUI instances.
    public static class VectorMaterialManager
    {
        // Reusable temp state — used to build state before lookup, avoiding allocation
        public static readonly VectorShaderState TempState = new VectorShaderState();

        private class PoolEntry
        {
            public Material          Material;
            public int               RefCount;
            public VectorShaderState State;
        }

        private static readonly Dictionary<int, List<PoolEntry>> s_Pool          = new Dictionary<int, List<PoolEntry>>();
        private static readonly Dictionary<int, PoolEntry>       s_InstToEntry   = new Dictionary<int, PoolEntry>();

        public static Material GetMaterial(VectorShaderState state, Material baseMat)
        {
            int hash = state.GetHashCode();

            if (s_Pool.TryGetValue(hash, out var list))
            {
                foreach (var entry in list)
                {
                    if (entry.State.Equals(state))
                    {
                        entry.RefCount++;
                        return entry.Material;
                    }
                }
            }
            else
            {
                list = new List<PoolEntry>();
                s_Pool[hash] = list;
            }

            var mat = new Material(baseMat) { hideFlags = HideFlags.HideAndDontSave };
            state.ApplyToMaterial(mat);

            var newEntry = new PoolEntry { Material = mat, RefCount = 1, State = state.Clone() };
            list.Add(newEntry);
            s_InstToEntry[mat.GetInstanceID()] = newEntry;
            return mat;
        }

        public static void ReleaseMaterial(Material mat)
        {
            if (mat == null) return;
            int id = mat.GetInstanceID();

            if (s_InstToEntry.TryGetValue(id, out var entry))
            {
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    int hash = entry.State.GetHashCode();
                    if (s_Pool.TryGetValue(hash, out var list)) { list.Remove(entry); if (list.Count == 0) s_Pool.Remove(hash); }
                    s_InstToEntry.Remove(id);
                    Destroy(entry.Material);
                }
            }
            else
            {
                Destroy(mat);
            }
        }

        public static void ClearAll()
        {
            foreach (var list in s_Pool.Values)
                foreach (var entry in list)
                    if (entry.Material) Destroy(entry.Material);
            s_Pool.Clear();
            s_InstToEntry.Clear();
        }

        private static void Destroy(Material mat)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(mat);
            else                       UnityEngine.Object.DestroyImmediate(mat);
        }
    }
}
