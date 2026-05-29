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
        // Channels required by the VectorShape shader (UV1/2/3, Normal, Tangent)
        private const AdditionalCanvasShaderChannels RequiredChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 |
            AdditionalCanvasShaderChannels.TexCoord3 |
            AdditionalCanvasShaderChannels.Normal     |
            AdditionalCanvasShaderChannels.Tangent;

        [SerializeReference]
        public ShapeDefinition Shape = new RectangleShape();

        [SerializeField]
        public List<FillLayer> Fills = new List<FillLayer> { new FillLayer() };

        [SerializeField]
        public List<StrokeLayer> Strokes = new List<StrokeLayer>();

        [SerializeReference]
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
        private int      _maskFillAtlasRow;

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

        // ── Lifecycle ─ (early) ──────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasChannels();
        }

        private void EnsureCanvasChannels()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas;
            if ((root.additionalShaderChannels & RequiredChannels) != RequiredChannels)
                root.additionalShaderChannels |= RequiredChannels;
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
            else if (_pooledMat == null)
            {
                // Material was lost (e.g. after domain reload) without a state rebuild — force one next frame.
                SetVerticesDirty();
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

        // ── Lifecycle ─ (late) ───────────────────────────────────────────────────

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseAtlasRows();
            ReleaseMaterial();
            ReleaseMaskAtlasRow();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseMaterial();
            ReleaseMaskAtlasRow();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureCanvasChannels();
            SetVerticesDirty();
            SetMaterialDirty();
        }
#endif

        // ── Internal ─────────────────────────────────────────────────────────────

        private void ApplySoftMaskToState()
        {
            ReleaseMaskAtlasRow();

            if (SoftMaskSource == null) { _state.HasMask = false; return; }

            var maskShape = SoftMaskSource.Shape;
            if (maskShape == null) { _state.HasMask = false; return; }

            _state.HasMask    = true;
            _state.MaskMatrix = MaskWorldToLocal;

            var maskRect = SoftMaskSource.GetPixelAdjustedRect();
            float maskHW = maskRect.width  * 0.5f * maskShape.Scale.x;
            float maskHH = maskRect.height * 0.5f * maskShape.Scale.y;

            float maskKind   = (float)maskShape.Kind;
            float maskSmooth = maskShape is RectangleShape mrs ? mrs.CornerSmoothing : 0f;
            float feather    = Mathf.Max(0.001f, maskShape.EdgeSoftness);

            _state.MaskParams = new Vector4(1f, maskKind, maskSmooth, feather);
            _state.MaskSize   = new Vector4(maskHW * 2f, maskHH * 2f, 0f, 0f);
            _state.MaskShape  = maskShape.PackShaderParams();
            _state.MaskTex    = GradientAtlas.Texture;

            float opacity = SoftMaskSource.ShapeOpacity;
            var fills     = SoftMaskSource.Fills;
            var firstFill = (fills != null && fills.Count > 0) ? fills[0]?.Fill : null;

            switch (firstFill)
            {
                case LinearGradientFill lgf:
                    _maskFillAtlasRow     = GradientAtlas.Acquire(lgf);
                    _state.MaskFillParams = new Vector4((float)FillKind.LinearGradient, lgf.Angle, lgf.Scale, _maskFillAtlasRow);
                    _state.MaskFillOffset = new Vector4(0f, 0f, opacity, 0f);
                    break;
                case RadialGradientFill rgf:
                    _maskFillAtlasRow     = GradientAtlas.Acquire(rgf);
                    _state.MaskFillParams = new Vector4((float)FillKind.RadialGradient, 0f, rgf.Radius, _maskFillAtlasRow);
                    _state.MaskFillOffset = new Vector4(0f, 0f, opacity, 0f);
                    break;
                case ConicGradientFill cgf:
                    _maskFillAtlasRow     = GradientAtlas.Acquire(cgf);
                    _state.MaskFillParams = new Vector4((float)FillKind.ConicGradient, cgf.StartAngle, 1f, _maskFillAtlasRow);
                    _state.MaskFillOffset = new Vector4(0f, 0f, opacity, 0f);
                    break;
                default: // SolidFill or null
                    if (firstFill is SolidFill sf) opacity *= sf.Color.a;
                    _state.MaskFillParams = Vector4.zero;
                    _state.MaskFillOffset = new Vector4(0f, 0f, opacity, 0f);
                    break;
            }
        }

        private void ReleaseMaskAtlasRow()
        {
            if (_maskFillAtlasRow > 0) { GradientAtlas.Release(_maskFillAtlasRow); _maskFillAtlasRow = 0; }
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
