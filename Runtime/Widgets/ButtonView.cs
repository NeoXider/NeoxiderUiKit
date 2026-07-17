using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Typed button wrapper: <see cref="Clicked"/> event, 0.15s click throttle, press-scale pulse
    /// ("uikit-pressed"), click sound via <see cref="UiAudio"/> and the global
    /// <see cref="UiKit.AnyButtonClicked"/> hook. Created automatically by pages for every button.
    /// </summary>
    public class ButtonView : UiSubView
    {
        private const float ThrottleSeconds = 0.15f;
        private const long PressPulseMs = 120;

        private Button _button;
        private EventCallback<ClickEvent> _clickCallback;
        private IVisualElementScheduledItem _pressItem;
        private IUiAnimator _pressAnimator;
        private float _lastClickTime = float.MinValue;
        private bool _enabled = true;

        /// <summary>Raised on click (after throttling).</summary>
        public event Action Clicked;

        /// <summary>When false, this button does not play the click sound.</summary>
        public bool PlayClickSound { get; set; } = true;

        /// <summary>When false, the press-scale pulse is skipped.</summary>
        public bool PlayPressAnimation { get; set; } = true;

        protected override void OnBind(VisualElement root)
        {
            _button = root as Button;
            if (_button != null)
            {
                _button.clicked += OnClick;
            }
            else
            {
                _clickCallback = OnClickEvent;
                root.RegisterCallback(_clickCallback);
            }

            ApplyEnabled();
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
            _pressItem?.Pause();
            _pressItem = null;
        }

        /// <summary>Enables/disables the button, toggling the "uikit-disabled" class.</summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            ApplyEnabled();
        }

        /// <summary>Replaces the built-in press pulse with a custom animator.</summary>
        public void SetPressAnimation(IUiAnimator animator)
        {
            _pressAnimator = animator;
        }

        private void ApplyEnabled()
        {
            if (Root == null)
                return;

            Root.SetEnabled(_enabled);
            Root.EnableInClassList("uikit-disabled", !_enabled);
        }

        private void OnClickEvent(ClickEvent evt)
        {
            OnClick();
        }

        private void OnClick()
        {
            if (!_enabled || Root == null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastClickTime < ThrottleSeconds)
                return;
            _lastClickTime = now;

            if (PlayPressAnimation)
                PlayPressPulse();

            if (PlayClickSound)
                UiKit.Audio.PlayClick();

            UiKit.NotifyAnyButtonClicked(Root.name);
            Clicked?.Invoke();
        }

        private void PlayPressPulse()
        {
            if (_pressAnimator != null)
            {
                _pressAnimator.Show(Root, null);
                return;
            }

            VisualElement root = Root;
            root.AddToClassList("uikit-pressed");
            _pressItem?.Pause();
            _pressItem = root.schedule.Execute(() => root.RemoveFromClassList("uikit-pressed")).StartingIn(PressPulseMs);
        }
    }
}
