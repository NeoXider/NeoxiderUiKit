using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Parses importer-produced UXML files into <see cref="UiPageModel"/>s. Detection is class
    /// driven (fui_type_screen/popup/button/panel/label/image/progressbar); element names are
    /// identifiers only. Duplicate names across popups are handled by scoped paths that follow
    /// the runtime resolver semantics (each segment unique within its scope).
    /// </summary>
    public static class FuiUxmlScanner
    {
        private const string ScreenClass = "fui_type_screen";
        private const string PopupClass = "fui_type_popup";
        private const string ButtonClass = "fui_type_button";
        private const string PanelClass = "fui_type_panel";
        private const string LabelClass = "fui_type_label";
        private const string ImageClass = "fui_type_image";
        private const string ProgressBarClass = "fui_type_progressbar";
        private const string ScreenIdClassPrefix = "fui_screen_";
        private const string PanelNamePrefix = "panel_";
        private const string WrapperPanelPrefix = "panel_panel_";

        private static readonly Regex GradientNumberRegex =
            new Regex("<gradient[^>]*>([^<]*)</gradient>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private sealed class Node
        {
            public string Tag;
            public string Name;
            public string Text;
            public HashSet<string> Classes;
            public Node Parent;
            public readonly List<Node> Children = new List<Node>();
            public int Depth;
            public int DocumentIndex;

            public bool HasClass(string cls)
            {
                return Classes.Contains(cls);
            }

            public IEnumerable<Node> Descendants()
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    yield return Children[i];
                    foreach (Node nested in Children[i].Descendants())
                        yield return nested;
                }
            }
        }

        /// <summary>Scans all given UXML files and returns the pages sorted by page id.</summary>
        public static UiScanResult Scan(IEnumerable<string> uxmlPaths)
        {
            var result = new UiScanResult();

            foreach (string path in uxmlPaths)
            {
                UiPageModel page = ScanFile(path, result.Warnings);
                if (page != null)
                    result.Pages.Add(page);
            }

            result.Pages.Sort((a, b) => string.CompareOrdinal(a.PageId, b.PageId));
            return result;
        }

        /// <summary>
        /// Scans a single UXML file. Returns null (with a warning) when the file contains
        /// no fui_type_screen root.
        /// </summary>
        public static UiPageModel ScanFile(string uxmlPath, List<string> warnings)
        {
            if (!File.Exists(uxmlPath))
            {
                warnings?.Add($"UXML file not found: '{uxmlPath}'.");
                return null;
            }

            var document = new XmlDocument();
            document.Load(uxmlPath);

            var counter = new int[1];
            Node root = BuildNode(document.DocumentElement, null, 0, counter);
            Node screen = FindScreen(root);
            if (screen == null)
            {
                warnings?.Add($"No '{ScreenClass}' element in '{uxmlPath}'; file skipped.");
                return null;
            }

            var page = new UiPageModel
            {
                PageId = ResolvePageId(screen),
                UxmlPath = uxmlPath.Replace('\\', '/')
            };

            Dictionary<Node, UiWidgetKind> panelWidgets = ClassifyPanels(screen, out Dictionary<Node, string> counterIds);

            var popupByRoot = new Dictionary<Node, UiPopupModel>();
            foreach (Node node in screen.Descendants())
            {
                if (!IsPopupRoot(node))
                    continue;

                if (string.IsNullOrEmpty(node.Name))
                {
                    warnings?.Add($"Unnamed popup in '{page.PageId}' skipped.");
                    continue;
                }

                var popup = new UiPopupModel
                {
                    Name = node.Name,
                    FullPath = page.PageId + "/" + node.Name
                };
                popupByRoot.Add(node, popup);
                page.Popups.Add(popup);
            }

            foreach (Node node in screen.Descendants())
            {
                if (string.IsNullOrEmpty(node.Name) || IsPopupRoot(node))
                    continue;

                Node popupRoot = FindPopupRoot(node, screen);
                if (popupRoot != null && !popupByRoot.ContainsKey(popupRoot))
                    continue;

                Node scopeRoot = popupRoot ?? screen;
                string relativePath = BuildRelativePath(node, scopeRoot, out bool ambiguous);
                if (ambiguous)
                {
                    warnings?.Add($"Element '{node.Name}' in '{page.PageId}' has no unambiguous scoped path; " +
                                  $"best effort path '{relativePath}' is used.");
                }

                string scopePrefix = popupRoot != null
                    ? page.PageId + "/" + popupRoot.Name + "/"
                    : page.PageId + "/";

                var element = new UiElementModel
                {
                    Name = node.Name,
                    Widget = ClassifyElement(node, panelWidgets),
                    RelativePath = relativePath,
                    FullPath = scopePrefix + relativePath,
                    CounterId = counterIds.TryGetValue(node, out string counterId) ? counterId : null,
                    IsService = IsService(node.Name),
                    UxmlTag = node.Tag,
                    IsAmbiguous = ambiguous
                };

                if (popupRoot != null)
                    popupByRoot[popupRoot].Elements.Add(element);
                else
                    page.Elements.Add(element);
            }

            return page;
        }

        private static Node BuildNode(XmlElement element, Node parent, int depth, int[] counter)
        {
            var node = new Node
            {
                Tag = element.LocalName,
                Name = element.GetAttribute("name"),
                Text = element.GetAttribute("text"),
                Classes = SplitClasses(element.GetAttribute("class")),
                Parent = parent,
                Depth = depth,
                DocumentIndex = counter[0]++
            };

            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement childElement && childElement.LocalName != "Style" &&
                    childElement.LocalName != "Template")
                {
                    node.Children.Add(BuildNode(childElement, node, depth + 1, counter));
                }
            }

            return node;
        }

        private static HashSet<string> SplitClasses(string classAttribute)
        {
            var classes = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(classAttribute))
            {
                foreach (string token in classAttribute.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    classes.Add(token);
            }

            return classes;
        }

        private static Node FindScreen(Node root)
        {
            if (root.HasClass(ScreenClass))
                return root;

            foreach (Node node in root.Descendants())
            {
                if (node.HasClass(ScreenClass))
                    return node;
            }

            return null;
        }

        private static string ResolvePageId(Node screen)
        {
            foreach (string cls in screen.Classes)
            {
                if (cls.StartsWith(ScreenIdClassPrefix, StringComparison.Ordinal))
                    return cls.Substring(ScreenIdClassPrefix.Length);
            }

            return screen.Name;
        }

        private static bool IsPopupRoot(Node node)
        {
            return node.HasClass(PopupClass);
        }

        private static Node FindPopupRoot(Node node, Node screen)
        {
            for (Node current = node.Parent; current != null && current != screen; current = current.Parent)
            {
                if (IsPopupRoot(current))
                    return current;
            }

            return null;
        }

        private static bool IsService(string name)
        {
            return name.StartsWith("spacer", StringComparison.Ordinal) ||
                   name.StartsWith("layout_group", StringComparison.Ordinal);
        }

        private static bool IsInsideButton(Node node, Node stopAt)
        {
            for (Node current = node.Parent; current != null && current != stopAt.Parent; current = current.Parent)
            {
                if (current.Tag == "Button")
                    return true;
            }

            return false;
        }

        private static bool HasNumericGradientText(Node node)
        {
            if (string.IsNullOrEmpty(node.Text))
                return false;

            Match match = GradientNumberRegex.Match(node.Text);
            if (!match.Success)
                return false;

            string digits = match.Groups[1].Value.Trim().Replace(" ", "");
            return digits.Length > 0 && long.TryParse(digits, out _);
        }

        private static Dictionary<Node, UiWidgetKind> ClassifyPanels(Node screen, out Dictionary<Node, string> counterIds)
        {
            var widgets = new Dictionary<Node, UiWidgetKind>();
            counterIds = new Dictionary<Node, string>();

            var panels = new List<Node>();
            foreach (Node node in screen.Descendants())
            {
                if (!string.IsNullOrEmpty(node.Name) && node.HasClass(PanelClass) && !IsPopupRoot(node) &&
                    !IsService(node.Name) && !IsInsideButton(node, screen))
                {
                    panels.Add(node);
                }
            }

            panels.Sort((a, b) => a.Depth != b.Depth ? b.Depth.CompareTo(a.Depth) : a.DocumentIndex.CompareTo(b.DocumentIndex));

            var claimed = new HashSet<Node>();
            foreach (Node panel in panels)
            {
                if (panel.Name.StartsWith(WrapperPanelPrefix, StringComparison.Ordinal))
                    continue;

                if (panel.Name.IndexOf("timer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    widgets[panel] = UiWidgetKind.Timer;
                    ClaimLabels(panel, claimed);
                    continue;
                }

                Node buyButton = FindUnclaimedPriceButton(panel, claimed);
                if (buyButton != null)
                {
                    widgets[panel] = UiWidgetKind.ShopItem;
                    claimed.Add(buyButton);
                    ClaimLabels(buyButton, claimed);
                    continue;
                }

                if (ClaimNumericLabels(panel, claimed))
                {
                    string counterId = panel.Name.StartsWith(PanelNamePrefix, StringComparison.Ordinal)
                        ? panel.Name.Substring(PanelNamePrefix.Length)
                        : panel.Name;

                    widgets[panel] = CounterKind(counterId);
                    counterIds[panel] = counterId;
                }
            }

            return widgets;
        }

        private static UiWidgetKind CounterKind(string counterId)
        {
            if (counterId.IndexOf("score", StringComparison.OrdinalIgnoreCase) >= 0)
                return UiWidgetKind.Score;
            if (counterId.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0)
                return UiWidgetKind.Level;
            return UiWidgetKind.Counter;
        }

        private static Node FindUnclaimedPriceButton(Node panel, HashSet<Node> claimed)
        {
            foreach (Node node in panel.Descendants())
            {
                if (node.Tag != "Button" || claimed.Contains(node))
                    continue;

                foreach (Node inner in node.Descendants())
                {
                    if (inner.Tag == "Label" && HasNumericGradientText(inner))
                        return node;
                }
            }

            return null;
        }

        private static bool ClaimNumericLabels(Node panel, HashSet<Node> claimed)
        {
            bool any = false;
            foreach (Node node in panel.Descendants())
            {
                if (node.Tag == "Label" && !claimed.Contains(node) && !IsInsideButton(node, panel) &&
                    HasNumericGradientText(node))
                {
                    claimed.Add(node);
                    any = true;
                }
            }

            return any;
        }

        private static void ClaimLabels(Node scope, HashSet<Node> claimed)
        {
            foreach (Node node in scope.Descendants())
            {
                if (node.Tag == "Label")
                    claimed.Add(node);
            }
        }

        private static UiWidgetKind ClassifyElement(Node node, Dictionary<Node, UiWidgetKind> panelWidgets)
        {
            if (panelWidgets.TryGetValue(node, out UiWidgetKind widget))
                return widget;

            if (node.Tag == "Button")
            {
                return node.Name.StartsWith("toggle_", StringComparison.Ordinal)
                    ? UiWidgetKind.Toggle
                    : UiWidgetKind.Button;
            }

            if (node.HasClass(ProgressBarClass))
                return UiWidgetKind.Bar;

            if (node.HasClass(LabelClass))
                return UiWidgetKind.Label;

            if (node.HasClass(ImageClass))
                return UiWidgetKind.Image;

            return UiWidgetKind.Element;
        }

        private static string BuildRelativePath(Node node, Node scopeRoot, out bool ambiguous)
        {
            var segments = new List<string> { node.Name };
            Node anchor = node;

            while (!ResolvesTo(scopeRoot, segments, node))
            {
                anchor = FindNamedAncestorBelow(anchor, scopeRoot);
                if (anchor == null)
                {
                    ambiguous = true;
                    return string.Join("/", segments);
                }

                segments.Insert(0, anchor.Name);
            }

            ambiguous = false;
            return string.Join("/", segments);
        }

        private static Node FindNamedAncestorBelow(Node node, Node scopeRoot)
        {
            for (Node current = node.Parent; current != null && current != scopeRoot; current = current.Parent)
            {
                if (!string.IsNullOrEmpty(current.Name))
                    return current;
            }

            return null;
        }

        private static bool ResolvesTo(Node scopeRoot, List<string> segments, Node target)
        {
            Node current = scopeRoot;
            for (int i = 0; i < segments.Count; i++)
            {
                Node match = null;
                int count = 0;
                foreach (Node descendant in current.Descendants())
                {
                    if (descendant.Name == segments[i])
                    {
                        match = descendant;
                        count++;
                        if (count > 1)
                            return false;
                    }
                }

                if (match == null)
                    return false;

                current = match;
            }

            return current == target;
        }
    }
}
