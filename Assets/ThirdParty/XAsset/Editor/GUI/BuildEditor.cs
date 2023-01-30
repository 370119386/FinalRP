using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VEngine.Editor.Builds;

namespace VEngine.Editor.GUI
{
    [CustomEditor(typeof(Build))]
    public class BuildEditor : UnityEditor.Editor
    {
        private void CollectAssets()
        {
            if (!(target is Build build))
            {
                return;
            }
            var assets = new BuildTask(build).CollectAssets();
            var sb = new StringBuilder($"//Assets:{assets.Count}\n//Format:Asset = Bundle");
            foreach (var asset in assets)
            {
                sb.AppendFormat("\n{0} = {1}", asset.path, asset.bundle);
            }

            var path = $"collected_assets_for_{build.name}.txt";
            File.WriteAllText(path, sb.ToString());
            EditorUtility.OpenWithDefaultApp(Path.GetDirectoryName(path));
        }

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
                    var selection = DragAndDrop.objectReferences;
                    if (target is Build build)
                    {
                        foreach (var path in selection)
                        {
                            if (build.groups.Exists(group => group.target.Equals(path)))
                            {
                                continue;
                            }

                            build.groups.Add(new Group
                            {
                                name = path.name,
                                bundleMode = build.defaultBundleMode,
                                target = path
                            });
                        }

                        Settings.SaveAsset(build);
                    }

                    Event.current.Use();
                }
            }

            serializedObject.ApplyModifiedProperties();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(32);

                if (GUILayout.Button("Collect Assets"))
                {
                    CollectAssets();
                }

                if (GUILayout.Button("Build Bundles"))
                {
                    if (target is Build build)
                    {
                        RunOnUpdate.RunAction(() =>
                        {
                            new BuildTask(build).BuildBundles();
                            EditorUtility.OpenWithDefaultApp(Settings.PlatformBuildPath);
                        });
                    }
                }

                if (GUILayout.Button("Build Manifest"))
                {
                    var file = EditorUtility.OpenFilePanel("选择清单", Settings.PlatformBuildPath, "");
                    if (string.IsNullOrEmpty(file))
                    {
                        return;
                    }

                    var manifest = CreateInstance<Manifest>();
                    manifest.Load(file);
                    var task = new BuildTask(target.name);
                    task.CreateManifest(manifest.bundles);
                }

                GUILayout.Space(32);
            }
        }
    }
}