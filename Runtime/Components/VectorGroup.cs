using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    // Isolation group for VectorKit layers.
    // In Normal mode: acts as a transparent CanvasGroup container (opacity + raycasting).
    // In non-Normal blend modes: marks the group for compositing; full RT-based blend-mode
    // isolation requires a URP RenderFeature (see VectorBlend.shader) and is applied when
    // a VectorGroupRenderer (URP feature) is present in the active renderer asset.
    [AddComponentMenu("VectorKit/Vector Group")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class VectorGroup : MonoBehaviour
    {
        public VectorBlendMode BlendMode = VectorBlendMode.Normal;
        [Range(0f, 1f)] public float Opacity = 1f;

        private CanvasGroup _group;
        private RenderTexture _rt;
        private Material _blendMat;

        private static Material s_BlendBaseMat;
        private static Material BlendBaseMat
        {
            get
            {
                if (s_BlendBaseMat == null)
                {
                    var shader = Shader.Find("VectorKit/Blend");
                    if (shader != null)
                        s_BlendBaseMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return s_BlendBaseMat;
            }
        }

        private void OnEnable()
        {
            EnsureCanvasGroup();
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            ReleaseRT();
        }

        private void OnDestroy()
        {
            if (_blendMat) Destroy(_blendMat);
        }

        private void OnValidate()
        {
            EnsureCanvasGroup();
            SyncCanvasGroup();
        }

        private void OnWillRenderCanvases()
        {
            SyncCanvasGroup();

            if (!isActiveAndEnabled || BlendMode == VectorBlendMode.Normal)
            {
                ReleaseRT();
                return;
            }

            // Acquire RT and set blend material properties for URP feature compositing
            var rt = GetOrCreateRT();
            if (rt == null) return;

            if (_blendMat == null && BlendBaseMat != null)
                _blendMat = new Material(BlendBaseMat) { hideFlags = HideFlags.HideAndDontSave };

            if (_blendMat != null)
            {
                _blendMat.SetTexture(ShaderPropertyIDs.SrcTex, rt);
                _blendMat.SetInt(ShaderPropertyIDs.BlendMode, (int)BlendMode);
            }
        }

        private void SyncCanvasGroup()
        {
            if (_group == null) EnsureCanvasGroup();
            if (_group != null)
                _group.alpha = Opacity;
        }

        private RenderTexture GetOrCreateRT()
        {
            if (_rt != null && _rt.IsCreated()) return _rt;
            var rt = (transform as RectTransform)?.rect;
            int w = Mathf.Max(1, Mathf.RoundToInt(rt?.width  ?? 256));
            int h = Mathf.Max(1, Mathf.RoundToInt(rt?.height ?? 256));
            _rt = BlendCompositor.Acquire(w, h);
            return _rt;
        }

        private void ReleaseRT()
        {
            if (_rt != null) { BlendCompositor.Release(_rt); _rt = null; }
        }

        private void EnsureCanvasGroup()
        {
            if (_group == null)
                _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
    }
}
