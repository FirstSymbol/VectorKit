using System;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Complete snapshot of shader parameters used as a pool key.
    public class VectorShaderState : IEquatable<VectorShaderState>
    {
        public ShapeKind ShapeKind;
        public int       BaseMatId;
        public Texture   AtlasTex;
        public Texture   PatternTex;
        public bool      HasNoise;

        // Path data - upgraded to 256 Vector4s (512 points)
        public int      PathPointCount;
        public Vector4[] PathData; // [256]

        public int      BoolPathPointCount;
        public Vector4[] BoolPathData; // [256]

        // Boolean ops - upgraded to 16
        public int      BoolCount;
        public Vector4[] BoolOpType;      // [16]
        public Vector4[] BoolShapeParams; // [16]
        public Vector4[] BoolTransform;   // [16]
        public Vector4[] BoolSize;        // [16]

        // Soft mask
        public bool      HasMask;
        public Matrix4x4 MaskMatrix;
        public Vector4   MaskParams;
        public Vector4   MaskSize;
        public Vector4   MaskShape;
        public Texture   MaskTex;
        public Vector4   MaskFillParams;
        public Vector4   MaskFillOffset;
        public int       MaskBoolCount;
        public Vector4[] MaskBoolOpType;
        public Vector4[] MaskBoolShapeParams;
        public Vector4[] MaskBoolTransform;
        public Vector4[] MaskBoolSize;

        public void Clear()
        {
            ShapeKind = ShapeKind.Rectangle;
            BaseMatId = 0; AtlasTex = null; PatternTex = null;
            HasNoise = false; PathPointCount = 0; BoolPathPointCount = 0;
            BoolCount = 0; HasMask = false; MaskBoolCount = 0;
        }

        private void EnsureArrays()
        {
            if (PathData == null)     PathData     = new Vector4[256];
            if (BoolPathData == null) BoolPathData = new Vector4[256];
            if (BoolOpType == null)
            {
                BoolOpType = new Vector4[16]; BoolShapeParams = new Vector4[16];
                BoolTransform = new Vector4[16]; BoolSize = new Vector4[16];
            }
            if (MaskBoolOpType == null)
            {
                MaskBoolOpType = new Vector4[16]; MaskBoolShapeParams = new Vector4[16];
                MaskBoolTransform = new Vector4[16]; MaskBoolSize = new Vector4[16];
            }
        }

        public VectorShaderState Clone()
        {
            var c = new VectorShaderState();
            c.ShapeKind = ShapeKind; c.BaseMatId = BaseMatId;
            c.AtlasTex = AtlasTex; c.PatternTex = PatternTex; c.HasNoise = HasNoise;

            c.PathPointCount = PathPointCount;
            if (PathPointCount > 0) { c.PathData = new Vector4[256]; Array.Copy(PathData, c.PathData, 256); }

            c.BoolPathPointCount = BoolPathPointCount;
            if (BoolPathPointCount > 0) { c.BoolPathData = new Vector4[256]; Array.Copy(BoolPathData, c.BoolPathData, 256); }

            c.BoolCount = BoolCount;
            if (BoolCount > 0)
            {
                c.BoolOpType      = new Vector4[16]; c.BoolShapeParams = new Vector4[16];
                c.BoolTransform   = new Vector4[16]; c.BoolSize        = new Vector4[16];
                Array.Copy(BoolOpType,      c.BoolOpType,      16);
                Array.Copy(BoolShapeParams, c.BoolShapeParams, 16);
                Array.Copy(BoolTransform,   c.BoolTransform,   16);
                Array.Copy(BoolSize,        c.BoolSize,        16);
            }

            c.HasMask = HasMask;
            if (HasMask)
            {
                c.MaskMatrix = MaskMatrix; c.MaskParams = MaskParams; c.MaskSize = MaskSize;
                c.MaskShape = MaskShape; c.MaskTex = MaskTex; c.MaskFillParams = MaskFillParams;
                c.MaskFillOffset = MaskFillOffset; c.MaskBoolCount = MaskBoolCount;
                if (MaskBoolCount > 0)
                {
                    c.MaskBoolOpType      = new Vector4[16]; c.MaskBoolShapeParams = new Vector4[16];
                    c.MaskBoolTransform   = new Vector4[16]; c.MaskBoolSize        = new Vector4[16];
                    Array.Copy(MaskBoolOpType,      c.MaskBoolOpType,      16);
                    Array.Copy(MaskBoolShapeParams, c.MaskBoolShapeParams, 16);
                    Array.Copy(MaskBoolTransform,   c.MaskBoolTransform,   16);
                    Array.Copy(MaskBoolSize,        c.MaskBoolSize,        16);
                }
            }
            return c;
        }

        public void ApplyToMaterial(Material mat)
        {
            // Shape type keywords
            string[] shapeKeywords = { "SHAPE_RECTANGLE","SHAPE_ELLIPSE","SHAPE_POLYGON","SHAPE_STAR","SHAPE_LINE","SHAPE_ARC","SHAPE_PATH","_","SHAPE_CAPSULE","SHAPE_TRIANGLE","SHAPE_HEART" };
            foreach (var kw in shapeKeywords) mat.DisableKeyword(kw);
            switch (ShapeKind)
            {
                case ShapeKind.Rectangle: mat.EnableKeyword("SHAPE_RECTANGLE"); break;
                case ShapeKind.Ellipse:   mat.EnableKeyword("SHAPE_ELLIPSE");   break;
                case ShapeKind.Polygon:   mat.EnableKeyword("SHAPE_POLYGON");   break;
                case ShapeKind.Star:      mat.EnableKeyword("SHAPE_STAR");      break;
                case ShapeKind.Line:      mat.EnableKeyword("SHAPE_LINE");      break;
                case ShapeKind.Arc:       mat.EnableKeyword("SHAPE_ARC");       break;
                case ShapeKind.Path:      mat.EnableKeyword("SHAPE_PATH");      break;
                case ShapeKind.Capsule:   mat.EnableKeyword("SHAPE_CAPSULE");   break;
                case ShapeKind.Triangle:  mat.EnableKeyword("SHAPE_TRIANGLE");  break;
                case ShapeKind.Heart:     mat.EnableKeyword("SHAPE_HEART");     break;
            }

            if (BoolCount > 0) mat.EnableKeyword("HAS_BOOLEANS"); else mat.DisableKeyword("HAS_BOOLEANS");
            if (HasMask)       mat.EnableKeyword("HAS_MASK");      else mat.DisableKeyword("HAS_MASK");
            if (HasNoise)      mat.EnableKeyword("HAS_NOISE");     else mat.DisableKeyword("HAS_NOISE");

            if (AtlasTex)   mat.SetTexture(ShaderPropertyIDs.MainTex,    AtlasTex);
            if (PatternTex) mat.SetTexture(ShaderPropertyIDs.PatternTex, PatternTex);

            mat.SetFloat(ShaderPropertyIDs.AtlasHeightInv, GradientAtlas.HeightInverse);

            if (PathPointCount > 0) { EnsureArrays(); mat.SetVectorArray(ShaderPropertyIDs.PathData, PathData); mat.SetInt(ShaderPropertyIDs.PathPointCount, PathPointCount); }
            if (BoolPathPointCount > 0) { EnsureArrays(); mat.SetVectorArray(ShaderPropertyIDs.BoolPathData, BoolPathData); mat.SetInt(ShaderPropertyIDs.BoolPathPointCount, BoolPathPointCount); }

            mat.SetInt(ShaderPropertyIDs.BoolParams1, BoolCount);
            if (BoolCount > 0)
            {
                EnsureArrays();
                mat.SetVectorArray(ShaderPropertyIDs.BoolData_OpType,      BoolOpType);
                mat.SetVectorArray(ShaderPropertyIDs.BoolData_ShapeParams,  BoolShapeParams);
                mat.SetVectorArray(ShaderPropertyIDs.BoolData_Transform,    BoolTransform);
                mat.SetVectorArray(ShaderPropertyIDs.BoolData_Size,         BoolSize);
            }

            if (HasMask)
            {
                mat.SetVector(ShaderPropertyIDs.MaskMatrixX, MaskMatrix.GetRow(0));
                mat.SetVector(ShaderPropertyIDs.MaskMatrixY, MaskMatrix.GetRow(1));
                mat.SetVector(ShaderPropertyIDs.MaskMatrixZ, MaskMatrix.GetRow(2));
                mat.SetVector(ShaderPropertyIDs.MaskMatrixW, MaskMatrix.GetRow(3));
                mat.SetVector(ShaderPropertyIDs.MaskParams,     MaskParams);
                mat.SetVector(ShaderPropertyIDs.MaskSize,       MaskSize);
                mat.SetVector(ShaderPropertyIDs.MaskShape,      MaskShape);
                mat.SetTexture(ShaderPropertyIDs.MaskTex, MaskTex ? MaskTex : Texture2D.whiteTexture);
                mat.SetVector(ShaderPropertyIDs.MaskFillParams, MaskFillParams);
                mat.SetVector(ShaderPropertyIDs.MaskFillOffset, MaskFillOffset);
                mat.SetInt(ShaderPropertyIDs.MaskBoolParams, MaskBoolCount);
                if (MaskBoolCount > 0)
                {
                    EnsureArrays();
                    mat.SetVectorArray(ShaderPropertyIDs.MaskBoolOpType,       MaskBoolOpType);
                    mat.SetVectorArray(ShaderPropertyIDs.MaskBoolShapeParams,   MaskBoolShapeParams);
                    mat.SetVectorArray(ShaderPropertyIDs.MaskBoolTransform,     MaskBoolTransform);
                    mat.SetVectorArray(ShaderPropertyIDs.MaskBoolSize,          MaskBoolSize);
                }
            }
            else
            {
                mat.SetVector(ShaderPropertyIDs.MaskParams, Vector4.zero);
                mat.SetInt(ShaderPropertyIDs.MaskBoolParams, 0);
            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 1000003 ^ (int)ShapeKind;
                h = h * 1000003 ^ BaseMatId;
                h = h * 1000003 ^ (AtlasTex ? AtlasTex.GetInstanceID() : 0);
                h = h * 1000003 ^ BoolCount;
                h = h * 1000003 ^ PathPointCount;
                h = h * 1000003 ^ (HasMask ? 1 : 0);
                h = h * 1000003 ^ (HasNoise ? 1 : 0);
                if (BoolCount > 0 && BoolTransform != null) h = h * 1000003 ^ BoolTransform[0].GetHashCode();
                if (HasMask) h = h * 1000003 ^ MaskMatrix.GetHashCode();
                return h;
            }
        }

        public bool Equals(VectorShaderState other)
        {
            if (other == null) return false;
            if (ShapeKind != other.ShapeKind || BaseMatId != other.BaseMatId ||
                AtlasTex != other.AtlasTex || PatternTex != other.PatternTex ||
                HasMask != other.HasMask || HasNoise != other.HasNoise) return false;

            if (PathPointCount != other.PathPointCount) return false;
            if (PathPointCount > 0) { for (int i = 0; i < (PathPointCount+1)/2; i++) if (PathData[i] != other.PathData[i]) return false; }

            if (BoolPathPointCount != other.BoolPathPointCount) return false;
            if (BoolPathPointCount > 0) { for (int i = 0; i < (BoolPathPointCount+1)/2; i++) if (BoolPathData[i] != other.BoolPathData[i]) return false; }

            if (BoolCount != other.BoolCount) return false;
            for (int i = 0; i < BoolCount; i++)
                if (BoolOpType[i] != other.BoolOpType[i] || BoolShapeParams[i] != other.BoolShapeParams[i] ||
                    BoolTransform[i] != other.BoolTransform[i] || BoolSize[i] != other.BoolSize[i]) return false;

            if (HasMask)
            {
                if (MaskMatrix != other.MaskMatrix || MaskParams != other.MaskParams || MaskSize != other.MaskSize ||
                    MaskShape != other.MaskShape || MaskTex != other.MaskTex ||
                    MaskFillParams != other.MaskFillParams || MaskFillOffset != other.MaskFillOffset) return false;
                if (MaskBoolCount != other.MaskBoolCount) return false;
                for (int i = 0; i < MaskBoolCount; i++)
                    if (MaskBoolOpType[i] != other.MaskBoolOpType[i] || MaskBoolShapeParams[i] != other.MaskBoolShapeParams[i] ||
                        MaskBoolTransform[i] != other.MaskBoolTransform[i] || MaskBoolSize[i] != other.MaskBoolSize[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is VectorShaderState other && Equals(other);
    }
}
