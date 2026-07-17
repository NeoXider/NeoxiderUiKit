using System.Collections.Generic;

namespace Neo.UIKit.Editor
{
    /// <summary>Widget kind recognized by the scanner for a named UXML element.</summary>
    public enum UiWidgetKind
    {
        /// <summary>Plain VisualElement fallback (panels, unrecognized elements).</summary>
        Element = 0,
        Button,
        Toggle,
        Label,
        Image,
        Counter,
        Score,
        Level,
        Timer,
        Bar,
        ShopItem
    }

    /// <summary>A named element found on a page or inside a popup.</summary>
    public sealed class UiElementModel
    {
        /// <summary>Raw UXML element name.</summary>
        public string Name;

        /// <summary>Widget kind recognized for this element.</summary>
        public UiWidgetKind Widget;

        /// <summary>Query path relative to its scope root (page or popup); may contain '/'.</summary>
        public string RelativePath;

        /// <summary>Full addressable path, e.g. "gameplay/popup_pause/button_restart".</summary>
        public string FullPath;

        /// <summary>Counter id for Counter/Score/Level widgets, otherwise null.</summary>
        public string CounterId;

        /// <summary>True for auto-numbered layout helpers (spacer_*, layout_group_*).</summary>
        public bool IsService;

        /// <summary>UXML tag (VisualElement, Button, Label, ...).</summary>
        public string UxmlTag;

        /// <summary>True when no unambiguous scoped path could be built for the element.</summary>
        public bool IsAmbiguous;
    }

    /// <summary>A popup (fui_type_popup) and its scoped elements.</summary>
    public sealed class UiPopupModel
    {
        /// <summary>Popup element name, e.g. "popup_pause".</summary>
        public string Name;

        /// <summary>Popup path "pageId/popupName".</summary>
        public string FullPath;

        /// <summary>Named elements inside the popup, in document order.</summary>
        public List<UiElementModel> Elements = new List<UiElementModel>();
    }

    /// <summary>A screen page (fui_type_screen) parsed from one UXML file.</summary>
    public sealed class UiPageModel
    {
        /// <summary>Page id from the fui_screen_* class (falls back to the root element name).</summary>
        public string PageId;

        /// <summary>Source UXML path.</summary>
        public string UxmlPath;

        /// <summary>Named screen-scope elements (popup content excluded), in document order.</summary>
        public List<UiElementModel> Elements = new List<UiElementModel>();

        /// <summary>Popups of the page, in document order.</summary>
        public List<UiPopupModel> Popups = new List<UiPopupModel>();
    }

    /// <summary>Result of scanning a set of UXML files.</summary>
    public sealed class UiScanResult
    {
        /// <summary>Pages found, sorted by page id.</summary>
        public List<UiPageModel> Pages = new List<UiPageModel>();

        /// <summary>Non-fatal scan warnings (ambiguous names, files without screens, ...).</summary>
        public List<string> Warnings = new List<string>();
    }
}
