using UnityEngine;

namespace VectorKit.Runtime
{
    internal static class ShaderPropertyIDs
    {
        public static readonly int MainTex           = Shader.PropertyToID("_MainTex");
        public static readonly int PatternTex        = Shader.PropertyToID("_PatternTex");
        public static readonly int Color             = Shader.PropertyToID("_Color");
        public static readonly int AtlasHeightInv    = Shader.PropertyToID("_AtlasHeightInv");
        public static readonly int InternalPadding   = Shader.PropertyToID("_InternalPadding");

        public static readonly int BoolParams1           = Shader.PropertyToID("_BoolParams1");
        public static readonly int BoolData_OpType       = Shader.PropertyToID("_BoolData_OpType");
        public static readonly int BoolData_ShapeParams  = Shader.PropertyToID("_BoolData_ShapeParams");
        public static readonly int BoolData_Transform    = Shader.PropertyToID("_BoolData_Transform");
        public static readonly int BoolData_Size         = Shader.PropertyToID("_BoolData_Size");

        public static readonly int PathData         = Shader.PropertyToID("_PathData");
        public static readonly int PathPointCount   = Shader.PropertyToID("_PathPointCount");
        public static readonly int BoolPathData     = Shader.PropertyToID("_BoolPathData");
        public static readonly int BoolPathPointCount = Shader.PropertyToID("_BoolPathPointCount");

        public static readonly int MaskMatrixX     = Shader.PropertyToID("_MaskMatrixX");
        public static readonly int MaskMatrixY     = Shader.PropertyToID("_MaskMatrixY");
        public static readonly int MaskMatrixZ     = Shader.PropertyToID("_MaskMatrixZ");
        public static readonly int MaskMatrixW     = Shader.PropertyToID("_MaskMatrixW");
        public static readonly int MaskParams      = Shader.PropertyToID("_MaskParams");
        public static readonly int MaskSize        = Shader.PropertyToID("_MaskSize");
        public static readonly int MaskShape       = Shader.PropertyToID("_MaskShape");
        public static readonly int MaskTex         = Shader.PropertyToID("_MaskTex");
        public static readonly int MaskFillParams  = Shader.PropertyToID("_MaskFillParams");
        public static readonly int MaskFillOffset  = Shader.PropertyToID("_MaskFillOffset");
        public static readonly int MaskBoolParams  = Shader.PropertyToID("_MaskBoolParams");
        public static readonly int MaskBoolOpType  = Shader.PropertyToID("_MaskBoolOpType");
        public static readonly int MaskBoolShapeParams = Shader.PropertyToID("_MaskBoolShapeParams");
        public static readonly int MaskBoolTransform   = Shader.PropertyToID("_MaskBoolTransform");
        public static readonly int MaskBoolSize        = Shader.PropertyToID("_MaskBoolSize");

        public static readonly int BlendMode       = Shader.PropertyToID("_BlendMode");
        public static readonly int SrcTex          = Shader.PropertyToID("_SrcTex");
    }
}
