using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Progress bar over an imported fui_type_progressbar element. The fill width is set in
    /// pixels as a fraction of the track's resolved width (percentage of the parent would be
    /// wrong for imported bars whose fill is narrower than the track), re-applied on
    /// GeometryChangedEvent and on every bind.
    /// </summary>
    public class BarView : UiSubView
    {
        private VisualElement _fill;
        private bool _explicitFill;
        private EventCallback<GeometryChangedEvent> _geometryCallback;
        private float _progress;
        private bool _hasProgress;
        private float _designWidth = -1f;

        /// <summary>The fill element, or null.</summary>
        public VisualElement Fill => _fill;

        /// <summary>Last set progress in 0..1.</summary>
        public float Progress => _progress;

        /// <summary>Binds with an explicit fill element (root acts as the track).</summary>
        public void Bind(VisualElement track, VisualElement fill)
        {
            _fill = fill;
            _explicitFill = fill != null;
            Bind(track);
        }

        protected override void OnBind(VisualElement root)
        {
            if (!_explicitFill)
                _fill = FindFill(root);

            // Let the importer's USS width apply so the design (full-progress) width can be
            // measured; the fill must never grow past it (it is inset within the track art).
            _designWidth = -1f;
            if (_fill != null)
                _fill.style.width = StyleKeyword.Null;

            _geometryCallback = OnGeometryChanged;
            root.RegisterCallback(_geometryCallback);

            if (_hasProgress)
                Apply();
        }

        protected override void OnUnwire()
        {
            if (_geometryCallback != null && Root != null)
                Root.UnregisterCallback(_geometryCallback);

            _geometryCallback = null;
            if (!_explicitFill)
                _fill = null;
        }

        /// <summary>Sets the progress in 0..1; cached and re-applied on layout changes and rebinds.</summary>
        public void SetProgress(float normalized)
        {
            _progress = Mathf.Clamp01(normalized);
            _hasProgress = true;
            Apply();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            CaptureDesignWidth();
            if (_hasProgress)
                Apply();
        }

        private void CaptureDesignWidth()
        {
            if (_designWidth > 0f || _fill == null)
                return;

            float width = _fill.resolvedStyle.width;
            if (!float.IsNaN(width) && width > 0f)
                _designWidth = width;
        }

        private void Apply()
        {
            if (Root == null || _fill == null)
                return;

            if (_designWidth <= 0f)
                return;

            _fill.style.width = _designWidth * _progress;
        }

        private static VisualElement FindFill(VisualElement root)
        {
            VisualElement byName = root.Query<VisualElement>().Where(e => e != root && e.name != null && e.name.Contains("fill")).First();
            if (byName != null)
                return byName;

            return root.childCount > 0 ? root[root.childCount - 1] : null;
        }
    }
}
