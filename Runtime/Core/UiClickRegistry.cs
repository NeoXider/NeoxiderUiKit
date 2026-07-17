using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Registry of "element path → click handlers". Survives panel reloads: pages re-apply
    /// the registry on every bind, so handlers can be registered before pages exist.
    /// </summary>
    internal sealed class UiClickRegistry
    {
        private readonly Dictionary<string, List<Action>> _handlers = new Dictionary<string, List<Action>>(StringComparer.Ordinal);

        /// <summary>Adds a handler; returns true when the path was not registered before.</summary>
        public bool Add(string path, Action handler)
        {
            if (_handlers.TryGetValue(path, out List<Action> list))
            {
                list.Add(handler);
                return false;
            }

            _handlers.Add(path, new List<Action> { handler });
            return true;
        }

        /// <summary>All registered full paths whose first segment is the given page id.</summary>
        public IEnumerable<string> PathsForPage(string pageId)
        {
            string prefix = pageId + "/";
            foreach (string path in _handlers.Keys)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                    yield return path;
            }
        }

        /// <summary>Invokes all handlers registered for the path.</summary>
        public void Invoke(string path)
        {
            if (!_handlers.TryGetValue(path, out List<Action> list))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    list[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
