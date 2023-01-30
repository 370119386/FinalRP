﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Object = UnityEngine.Object;

namespace VEngine
{
    public class Asset : Loadable, IEnumerator
    {
        public static readonly Dictionary<string, Asset> Cache = new Dictionary<string, Asset>();

        public Action<Asset> completed;

        public static Func<string, Type, Asset> Creator { get; set; } = BundledAsset.Create;

        public Object asset { get; protected set; }

        protected Type type { get; set; }

        public Task<Asset> Task
        {
            get
            {
                var tcs = new TaskCompletionSource<Asset>();
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

        private static Asset CreateInstance(string path, Type type)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(nameof(path));
            }
            return Creator(path, type);
        }

        protected void OnLoaded(Object target)
        {
            asset = target;
            Finish(asset == null ? "asset == null" : null);
        }

        public T Get<T>() where T : Object
        {
            return asset as T;
        }

        protected override void OnComplete()
        {
            if (completed == null)
            {
                return;
            }

            var saved = completed;
            completed?.Invoke(this);

            completed -= saved;
        }

        protected override void OnUnused()
        {
            completed = null;
        }

        protected override void OnUnload()
        {
            Cache.Remove(pathOrURL);
        }

        public static Asset LoadAsync(string path, Type type, Action<Asset> completed = null)
        {
            return LoadInternal(path, type, false, completed);
        }

        public static Asset Load(string path, Type type)
        {
            return LoadInternal(path, type, true);
        }

        internal static Asset LoadInternal(string path, Type type, bool mustCompleteOnNextFrame,
            Action<Asset> completed = null)
        {
            Versions.GetActualPath(ref path);
            if (!Versions.Contains(path))
            {
                Logger.E("FileNotFoundException {0}", path);
                return null;
            }

            if (!Cache.TryGetValue(path, out var item))
            {
                item = CreateInstance(path, type);
                Cache.Add(path, item);
            }

            if (completed != null)
            {
                item.completed += completed;
            }

            item.mustCompleteOnNextFrame = mustCompleteOnNextFrame;
            item.Load();
            if (mustCompleteOnNextFrame)
            {
                item.LoadImmediate();
            }

            return item;
        }
    }
}