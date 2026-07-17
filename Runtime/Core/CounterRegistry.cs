using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// A global counter model (money, score, level, ...). Views subscribe to <see cref="Changed"/>
    /// and re-apply <see cref="Value"/> on every bind, so inactive pages pick the value up on their next bind.
    /// </summary>
    public sealed class Counter
    {
        /// <summary>Counter id (e.g. "coin").</summary>
        public string Id { get; }

        /// <summary>Current value.</summary>
        public long Value { get; private set; }

        /// <summary>Raised on every <see cref="Set"/>/<see cref="Add"/> call.</summary>
        public event Action<long> Changed;

        internal Counter(string id)
        {
            Id = id;
        }

        /// <summary>Sets the value and broadcasts it to all live bound views.</summary>
        public void Set(long value)
        {
            Value = value;
            Changed?.Invoke(value);
        }

        /// <summary>Adds a delta to the current value.</summary>
        public void Add(long delta)
        {
            Set(Value + delta);
        }
    }

    /// <summary>
    /// Registry of global counters addressed by id. Counter values live here, not in elements:
    /// the config maps each id to the element paths it is displayed at.
    /// </summary>
    public sealed class CounterRegistry
    {
        private readonly Dictionary<string, Counter> _counters = new Dictionary<string, Counter>(StringComparer.Ordinal);

        /// <summary>All known counter ids.</summary>
        public IEnumerable<string> Ids => _counters.Keys;

        /// <summary>
        /// Returns the counter for the id. Unknown id logs a clear error listing known ids
        /// and defines the counter on the fly so calling code keeps working.
        /// </summary>
        public Counter this[string id]
        {
            get
            {
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError("[UiKit] Counter id is null or empty.");
                    id = "<empty>";
                }

                if (_counters.TryGetValue(id, out Counter counter))
                    return counter;

                Debug.LogError($"[UiKit] Unknown counter id '{id}'. Known ids: [{string.Join(", ", _counters.Keys)}]. " +
                               "Add it to UiKitConfig.counters or call UiKit.Counters.Define(id).");
                return Define(id);
            }
        }

        /// <summary>True when the id is registered.</summary>
        public bool Contains(string id)
        {
            return id != null && _counters.ContainsKey(id);
        }

        /// <summary>Registers a counter id (idempotent) and returns its counter.</summary>
        public Counter Define(string id)
        {
            if (!_counters.TryGetValue(id, out Counter counter))
            {
                counter = new Counter(id);
                _counters.Add(id, counter);
            }

            return counter;
        }

        internal Counter TryGet(string id)
        {
            return id != null && _counters.TryGetValue(id, out Counter counter) ? counter : null;
        }
    }
}
