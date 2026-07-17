using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Popup over an imported ".fui_type_popup" element. Hidden by a class before the first frame,
    /// opened/closed via ".is-open" (uikit.uss rules use 2-class specificity to beat importer rules),
    /// display:none is applied deferred after close. <see cref="OpenAsync"/> turns the popup into a
    /// modal dialog: any button click closes it and resolves the task with a result string.
    /// </summary>
    public class PopupView : UiSubView
    {
        private UiPageBase _owner;
        private string _name;
        private string _preset = "scale-pop";
        private UiAnimationMode _mode = UiAnimationMode.ForwardAndBackward;
        private TaskCompletionSource<string> _pendingResult;
        private string _asyncResult;
        private readonly List<(Button button, EventCallback<ClickEvent> callback)> _asyncHandlers =
            new List<(Button, EventCallback<ClickEvent>)>();
        private readonly List<VisualElement> _cascadeItems = new List<VisualElement>();
        private readonly List<VisualElement> _panels = new List<VisualElement>();
        private VisualElement _dim;
        private IVisualElementScheduledItem _cascadeSchedule;
        private IVisualElementScheduledItem _animSchedule;

        private const long AnimDurationMs = 300;

        private static bool CascadeEnabled => UiKit.Config != null && UiKit.Config.popupCascadeEnabled;

        /// <summary>Popup element name (e.g. "popup_pause").</summary>
        public string Name => _name;

        /// <summary>Full popup path "pageId/popupName".</summary>
        public string Path => _owner != null ? $"{_owner.PageId}/{_name}" : _name;

        /// <summary>Owning page.</summary>
        public UiPageBase Owner => _owner;

        /// <summary>True between Open and the start of Close.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised when the open animation is requested.</summary>
        public event Action Opened;

        /// <summary>Raised after the close animation completes.</summary>
        public event Action Closed;

        internal void Initialize(UiPageBase owner, string name, string preset, UiAnimationMode mode)
        {
            _owner = owner;
            _name = name;
            if (!string.IsNullOrEmpty(preset))
                _preset = preset;
            _mode = mode;
        }

        protected override void OnBind(VisualElement root)
        {
            if (string.IsNullOrEmpty(_name))
                _name = root.name;

            root.AddToClassList("uikit-popup");
            UiAnimations.ApplyPresetClass(root, _preset);

            VisualElement dim = root.Q<VisualElement>("background");
            dim?.AddToClassList("uikit-dim");

            BindContentPanel(root, dim);

            if (IsOpen)
            {
                root.style.display = DisplayStyle.Flex;
                root.AddToClassList(UiAnimations.OpenClass);
                ApplyPanelState(1f);
                UiCascade.SetHidden(_cascadeItems, false);
                if (_pendingResult != null)
                    WireAsyncButtons();
            }
            else
            {
                root.RemoveFromClassList(UiAnimations.OpenClass);
                ApplyPanelState(0f);
                root.style.display = DisplayStyle.None;
            }
        }

        protected override void OnUnwire()
        {
            UnwireAsyncButtons();
            _cascadeSchedule?.Pause();
            _cascadeSchedule = null;
            _animSchedule?.Pause();
            _animSchedule = null;
            _cascadeItems.Clear();
            _panels.Clear();
            _dim = null;
        }

        /// <summary>
        /// Marks the popup's content panel(s) so presets animate the panel instead of the whole
        /// fullscreen root, and collects the cascade reveal list via <see cref="UiCascade"/>.
        /// </summary>
        private void BindContentPanel(VisualElement root, VisualElement dim)
        {
            _dim = dim;
            _panels.Clear();

            foreach (VisualElement child in root.Children())
            {
                if (child == dim || string.IsNullOrEmpty(child.name))
                    continue;

                // The header and the content panels are the moving parts of the popup.
                if (child.name == "panel_header" || child.ClassListContains("fui_type_panel") ||
                    child.ClassListContains("fui_as_panel"))
                {
                    child.AddToClassList("uikit-popup-panel");
                    _panels.Add(child);
                }
            }

            _cascadeItems.Clear();
            _cascadeItems.AddRange(UiCascade.CollectPopupItems(root, dim));
        }

        /// <summary>Opens the popup (no-op when already open).</summary>
        public void Open()
        {
            if (IsOpen || Root == null)
                return;

            IsOpen = true;
            _closing = false;
            Root.style.display = DisplayStyle.Flex;
            Root.AddToClassList(UiAnimations.OpenClass);
            ApplyPanelState(0f);

            if (CascadeEnabled && _cascadeItems.Count > 0)
            {
                UiCascade.SetHidden(_cascadeItems, true);
                _cascadeSchedule?.Pause();
                _cascadeSchedule = UiCascade.Play(Root, _cascadeItems, true, null, 0, 220);
            }

            PlayShowAnimation(Root, () => Opened?.Invoke());
            UiKit.Flow.NotifyPopupToggled(Path, true);
        }

        private bool _closing;

        /// <summary>
        /// Closes the popup: the content panel shrinks back (scale 1 -> 0 with its contents), the
        /// dim fades out, and the flow (pause) is released only after the animation completes.
        /// </summary>
        public void Close()
        {
            if (!IsOpen || _closing || Root == null)
                return;

            _closing = true;
            _cascadeSchedule?.Pause();
            _cascadeSchedule = null;

            IsOpen = false;
            VisualElement root = Root;
            PlayHideAnimation(root, () =>
            {
                _closing = false;
                if (!IsOpen)
                {
                    root.RemoveFromClassList(UiAnimations.OpenClass);
                    root.style.display = DisplayStyle.None;
                }

                CompleteAsync();
                Closed?.Invoke();
                UiKit.Flow.NotifyPopupToggled(Path, false);
            });
        }

        /// <summary>
        /// Opens the popup as a modal dialog. The returned task resolves when the popup closes;
        /// a clicked button maps to its result string (config override, or the button name minus
        /// the "button_" prefix). Closing without a button click resolves to an empty string.
        /// </summary>
        public Task<string> OpenAsync()
        {
            if (_pendingResult != null)
                return _pendingResult.Task;

            _pendingResult = new TaskCompletionSource<string>();
            _asyncResult = null;
            WireAsyncButtons();
            Open();
            return _pendingResult.Task;
        }

        /// <summary>
        /// Show animation hook: the dim fades in and the content panel plays the configured preset
        /// (scale-pop / zoom-in / slide-up / fade), driven by a code tween so it always plays.
        /// </summary>
        protected virtual void PlayShowAnimation(VisualElement element, Action onDone)
        {
            bool animated = _mode == UiAnimationMode.ForwardAndBackward || _mode == UiAnimationMode.ForwardOnly;
            AnimatePanels(true, animated, onDone);
        }

        /// <summary>Hide animation hook: the content panel plays the preset in reverse.</summary>
        protected virtual void PlayHideAnimation(VisualElement element, Action onDone)
        {
            bool animated = _mode == UiAnimationMode.ForwardAndBackward || _mode == UiAnimationMode.BackwardOnly;
            AnimatePanels(false, animated, onDone);
        }

        private void AnimatePanels(bool show, bool animated, Action onDone)
        {
            _animSchedule?.Pause();

            if (!animated || _preset == "none" || Root == null)
            {
                ApplyPanelState(show ? 1f : 0f);
                onDone?.Invoke();
                return;
            }

            UiTween.Ease ease = _preset == "scale-pop" ? UiTween.Ease.OutBack : UiTween.Ease.OutCubic;
            _animSchedule = UiTween.Play(Root, AnimDurationMs, p =>
            {
                float v = show ? p : 1f - p;
                ApplyPanelState(v);
            }, ease, onDone);
        }

        /// <summary>Applies the preset at progress <paramref name="v"/> (0 = closed, 1 = open) to panels and dim.</summary>
        private void ApplyPanelState(float v)
        {
            if (_dim != null)
                _dim.style.opacity = v;

            for (int i = 0; i < _panels.Count; i++)
            {
                VisualElement panel = _panels[i];
                switch (_preset)
                {
                    case "zoom-in":
                        panel.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.5f, 1f, v));
                        panel.style.opacity = v;
                        break;
                    case "slide-up":
                        panel.style.translate = new Translate(0, Length.Percent(Mathf.Lerp(40f, 0f, v)));
                        panel.style.opacity = v;
                        break;
                    case "fade":
                        panel.style.opacity = v;
                        break;
                    default: // scale-pop
                        panel.style.scale = new Scale(Vector3.one * v);
                        break;
                }
            }
        }

        private void WireAsyncButtons()
        {
            UnwireAsyncButtons();
            if (Root == null)
                return;

            foreach (Button button in Root.Query<Button>().ToList())
            {
                Button captured = button;
                EventCallback<ClickEvent> callback = evt =>
                {
                    _asyncResult = ResolveResult(captured.name);
                    Close();
                };
                captured.RegisterCallback(callback);
                _asyncHandlers.Add((captured, callback));
            }
        }

        private void UnwireAsyncButtons()
        {
            for (int i = 0; i < _asyncHandlers.Count; i++)
                _asyncHandlers[i].button.UnregisterCallback(_asyncHandlers[i].callback);
            _asyncHandlers.Clear();
        }

        private void CompleteAsync()
        {
            if (_pendingResult == null)
                return;

            UnwireAsyncButtons();
            TaskCompletionSource<string> tcs = _pendingResult;
            _pendingResult = null;
            tcs.TrySetResult(_asyncResult ?? string.Empty);
            _asyncResult = null;
        }

        private string ResolveResult(string buttonName)
        {
            UiKitConfig config = UiKit.Config;
            if (config != null)
            {
                string fullPath = $"{Path}/{buttonName}";
                for (int i = 0; i < config.popupResults.Count; i++)
                {
                    UiKitConfig.PopupResultEntry entry = config.popupResults[i];
                    if (entry != null && entry.buttonPath == fullPath)
                        return entry.result;
                }
            }

            const string prefix = "button_";
            return buttonName != null && buttonName.StartsWith(prefix, StringComparison.Ordinal)
                ? buttonName.Substring(prefix.Length)
                : buttonName;
        }
    }
}
