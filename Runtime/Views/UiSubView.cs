using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Plain C# base for everything living inside someone else's visual tree (popups, widgets,
    /// sections). The owner binds it to a sub-branch from its own BindUi and unwires it from
    /// its Unwire; all queries must be scoped to <see cref="Root"/>.
    /// </summary>
    public abstract class UiSubView
    {
        /// <summary>Root element of this sub-view; valid only while bound.</summary>
        public VisualElement Root { get; private set; }

        /// <summary>True between <see cref="Bind"/> and <see cref="Unwire"/>.</summary>
        public bool IsBound { get; private set; }

        /// <summary>Binds the view to a sub-branch. Re-binding unwires first.</summary>
        public void Bind(VisualElement root)
        {
            if (root == null)
            {
                Debug.LogError($"[UiKit] {GetType().Name}.Bind called with a null root.");
                return;
            }

            if (IsBound)
                Unwire();

            Root = root;
            IsBound = true;
            OnBind(root);
        }

        /// <summary>Removes subscriptions, cancels scheduled items and clears the cache. Idempotent.</summary>
        public void Unwire()
        {
            if (!IsBound && Root == null)
                return;

            try
            {
                OnUnwire();
            }
            finally
            {
                Root = null;
                IsBound = false;
            }
        }

        /// <summary>Query elements and subscribe here; called on every bind.</summary>
        protected virtual void OnBind(VisualElement root)
        {
        }

        /// <summary>Undo everything done in <see cref="OnBind"/>; must be null-safe.</summary>
        protected virtual void OnUnwire()
        {
        }
    }
}
