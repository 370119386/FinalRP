namespace VEngine
{
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class AssetInUsed
    {
        public string AssetName;
        public string AssetType;
        public string[] dependencies;
    }

    [System.Serializable]
    public class BundleInUsed
    {
        public string bundleName;
        public int bundleRef;
    }

    [System.Serializable]
    public class SceneInUsed
    {
        public string AssetName;
        public string AssetType;
        public string[] dependencies;
    }

    public class AssetMonitor : MonoBehaviour
    {
        [SerializeField]
        List<SceneInUsed> mCahcedScene = new List<SceneInUsed>(1024);
        [SerializeField]
        List<AssetInUsed> mCachedAsset = new List<AssetInUsed>(1024);
        [SerializeField]
        List<BundleInUsed> mCachedBundles = new List<BundleInUsed>(1024);

        readonly ObjectPool<SceneInUsed> mPooledScenes = new ObjectPool<SceneInUsed>();
        readonly ObjectPool<AssetInUsed> mPooledAssets = new ObjectPool<AssetInUsed>();
        readonly ObjectPool<BundleInUsed> mPooledBundles = new ObjectPool<BundleInUsed>();

        void AddScene(Scene scene)
        {
            if(null != scene)
            {
                var sceneInUsed = mPooledScenes.Get();
                sceneInUsed.AssetName = scene.SceneName;
                sceneInUsed.AssetType = nameof(UnityEngine.SceneManagement.Scene);
                Dependencies.Report(scene.pathOrURL, ref sceneInUsed.dependencies);
                mCahcedScene.Add(sceneInUsed);
            }
        }

        // Update is called once per frame
        void Update()
        {
            mCahcedScene.ForEach(mPooledScenes.Put);
            mCahcedScene.Clear();
            if(null != Scene.current)
            {
                var scene = Scene.current;
                AddScene(scene);
                foreach(var s in scene.additives)
                {
                    AddScene(s);
                }
            }

            mCachedAsset.ForEach(mPooledAssets.Put);
            mCachedAsset.Clear();

            var cachedAssets = Asset.Cache;
            foreach(var kv in cachedAssets)
            {
                var asset = kv.Value;
                if(null != asset && null != asset.asset)
                {
                    var assetInUsed = mPooledAssets.Get();
                    assetInUsed.AssetName = kv.Key;
                    assetInUsed.AssetType = asset.asset.GetType().Name;
                    Dependencies.Report(kv.Key, ref assetInUsed.dependencies);
                    mCachedAsset.Add(assetInUsed);
                }
            }

            mCachedBundles.ForEach(mPooledBundles.Put);
            mCachedBundles.Clear();

            var cachedBundles = Bundle.Cache;
            foreach (var bundle in cachedBundles)
            {
                var bundleInUsed = mPooledBundles.Get();
                bundleInUsed.bundleRef = bundle.Value.RefCount;
                bundleInUsed.bundleName = bundle.Key;
                mCachedBundles.Add(bundleInUsed);
            }
        }
    }
}