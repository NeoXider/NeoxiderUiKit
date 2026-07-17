using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Typed wrapper over a Label. <see cref="SetText"/> preserves the rich-text gradient
    /// wrapper produced by the importer, replacing only the inner content.
    /// </summary>
    public class LabelView : UiSubView
    {
        private Label _label;
        private UiRichText _template;
        private string _cachedText;
        private bool _hasCachedText;

        /// <summary>The bound label, or null.</summary>
        public Label Label => _label;

        /// <summary>Current logical text (inner content, without the rich-text wrapper).</summary>
        public string Text => _hasCachedText ? _cachedText : _template.Inner;

        protected override void OnBind(VisualElement root)
        {
            _label = root as Label ?? root.Q<Label>();
            if (_label == null)
                return;

            _template = UiRichText.Parse(_label.text);
            if (_hasCachedText)
                Apply(_cachedText);
        }

        protected override void OnUnwire()
        {
            _label = null;
        }

        /// <summary>Sets the text, preserving the original gradient wrapper. Value is cached and re-applied on bind.</summary>
        public void SetText(string text)
        {
            _cachedText = text;
            _hasCachedText = true;
            Apply(text);
        }

        private void Apply(string text)
        {
            if (_label != null)
                _label.text = _template.Apply(text);
        }
    }
}
