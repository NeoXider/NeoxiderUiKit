using System;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// On/off switch (sound, music, vibration, ...): clicking toggles the state, the off state
    /// is expressed by the "is-off" class, and <see cref="Changed"/> reports user changes.
    /// </summary>
    public class ToggleView : UiSubView
    {
        private Button _button;
        private EventCallback<ClickEvent> _clickCallback;
        private bool _isOn = true;

        /// <summary>Raised when the user toggles the state (not by SetWithoutNotify).</summary>
        public event Action<bool> Changed;

        /// <summary>Current state; setting it raises <see cref="Changed"/>.</summary>
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value)
                    return;
                _isOn = value;
                ApplyState();
                Changed?.Invoke(value);
            }
        }

        protected override void OnBind(VisualElement root)
        {
            _button = root as Button;
            if (_button != null)
            {
                _button.clicked += OnClick;
            }
            else
            {
                _clickCallback = evt => OnClick();
                root.RegisterCallback(_clickCallback);
            }

            ApplyState();
        }

        protected override void OnUnwire()
        {
            if (_button != null)
            {
                _button.clicked -= OnClick;
                _button = null;
            }
            else if (_clickCallback != null && Root != null)
            {
                Root.UnregisterCallback(_clickCallback);
            }

            _clickCallback = null;
        }

        /// <summary>Sets the state without raising <see cref="Changed"/>.</summary>
        public void SetWithoutNotify(bool isOn)
        {
            _isOn = isOn;
            ApplyState();
        }

        private void OnClick()
        {
            IsOn = !IsOn;
        }

        private void ApplyState()
        {
            Root?.EnableInClassList("is-off", !_isOn);
        }
    }
}
