using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Synchronizes the <see cref="UiKitConfig"/> asset with a scan/generation result: page entries
    /// (uxml + PanelSettings references, start page), counter id-to-paths mappings, navigation
    /// proposals and popup result mappings. User-edited entries are never overwritten; only
    /// missing entries are added and scanned paths refreshed.
    /// </summary>
    public static class UiKitConfigUpdater
    {
        private const string UikitUssGuid = "7ecbf43899407c54c92fd000d8cd122e";
        private const string UikitUssPath = "Packages/com.neoxider.uikit/Runtime/Styles/uikit.uss";
        private const string ButtonPrefix = "button_";

        /// <summary>
        /// Applies the scan result to the config and returns a human-readable list of changes.
        /// </summary>
        public static List<string> Update(UiKitConfig config, UiScanResult scan)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (scan == null)
                throw new ArgumentNullException(nameof(scan));

            var report = new List<string>();

            UpdatePages(config, scan, report);
            UpdateCounters(config, scan, report);
            UpdateNavigation(config, scan, report);
            UpdatePopupResults(config, scan, report);
            UpdateStyleSheet(config, report);
            UpdateBackgroundLayer(config, scan, report);
            UpdateFlow(config, scan, report);

            EditorUtility.SetDirty(config);
            return report;
        }

        private static void UpdatePages(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            foreach (UiPageModel page in scan.Pages)
            {
                UiKitConfig.PageEntry entry = config.GetPage(page.PageId);
                if (entry == null)
                {
                    entry = new UiKitConfig.PageEntry { pageId = page.PageId };
                    ApplyPageDefaults(entry);
                    config.pages.Add(entry);
                    report.Add($"+ page '{page.PageId}'");
                }

                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(page.UxmlPath);
                if (uxml != null && entry.visualTreeAsset != uxml)
                {
                    entry.visualTreeAsset = uxml;
                    report.Add($"~ page '{page.PageId}': uxml -> {page.UxmlPath}");
                }

                if (entry.panelSettings == null)
                {
                    entry.panelSettings = FindPanelSettings(page.UxmlPath);
                    if (entry.panelSettings != null)
                        report.Add($"~ page '{page.PageId}': PanelSettings -> {entry.panelSettings.name}");
                }
            }

            var scannedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiPageModel page in scan.Pages)
                scannedIds.Add(page.PageId);

            foreach (UiKitConfig.PageEntry entry in config.pages)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.pageId) && !scannedIds.Contains(entry.pageId))
                    report.Add($"! page '{entry.pageId}' is in the config but missing from the scan");
            }

            EnsureStartPage(config, report);
        }

        /// <summary>
        /// Recommended per-page defaults for a clean dissolve: the incoming page appears instantly
        /// (its own opaque background is present from the first frame) and the outgoing page fades
        /// out on top of it — so one page dissolves into the next with no gap or flash.
        /// </summary>
        private static void ApplyPageDefaults(UiKitConfig.PageEntry entry)
        {
            entry.showPreset = "none";
            entry.hidePreset = "fade";
            entry.animationMode = UiAnimationMode.BackwardOnly;
        }

        private static void EnsureStartPage(UiKitConfig config, List<string> report)
        {
            foreach (UiKitConfig.PageEntry entry in config.pages)
            {
                if (entry != null && entry.isStart)
                    return;
            }

            UiKitConfig.PageEntry start = config.GetPage("loading") ??
                                          config.GetPage("mainmenu") ??
                                          (config.pages.Count > 0 ? config.pages[0] : null);
            if (start != null)
            {
                start.isStart = true;
                report.Add($"~ start page -> '{start.pageId}'");
            }
        }

        private static PanelSettings FindPanelSettings(string uxmlPath)
        {
            string folder = Path.GetDirectoryName(uxmlPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                string[] guids = AssetDatabase.FindAssets("t:PanelSettings", new[] { folder });
                if (guids.Length > 0)
                    return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));

                if (folder == "Assets")
                    break;
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }

            return null;
        }

        private static void UpdateCounters(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            var scanned = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var scannedPageIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (UiPageModel page in scan.Pages)
            {
                scannedPageIds.Add(page.PageId);
                foreach (UiElementModel element in AllElements(page))
                {
                    if (string.IsNullOrEmpty(element.CounterId))
                        continue;

                    if (!scanned.TryGetValue(element.CounterId, out List<string> paths))
                        scanned[element.CounterId] = paths = new List<string>();
                    paths.Add(element.FullPath);
                }
            }

            foreach (KeyValuePair<string, List<string>> counter in scanned)
            {
                UiKitConfig.CounterEntry entry = FindCounter(config, counter.Key);
                if (entry == null)
                {
                    entry = new UiKitConfig.CounterEntry { id = counter.Key };
                    config.counters.Add(entry);
                    report.Add($"+ counter '{counter.Key}'");
                }

                // Refresh scanned-page paths, keep manual paths pointing at pages outside this scan.
                var merged = new List<string>(counter.Value);
                foreach (string path in entry.paths)
                {
                    int slash = string.IsNullOrEmpty(path) ? -1 : path.IndexOf('/');
                    if (slash > 0 && !scannedPageIds.Contains(path.Substring(0, slash)) && !merged.Contains(path))
                        merged.Add(path);
                }

                if (!merged.TrueForAll(entry.paths.Contains) || entry.paths.Count != merged.Count)
                    report.Add($"~ counter '{counter.Key}': {merged.Count} path(s)");

                entry.paths = merged;
            }

            ApplyMoneyAlias(config, report);
        }

        private static void ApplyMoneyAlias(UiKitConfig config, List<string> report)
        {
            string moneyId = string.IsNullOrEmpty(config.generatorMoneyCounterId) ? "coin" : config.generatorMoneyCounterId;

            bool changed = false;
            foreach (UiKitConfig.CounterEntry entry in config.counters)
            {
                if (entry == null)
                    continue;

                bool alias = entry.id == moneyId;
                if (entry.moneyAlias != alias)
                {
                    entry.moneyAlias = alias;
                    changed = true;
                }
            }

            if (changed)
                report.Add($"~ money alias -> '{moneyId}'");
        }

        private static UiKitConfig.CounterEntry FindCounter(UiKitConfig config, string id)
        {
            foreach (UiKitConfig.CounterEntry entry in config.counters)
            {
                if (entry != null && entry.id == id)
                    return entry;
            }

            return null;
        }

        private static void UpdateNavigation(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiKitConfig.NavigationEntry entry in config.navigation)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.buttonPath))
                    known.Add(entry.buttonPath);
            }

            foreach (UiKitConfig.NavigationEntry proposal in UiNavigationProposer.Propose(scan))
            {
                if (!known.Add(proposal.buttonPath))
                    continue;

                config.navigation.Add(proposal);
                report.Add($"+ navigation '{proposal.buttonPath}' -> {proposal.action} {proposal.targetId}".TrimEnd());
            }
        }

        private static void UpdatePopupResults(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiKitConfig.PopupResultEntry entry in config.popupResults)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.buttonPath))
                    known.Add(entry.buttonPath);
            }

            foreach (UiPageModel page in scan.Pages)
            {
                foreach (UiPopupModel popup in page.Popups)
                {
                    foreach (UiElementModel element in popup.Elements)
                    {
                        if (element.Widget != UiWidgetKind.Button ||
                            !element.Name.StartsWith(ButtonPrefix, StringComparison.Ordinal))
                            continue;

                        // PopupView resolves results by "popupPath/buttonName".
                        string buttonPath = popup.FullPath + "/" + element.Name;
                        if (!known.Add(buttonPath))
                            continue;

                        config.popupResults.Add(new UiKitConfig.PopupResultEntry
                        {
                            buttonPath = buttonPath,
                            result = element.Name.Substring(ButtonPrefix.Length)
                        });
                        report.Add($"+ popup result '{buttonPath}' -> \"{element.Name.Substring(ButtonPrefix.Length)}\"");
                    }
                }
            }
        }

        private static void UpdateStyleSheet(UiKitConfig config, List<string> report)
        {
            if (config.uikitStyleSheet != null)
                return;

            string path = AssetDatabase.GUIDToAssetPath(UikitUssGuid);
            if (string.IsNullOrEmpty(path))
                path = UikitUssPath;

            config.uikitStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (config.uikitStyleSheet != null)
                report.Add("~ uikit.uss reference assigned");
        }

        /// <summary>
        /// Per-page backgrounds (add-only). A page that already has a screen-level "background"
        /// image in its UXML (e.g. loading) renders its own and is left untouched. A transparent
        /// menu-like page gets the design's scenic background so it isn't empty; game pages (id
        /// starting with "game") are left transparent so the game world shows through.
        /// </summary>
        private static void UpdateBackgroundLayer(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            Sprite scenic = FindScenicBackground(scan);
            if (scenic == null)
                return;

            var urlRegex = new System.Text.RegularExpressions.Regex(
                "name=\"background\"[^>]*background-image:\\s*url\\(&quot;([^&]+)&quot;\\)");

            foreach (UiPageModel page in scan.Pages)
            {
                UiKitConfig.PageEntry entry = config.GetPage(page.PageId);
                if (entry == null || entry.backgroundSprite != null)
                    continue;

                if (page.PageId.StartsWith("game", StringComparison.OrdinalIgnoreCase))
                    continue; // game world renders behind a transparent screen

                if (HasScreenBackground(page, urlRegex))
                    continue; // renders its own background from the UXML

                entry.backgroundSprite = scenic;
                report.Add($"+ page '{page.PageId}': background -> {scenic.name}");
            }
        }

        /// <summary>Finds the design's scenic backdrop: the most frequent screen-level "background" image.</summary>
        private static Sprite FindScenicBackground(UiScanResult scan)
        {
            var urlRegex = new System.Text.RegularExpressions.Regex(
                "name=\"background\"[^>]*background-image:\\s*url\\(&quot;([^&]+)&quot;\\)");
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (UiPageModel page in scan.Pages)
            {
                if (string.IsNullOrEmpty(page.UxmlPath) || !System.IO.File.Exists(page.UxmlPath))
                    continue;

                string text = System.IO.File.ReadAllText(page.UxmlPath);
                int firstPopup = text.IndexOf("fui_type_popup", StringComparison.Ordinal);
                foreach (System.Text.RegularExpressions.Match match in urlRegex.Matches(text))
                {
                    if (firstPopup >= 0 && match.Index > firstPopup)
                        continue; // popup "background" = dim overlay, not scenic
                    string url = match.Groups[1].Value;
                    counts.TryGetValue(url, out int count);
                    counts[url] = count + 1;
                }
            }

            string best = null;
            int bestCount = 0;
            foreach (var pair in counts)
            {
                if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            }

            return best != null ? AssetDatabase.LoadAssetAtPath<Sprite>(ToAssetPath(best)) : null;
        }

        private static bool HasScreenBackground(UiPageModel page, System.Text.RegularExpressions.Regex urlRegex)
        {
            if (string.IsNullOrEmpty(page.UxmlPath) || !System.IO.File.Exists(page.UxmlPath))
                return false;

            string text = System.IO.File.ReadAllText(page.UxmlPath);
            int firstPopup = text.IndexOf("fui_type_popup", StringComparison.Ordinal);
            foreach (System.Text.RegularExpressions.Match match in urlRegex.Matches(text))
            {
                if (firstPopup < 0 || match.Index < firstPopup)
                    return true;
            }

            return false;
        }

        private static string ToAssetPath(string url)
        {
            const string prefix = "project://database/";
            string assetPath = url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? url.Substring(prefix.Length) : url;
            int query = assetPath.IndexOf('?');
            return query >= 0 ? assetPath.Substring(0, query) : assetPath;
        }

        /// <summary>
        /// Proposes flow mappings from name conventions (add-only): Win/Lose/GameEnd open the
        /// matching popup (popup_win / popup_lose / popup_endgame) or show the matching page
        /// (win / lose); Pause opens popup_pause; Menu shows mainmenu.
        /// </summary>
        private static void UpdateFlow(UiKitConfig config, UiScanResult scan, List<string> report)
        {
            var popupPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiPageModel page in scan.Pages)
            {
                pageIds.Add(page.PageId);
                foreach (UiPopupModel popup in page.Popups)
                {
                    if (!popupPaths.ContainsKey(popup.Name))
                        popupPaths.Add(popup.Name, page.PageId + "/" + popup.Name);
                }
            }

            void Propose(UiGameMoment moment, string popupName, string pageName)
            {
                foreach (UiKitConfig.FlowEntry existing in config.flow)
                {
                    if (existing != null && existing.moment == moment)
                        return;
                }

                UiKitConfig.FlowEntry entry = null;
                if (popupName != null && popupPaths.TryGetValue(popupName, out string path))
                    entry = new UiKitConfig.FlowEntry { moment = moment, action = UiNavigationAction.OpenPopup, targetId = path };
                else if (pageName != null && pageIds.Contains(pageName))
                    entry = new UiKitConfig.FlowEntry { moment = moment, action = UiNavigationAction.Show, targetId = pageName };

                if (entry != null)
                {
                    config.flow.Add(entry);
                    report.Add($"+ flow {moment} -> {entry.action} '{entry.targetId}'");
                }
            }

            Propose(UiGameMoment.Win, popupPaths.ContainsKey("popup_win") ? "popup_win" : "popup_endgame", "win");
            Propose(UiGameMoment.Lose, popupPaths.ContainsKey("popup_lose") ? "popup_lose" : "popup_endgame", "lose");
            Propose(UiGameMoment.GameEnd, "popup_endgame", null);
            Propose(UiGameMoment.Pause, "popup_pause", null);
            Propose(UiGameMoment.Menu, null, "mainmenu");
        }

        private static IEnumerable<UiElementModel> AllElements(UiPageModel page)
        {
            foreach (UiElementModel element in page.Elements)
                yield return element;

            foreach (UiPopupModel popup in page.Popups)
            {
                foreach (UiElementModel element in popup.Elements)
                    yield return element;
            }
        }
    }
}
