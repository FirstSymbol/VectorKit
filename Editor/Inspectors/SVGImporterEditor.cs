#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [CustomEditor(typeof(SVGImporter))]
    public class SVGImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            var importer = (SVGImporter)target;

            EditorGUI.BeginChangeCheck();

            importer.PixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", importer.PixelsPerUnit);
            importer.UseUICanvas   = EditorGUILayout.Toggle("Use UI Canvas",       importer.UseUICanvas);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Re-import"))
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(importer));

            ApplyRevertGUI();
        }
    }
}
#endif
