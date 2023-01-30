using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VEngine.Editor.Builds
{
    public static class RunOnUpdate
    {
        private static readonly List<Action> _actions = new List<Action>();

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.update += Update;
        }

        public static void RunAction(Action action)
        {
            _actions.Add(action);
        }

        private static void Update()
        {
            foreach (var action in _actions)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    break;
                }
            }
            _actions.Clear();
        }
    }
}