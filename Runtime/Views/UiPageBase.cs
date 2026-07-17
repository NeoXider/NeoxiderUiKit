using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Base MonoBehaviour of a screen page. Owns the <see cref="PanelRenderer"/> on the same
    /// GameObject and follows the mandatory reload lifecycle: OnEnable registers the UI reload
    /// callback, OnDisable unregisters and unwires; every query happens inside the bind pass.
    /// During bind (before the first frame) it attaches uikit.uss, hides all popups, applies the
    /// OnClick registry, counter values and declarative button navigation.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public abstract class UiPageBase : MonoBehaviour
    {
        [SerializeField] private string pageId;
        [SerializeField] private PanelRenderer panelRenderer;

        private readonly Dictionary<string, PopupView> _popups = new Dictionary<string, PopupView>(StringComparer.Ordinal);
        private readonly Dictionary<VisualElement, ButtonView> _autoButtons = new Dictionary<VisualElement, ButtonView>();
        private readonly List<CounterView> _autoCounters = new List<CounterView>();
        private readonly List<(VisualElement element, EventCallback<ClickEvent> callback)> _rawClicks =
            new List<(VisualElement, EventCallback<ClickEvent>)>();

        private bool _pendingShow;
        private bool _isShown;
        private VisualElement _lastReloadRoot;
        private readonly List<VisualElement> _cascadeItems = new List<VisualElement>();
        private IVisualElementScheduledItem _cascadeSchedule;

        // The fake-loading page animates its bar instead of cascading its elements.
        private bool PageCascadeEnabled =>
            UiKit.Config != null && UiKit.Config.pageCascadeEnabled && GetComponent<UiFakeLoading>() == null;

        /// <summary>Page id used by the router and element paths.</summary>
        public string PageId => pageId;

        /// <summary>The owned panel renderer.</summary>
        public PanelRenderer PanelRenderer => panelRenderer;

        /// <summary>Panel root element of the current visual tree; valid only while bound.</summary>
        public VisualElement Root { get; private set; }

        /// <summary>The screen element (class "fui_type_screen"), animation classes live here.</summary>
        public VisualElement ScreenRoot { get; private set; }

        /// <summary>True while a visual tree is bound.</summary>
        public bool IsBound { get; private set; }

        /// <summary>Popups discovered on this page (elements with class "fui_type_popup"), keyed by name.</summary>
        public IReadOnlyDictionary<string, PopupView> Popups => _popups;

        /// <summary>Raised after every completed bind (extension point, e.g. for localization).</summary>
        public event Action Bound;

        private UiKitConfig.PageEntry ConfigEntry => UiKit.Config != null ? UiKit.Config.GetPage(pageId) : null;

        private string ShowPreset => ConfigEntry?.showPreset ?? "fade";
        private string HidePreset => ConfigEntry?.hidePreset;
        private string PopupPreset => ConfigEntry?.popupPreset ?? "scale-pop";

        /// <summary>Animation mode configured for this page.</summary>
        public UiAnimationMode AnimationMode => ConfigEntry?.animationMode ?? UiAnimationMode.ForwardAndBackward;

        protected virtual void Reset()
        {
            panelRenderer = GetComponent<PanelRenderer>();
        }

        protected virtual void Awake()
        {
            UiKit.Pages.Register(this);
        }

        protected virtual void OnEnable()
        {
            if (panelRenderer == null)
                panelRenderer = GetComponent<PanelRenderer>();

            ApplyConfiguredAssets();
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
            StartCoroutine(EnsureBoundNextFrame());
        }

        /// <summary>
        /// PanelRenderer fires the reload callback only when it (re)creates its visual tree; on
        /// re-enable the previous tree survives and no callback arrives. When no bind happened
        /// within a frame, re-bind the cached root if it is still attached to a panel, otherwise
        /// toggle the renderer to force a fresh reload.
        /// </summary>
        private System.Collections.IEnumerator EnsureBoundNextFrame()
        {
            yield return null;
            if (IsBound || panelRenderer == null || !isActiveAndEnabled)
                yield break;

            if (_lastReloadRoot != null && _lastReloadRoot.panel != null)
            {
                Root = _lastReloadRoot;
                BindInternal(_lastReloadRoot);
                yield break;
            }

            panelRenderer.enabled = false;
            panelRenderer.enabled = true;
        }

        protected virtual void OnDisable()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            Unwire();
            Root = null;
            ScreenRoot = null;
            IsBound = false;
        }

        private void ApplyConfiguredAssets()
        {
            UiKitConfig.PageEntry entry = ConfigEntry;
            if (entry == null)
                return;

            if (panelRenderer.visualTreeAsset == null && entry.visualTreeAsset != null)
                panelRenderer.visualTreeAsset = entry.visualTreeAsset;
            if (panelRenderer.panelSettings == null && entry.panelSettings != null)
                panelRenderer.panelSettings = entry.panelSettings;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root)
        {
            Unwire();
            Root = root;
            _lastReloadRoot = root;
            BindInternal(root);
        }

        private void BindInternal(VisualElement root)
        {
            IsBound = true;
            ScreenRoot = root.Q<VisualElement>(className: "fui_type_screen") ?? root;

            AttachStyleSheets(root);

            ScreenRoot.AddToClassList("uikit-page");
            UiAnimations.ApplyPresetClass(ScreenRoot, ShowPreset);
            if (_isShown)
                ScreenRoot.AddToClassList(UiAnimations.OpenClass);

            ApplyTopBar();
            BindPopups();
            BindAutoButtons();
            BindConventionAudioToggles();

            BindUi(root);

            ApplyClickRegistry();
            BindCounters();
            ApplyNavigation();

            OnBind();
            Bound?.Invoke();

            _cascadeItems.Clear();
            if (PageCascadeEnabled)
            {
                _cascadeItems.AddRange(UiCascade.CollectPageItems(ScreenRoot));
                // Keep them hidden until Show; if already shown (rebind), reveal immediately.
                UiCascade.SetHidden(_cascadeItems, !_isShown);
            }

            if (_pendingShow)
            {
                _pendingShow = false;
                DoShow();
            }
        }

        /// <summary>Removes all subscriptions and cached elements. Idempotent; called on every reload and disable.</summary>
        public void Unwire()
        {
            foreach (var pair in _rawClicks)
                pair.element?.UnregisterCallback(pair.callback);
            _rawClicks.Clear();

            for (int i = 0; i < _autoCounters.Count; i++)
                _autoCounters[i].Unwire();
            _autoCounters.Clear();

            foreach (ButtonView button in _autoButtons.Values)
                button.Unwire();
            _autoButtons.Clear();

            foreach (PopupView popup in _popups.Values)
                popup.Unwire();

            _cascadeSchedule?.Pause();
            _cascadeSchedule = null;
            _cascadeItems.Clear();
            _pageAnimSchedule?.Pause();
            _pageAnimSchedule = null;

            UnwireAudioToggles();
            UnwireUi();
            OnUnwire();
            IsBound = false;
        }

        private void AttachStyleSheets(VisualElement root)
        {
            UiKitConfig config = UiKit.Config;
            if (config == null)
                return;

            if (config.uikitStyleSheet != null && !root.styleSheets.Contains(config.uikitStyleSheet))
                root.styleSheets.Add(config.uikitStyleSheet);
            if (config.projectOverrideStyleSheet != null && !root.styleSheets.Contains(config.projectOverrideStyleSheet))
                root.styleSheets.Add(config.projectOverrideStyleSheet);
        }

        private void ApplyTopBar()
        {
            string topBarPath = ConfigEntry?.topBarPath;
            if (string.IsNullOrEmpty(topBarPath))
                return;

            VisualElement topBar = UiElementResolver.Resolve(ScreenRoot, pageId, topBarPath, out string error);
            if (topBar != null)
                topBar.AddToClassList("uikit-topbar");
            else
                Debug.LogWarning(error, this);
        }

        private void BindPopups()
        {
            foreach (VisualElement element in ScreenRoot.Query<VisualElement>(className: "fui_type_popup").ToList())
            {
                string popupName = element.name;
                if (string.IsNullOrEmpty(popupName))
                    continue;

                if (!_popups.TryGetValue(popupName, out PopupView popup))
                {
                    popup = CreatePopupView(popupName) ?? new PopupView();
                    _popups.Add(popupName, popup);
                }

                popup.Initialize(this, popupName, PopupPreset, UiAnimationMode.ForwardAndBackward);
                popup.Bind(element);
            }
        }

        /// <summary>Factory hook allowing generated pages to substitute typed popup views.</summary>
        protected virtual PopupView CreatePopupView(string popupName)
        {
            return null;
        }

        private void BindAutoButtons()
        {
            string pressPreset = UiKit.Config != null ? UiKit.Config.buttonPressPreset : null;
            bool customPress = !string.IsNullOrEmpty(pressPreset) && pressPreset != "scale";

            foreach (Button element in ScreenRoot.Query<Button>().ToList())
            {
                if (customPress)
                    element.AddToClassList("uikit-press-" + pressPreset);

                var view = new ButtonView();
                view.Bind(element);
                _autoButtons[element] = view;
            }
        }

        private static readonly string[] SoundToggleNames = { "toggle_sound", "button_sound" };
        private static readonly string[] MusicToggleNames = { "toggle_music", "button_music" };

        private readonly List<ToggleView> _audioToggles = new List<ToggleView>();
        private Action<bool> _soundSync;
        private Action<bool> _musicSync;

        /// <summary>
        /// Name-convention audio toggles: elements called toggle_sound/button_sound and
        /// toggle_music/button_music anywhere on the page become ToggleViews bound to
        /// <see cref="UiKit.Audio"/> in both directions.
        /// </summary>
        private void BindConventionAudioToggles()
        {
            BindAudioToggleGroup(SoundToggleNames, () => UiKit.Audio.SoundOn, v => UiKit.Audio.SoundOn = v, ref _soundSync, h => UiKit.Audio.SoundChanged += h);
            BindAudioToggleGroup(MusicToggleNames, () => UiKit.Audio.MusicOn, v => UiKit.Audio.MusicOn = v, ref _musicSync, h => UiKit.Audio.MusicChanged += h);
        }

        private void BindAudioToggleGroup(string[] names, Func<bool> get, Action<bool> set,
            ref Action<bool> syncHandler, Action<Action<bool>> subscribe)
        {
            var views = new List<ToggleView>();
            foreach (string toggleName in names)
            {
                foreach (VisualElement element in ScreenRoot.Query<VisualElement>(name: toggleName).ToList())
                {
                    element.AddToClassList("uikit-toggle");
                    var view = new ToggleView();
                    view.Bind(element);
                    view.SetWithoutNotify(get());
                    view.Changed += set;
                    views.Add(view);
                    _audioToggles.Add(view);
                }
            }

            if (views.Count == 0)
                return;

            Action<bool> handler = value =>
            {
                for (int i = 0; i < views.Count; i++)
                    views[i].SetWithoutNotify(value);
            };
            syncHandler = handler;
            subscribe(handler);
        }

        private void UnwireAudioToggles()
        {
            for (int i = 0; i < _audioToggles.Count; i++)
                _audioToggles[i].Unwire();
            _audioToggles.Clear();

            if (_soundSync != null)
            {
                UiKit.Audio.SoundChanged -= _soundSync;
                _soundSync = null;
            }

            if (_musicSync != null)
            {
                UiKit.Audio.MusicChanged -= _musicSync;
                _musicSync = null;
            }
        }

        private void ApplyClickRegistry()
        {
            foreach (string fullPath in UiKit.Clicks.PathsForPage(pageId))
                WireRegisteredClick(fullPath);
        }

        internal void WireRegisteredClick(string fullPath)
        {
            string relative = fullPath.Substring(pageId.Length + 1);
            VisualElement element = UiElementResolver.Resolve(ScreenRoot, pageId, relative, out string error);
            if (element == null)
            {
                Debug.LogError($"[UiKit] OnClick path '{fullPath}' could not be resolved. {error}", this);
                return;
            }

            string captured = fullPath;
            WireClickTo(element, () => UiKit.Clicks.Invoke(captured));
        }

        private void BindCounters()
        {
            UiKitConfig config = UiKit.Config;
            if (config == null)
                return;

            string prefix = pageId + "/";
            for (int i = 0; i < config.counters.Count; i++)
            {
                UiKitConfig.CounterEntry entry = config.counters[i];
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;

                for (int p = 0; p < entry.paths.Count; p++)
                {
                    string path = entry.paths[p];
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    VisualElement element = UiElementResolver.Resolve(ScreenRoot, pageId, path.Substring(prefix.Length), out string error);
                    if (element == null)
                    {
                        Debug.LogError($"[UiKit] Counter '{entry.id}' path '{path}' could not be resolved. {error}", this);
                        continue;
                    }

                    var view = new CounterView { CounterId = entry.id };
                    view.Bind(element);
                    _autoCounters.Add(view);
                }
            }
        }

        private void ApplyNavigation()
        {
            UiKitConfig config = UiKit.Config;
            if (config == null)
                return;

            string prefix = pageId + "/";
            for (int i = 0; i < config.navigation.Count; i++)
            {
                UiKitConfig.NavigationEntry entry = config.navigation[i];
                if (entry == null || entry.action == UiNavigationAction.None ||
                    string.IsNullOrEmpty(entry.buttonPath) || !entry.buttonPath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                VisualElement element = UiElementResolver.Resolve(ScreenRoot, pageId, entry.buttonPath.Substring(prefix.Length), out string error);
                if (element == null)
                {
                    Debug.LogError($"[UiKit] Navigation button path '{entry.buttonPath}' could not be resolved. {error}", this);
                    continue;
                }

                PopupView contextPopup = FindContainingPopup(element);
                UiNavigationAction action = entry.action;
                string targetId = entry.targetId;
                WireClickTo(element, () => UiActions.Execute(action, targetId, this, contextPopup));
            }
        }

        /// <summary>
        /// Subscribes an action to element clicks, reusing the auto ButtonView (throttle, sound,
        /// press animation) when the element is a button.
        /// </summary>
        internal void WireClickTo(VisualElement element, Action action)
        {
            if (_autoButtons.TryGetValue(element, out ButtonView view))
            {
                view.Clicked += action;
                return;
            }

            EventCallback<ClickEvent> callback = evt => action();
            element.RegisterCallback(callback);
            _rawClicks.Add((element, callback));
        }

        private PopupView FindContainingPopup(VisualElement element)
        {
            VisualElement current = element.parent;
            while (current != null)
            {
                if (current.ClassListContains("fui_type_popup") && !string.IsNullOrEmpty(current.name) &&
                    _popups.TryGetValue(current.name, out PopupView popup))
                    return popup;

                current = current.parent;
            }

            return null;
        }

        /// <summary>Closes every open popup of this page.</summary>
        public void CloseAllPopups()
        {
            foreach (PopupView popup in _popups.Values)
            {
                if (popup.IsOpen)
                    popup.Close();
            }
        }

        /// <summary>Returns a popup by name; errors list the known popups of this page.</summary>
        public PopupView GetPopup(string popupName)
        {
            if (_popups.TryGetValue(popupName, out PopupView popup))
                return popup;

            Debug.LogError($"[UiKit] Popup '{popupName}' not found on page '{pageId}'. " +
                           $"Known popups: [{string.Join(", ", _popups.Keys)}]. " +
                           (IsBound ? "" : "Page is not bound yet."), this);
            return null;
        }

        /// <summary>Resolves a page-relative element path ("element" or "popup_x/element").</summary>
        public VisualElement ResolveElement(string relativePath)
        {
            VisualElement element = UiElementResolver.Resolve(ScreenRoot, pageId, relativePath, out string error);
            if (element == null)
                Debug.LogError(error, this);
            return element;
        }

        /// <summary>Returns the auto-created ButtonView of a button element, or null.</summary>
        public ButtonView GetButtonView(VisualElement element)
        {
            return element != null && _autoButtons.TryGetValue(element, out ButtonView view) ? view : null;
        }

        internal void SetSortingOrder(int order)
        {
            if (panelRenderer != null)
                panelRenderer.sortingOrder = order;
        }

        internal void ShowInternal()
        {
            if (!gameObject.activeSelf)
            {
                _pendingShow = true;
                gameObject.SetActive(true);
                return;
            }

            if (IsBound)
                DoShow();
            else
                _pendingShow = true;
        }

        private void DoShow()
        {
            _isShown = true;
            PlayShowAnimation(ScreenRoot, () =>
            {
                if (_isShown)
                    OnShow();
            });

            if (PageCascadeEnabled && _cascadeItems.Count > 0)
            {
                _cascadeSchedule?.Pause();
                UiCascade.SetHidden(_cascadeItems, true);
                _cascadeSchedule = UiCascade.Play(ScreenRoot, _cascadeItems, true, null, 0, 60);
            }
        }

        internal void HideInternal(bool animated, Action onHidden)
        {
            _pendingShow = false;

            if (!gameObject.activeSelf || !_isShown && !IsBound)
            {
                _isShown = false;
                gameObject.SetActive(false);
                onHidden?.Invoke();
                return;
            }

            _isShown = false;

            Action complete = () =>
            {
                if (_isShown)
                    return;
                OnHide();
                gameObject.SetActive(false);
                onHidden?.Invoke();
            };

            if (animated && IsBound)
            {
                PlayHideAnimation(ScreenRoot, complete);
            }
            else
            {
                ScreenRoot?.RemoveFromClassList(UiAnimations.OpenClass);
                complete();
            }
        }

        private const long PageAnimDurationMs = 260;
        private IVisualElementScheduledItem _pageAnimSchedule;

        /// <summary>Show animation hook; the default plays the configured preset via a code tween.</summary>
        protected virtual void PlayShowAnimation(VisualElement element, Action onDone)
        {
            UiAnimationMode mode = AnimationMode;
            bool animated = mode == UiAnimationMode.ForwardAndBackward || mode == UiAnimationMode.ForwardOnly;
            AnimatePage(element, ShowPreset, true, animated, onDone);
        }

        /// <summary>Hide animation hook; the default uses the hide preset when configured.</summary>
        protected virtual void PlayHideAnimation(VisualElement element, Action onDone)
        {
            UiAnimationMode mode = AnimationMode;
            bool animated = mode == UiAnimationMode.ForwardAndBackward || mode == UiAnimationMode.BackwardOnly;
            string preset = string.IsNullOrEmpty(HidePreset) ? ShowPreset : HidePreset;
            AnimatePage(element, preset, false, animated, onDone);
        }

        private void AnimatePage(VisualElement element, string preset, bool show, bool animated, Action onDone)
        {
            _pageAnimSchedule?.Pause();

            if (element == null || !animated || string.IsNullOrEmpty(preset) || preset == "none")
            {
                ApplyPagePreset(element, preset, show ? 1f : 0f);
                onDone?.Invoke();
                return;
            }

            _pageAnimSchedule = UiTween.Play(element, PageAnimDurationMs, p =>
            {
                float v = show ? p : 1f - p;
                ApplyPagePreset(element, preset, v);
            }, UiTween.Ease.OutCubic, onDone);
        }

        /// <summary>Applies a page preset at progress (0 = hidden, 1 = shown) via inline styles.</summary>
        private static void ApplyPagePreset(VisualElement element, string preset, float v)
        {
            if (element == null)
                return;

            switch (preset)
            {
                case "fade":
                    element.style.opacity = v;
                    break;
                case "scale":
                    element.style.opacity = v;
                    element.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.92f, 1f, v));
                    break;
                case "slide-up":
                    element.style.opacity = v;
                    element.style.translate = new Translate(0, Length.Percent(Mathf.Lerp(8f, 0f, v)));
                    break;
                case "slide-down":
                    element.style.opacity = v;
                    element.style.translate = new Translate(0, Length.Percent(Mathf.Lerp(-8f, 0f, v)));
                    break;
                case "slide-left":
                    element.style.opacity = v;
                    element.style.translate = new Translate(Length.Percent(Mathf.Lerp(8f, 0f, v)), 0);
                    break;
                case "slide-right":
                    element.style.opacity = v;
                    element.style.translate = new Translate(Length.Percent(Mathf.Lerp(-8f, 0f, v)), 0);
                    break;
                default:
                    element.style.opacity = v;
                    break;
            }
        }

        /// <summary>Query elements and subscribe here (generated code overrides this). Called on every reload.</summary>
        protected virtual void BindUi(VisualElement root)
        {
        }

        /// <summary>Undo everything done in <see cref="BindUi"/>; must be idempotent.</summary>
        protected virtual void UnwireUi()
        {
        }

        /// <summary>Called after the full bind pass completes.</summary>
        protected virtual void OnBind()
        {
        }

        /// <summary>User-partial counterpart of <see cref="OnBind"/>: undo manual subscriptions here.</summary>
        protected virtual void OnUnwire()
        {
        }

        /// <summary>Called when the page finished its show transition.</summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>Called when the page finished its hide transition, right before deactivation.</summary>
        protected virtual void OnHide()
        {
        }
    }
}
