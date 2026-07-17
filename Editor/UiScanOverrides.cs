using System;
using System.Collections.Generic;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Applies window-edited scanner overrides (stored in <see cref="UiKitConfig.ScanOverrideEntry"/>)
    /// to a scan result: excludes pages/popups/elements and replaces widget kinds and counter ids.
    /// </summary>
    public static class UiScanOverrides
    {
        private const string PanelNamePrefix = "panel_";

        /// <summary>Mutates the scan result according to the overrides. Null/empty overrides are a no-op.</summary>
        public static void Apply(UiScanResult scan, IReadOnlyList<UiKitConfig.ScanOverrideEntry> overrides)
        {
            if (scan == null || overrides == null || overrides.Count == 0)
                return;

            var byPath = new Dictionary<string, UiKitConfig.ScanOverrideEntry>(StringComparer.Ordinal);
            foreach (UiKitConfig.ScanOverrideEntry entry in overrides)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.elementPath))
                    byPath[entry.elementPath] = entry;
            }

            if (byPath.Count == 0)
                return;

            scan.Pages.RemoveAll(page => IsExcluded(byPath, page.PageId));

            foreach (UiPageModel page in scan.Pages)
            {
                page.Popups.RemoveAll(popup => IsExcluded(byPath, popup.FullPath));

                ApplyToElements(page.Elements, byPath);
                foreach (UiPopupModel popup in page.Popups)
                    ApplyToElements(popup.Elements, byPath);
            }
        }

        /// <summary>Parses a widget kind name stored in an override; empty/invalid returns null.</summary>
        public static UiWidgetKind? ParseKind(string widgetKind)
        {
            if (!string.IsNullOrEmpty(widgetKind) && Enum.TryParse(widgetKind, out UiWidgetKind kind))
                return kind;

            return null;
        }

        private static bool IsExcluded(Dictionary<string, UiKitConfig.ScanOverrideEntry> byPath, string path)
        {
            return byPath.TryGetValue(path, out UiKitConfig.ScanOverrideEntry entry) && entry.excluded;
        }

        private static void ApplyToElements(List<UiElementModel> elements,
            Dictionary<string, UiKitConfig.ScanOverrideEntry> byPath)
        {
            elements.RemoveAll(element => IsExcluded(byPath, element.FullPath));

            foreach (UiElementModel element in elements)
            {
                if (!byPath.TryGetValue(element.FullPath, out UiKitConfig.ScanOverrideEntry entry))
                    continue;

                UiWidgetKind? kind = ParseKind(entry.widgetKind);
                if (kind.HasValue)
                    element.Widget = kind.Value;

                if (IsCounterKind(element.Widget))
                {
                    if (!string.IsNullOrEmpty(entry.counterId))
                        element.CounterId = entry.counterId;
                    else if (string.IsNullOrEmpty(element.CounterId))
                        element.CounterId = DeriveCounterId(element.Name);
                }
                else
                {
                    element.CounterId = null;
                }
            }
        }

        private static bool IsCounterKind(UiWidgetKind kind)
        {
            return kind == UiWidgetKind.Counter || kind == UiWidgetKind.Score || kind == UiWidgetKind.Level;
        }

        private static string DeriveCounterId(string elementName)
        {
            return elementName != null && elementName.StartsWith(PanelNamePrefix, StringComparison.Ordinal)
                ? elementName.Substring(PanelNamePrefix.Length)
                : elementName;
        }
    }
}
