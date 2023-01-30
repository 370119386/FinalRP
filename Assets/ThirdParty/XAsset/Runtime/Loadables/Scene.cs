using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VEngine
{
    public partial class Scene : Loadable, IEnumerator
    {
        public static Action<Scene> onSceneUnloaded;
        public static Action<Scene> onSceneLoaded;

        private static readonly List<AsyncOperation> Unloading = new List<AsyncOperation>();
        public readonly List<Scene> additives = new List<Scene>();
        public Action<Scene> completed;
        protected string sceneName;
        public Action<Scene> updated;
        public static Func<string, bool, Scene> Creator { get; set; } = Create;
        public AsyncOperation operation { get; protected set; }
        private static Scene main { get; set; }
        public static Scene current { get; private set; }
        private Scene parent { get; set; }
        protected LoadSceneMode loadSceneMode { get; set; }

        public Task<Scene> Task
        {
            get
            {
                var tcs = new TaskCompletionSource<Scene>();
                completed += operation =>
                {
                    tcs.SetResult(this);
                };
                return tcs.Task;
            }
        }

        public bool MoveNext()
        {
            return !isDone;
        }

        public void Reset()
        {
        }

        public object Current => null;

        private static Scene CreateInstance(string path, bool additive)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(nameof(path));
            }

            Versions.GetActualPath(ref path);
            return Creator(path, additive);
        }

        public static Scene LoadAsync(string assetPath, Action<Scene> completed = null, bool additive = false)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            var scene = CreateInstance(assetPath, additive);
            current = scene;
            scene.Load();
            if (completed != null)
            {
                scene.completed += completed;
            }
            return scene;
        }

        public static Scene LoadAdditiveAsync(string assetPath, Action<Scene> completed = null)
        {
            return LoadAsync(assetPath, completed, true);
        }

        public static Scene Load(string assetPath, bool additive = false)
        {
            var scene = CreateInstance(assetPath, additive);
            current = scene;
            scene.mustCompleteOnNextFrame = true;
            scene.Load();
            return scene;
        }

        protected override void OnUpdate()
        {
            if (status != LoadableStatus.Loading)
            {
                return;
            }
            UpdateLoading();
            updated?.Invoke(this);
        }

        protected void UpdateLoading()
        {
            if (operation == null)
            {
                Finish("operation == null");
                return;
            }

            progress = 0.5f + operation.progress * 0.5f;

            if (operation.allowSceneActivation)
            {
                if (!operation.isDone)
                {
                    return;
                }
            }
            else
            {
                // https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html
                if (operation.progress < 0.9f)
                {
                    return;
                }
            }
            Finish();
        }

        protected override void OnLoad()
        {
            PrepareToLoad();
            if (mustCompleteOnNextFrame)
            {
                SceneManager.LoadScene(sceneName, loadSceneMode);
                Finish();
            }
            else
            {
                operation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            }
        }

        protected void PrepareToLoad()
        {
            sceneName = Path.GetFileNameWithoutExtension(pathOrURL);
            if (loadSceneMode == LoadSceneMode.Single)
            {
                if (main != null)
                {
                    main.Release();
                    main = null;
                }

                main = this;
            }
            else
            {
                if (main == null)
                {
                    return;
                }
                main.additives.Add(this);
                parent = main;
            }
        }

        protected override void OnUnused()
        {
            completed = null;
            Unused.Add(this);
        }

        private static void UnloadSceneAsync(string sceneName)
        {
            var unloadSceneAsync = SceneManager.UnloadSceneAsync(sceneName);
            if (unloadSceneAsync == null)
            {
                return;
            }
            Unloading.Add(unloadSceneAsync);
        }

        public static bool IsLoadingOrUnloading()
        {
            if (current != null && !current.isDone)
            {
                return true;
            }

            for (var i = 0; i < Unloading.Count; i++)
            {
                var item = Unloading[i];
                if (!item.isDone)
                {
                    return true;
                }
                Unloading.RemoveAt(i);
                i--;
            }

            return false;
        }

        protected override void OnUnload()
        {
            if (loadSceneMode == LoadSceneMode.Additive)
            {
                main?.additives.Remove(this);
                if (parent != null && string.IsNullOrEmpty(error))
                {
                    UnloadSceneAsync(sceneName);
                }
                parent = null;
            }
            else
            {
                foreach (var item in additives)
                {
                    item.Release();
                    item.parent = null;
                }

                additives.Clear();
            }

            onSceneUnloaded?.Invoke(this);
        }

        protected override void OnComplete()
        {
            onSceneLoaded?.Invoke(this);
            if (completed == null)
            {
                return;
            }
            var saved = completed;
            completed?.Invoke(this);
            completed -= saved;
        }

        private static Scene Create(string assetPath, bool additive = false)
        {
            Versions.GetActualPath(ref assetPath);
            if (!Versions.Contains(assetPath))
            {
                return new Scene
                {
                    pathOrURL = assetPath,
                    loadSceneMode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single
                };
            }

            return new BundledScene
            {
                pathOrURL = assetPath,
                loadSceneMode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single
            };
        }
    }
}