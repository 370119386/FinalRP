using System;
using UnityEngine;

namespace VEngine
{
    [CreateAssetMenu(menuName = "Versions/Group", fileName = "Group", order = 0)]
    public class AssetGroup : ScriptableObject
    {
        [TextArea(3, 100)] public string notes;

        public string[] assets = Array.Empty<string>();
    }
}