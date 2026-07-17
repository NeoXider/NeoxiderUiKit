using System.Globalization;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Displays a value from <see cref="CounterRegistry"/> inside a numeric label, preserving
    /// the rich-text gradient wrapper and any text around the number ("LEVEL 5" keeps "LEVEL ").
    /// Caches the last value and re-applies it on every bind.
    /// </summary>
    public class CounterView : UiSubView
    {
        private Label _label;
        private UiRichText _template;
        private string _numberPrefix = "";
        private string _numberSuffix = "";
        private Counter _counter;
        private IVisualElementScheduledItem _pulseItem;
        private long _cachedValue;
        private bool _hasCachedValue;

        /// <summary>Id of the counter this view mirrors; may be null for manual <see cref="SetValue"/> usage.</summary>
        public string CounterId { get; set; }

        /// <summary>When true, large values are shortened (12 300 → "12.3K", 3 400 000 → "3.4M").</summary>
        public bool UseCompactFormat { get; set; } = true;

        /// <summary>Values below this threshold are never compacted.</summary>
        public long CompactThreshold { get; set; } = 100000;

        /// <summary>When true, a short scale pulse plays on value changes.</summary>
        public bool PulseOnChange { get; set; } = true;

        /// <summary>Last displayed value.</summary>
        public long Value => _cachedValue;

        /// <summary>The bound value label, or null.</summary>
        public Label Label => _label;

        protected override void OnBind(VisualElement root)
        {
            _label = FindValueLabel(root);
            if (_label != null)
            {
                _template = UiRichText.Parse(_label.text);
                if (_template.TrySplitNumber(out string prefix, out string digits, out string suffix))
                {
                    _numberPrefix = prefix;
                    _numberSuffix = suffix;
                    if (!_hasCachedValue && long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long initial))
                        _cachedValue = initial;
                }
                else
                {
                    _numberPrefix = "";
                    _numberSuffix = "";
                }

                _label.AddToClassList("uikit-counter");
            }

            if (!string.IsNullOrEmpty(CounterId))
            {
                _counter = UiKit.Counters.TryGet(CounterId) ?? UiKit.Counters.Define(CounterId);
                _counter.Changed += OnCounterChanged;
                _cachedValue = _counter.Value;
                _hasCachedValue = true;
            }

            Apply(_cachedValue, false);
        }

        protected override void OnUnwire()
        {
            if (_counter != null)
            {
                _counter.Changed -= OnCounterChanged;
                _counter = null;
            }

            _pulseItem?.Pause();
            _pulseItem = null;
            _label = null;
        }

        /// <summary>Sets the displayed value directly (bypassing the registry).</summary>
        public void SetValue(long value)
        {
            bool changed = !_hasCachedValue || value != _cachedValue;
            _cachedValue = value;
            _hasCachedValue = true;
            Apply(value, changed && PulseOnChange);
        }

        private void OnCounterChanged(long value)
        {
            SetValue(value);
        }

        private void Apply(long value, bool pulse)
        {
            if (_label == null)
                return;

            _label.text = _template.Apply(_numberPrefix + Format(value) + _numberSuffix);

            if (pulse)
            {
                _label.AddToClassList("uikit-counter-pulse");
                _pulseItem?.Pause();
                _pulseItem = _label.schedule.Execute(() => _label.RemoveFromClassList("uikit-counter-pulse")).StartingIn(160);
            }
        }

        /// <summary>Formats a value for display, optionally compacting to 1.2K / 3.4M.</summary>
        protected virtual string Format(long value)
        {
            if (!UseCompactFormat || System.Math.Abs(value) < CompactThreshold)
                return value.ToString(CultureInfo.InvariantCulture);

            double abs = System.Math.Abs(value);
            if (abs >= 1000000000d)
                return Compact(value / 1000000000d, "B");
            if (abs >= 1000000d)
                return Compact(value / 1000000d, "M");
            return Compact(value / 1000d, "K");
        }

        private static string Compact(double value, string suffix)
        {
            return value.ToString(System.Math.Abs(value) >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture) + suffix;
        }

        private static Label FindValueLabel(VisualElement root)
        {
            if (root is Label rootLabel)
                return rootLabel;

            var labels = root.Query<Label>().ToList();
            if (labels.Count == 0)
                return null;

            Label fallback = null;
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                UiRichText template = UiRichText.Parse(labels[i].text);
                if (!template.TrySplitNumber(out _, out string digits, out _))
                    continue;

                if (long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return labels[i];

                fallback = fallback != null ? fallback : labels[i];
            }

            return fallback != null ? fallback : labels[labels.Count - 1];
        }
    }
}
