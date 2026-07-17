using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Reusable staggered reveal: elements appear one after another (and hide in reverse).
    /// Visuals live in uikit.uss ("uikit-cascade-item" / "uikit-cascade-hidden"); this utility
    /// only schedules the class flips, so it works for any container — popups use it for the
    /// cascade mode, and game code can run it on any list of elements.
    /// </summary>
    public static class UiCascade
    {
        /// <summary>Class carrying the reveal transition.</summary>
        public const string ItemClass = "uikit-cascade-item";

        /// <summary>Class of the hidden state.</summary>
        public const string HiddenClass = "uikit-cascade-hidden";

        /// <summary>Default per-element interval for reveals, ms.</summary>
        public const long RevealIntervalMs = 70;

        /// <summary>Default per-element interval for reverse hiding, ms.</summary>
        public const long HideIntervalMs = 45;

        /// <summary>
        /// Collects a popup's reveal list: the header first, then the children of every content
        /// panel in tree order, skipping each panel's own layered background image.
        /// Adds <see cref="ItemClass"/> to every collected element.
        /// </summary>
        public static List<VisualElement> CollectPopupItems(VisualElement popupRoot, VisualElement dim)
        {
            var items = new List<VisualElement>();
            VisualElement header = popupRoot.Q<VisualElement>("panel_header");

            foreach (VisualElement child in popupRoot.Children())
            {
                if (child == dim || child == header || string.IsNullOrEmpty(child.name))
                    continue;

                if (!child.ClassListContains("fui_type_panel") && !child.ClassListContains("fui_as_panel"))
                    continue;

                bool skippedBackground = false;
                foreach (VisualElement item in child.Children())
                {
                    if (!skippedBackground && item.ClassListContains("fui_type_image") &&
                        item.ClassListContains("fui_layered"))
                    {
                        skippedBackground = true;
                        continue;
                    }

                    items.Add(item);
                }
            }

            if (header != null)
                items.Insert(0, header);

            foreach (VisualElement item in items)
                item.AddToClassList(ItemClass);

            return items;
        }

        /// <summary>
        /// Collects a page's reveal list: the direct interface children of the screen root in
        /// tree order, skipping the fullscreen layered background image. Adds <see cref="ItemClass"/>
        /// to every collected element.
        /// </summary>
        public static List<VisualElement> CollectPageItems(VisualElement screenRoot)
        {
            var items = new List<VisualElement>();
            if (screenRoot == null)
                return items;

            bool skippedBackground = false;
            foreach (VisualElement child in screenRoot.Children())
            {
                if (string.IsNullOrEmpty(child.name))
                    continue;

                if (!skippedBackground && child.ClassListContains("fui_type_image") &&
                    child.ClassListContains("fui_layered"))
                {
                    skippedBackground = true;
                    continue;
                }

                // Never cascade popups; they run their own reveal on open.
                if (child.ClassListContains("fui_type_popup"))
                    continue;

                items.Add(child);
            }

            foreach (VisualElement item in items)
                item.AddToClassList(ItemClass);

            return items;
        }

        /// <summary>Shows or hides all items at once (no animation scheduling).</summary>
        public static void SetHidden(IReadOnlyList<VisualElement> items, bool hidden)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].EnableInClassList(HiddenClass, hidden);
        }

        /// <summary>
        /// Plays the cascade: reveals items front-to-back, or hides them back-to-front.
        /// Returns the scheduled item (pause it to cancel); onDone fires after the last element.
        /// </summary>
        public static IVisualElementScheduledItem Play(VisualElement host, IReadOnlyList<VisualElement> items,
            bool reveal, Action onDone = null, long intervalMs = 0, long startDelayMs = 0)
        {
            if (host == null || items == null || items.Count == 0)
            {
                onDone?.Invoke();
                return null;
            }

            if (intervalMs <= 0)
                intervalMs = reveal ? RevealIntervalMs : HideIntervalMs;

            int index = reveal ? 0 : items.Count - 1;
            IVisualElementScheduledItem schedule = null;
            schedule = host.schedule.Execute(() =>
            {
                bool done = reveal ? index >= items.Count : index < 0;
                if (done)
                {
                    schedule?.Pause();
                    onDone?.Invoke();
                    return;
                }

                items[index].EnableInClassList(HiddenClass, !reveal);
                index += reveal ? 1 : -1;
            }).Every(intervalMs);

            if (startDelayMs > 0)
                schedule.StartingIn(startDelayMs);

            return schedule;
        }
    }
}
