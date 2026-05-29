using UnityEngine;

namespace VectorKit.Runtime
{
    // Applies a VectorShapeUI as a soft (alpha) mask to the VectorShapeUI on the same GameObject.
    // Drag the mask shape into MaskSource. The mask uses its shape SDF and fill alpha to
    // modulate the target's alpha channel — works at any rotation/scale.
    [ExecuteAlways]
    [AddComponentMenu("VectorKit/Vector Soft Maskable")]
    [RequireComponent(typeof(VectorShapeUI))]
    [DisallowMultipleComponent]
    public class VectorSoftMaskable : MonoBehaviour
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

        private VectorShapeUI _target;

        private void OnEnable()  => Apply();
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();

        private void Apply()
        {
            _target = GetComponent<VectorShapeUI>();
            if (_target == null || _maskSource == null) return;
            _target.SoftMaskSource = _maskSource;
            UpdateMatrix();
            _target.SetVerticesDirty();
            _target.SetMaterialDirty();
        }

        private void Clear()
        {
            if (_target == null) return;
            _target.SoftMaskSource = null;
            _target.SetVerticesDirty();
            _target.SetMaterialDirty();
        }

        // Call when either transform moves, to refresh the coordinate mapping.
        public void UpdateMatrix()
        {
            if (_target == null || _maskSource == null) return;
            // Transforms from this element's local space into the mask's local space.
            _target.MaskWorldToLocal = _maskSource.transform.worldToLocalMatrix
                                       * _target.transform.localToWorldMatrix;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_target == null) _target = GetComponent<VectorShapeUI>();
            if (isActiveAndEnabled) Apply();
        }
#endif
    }
}
