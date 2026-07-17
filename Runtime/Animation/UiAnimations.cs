using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Registry of animation presets. Built-in presets are driven by USS classes from uikit.uss
    /// (they only toggle "is-open"/"is-hiding" and wait for the transition); custom
    /// <see cref="IUiAnimator"/> implementations can be registered under their own names.
    /// </summary>
    public static class UiAnimations
    {
        /// <summary>Class marking an element as shown; USS transitions animate towards it.</summary>
        public const string OpenClass = "is-open";

        /// <summary>Class present while a hide transition is running.</summary>
        public const string HidingClass = "is-hiding";

        /// <summary>Prefix of preset classes, e.g. "uikit-anim-fade".</summary>
        public const string AnimClassPrefix = "uikit-anim-";

        /// <summary>Default transition duration of the built-in presets, seconds.</summary>
        public const float DefaultDuration = 0.25f;

        private static readonly string[] BuiltInNames =
        {
            "fade", "slide-up", "slide-down", "slide-left", "slide-right", "scale", "scale-pop", "none"
        };

        private static Dictionary<string, IUiAnimator> _custom = new Dictionary<string, IUiAnimator>(StringComparer.Ordinal);
        private static IUiAnimator _instant = new InstantUiAnimator();
        private static IUiAnimator _transition = new UssTransitionAnimator(DefaultDuration);

        /// <summary>Animator that toggles classes without waiting for a transition.</summary>
        public static IUiAnimator Instant => _instant;

        /// <summary>All available preset names (built-in + registered).</summary>
        public static IEnumerable<string> Names
        {
            get
            {
                foreach (string name in BuiltInNames)
                    yield return name;
                foreach (string name in _custom.Keys)
                    yield return name;
            }
        }

        /// <summary>Registers (or replaces) a custom animator under a preset name.</summary>
        public static void Register(string name, IUiAnimator animator)
        {
            if (string.IsNullOrEmpty(name) || animator == null)
            {
                Debug.LogError("[UiKit] UiAnimations.Register requires a non-empty name and a non-null animator.");
                return;
            }

            _custom[name] = animator;
        }

        /// <summary>Resolves a preset name to an animator; unknown names fall back to the class-driven default.</summary>
        public static IUiAnimator Get(string preset)
        {
            if (!string.IsNullOrEmpty(preset) && _custom.TryGetValue(preset, out IUiAnimator animator))
                return animator;

            if (string.IsNullOrEmpty(preset) || preset == "none")
                return _instant;

            return _transition;
        }

        /// <summary>Replaces preset classes ("uikit-anim-*") on the element with the given preset class.</summary>
        public static void ApplyPresetClass(VisualElement element, string preset)
        {
            if (element == null)
                return;

            var toRemove = new List<string>();
            foreach (string cls in element.GetClasses())
            {
                if (cls.StartsWith(AnimClassPrefix, StringComparison.Ordinal))
                    toRemove.Add(cls);
            }

            for (int i = 0; i < toRemove.Count; i++)
                element.RemoveFromClassList(toRemove[i]);

            if (!string.IsNullOrEmpty(preset) && preset != "none")
                element.AddToClassList(AnimClassPrefix + preset);
        }

        internal static void ResetStatics()
        {
            _custom = new Dictionary<string, IUiAnimator>(StringComparer.Ordinal);
            _instant = new InstantUiAnimator();
            _transition = new UssTransitionAnimator(DefaultDuration);
        }
    }

    /// <summary>Animator that applies the final state immediately (used by the "none" preset and disabled modes).</summary>
    internal sealed class InstantUiAnimator : IUiAnimator
    {
        public void Show(VisualElement element, Action onDone)
        {
            element?.AddToClassList(UiAnimations.OpenClass);
            onDone?.Invoke();
        }

        public void Hide(VisualElement element, Action onDone)
        {
            element?.RemoveFromClassList(UiAnimations.OpenClass);
            onDone?.Invoke();
        }
    }

    /// <summary>
    /// Class-driven animator for the built-in USS presets: skips one frame before adding "is-open"
    /// (so the transition has a previous style), then waits for TransitionEndEvent with a scheduled
    /// timeout fallback (duration + 0.1s).
    /// </summary>
    internal sealed class UssTransitionAnimator : IUiAnimator
    {
        private readonly long _timeoutMs;

        public UssTransitionAnimator(float durationSeconds)
        {
            _timeoutMs = (long)(durationSeconds * 1000f) + 100;
        }

        public void Show(VisualElement element, Action onDone)
        {
            if (element == null)
            {
                onDone?.Invoke();
                return;
            }

            element.schedule.Execute(() =>
            {
                element.AddToClassList(UiAnimations.OpenClass);
                WaitForTransition(element, onDone);
            });
        }

        public void Hide(VisualElement element, Action onDone)
        {
            if (element == null)
            {
                onDone?.Invoke();
                return;
            }

            element.AddToClassList(UiAnimations.HidingClass);
            element.RemoveFromClassList(UiAnimations.OpenClass);
            WaitForTransition(element, () =>
            {
                element.RemoveFromClassList(UiAnimations.HidingClass);
                onDone?.Invoke();
            });
        }

        private void WaitForTransition(VisualElement element, Action onDone)
        {
            bool completed = false;
            EventCallback<TransitionEndEvent> callback = null;
            IVisualElementScheduledItem timeout = null;

            Action finish = () =>
            {
                if (completed)
                    return;
                completed = true;
                element.UnregisterCallback(callback);
                timeout?.Pause();
                onDone?.Invoke();
            };

            callback = evt =>
            {
                if (evt.target == element)
                    finish();
            };

            element.RegisterCallback(callback);
            timeout = element.schedule.Execute(() => finish()).StartingIn(_timeoutMs);
        }
    }
}
