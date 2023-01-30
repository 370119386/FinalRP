using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VEngine.Editor.GUI
{
    public class LoadablesWindow : EditorWindow
    {
        [SerializeField] private MultiColumnHeaderState m_MultiColumnHeaderState;
        [SerializeField] private TreeViewState m_TreeViewState;
        private readonly Dictionary<int, List<Loadable>> frameWithLoadables = new Dictionary<int, List<Loadable>>();

        private int current;

        private int frame;

        private List<Loadable> loadables = new List<Loadable>();

        private LoadableTreeView m_TreeView;

        private bool recording = true;

        private void Update()
        {
            if (recording && Application.isPlaying)
            {
                TakeASample();
            }
        }


        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                recording = GUILayout.Toggle(recording, "Record", EditorStyles.toolbarButton);
                if (GUILayout.Button("Sample", EditorStyles.toolbarButton))
                {
                    TakeASample();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Frame:", $"{(current == frame ? "Current" : frame.ToString())}",
                    EditorStyles.miniLabel);
                if (GUILayout.Button("<", EditorStyles.toolbarButton))
                {
                    frame = Mathf.Max(0, frame - 1);
                    ReloadFrameData();
                    recording = false;
                }

                if (GUILayout.Button("Current", EditorStyles.toolbarButton))
                {
                    TakeASample();
                    recording = false;
                }

                if (GUILayout.Button(">", EditorStyles.toolbarButton))
                {
                    frame = Mathf.Min(frame + 1, Time.frameCount);
                    ReloadFrameData();
                    recording = false;
                }

                EditorGUI.BeginChangeCheck();
                frame = EditorGUILayout.IntSlider(frame, 0, current);
                if (EditorGUI.EndChangeCheck())
                {
                    recording = false;
                    ReloadFrameData();
                }
            }

            if (m_TreeView == null)
            {
                m_TreeViewState = new TreeViewState();
                var headerState =
                    LoadableTreeView.CreateDefaultMultiColumnHeaderState(); // multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
                {
                    MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
                }

                m_MultiColumnHeaderState = headerState;
                m_TreeView = new LoadableTreeView(m_TreeViewState, headerState);
                m_TreeView.SetAssets(loadables);
            }

            var treeRect = GUILayoutUtility.GetLastRect();
            m_TreeView.OnGUI(new Rect(0, treeRect.yMax, position.width, position.height - treeRect.yMax));
        }

        [MenuItem("Versions/Tools/Loadables", false, 2)]
        private static void DoIt()
        {
            GetWindow<LoadablesWindow>(false, "Loadables");
        }

        private void ReloadFrameData()
        {
            if (m_TreeView != null && frameWithLoadables.TryGetValue(frame, out var value))
            {
                m_TreeView.SetAssets(value);
            }
        }

        private void TakeASample()
        {
            current = frame = Time.frameCount;
            loadables = new List<Loadable>();

            foreach (var item in Asset.Cache.Values)
            {
                if (item.isDone)
                {
                    loadables.Add(item);
                }
            }

            foreach (var item in Bundle.Cache.Values)
            {
                if (item.isDone)
                {
                    loadables.Add(item);
                }
            }

            foreach (var item in RawAsset.Cache.Values)
            {
                if (item.isDone)
                {
                    loadables.Add(item);
                }
            }

            if (Scene.current != null && Scene.current.isDone)
            {
                loadables.Add(Scene.current);
                loadables.AddRange(Scene.current.additives);
            }

            frameWithLoadables[frame] = loadables;
            ReloadFrameData();
        }
    }
}