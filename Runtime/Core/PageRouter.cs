using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Registry and stack of pages. <see cref="Show"/> switches to a page (clearing the stack),
    /// <see cref="Push"/>/<see cref="Pop"/> layer pages on top; the sorting order of each panel
    /// renderer is set to the configured base plus the stack position.
    /// </summary>
    public sealed class PageRouter
    {
        private readonly Dictionary<string, UiPageBase> _pages = new Dictionary<string, UiPageBase>(StringComparer.Ordinal);
        private readonly List<UiPageBase> _stack = new List<UiPageBase>();
        private readonly List<string> _history = new List<string>();

        /// <summary>Raised with the page id after a page is shown/pushed.</summary>
        public event Action<string> PageShown;

        /// <summary>Raised with the page id when a page starts hiding.</summary>
        public event Action<string> PageHidden;

        /// <summary>Raised when Back is requested with nothing left to close (e.g. "quit game?" prompt).</summary>
        public event Action BackOnRoot;

        /// <summary>Current page stack, bottom to top.</summary>
        public IReadOnlyList<UiPageBase> Stack => _stack;

        /// <summary>Topmost page, or null.</summary>
        public UiPageBase Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        /// <summary>All registered page ids.</summary>
        public IEnumerable<string> Ids => _pages.Keys;

        /// <summary>Registers a page (idempotent). Pages self-register on Awake; the bootstrap registers inactive ones.</summary>
        public void Register(UiPageBase page)
        {
            if (page == null || string.IsNullOrEmpty(page.PageId))
            {
                Debug.LogError("[UiKit] Cannot register a page without a pageId.", page);
                return;
            }

            if (_pages.TryGetValue(page.PageId, out UiPageBase existing) && existing != null && existing != page)
            {
                Debug.LogError($"[UiKit] Duplicate pageId '{page.PageId}' ({existing.name} vs {page.name}).", page);
                return;
            }

            _pages[page.PageId] = page;
        }

        /// <summary>Returns the page by id, or null with an error listing known ids.</summary>
        public UiPageBase Get(string pageId)
        {
            if (pageId != null && _pages.TryGetValue(pageId, out UiPageBase page) && page != null)
                return page;

            Debug.LogError($"[UiKit] Unknown pageId '{pageId}'. Known pages: [{string.Join(", ", _pages.Keys)}].");
            return null;
        }

        /// <summary>Returns the registered page of the given type, or null.</summary>
        public T Get<T>() where T : UiPageBase
        {
            foreach (UiPageBase page in _pages.Values)
            {
                if (page is T typed)
                    return typed;
            }

            Debug.LogError($"[UiKit] No registered page of type {typeof(T).Name}. Known pages: [{string.Join(", ", _pages.Keys)}].");
            return null;
        }

        internal bool TryGet(string pageId, out UiPageBase page)
        {
            return _pages.TryGetValue(pageId, out page) && page != null;
        }

        /// <summary>
        /// Switches to the page: hides the whole current stack, then shows the target. When the
        /// target is already in the stack it is not re-created — pages above it are hidden and
        /// its popups are closed (so "restart" buttons inside popups land on a clean page).
        /// </summary>
        public void Show(string pageId)
        {
            UiPageBase target = Get(pageId);
            if (target == null)
                return;

            if (_stack.Contains(target))
            {
                for (int i = _stack.Count - 1; i >= 0; i--)
                {
                    UiPageBase page = _stack[i];
                    if (page == target)
                        break;

                    _stack.RemoveAt(i);
                    PageHidden?.Invoke(page.PageId);
                    page.HideInternal(i == _stack.Count, null);
                }

                target.CloseAllPopups();
                _history.Add(target.PageId);
                PageShown?.Invoke(target.PageId);
                return;
            }

            int top = _stack.Count - 1;
            for (int i = top; i >= 0; i--)
            {
                UiPageBase page = _stack[i];
                _stack.RemoveAt(i);
                PageHidden?.Invoke(page.PageId);
                page.HideInternal(i == top, null);
            }

            PushInternal(target);
        }

        /// <summary>Pushes the page on top of the stack.</summary>
        public void Push(string pageId)
        {
            UiPageBase target = Get(pageId);
            if (target == null)
                return;

            if (_stack.Contains(target))
            {
                Debug.LogWarning($"[UiKit] Page '{pageId}' is already in the stack; Push ignored.");
                return;
            }

            PushInternal(target);
        }

        private void PushInternal(UiPageBase page)
        {
            int baseOrder = UiKit.Config?.GetPage(page.PageId)?.sortingOrderBase ?? 0;
            page.SetSortingOrder(baseOrder + _stack.Count);
            _stack.Add(page);
            _history.Add(page.PageId);
            page.ShowInternal();
            PageShown?.Invoke(page.PageId);
        }

        /// <summary>Pops and hides the topmost page. The stack root is never popped.</summary>
        public void Pop()
        {
            if (_stack.Count <= 1)
            {
                Debug.LogWarning("[UiKit] Pop ignored: the stack root cannot be popped.");
                return;
            }

            UiPageBase page = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            PageHidden?.Invoke(page.PageId);
            page.HideInternal(true, null);
        }

        /// <summary>
        /// Back/Escape handling: closes the topmost open popup first, then pops the stack,
        /// and raises <see cref="BackOnRoot"/> when already at the root.
        /// </summary>
        public bool Back()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                foreach (PopupView popup in _stack[i].Popups.Values)
                {
                    if (popup.IsOpen)
                    {
                        popup.Close();
                        return true;
                    }
                }
            }

            if (_stack.Count > 1)
            {
                Pop();
                return true;
            }

            BackOnRoot?.Invoke();
            return false;
        }
    }
}
