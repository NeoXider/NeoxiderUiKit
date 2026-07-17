using System;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Static facade of the UI Kit: pages, popups, counters, audio, flow adapters and the
    /// global click registry. All statics are reset on subsystem registration, so the kit
    /// works with domain reload disabled.
    /// </summary>
    public static class UiKit
    {
        /// <summary>The active configuration, set by <see cref="Initialize"/>.</summary>
        public static UiKitConfig Config { get; private set; }

        /// <summary>Page registry and stack.</summary>
        public static PageRouter Pages { get; private set; } = new PageRouter();

        /// <summary>Popup facade addressed by "pageId/popup_name" paths.</summary>
        public static PopupService Popups { get; private set; } = new PopupService();

        /// <summary>Global counters addressed by id, e.g. UiKit.Counters["coin"].</summary>
        public static CounterRegistry Counters { get; private set; } = new CounterRegistry();

        /// <summary>UI audio settings and click sound.</summary>
        public static UiAudio Audio { get; private set; } = new UiAudio();

        /// <summary>Game integration adapters, connected via UiKit.Flow.Connect(adapter).</summary>
        public static UiFlow Flow { get; private set; } = new UiFlow();

        internal static UiClickRegistry Clicks { get; private set; } = new UiClickRegistry();

        /// <summary>Global hook raised with the element name on every (throttled) button click.</summary>
        public static event Action<string> AnyButtonClicked;

        /// <summary>True after <see cref="Initialize"/> ran.</summary>
        public static bool IsInitialized => Config != null;

        /// <summary>Counter aliased as money in the config (defaults to id "coin").</summary>
        public static Counter Money => Counters.Define(Config != null ? Config.MoneyCounterId : "coin");

        /// <summary>
        /// Applies the configuration: defines counters and configures audio. Called by
        /// <see cref="UiKitBootstrap"/> in Awake, before the first frame.
        /// </summary>
        public static void Initialize(UiKitConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[UiKit] Initialize called with a null config.");
                return;
            }

            Config = config;

            for (int i = 0; i < config.counters.Count; i++)
            {
                if (config.counters[i] != null && !string.IsNullOrEmpty(config.counters[i].id))
                    Counters.Define(config.counters[i].id);
            }

            Audio.Configure(config);
        }

        /// <summary>Returns the registered page view of the given type (typed access to generated views).</summary>
        public static T Get<T>() where T : UiPageBase
        {
            return Pages.Get<T>();
        }

        /// <summary>
        /// Registers a click handler for an element path ("pageId/element" or
        /// "pageId/popup_x/element"). Survives reloads and may be called before pages exist;
        /// paths that cannot be resolved during a page bind are logged as errors.
        /// </summary>
        public static void OnClick(string path, Action handler)
        {
            if (handler == null)
                return;

            string normalized = path?.Trim().Trim('/');
            if (string.IsNullOrEmpty(normalized) || normalized.IndexOf('/') < 0)
            {
                Debug.LogError($"[UiKit] OnClick path '{path}' must start with a pageId, e.g. \"mainmenu/button_play\".");
                return;
            }

            bool isNewPath = Clicks.Add(normalized, handler);
            if (!isNewPath)
                return;

            string pageId = normalized.Substring(0, normalized.IndexOf('/'));
            if (Pages.TryGet(pageId, out UiPageBase page) && page.IsBound)
                page.WireRegisteredClick(normalized);
        }

        internal static void NotifyAnyButtonClicked(string elementName)
        {
            AnyButtonClicked?.Invoke(elementName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Config = null;
            Pages = new PageRouter();
            Popups = new PopupService();
            Counters = new CounterRegistry();
            Audio = new UiAudio();
            Flow = new UiFlow();
            Clicks = new UiClickRegistry();
            AnyButtonClicked = null;
            UiAnimations.ResetStatics();
        }
    }
}
