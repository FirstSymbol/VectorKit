using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    // Clipping frame (like a Figma frame) — MaskableGraphic + Mask.
    // Clips all child UI elements to the shape boundary.
    // Add a Mask component to the same GameObject to activate clipping.
    [AddComponentMenu("VectorKit/Vector Frame")]
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public class VectorFrame : MaskableGraphic
    {
        [SerializeReference]
        public ShapeDefinition Shape = new RectangleShape();

        [SerializeField]
        public FillLayer Background = new FillLayer { Fill = new SolidFill { Color = Color.white } };

        private readonly VectorShaderState _state     = new VectorShaderState();
        private readonly System.Collections.Generic.List<int> _atlasRows = new();
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            ReleaseAtlasRows();

            var rect  = GetPixelAdjustedRect();
            var fills = Background is { Enabled: true, Fill: not null }
                        ? new System.Collections.Generic.List<FillLayer> { Background }
                        : null;

            SDFMeshBuilder.Populate(
                vh, rect.size, color,
                Shape, fills, null, null,
                _state, _atlasRows);

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
        }

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
