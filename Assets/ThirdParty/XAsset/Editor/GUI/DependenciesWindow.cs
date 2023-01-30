using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VEngine.Editor.Builds;

namespace VEngine.Editor.GUI
{
    public class DependenciesWindow : EditorWindow
    {
        private const int k_SearchHeight = 20;
        [SerializeField] private MultiColumnHeaderState multiColumnHeaderState;
        [SerializeField] private TreeViewState treeViewState;
        public bool reloadSelectedNow;

        [SerializeField] private TreeViewState dependenciesTreeViewState;

        private readonly GUIContent manifestModule = new GUIContent("模块");
        private readonly List<Manifest> manifests = new List<Manifest>();
        private readonly GUIContent typeModule = new GUIContent("模块");

        private readonly List<string> types = new List<string>();
        private readonly Dictionary<string, List<string>> typeWithAssets = new Dictionary<string, List<string>>();
        private DependenciesTreeView dependenciesTreeView;

        private bool reloadAssets;
        private bool reloadNow = true;

        private int selected;

        private AssetTreeView treeView;
        private VerticalSplitter verticalSplitter;
        private int selectedType { get; set; }

        private void OnEnable()
        {
            reloadNow = true;
        }

        private void OnGUI()
        {
            if (reloadNow)
            {
                Reload();
                reloadNow = false;
                reloadSelectedNow = true;
            }

            if (reloadSelectedNow)
            {
                var manifest = manifests[selected];
                typeWithAssets.Clear();
                types.Clear();
                var all = new List<string>();
                typeWithAssets.Add("All", all);
                types.Add("All");
                foreach (var bundle in manifest.bundles)
                {
                    foreach (var asset in bundle.assets)
                    {
                        if (!File.Exists(asset))
                        {
                            Debug.LogErrorFormat("文件不存在：{0}", asset);
                            continue;
                        }
                        var type = AssetDatabase.GetMainAssetTypeAtPath(asset);
                        if (!typeWithAssets.TryGetValue(type.Name, out var assets))
                        {
                            assets = new List<string>();
                            typeWithAssets.Add(type.Name, assets);
                            types.Add(type.Name);
                        }
                        assets.Add(asset);
                        all.Add(asset);
                    }
                }

                reloadSelectedNow = false;
                reloadAssets = true;
            }

            if (reloadAssets)
            {
                if (treeView != null)
                {
                    treeView.SetAssets(this);
                    reloadAssets = false;
                }
            }

            if (manifests.Count == 0)
            {
                GUILayout.Label("没有加载到当前平台的打包数据，请在打包后再打开此界面");
                return;
            }

            if (verticalSplitter == null)
            {
                verticalSplitter = new VerticalSplitter();
            }

            var rect = new Rect(0, 0, position.width, position.height);
            DrawTree(rect);
            DrawToolbar(new Rect(rect.xMin, rect.yMin, rect.width, k_SearchHeight));
        }

        [MenuItem("Versions/Tools/Dependencies", false, 2)]
        private static void DoIt()
        {
            GetWindow<DependenciesWindow>(false, "Dependencies");
        }

        private void Reload()
        {
            var builds = Build.GetAllBuilds();
            foreach (var build in builds)
            {
                var manifest = Build.GetManifest(build.name);
                if (manifest == null)
                {
                    continue;
                }

                manifests.Add(manifest);
            }
        }


        private void DrawManifest()
        {
            manifestModule.text = manifests[selected].name;
            var rect = GUILayoutUtility.GetRect(manifestModule, EditorStyles.toolbarDropDown);
            if (!EditorGUI.DropdownButton(rect, manifestModule, FocusType.Keyboard,
                EditorStyles.toolbarDropDown))
            {
                return;
            }

            var menu = new GenericMenu();
            for (var index = 0; index < manifests.Count; index++)
            {
                var manifest = manifests[index];
                menu.AddItem(new GUIContent(manifest.name), selected == index,
                    data =>
                    {
                        selected = (int)data;
                        reloadSelectedNow = true;
                    }, index);
            }

            menu.DropDown(rect);
        }

        private void DrawTypes()
        {
            typeModule.text = types[selectedType];
            var rect = GUILayoutUtility.GetRect(typeModule, EditorStyles.toolbarDropDown);
            if (!EditorGUI.DropdownButton(rect, typeModule, FocusType.Keyboard,
                EditorStyles.toolbarDropDown))
            {
                return;
            }
            var menu = new GenericMenu();
            for (var index = 0; index < types.Count; index++)
            {
                var type = types[index];
                menu.AddItem(new GUIContent(type), selectedType == index,
                    data =>
                    {
                        selectedType = (int)data;
                        reloadAssets = true;
                    }, index);
            }
            menu.DropDown(rect);
        }

