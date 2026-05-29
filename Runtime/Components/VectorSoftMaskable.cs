using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    // Applies a VectorShapeUI as a soft (alpha) mask to any MaskableGraphic on the same GameObject.
    // Drag the mask shape into MaskSource.
    //
    // • VectorShapeUI target  — mask is injected into the shape's own SDF shader state.
    // • Any other Graphic     — mask is applied via IMaterialModifier using VectorKit/SoftMaskedImage.
    //                           The mask matrix is maskSource.worldToLocalMatrix so no uv1 injection is needed.
    [ExecuteAlways]
    [AddComponentMenu("VectorKit/Vector Soft Maskable")]
    [DisallowMultipleComponent]
    public class VectorSoftMaskable : MonoBehaviour, IMaterialModifier
    {
        [Tooltip("VectorShapeUI whose shape acts as an alpha mask for this element.")]
        [SerializeField]
        private VectorShapeUI _maskSource;

        public VectorShapeUI MaskSource
        {
            get => _maskSource;
            set
            {
                if (_maskSource == value) return;
                _maskSource = value;
                Apply();
            }
        }

        private Graphic        _target;
        private VectorShapeUI  _targetVS;   // non-null when target is VectorShapeUI

        // Material used for non-VectorShapeUI targets (Image, RawImage, Text, etc.)
        private Material       _softMaskMat;
        private static Shader  s_SoftMaskShader;

        private void OnEnable()  => Apply();
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();

        private void Apply()
        {
            _target   = GetComponent<Graphic>();
            _targetVS = _target as VectorShapeUI;

            if (_target == null) return;

            if (_targetVS != null)
            {
                // VectorShapeUI path — inject mask directly into SDF state
                if (_maskSource == null) { Clear(); return; }
                _targetVS.SoftMaskSource = _maskSource;
                UpdateMatrix();
                _targetVS.SetVerticesDirty();
                _targetVS.SetMaterialDirty();
            }
            else
            {
                // Generic Graphic path — handled by GetModifiedMaterial
                _target.SetMaterialDirty();
            }
        }

        private void Clear()
        {
            if (_targetVS != null)
            {
                _targetVS.SoftMaskSource = null;
                _targetVS.SetVerticesDirty();
                _targetVS.SetMaterialDirty();
            }
            else if (_target != null)
            {
                _target.SetMaterialDirty();
            }
            ReleaseSoftMaskMat();
        }

        // Called when either transform moves — refreshes coordinate mapping.
        public void UpdateMatrix()
        {
            if (_targetVS != null && _maskSource != null)
                _targetVS.MaskWorldToLocal = _maskSource.transform.worldToLocalMatrix
                                             * _targetVS.transform.localToWorldMatrix;
        }

        // ── IMaterialModifier ─────────────────────────────────────────────────────

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // VectorShapeUI handles masking itself; don't intercept
            if (_targetVS != null) return baseMaterial;

            if (!isActiveAndEnabled || _maskSource == null)
            {
                ReleaseSoftMaskMat();
                return baseMaterial;
            }

            EnsureSoftMaskShader();
            if (s_SoftMaskShader == null) return baseMaterial;

            if (_softMaskMat == null)
                _softMaskMat = new Material(s_SoftMaskShader) { hideFlags = HideFlags.HideAndDontSave };

            // Copy UI-compatible properties explicitly from the base material.
            // CopyPropertiesFromMaterial also copies the shader (a Unity quirk), so we do
            // a targeted copy instead to keep the VectorKit/SoftMaskedImage shader intact.
            CopyUIPropertiesToSoftMaskMat(baseMaterial, _softMaskMat);
            ApplyMaskToMaterial(_softMaskMat);
            return _softMaskMat;
        }

        private static readonly int[] s_UIMatProps = new int[0]; // allocated lazily

        private static void CopyUIPropertiesToSoftMaskMat(Material src, Material dst)
        {
            // Texture
            var mainTex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            if (mainTex != null) dst.SetTexture("_MainTex", mainTex);

            // Stencil / clip props set by Unity's Mask system
            if (src.HasProperty("_StencilComp"))   dst.SetFloat("_StencilComp",   src.GetFloat("_StencilComp"));
            if (src.HasProperty("_Stencil"))        dst.SetFloat("_Stencil",        src.GetFloat("_Stencil"));
            if (src.HasProperty("_StencilOp"))      dst.SetFloat("_StencilOp",      src.GetFloat("_StencilOp"));
            if (src.HasProperty("_StencilWriteMask"))dst.SetFloat("_StencilWriteMask",src.GetFloat("_StencilWriteMask"));
            if (src.HasProperty("_StencilReadMask")) dst.SetFloat("_StencilReadMask", src.GetFloat("_StencilReadMask"));
            if (src.HasProperty("_ColorMask"))      dst.SetFloat("_ColorMask",      src.GetFloat("_ColorMask"));
        }

        // ── Internal ─────────────────────────────────────────────────────────────

        private void ApplyMaskToMaterial(Material mat)
        {
            if (_maskSource == null || _target == null) return;

            var maskShape = _maskSource.Shape;
            if (maskShape == null) return;

            // worldToLocalMatrix of the mask source transforms canvas-space vertex
            // positions (worldPos) into the mask's local SDF coordinate frame.
            var maskMatrix = _maskSource.transform.worldToLocalMatrix;

            mat.SetVector("_MaskMatrixX", maskMatrix.GetRow(0));
            mat.SetVector("_MaskMatrixY", maskMatrix.GetRow(1));
            mat.SetVector("_MaskMatrixZ", maskMatrix.GetRow(2));
            mat.SetVector("_MaskMatrixW", maskMatrix.GetRow(3));

            var maskRect = _maskSource.GetPixelAdjustedRect();
            float maskHW = maskRect.width  * 0.5f * maskShape.Scale.x;
            float maskHH = maskRect.height * 0.5f * maskShape.Scale.y;

            float maskKind   = (float)maskShape.Kind;
            float maskSmooth = maskShape is RectangleShape mrs ? mrs.CornerSmoothing : 0f;
            float feather    = Mathf.Max(0.001f, maskShape.EdgeSoftness);

            mat.SetVector("_MaskParams",  new Vector4(1f, maskKind, maskSmooth, feather));
            mat.SetVector("_MaskSize",    new Vector4(maskHW * 2f, maskHH * 2f, 0f, 0f));
            mat.SetVector("_MaskShape",   maskShape.PackShaderParams());
            mat.SetTexture("_MaskTex",    GradientAtlas.Texture != null ? GradientAtlas.Texture : Texture2D.whiteTexture);
            mat.SetFloat("_AtlasHeightInv", GradientAtlas.HeightInverse);

            float opacity  = _maskSource.ShapeOpacity;
            var fills      = _maskSource.Fills;
            var firstFill  = (fills != null && fills.Count > 0) ? fills[0]?.Fill : null;

            Vector4 maskFillParams = Vector4.zero;
            Vector4 maskFillOffset = new Vector4(0f, 0f, opacity, 0f);

            // For gradient fills: Acquire the atlas row (mask source already holds its own ref,
            // so refcount goes 1→2→1 — safe to read the row number without leaking).
            switch (firstFill)
            {
                case SolidFill sf:
                    maskFillOffset = new Vector4(0f, 0f, opacity * sf.Color.a, 0f);
                    break;
                case LinearGradientFill lgf:
                {
                    int row = GradientAtlas.Acquire(lgf);
                    maskFillParams = new Vector4((float)FillKind.LinearGradient, lgf.Angle, lgf.Scale, row);
                    GradientAtlas.Release(row); // mask source holds its own ref; row stays valid
                    break;
                }
                case RadialGradientFill rgf:
                {
                    int row = GradientAtlas.Acquire(rgf);
                    maskFillParams = new Vector4((float)FillKind.RadialGradient, 0f, rgf.Radius, row);
                    GradientAtlas.Release(row);
                    break;
                }
                case ConicGradientFill cgf:
                {
                    int row = GradientAtlas.Acquire(cgf);
                    maskFillParams = new Vector4((float)FillKind.ConicGradient, cgf.StartAngle, 1f, row);
                    GradientAtlas.Release(row);
                    break;
                }
            }

            mat.SetVector("_MaskFillParams", maskFillParams);
            mat.SetVector("_MaskFillOffset", maskFillOffset);
            mat.SetInt("_MaskBoolParams", 0);
        }

        private void ReleaseSoftMaskMat()
        {
            if (_softMaskMat == null) return;
#if UNITY_EDITOR
            if (Application.isPlaying) Object.Destroy(_softMaskMat);
            else                       Object.DestroyImmediate(_softMaskMat);
#else
            Object.Destroy(_softMaskMat);
#endif
            _softMaskMat = null;
        }

        private static void EnsureSoftMaskShader()
        {
            if (s_SoftMaskShader == null)
                s_SoftMaskShader = Shader.Find("VectorKit/SoftMaskedImage");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_target == null)   _target   = GetComponent<Graphic>();
            if (_targetVS == null) _targetVS = _target as VectorShapeUI;
            if (isActiveAndEnabled) Apply();
        }
#endif
    }
}
