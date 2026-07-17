using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Per-page/per-popup animation mode (which directions of the transition are animated).
    /// </summary>
    public enum UiAnimationMode
    {
        ForwardAndBackward = 0,
        ForwardOnly = 1,
        BackwardOnly = 2,
        None = 3
    }

    /// <summary>
    /// Declarative page/popup action used by button navigation and flow mappings.
    /// </summary>
    public enum UiNavigationAction
    {
        None = 0,
        Show = 1,
        Push = 2,
        Pop = 3,
        OpenPopup = 4,
        ClosePopup = 5
    }

    /// <summary>
    /// Game moments delivered by <see cref="IUiFlowSource"/> and mapped to page actions in the config.
    /// </summary>
    public enum UiGameMoment
    {
        Win = 0,
        Lose = 1,
        Pause = 2,
        Resume = 3,
        Menu = 4,
        GameStart = 5,
        GameEnd = 6
    }

    /// <summary>
    /// Project-side configuration asset of the UI Kit: pages, counters, declarative navigation,
    /// popup results, stylesheets, audio and flow mappings.
    /// </summary>
    [CreateAssetMenu(menuName = "Neoxider/UiKit Config", fileName = "UiKitConfig")]
    public sealed class UiKitConfig : ScriptableObject
    {
        /// <summary>Configuration of a single screen page.</summary>
        [Serializable]
        public sealed class PageEntry
        {
            public string pageId;
            public VisualTreeAsset visualTreeAsset;
            public PanelSettings panelSettings;
            public int sortingOrderBase;
            [Tooltip("Show animation preset name (fade, slide-up, slide-down, slide-left, slide-right, scale, none).")]
            public string showPreset = "fade";
            [Tooltip("Optional hide preset; empty = reverse of the show preset.")]
            public string hidePreset = "";
            [Tooltip("Animation preset applied to popups of this page (scale-pop, fade, slide-up, none).")]
            public string popupPreset = "scale-pop";
            public UiAnimationMode animationMode = UiAnimationMode.ForwardAndBackward;
            public bool isStart;
            [Tooltip("Optional path (relative to the page) of the top bar container that gets the intro animation.")]
            public string topBarPath = "";
        }

        /// <summary>A global counter and the element paths it is displayed at.</summary>
        [Serializable]
        public sealed class CounterEntry
        {
            public string id;
            [Tooltip("Element paths like \"gameplay/panel_coin\" or \"mainmenu/popup_x/panel_coin\".")]
            public List<string> paths = new List<string>();
            [Tooltip("When set, UiKit.Money resolves to this counter.")]
            public bool moneyAlias;
        }

        /// <summary>Declarative button-to-navigation mapping applied during page bind.</summary>
        [Serializable]
        public sealed class NavigationEntry
        {
            [Tooltip("Full button path like \"mainmenu/button_play\" or \"gameplay/popup_pause/button_restart\".")]
            public string buttonPath;
            public UiNavigationAction action;
            [Tooltip("Target page id or popup path; may be empty for Pop/ClosePopup.")]
            public string targetId;
        }

        /// <summary>Override of the result string returned by PopupView.OpenAsync for a specific button.</summary>
        [Serializable]
        public sealed class PopupResultEntry
        {
            [Tooltip("Full button path like \"gameplay/popup_endgame/button_restart\".")]
            public string buttonPath;
            public string result;
        }

        /// <summary>Mapping of a game moment to a declarative page action.</summary>
        [Serializable]
        public sealed class FlowEntry
        {
            public UiGameMoment moment;
            public UiNavigationAction action;
            public string targetId;
        }

        /// <summary>
        /// Scanner override for one element, edited in the UI Kit window. Overrides live in the
        /// config so they survive design reimports and regeneration.
        /// </summary>
        [Serializable]
        public sealed class ScanOverrideEntry
        {
            [Tooltip("Full element path (\"gameplay/popup_pause/button_restart\"), popup path or page id.")]
            public string elementPath;
            [Tooltip("When true, the element/popup/page is excluded from generation.")]
            public bool excluded;
            [Tooltip("Widget kind name (Button, Label, Counter, ...); empty keeps the scanned kind.")]
            public string widgetKind = "";
            [Tooltip("Counter id override for Counter/Score/Level widgets; empty keeps the derived id.")]
            public string counterId = "";
        }

        [Header("Pages")]
        public List<PageEntry> pages = new List<PageEntry>();

        [Header("Counters")]
        public List<CounterEntry> counters = new List<CounterEntry>();

        [Header("Button navigation")]
        public List<NavigationEntry> navigation = new List<NavigationEntry>();

        [Header("Popup results (OpenAsync overrides)")]
        public List<PopupResultEntry> popupResults = new List<PopupResultEntry>();

        [Header("Buttons")]
        [Tooltip("Press animation preset applied to every button: scale (default), sink, pop, none.")]
        public string buttonPressPreset = "scale";

        [Header("Background layer")]
        [Tooltip("Always-on backdrop sprite rendered behind the game world and the UI; auto-picked from the design's background image on generation.")]
        public Sprite backgroundSprite;
        [Tooltip("Sorting order of the backdrop sprite.")]
        public int backgroundSortingOrder = -1000;

        [Header("Styles")]
        public StyleSheet uikitStyleSheet;
        [Tooltip("Optional project stylesheet applied after uikit.uss to override timings/curves without forking the package.")]
        public StyleSheet projectOverrideStyleSheet;

        [Header("Audio")]
        public AudioClip clickSound;
        public AudioMixer audioMixer;
        [Tooltip("Exposed AudioMixer parameter for music volume; muted at -80 dB.")]
        public string musicParam = "";
        [Tooltip("Exposed AudioMixer parameter for sound volume; muted at -80 dB.")]
        public string soundParam = "";
        [Tooltip("Optional exposed master parameter; muted only when both sound and music are off.")]
        public string masterParam = "";

        [Header("Generator (editor-only)")]
        [Tooltip("FUI project folder scanned for UXML screens, e.g. \"Assets/MY_GAME_DESIGN\".")]
        public string fuiProjectFolder = "";
        [Tooltip("Namespace of the generated views.")]
        public string generatorNamespace = "Game.Ui";
        [Tooltip("Root output folder of generated code, views and docs.")]
        public string generatorOutputRoot = "Assets/UiKit";
        [Tooltip("When true, spacer_*/layout_group_* elements also become generated fields.")]
        public bool includeServiceElements;
        [Tooltip("Counter id aliased as UiKit.Money on the next generation.")]
        public string generatorMoneyCounterId = "coin";
        [Tooltip("Per-element scanner overrides edited in the UI Kit window; survive reimports.")]
        public List<ScanOverrideEntry> scanOverrides = new List<ScanOverrideEntry>();

        [Header("Flow")]
        [Tooltip("When no IUiFlowSource adapter is connected, toggle Time.timeScale 0/1 on pause popup open/close.")]
        public bool manageTimeScale = true;
        [Tooltip("Explicit pause popup path (e.g. \"gameplay/popup_pause\"); empty = any popup whose name contains \"pause\".")]
        public string pausePopupPath = "";
        public List<FlowEntry> flow = new List<FlowEntry>();

        /// <summary>Finds the page entry for the given page id, or null.</summary>
        public PageEntry GetPage(string pageId)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null && pages[i].pageId == pageId)
                    return pages[i];
            }

            return null;
        }

        /// <summary>Counter id aliased as <see cref="UiKit.Money"/>; defaults to "coin".</summary>
        public string MoneyCounterId
        {
            get
            {
                for (int i = 0; i < counters.Count; i++)
                {
                    if (counters[i] != null && counters[i].moneyAlias && !string.IsNullOrEmpty(counters[i].id))
                        return counters[i].id;
                }

                return "coin";
            }
        }
    }
}
