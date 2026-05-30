using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // World-space vector shape rendered via MeshFilter + MeshRenderer.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("VectorKit/Vector Shape (World)")]
    [DisallowMultipleComponent]
    public class VectorShapeWorld : MonoBehaviour
    {
        [SerializeReference]
        public ShapeDefinition Shape = new RectangleShape();

        [SerializeField]
        public List<FillLayer> Fills = new List<FillLayer> { new FillLayer() };

        [SerializeField]
        public List<StrokeLayer> Strokes = new List<StrokeLayer>();

        [SerializeReference]
        public List<VectorEffect> Effects = new List<VectorEffect>();

        public Vector2 Size = new Vector2(100f, 100f);
        public Color   Tint = Color.white;

        private MeshFilter   _filter;
        private MeshRenderer _renderer;
        private Mesh         _mesh;

        private readonly VectorShaderState _state     = new VectorShaderState();
        private readonly List<int>         _atlasRows = new List<int>();
        private Material _pooledMat;

        private static Material s_DefaultMat;

        private static Material DefaultMaterial
        {
            get
            {
                if (s_DefaultMat == null)
                {
                    var shader = Shader.Find("VectorKit/ShapeWorld");
                    if (shader != null)
                        s_DefaultMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return s_DefaultMat;
            }
        }

        private void Awake()
        {
            _filter   = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _mesh     = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _filter.sharedMesh = _mesh;
        }

        private void OnEnable()  => Rebuild();
        private void OnDisable() => ReleaseMaterial();

        private void OnDestroy()
        {
            ReleaseAtlasRows();
            ReleaseMaterial();
            if (_mesh) Destroy(_mesh);
        }

        public void Rebuild()
        {
            if (_mesh == null) return;
            ReleaseAtlasRows();

            if (Shape is PathShape tessPs && PathFlattener.HasSubPaths(tessPs))
            {
                // Bypass VertexHelper to avoid its 65 000-vertex ceiling.
                SDFMeshBuilder.PopulateLargeMesh(_mesh, Size, Tint, tessPs, Fills);
                _state.Clear();
                _state.ShapeKind = ShapeKind.Rectangle;
                _state.AtlasTex  = GradientAtlas.Texture;
            }
            else
            {
                SDFMeshBuilder.RebuildMesh(
                    _mesh, Size, Tint,
                    Shape, Fills, Strokes, Effects,
                    _state, _atlasRows);
            }

            ReleaseMaterial();
            var baseMat = DefaultMaterial;
            if (baseMat != null)
            {
                _state.BaseMatId = baseMat.GetInstanceID();
                _pooledMat       = VectorMaterialManager.GetMaterial(_state, baseMat);
                _renderer.sharedMaterial = _pooledMat;
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => Rebuild();
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
