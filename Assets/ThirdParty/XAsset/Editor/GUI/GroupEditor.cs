using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VEngine.Editor.Builds;

namespace VEngine.Editor.GUI
{
    [CustomEditor(typeof(AssetGroup))]
    public class GroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();
            var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            UnityEngine.GUI.Box(rect, "Drag and Drop selection to this box!");
            if (rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    var paths = DragAndDrop.paths;
                    if (target is AssetGroup bundledAsset)
                    {
                        var set = new HashSet<string>(bundledAsset.assets);
                        set.UnionWith(paths);
                        bundledAsset.assets = set.ToArray();
                        Settings.SaveAsset(bundledAsset);
                    }

                    Event.current.Use();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}