using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

            if (IsOpen)
            {
                root.style.display = DisplayStyle.Flex;
                root.AddToClassList(UiAnimations.OpenClass);
                if (_pendingResult != null)
                    WireAsyncButtons();
            }
            else
            {
                root.RemoveFromClassList(UiAnimations.OpenClass);
                root.style.display = DisplayStyle.None;
            }
        }

        protected override void OnUnwire()
        {
            UnwireAsyncButtons();
        }

        /// <summary>Opens the popup (no-op when already open).</summary>
        public void Open()
        {
            if (IsOpen || Root == null)
                return;

            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            PlayShowAnimation(Root, () => Opened?.Invoke());
            UiKit.Flow.NotifyPopupToggled(Path, true);
        }

        /// <summary>Closes the popup; display:none is applied after the hide animation.</summary>
        public void Close()
        {
            if (!IsOpen || Root == null)
                return;

            IsOpen = false;
            VisualElement root = Root;
            PlayHideAnimation(root, () =>
            {
                if (!IsOpen)
                    root.style.display = DisplayStyle.None;
                CompleteAsync();
                Closed?.Invoke();
            });
            UiKit.Flow.NotifyPopupToggled(Path, false);
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

        /// <summary>Show animation hook; override in a partial page/popup class for custom behavior.</summary>
        protected virtual void PlayShowAnimation(VisualElement element, Action onDone)
        {
            bool animated = _mode == UiAnimationMode.ForwardAndBackward || _mode == UiAnimationMode.ForwardOnly;
            (animated ? UiAnimations.Get(_preset) : UiAnimations.Instant).Show(element, onDone);
        }

        /// <summary>Hide animation hook; override for custom behavior.</summary>
        protected virtual void PlayHideAnimation(VisualElement element, Action onDone)
        {
            bool animated = _mode == UiAnimationMode.ForwardAndBackward || _mode == UiAnimationMode.BackwardOnly;
            (animated ? UiAnimations.Get(_preset) : UiAnimations.Instant).Hide(element, onDone);
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
