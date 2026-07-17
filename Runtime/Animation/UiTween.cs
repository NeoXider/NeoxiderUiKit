using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Tiny reusable scheduler-driven tween for UI Toolkit. Drives a 0..1 progress over a fixed
    /// duration via <c>schedule</c> (unscaled real time, so it runs while the game is paused) and
    /// hands the value to a setter. Used by popup/page animators; also usable from game code.
    /// Class-based CSS transitions are avoided here because UITK does not reliably play a
    /// transition when the triggering class is removed.
    /// </summary>
    public static class UiTween
    {
        /// <summary>Built-in easing curves.</summary>
        public enum Ease
        {
            Linear,
            OutCubic,
            OutBack,
            InCubic
        }

        /// <summary>
        /// Animates progress 0..1 over <paramref name="durationMs"/>, calling <paramref name="apply"/>
        /// each tick and <paramref name="onDone"/> at the end. Returns the scheduled item (pause it
        /// to cancel).
        /// </summary>
        public static IVisualElementScheduledItem Play(VisualElement host, long durationMs,
            Action<float> apply, Ease ease = Ease.OutCubic, Action onDone = null)
        {
            if (host == null || apply == null || durationMs <= 0)
            {
                apply?.Invoke(1f);
                onDone?.Invoke();
                return null;
            }

            // Real time (not TimerState.now, whose unit is unreliable across versions) so the tween
            // keeps a fixed wall-clock duration and runs even while the game is paused (timeScale 0).
            float start = Time.realtimeSinceStartup;
            float durationSec = durationMs / 1000f;
            apply(0f);

            IVisualElementScheduledItem item = null;
            item = host.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / durationSec);
                apply(Evaluate(ease, t));

                if (t >= 1f)
                {
                    item?.Pause();
                    onDone?.Invoke();
                }
            }).Every(16);

            return item;
        }

        /// <summary>Evaluates an easing curve at t in 0..1.</summary>
        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutCubic:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }
                case Ease.InCubic:
                    return t * t * t;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float inv = t - 1f;
                    return 1f + c3 * inv * inv * inv + c1 * inv * inv;
                }
                default:
                    return t;
            }
        }
    }
}
