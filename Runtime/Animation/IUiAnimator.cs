using System;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Code-level animation extension point. Register custom animators via
    /// <see cref="UiAnimations.Register"/>; built-in presets are USS class driven.
    /// </summary>
    public interface IUiAnimator
    {
        /// <summary>Plays the show animation and invokes <paramref name="onDone"/> exactly once when finished.</summary>
        void Show(VisualElement element, Action onDone);

        /// <summary>Plays the hide animation and invokes <paramref name="onDone"/> exactly once when finished.</summary>
        void Hide(VisualElement element, Action onDone);
    }
}
