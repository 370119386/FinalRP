using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace VEngine.Editor.GUI
{
    internal class DependenciesTreeView : TreeView
    {
        private readonly DependenciesWindow _window;

        public string assetPath;

        public DependenciesTreeView(TreeViewState treeViewState, DependenciesWindow dependenciesWindow)
            : base(treeViewState)
        {
            _window = dependenciesWindow;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            var dependenciesItem = new TreeViewItem("Dependencies".GetHashCode(), root.depth + 1, "Dependencies");
            var manifest = _window.GetSelectedManifest();
            foreach (var dependency in AssetDatabase.GetDependencies(assetPath))
            {
                if (dependency == assetPath || !manifest.Contains(dependency))
                {
                    continue;
                }
                dependenciesItem.AddChild(new TreeViewItem(dependency.GetHashCode(), dependenciesItem.depth + 1, dependency));
            }
            root.AddChild(dependenciesItem);
            return root;
        }
    }
}