using System;
using System.Collections.Generic;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Proposes declarative button navigation from name conventions: button_close closes its popup
    /// (or pops the page), button_&lt;x&gt; opens the same-page popup_&lt;x&gt;, button_play shows
    /// gameplay, button names matching a page id show that page. Buttons without a convention
    /// (button_restart, button_buy, ...) get no proposal — inside popups they resolve through the
    /// popup result mapping instead.
    /// </summary>
    public static class UiNavigationProposer
    {
        private const string ButtonPrefix = "button_";

        /// <summary>Builds navigation proposals for every button of the scanned pages.</summary>
        public static List<UiKitConfig.NavigationEntry> Propose(UiScanResult scan)
        {
            var proposals = new List<UiKitConfig.NavigationEntry>();
            if (scan == null)
                return proposals;

            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiPageModel page in scan.Pages)
                pageIds.Add(page.PageId);

            foreach (UiPageModel page in scan.Pages)
            {
                var popupNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (UiPopupModel popup in page.Popups)
                    popupNames.Add(popup.Name);

                foreach (UiElementModel element in page.Elements)
                    Add(proposals, element, page, popupNames, pageIds, false);

                foreach (UiPopupModel popup in page.Popups)
                {
                    foreach (UiElementModel element in popup.Elements)
                        Add(proposals, element, page, popupNames, pageIds, true);
                }
            }

            return proposals;
        }

        private static void Add(List<UiKitConfig.NavigationEntry> proposals, UiElementModel element,
            UiPageModel page, HashSet<string> popupNames, HashSet<string> pageIds, bool insidePopup)
        {
            if (element.Widget != UiWidgetKind.Button)
                return;

            UiKitConfig.NavigationEntry entry = Propose(element.Name, page.PageId, popupNames, pageIds, insidePopup);
            if (entry == null)
                return;

            entry.buttonPath = element.FullPath;
            proposals.Add(entry);
        }

        private static UiKitConfig.NavigationEntry Propose(string buttonName, string pageId,
            HashSet<string> popupNames, HashSet<string> pageIds, bool insidePopup)
        {
            if (string.IsNullOrEmpty(buttonName) || !buttonName.StartsWith(ButtonPrefix, StringComparison.Ordinal))
                return null;

            string rest = buttonName.Substring(ButtonPrefix.Length);

            if (rest == "close" || rest == "back")
            {
                return insidePopup
                    ? Entry(UiNavigationAction.ClosePopup, "")
                    : Entry(UiNavigationAction.Pop, "");
            }

            if (rest == "play" && pageIds.Contains("gameplay"))
                return Entry(UiNavigationAction.Show, "gameplay");

            if (popupNames.Contains("popup_" + rest))
                return Entry(UiNavigationAction.OpenPopup, pageId + "/popup_" + rest);

            string target = MatchPageId(rest, pageIds);
            if (target != null)
                return Entry(UiNavigationAction.Show, target);

            return null;
        }

        private static string MatchPageId(string rest, HashSet<string> pageIds)
        {
            if (pageIds.Contains(rest))
                return rest;

            string collapsed = rest.Replace("_", "");
            foreach (string pageId in pageIds)
            {
                if (string.Equals(pageId.Replace("_", ""), collapsed, StringComparison.OrdinalIgnoreCase))
                    return pageId;
            }

            return null;
        }

        private static UiKitConfig.NavigationEntry Entry(UiNavigationAction action, string targetId)
        {
            return new UiKitConfig.NavigationEntry { action = action, targetId = targetId };
        }
    }
}
