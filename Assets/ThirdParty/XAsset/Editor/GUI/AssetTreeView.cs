using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VEngine.Editor.GUI
{
    public sealed class AssetTreeViewItem : TreeViewItem
    {
        public readonly string data;
        public long dependenciesSize;
        public long size;

        public AssetTreeViewItem(string loadable, int depth) : base(loadable.GetHashCode(), depth)
        {
            if (loadable.StartsWith("Assets/"))
            {
                displayName = loadable;
                icon = AssetDatabase.GetCachedIcon(displayName) as Texture2D;
            }
            else
            {
                displayName = Path.GetFileName(loadable);
            }

            data = loadable;
        }
    }

    public class AssetTreeView : TreeView
    {
        private readonly List<string> assets = new List<string>();
        private readonly SortOption[] m_SortOptions = { SortOption.Asset, SortOption.Size, SortOption.DependenciesSize };

        private readonly List<TreeViewItem> result = new List<TreeViewItem>();
        private DependenciesWindow _window;

        internal AssetTreeView(TreeViewState state, MultiColumnHeaderState headerState) : base(state,
            new MultiColumnHeader(headerState))
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.ResizeToFit();
        }

        internal static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(GetColumns());
        }

        private static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Path"),
                    minWidth = 320,
                    width = 480,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Asset Size"),
                    minWidth = 64,
                    width = 96,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Dependencies Size"),
                    minWidth = 128,
                    width = 160,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = true
                }
            };
            return retVal;
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                SetSelection(Array.Empty<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            }
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = (List<TreeViewItem>)base.BuildRows(root);
            if (!string.IsNullOrEmpty(searchString))
            {
                result.Clear();
                var stack = new Stack<TreeViewItem>();
                foreach (var element in root.children)
                {
                    stack.Push(element);
                }

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    // Matches search?
                    if (current.displayName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(current as AssetTreeViewItem);
                    }

                    if (current.children != null && current.children.Count > 0)
                    {
                        foreach (var element in current.children)
                        {
                            stack.Push(element);
                        }
                    }
                }

                rows = result;
            }

            SortIfNeeded(root, rows);
            return rows;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            foreach (var asset in assets)
            {
                var item = new AssetTreeViewItem(asset, 0)
                {
                    size = GetAssetSize(asset)
                };
                root.AddChild(item);
                item.dependenciesSize = _window.GetBundlesSize(asset, out var bundles);
                foreach (var bundle in bundles)
                {
                    item.AddChild(new AssetTreeViewItem(bundle.nameWithAppendHash, item.depth + 1) { size = bundle.size, dependenciesSize = item.dependenciesSize });
                }
            }

            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (var i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var item = (AssetTreeViewItem)args.item;
                if (item?.data == null)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        base.RowGUI(args);
                    }
                }
                else
                {
                    CellGUI(args.GetCellRect(i), (AssetTreeViewItem)args.item, args.GetColumn(i), ref args);
                }
            }
        }

        private static long GetAssetSize(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? file.Length : 0;
        }

        private void CellGUI(Rect cellRect, AssetTreeViewItem item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0:
                    if (item.depth == 0)
                    {
                        cellRect.xMin += GetContentIndent(item) + extraSpaceBeforeIconAndLabel;
                    }

                    var iconRect = new Rect(cellRect.x + 1, cellRect.y + 1, cellRect.height - 2, cellRect.height - 2);
                    if (item.icon != null)
                    {
                        UnityEngine.GUI.DrawTexture(iconRect, item.icon, ScaleMode.ScaleToFit);
                    }
                    var content = item.displayName;
                    DefaultGUI.Label(
                        new Rect(cellRect.x + iconRect.xMax + 1, cellRect.y, cellRect.width - iconRect.width,
                            cellRect.height),
                        content,
                        args.selected,
                        args.focused);
                    break;
                case 1:
                    DefaultGUI.Label(cellRect, EditorUtility.FormatBytes(item.size), args.selected,
                        args.focused);
                    break;
                case 2:
                    DefaultGUI.Label(cellRect, EditorUtility.FormatBytes(item.dependenciesSize), args.selected,
                        args.focused);
                    break;
            }
        }

        protected override void SingleClickedItem(int id)
        {
            base.ContextClickedItem(id);
            var assetItem = (AssetTreeViewItem)FindItem(id, rootItem);
            if (assetItem != null)
            {
                _window.ReloadDependencies(assetItem.data);
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var assetItem = FindItem(id, rootItem);
            if (assetItem != null)
            {
                var o = AssetDatabase.LoadAssetAtPath<Object>(assetItem.displayName);
                EditorGUIUtility.PingObject(o);
                Selection.activeObject = o;
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds == null)
            {
                return;
            }

            var selectedObjects = new List<Object>();
            foreach (var id in selectedIds)
            {
                var assetItem = FindItem(id, rootItem);
                if (assetItem == null || !assetItem.displayName.StartsWith("Assets/"))
                {
                    continue;
                }
                var o = AssetDatabase.LoadAssetAtPath<Object>(assetItem.displayName);
                selectedObjects.Add(o);
                Selection.activeObject = o;
            }

            Selection.objects = selectedObjects.ToArray();
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            return true;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            args.draggedItemIDs = GetSelection();
            if (DragAndDrop.paths.Length == 0)
            {
                return false;
            }

            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            return DragAndDropVisualMode.Rejected;
        }

        private void OnSortingChanged(MultiColumnHeader header)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        private void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
            {
                return;
            }

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return;
            }

            SortByColumn();

            rows.Clear();

            foreach (var t in root.children)
            {
                rows.Add(t);
                if (!t.hasChildren || t.children[0] == null || !IsExpanded(t.id))
                {
                    continue;
                }

                foreach (var child in t.children)
                {
                    rows.Add(child);
                }
            }

            Repaint();
        }

        private void SortByColumn()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
            {
                return;
            }

            var assetList = new List<TreeViewItem>();
            foreach (var item in rootItem.children)
            {
                assetList.Add(item);
            }

            var orderedItems = InitialOrder(assetList, sortedColumns);
            rootItem.children = orderedItems.ToList();
        }

        private IEnumerable<TreeViewItem> InitialOrder(IEnumerable<TreeViewItem> myTypes, int[] columnList)
        {
            var sortOption = m_SortOptions[columnList[0]];
            var ascending = multiColumnHeader.IsSortedAscending(columnList[0]);
            switch (sortOption)
            {
                case SortOption.Asset:
                    return myTypes.Order(l => l.displayName, ascending);
                case SortOption.Size:
                    return myTypes.Order(l => ((AssetTreeViewItem)l).size, ascending);
                case SortOption.DependenciesSize:
                    return myTypes.Order(l => ((AssetTreeViewItem)l).dependenciesSize, ascending);
            }

            return myTypes.Order(l => new FileInfo(l.displayName).Length, ascending);
        }

        public void SetAssets(DependenciesWindow window)
        {
            assets.Clear();
            _window = window;
            foreach (var asset in window.GetSelectedAssets())
            {
                assets.Add(asset);
            }

            Reload();
        }

        private enum SortOption
        {
            Asset,
            Size,
            DependenciesSize
        }
    }
}