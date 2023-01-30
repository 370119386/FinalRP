using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEngine
{
    public class AsyncUpdate
    {
        public Action completed;

        public Func<bool> isDone;
        internal bool running;
        public Action updated;

        public static AsyncUpdate CreateInstance()
        {
            return new AsyncUpdate();
        }

        public bool Update()
        {
            updated?.Invoke();
            if (isDone == null || !isDone())
            {
                return true;
            }
            completed?.Invoke();
            return false;
        }

        public void Run()
        {
            if (running)
            {
                return;
            }

            Updater.Progressing.Add(this);
            running = true;
        }

        public void Stop()
        {
            if (!running)
            {
                return;
            }
            Updater.Progressing.Remove(this);
            running = false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Updater : MonoBehaviour
    {
        private static float realtimeSinceUpdateStartup;
        internal static readonly List<AsyncUpdate> Progressing = new List<AsyncUpdate>();
        [SerializeField] private float _maxUpdateTimeSlice = 0.01f;
        public static float maxUpdateTimeSlice { get; set; }
        public static bool busy => Time.realtimeSinceStartup - realtimeSinceUpdateStartup >= maxUpdateTimeSlice;

        private void Start()
        {
            maxUpdateTimeSlice = _maxUpdateTimeSlice;
        }

        private void Update()
        {
            realtimeSinceUpdateStartup = Time.realtimeSinceStartup;
            for (var i = 0; i < Progressing.Count; i++)
            {
                var item = Progressing[i];
                try
                {
                    if (item.Update())
                    {
                        continue;
                    }
                    item.running = false;
                    Progressing.RemoveAt(i);
                    i--;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    item.running = false;
                    Progressing.RemoveAt(i);
                    i--;
                }

                if (busy)
                {
                    return;
                }
            }

            Loadable.UpdateLoadingAndUnused();
            Operation.UpdateAll();
            Download.UpdateAll();
        }

        private void OnDestroy()
        {
            Download.ClearAllDownloads();
            Loadable.ClearAllCaches();
        }

        public static void Run(AsyncUpdate update)
        {
            update.Run();
        }

        public static void RunAsync(Action action)
        {
            var update = AsyncUpdate.CreateInstance();
            update.isDone = () => true;
            update.completed = action;
            Run(update);
        }

        public static void Stop(AsyncUpdate update)
        {
            update.Stop();
        }

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var updater = FindObjectOfType<Updater>();
            if (updater != null)
            {
                return;
            }
            updater = new GameObject("Updater").AddComponent<Updater>();
            DontDestroyOnLoad(updater);
        }
    }
}