        public List<string> GetSelectedAssets()
        {
            return selectedType < types.Count ? typeWithAssets[types[selectedType]] : new List<string>();
        }

        public Manifest GetSelectedManifest()
        {
            return manifests.Count > selected ? manifests[selected] : null;
        }

        private void DrawToolbar(Rect toolbarPos)
        {
            GUILayout.BeginArea(new Rect(0, 0, toolbarPos.width, k_SearchHeight * 2));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                DrawManifest();
                DrawTypes();
                GUILayout.Space(4);
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(128)))
                {
                    SaveSelected();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SaveSelected()
        {
            var manifest = GetSelectedManifest();
            var path = EditorUtility.SaveFilePanel("Save File", "", $"DependenciesWith{manifest.name}For{types[selectedType]}s",
                "txt");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ShowNotification(new GUIContent("Save Success!"));
            var assets = GetSelectedAssets();
            assets.Sort((a, b) => GetBundlesSize(b, out _).CompareTo(GetBundlesSize(a, out _)));
            var sb = new StringBuilder();
            foreach (var asset in assets)
            {
                var size = GetBundlesSize(asset, out var bundles);
                sb.AppendLine($"{asset}({EditorUtility.FormatBytes(size)})");
                sb.AppendLine(" - Bundles:" + EditorUtility.FormatBytes(size));
                bundles.Sort((a, b) => b.size.CompareTo(a.size));
                foreach (var bundle in bundles)
                {
                    sb.AppendLine($"  - {bundle.nameWithAppendHash}({EditorUtility.FormatBytes(bundle.size)})");
                }

                sb.AppendLine(" - Dependencies:");
                var dependencies = new List<string>();
                foreach (var dependency in AssetDatabase.GetDependencies(asset))
                {
                    if (asset == dependency || !manifest.Contains(dependency))
                    {
                        continue;
                    }
                    dependencies.Add(dependency);
                }
                dependencies.Sort((a, b) => GetBundlesSize(b, out _).CompareTo(GetBundlesSize(a, out _)));
                foreach (var dependency in dependencies)
                {
                    sb.AppendLine($"  - {dependency}({EditorUtility.FormatBytes(GetBundlesSize(dependency, out _))})");
                }
            }

            File.WriteAllText(path, sb.ToString());
            EditorUtility.OpenWithDefaultApp(path);
        }

        public void ReloadDependencies(string assetPath)
        {
            if (dependenciesTreeView == null)
            {
                return;
            }

            dependenciesTreeView.assetPath = assetPath;
            dependenciesTreeView.Reload();
            dependenciesTreeView.ExpandAll();
        }

        public long GetBundlesSize(string asset, out List<ManifestBundle> bundles)
        {
            var manifest = GetSelectedManifest();
            bundles = new List<ManifestBundle>();
            var bundlesSize = 0L;
            var bundle = manifest.GetBundle(asset);
            bundlesSize += bundle.size;
            bundles.Add(bundle);
            var dependencies = manifest.GetDependencies(bundle);
            if (dependencies == null)
            {
                return bundlesSize;
            }
            foreach (var dependency in dependencies)
            {
                bundlesSize += dependency.size;
                bundles.Add(dependency);
            }

            return bundlesSize;
        }

        private void DrawTree(Rect rect)
        {
            const int toolbarHeight = k_SearchHeight + 4;
            var treeRect = new Rect(
                rect.xMin,
                rect.yMin + toolbarHeight,
                rect.width,
                verticalSplitter.rect.y - toolbarHeight);

            if (treeView == null)
            {
                if (treeViewState == null)
                {
                    treeViewState = new TreeViewState();
                }

                var headerState =
                    AssetTreeView.CreateDefaultMultiColumnHeaderState(); // multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(multiColumnHeaderState, headerState))
                {
                    MultiColumnHeaderState.OverwriteSerializedFields(multiColumnHeaderState, headerState);
                }

                multiColumnHeaderState = headerState;
                treeView = new AssetTreeView(treeViewState, multiColumnHeaderState);
                treeView.Reload();
            }

            if (verticalSplitter == null)
            {
                verticalSplitter = new VerticalSplitter
                {
                    percent = 0.8f
                };
            }

            treeView.OnGUI(treeRect);
            verticalSplitter.OnGUI(rect);
            if (verticalSplitter.resizing)
            {
                Repaint();
            }

            if (dependenciesTreeViewState == null)
            {
                dependenciesTreeViewState = new TreeViewState();
            }

            if (dependenciesTreeView == null)
            {
                dependenciesTreeView = new DependenciesTreeView(dependenciesTreeViewState, this);
            }

            dependenciesTreeView.OnGUI(new Rect(treeRect.x, verticalSplitter.rect.y + 4, treeRect.width,
                rect.height - treeRect.yMax - 4));
        }
    }
}