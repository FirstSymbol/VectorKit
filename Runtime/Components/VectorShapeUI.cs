using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("VectorKit/Vector Shape (UI)")]
    [DisallowMultipleComponent]
    public class VectorShapeUI : MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeReference]
        public ShapeDefinition Shape = new RectangleShape();

        [SerializeField]
        public List<FillLayer> Fills = new List<FillLayer> { new FillLayer() };

        [SerializeField]
        public List<StrokeLayer> Strokes = new List<StrokeLayer>();

        [SerializeField]
        public List<VectorEffect> Effects = new List<VectorEffect>();

        [Range(0f, 1f)]
        public float ShapeOpacity = 1f;

        // Soft mask source (set by VectorSoftMaskable on the same GameObject, if present)
        internal VectorShapeUI SoftMaskSource;
        internal Matrix4x4    MaskWorldToLocal;

        private readonly VectorShaderState _state    = new VectorShaderState();
        private readonly List<int>         _atlasRows = new List<int>();
        private Material _pooledMat;
        private bool     _stateDirty;

        private static Material s_DefaultMat;

        public override Material defaultMaterial
        {
            get
            {
                if (s_DefaultMat == null)
                {
                    var shader = Shader.Find("VectorKit/Shape");
                    if (shader != null)
                        s_DefaultMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return s_DefaultMat;
            }
        }

        // ── Mesh Generation ──────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            ReleaseAtlasRows();

            var rect = GetPixelAdjustedRect();
            SDFMeshBuilder.Populate(
                vh, rect.size, color,
                Shape, Fills, Strokes, Effects,
                _state, _atlasRows);

            ApplySoftMaskToState();
            _stateDirty = true;
            SetMaterialDirty();
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive()) return;
            base.UpdateMaterial();

            if (_stateDirty)
            {
                _stateDirty = false;
                ReleaseMaterial();
                var baseMat = defaultMaterial;
                if (baseMat != null)
                {
                    _state.BaseMatId = baseMat.GetInstanceID();
                    _pooledMat       = VectorMaterialManager.GetMaterial(_state, baseMat);
                }
            }

            if (_pooledMat != null)
                canvasRenderer.SetMaterial(_pooledMat, GradientAtlas.Texture);

            canvasRenderer.SetAlpha(ShapeOpacity);
        }

        // ── Raycast Filter ───────────────────────────────────────────────────────

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (Shape == null) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out Vector2 local))
                return false;

            var  rect     = GetPixelAdjustedRect();
            var  halfSize = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            float d       = SDFMath.Evaluate(Shape, local, halfSize);
            return d <= 0f;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseAtlasRows();
            ReleaseMaterial();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseMaterial();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
            SetMaterialDirty();
        }
#endif

        // ── Internal ─────────────────────────────────────────────────────────────

        private void ApplySoftMaskToState()
        {
            if (SoftMaskSource == null)
            {
                _state.HasMask = false;
                return;
            }
            _state.HasMask     = true;
            _state.MaskMatrix  = MaskWorldToLocal;
            // mask shape kind and params are set externally by VectorSoftMaskable
        }

        private void ReleaseAtlasRows()
        {
            foreach (int row in _atlasRows) GradientAtlas.Release(row);
            _atlasRows.Clear();
        }

        private void ReleaseMaterial()
        {
            if (_pooledMat == null) return;
            VectorMaterialManager.ReleaseMaterial(_pooledMat);
            _pooledMat = null;
        }
    }
}
