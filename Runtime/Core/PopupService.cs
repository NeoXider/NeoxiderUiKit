using System.Threading.Tasks;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Facade for popups addressed by full path "pageId/popup_name".
    /// </summary>
    public sealed class PopupService
    {
        /// <summary>Resolves a popup by path; logs a clear error when the path is invalid.</summary>
        public PopupView Get(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[UiKit] Popup path is null or empty.");
                return null;
            }

            int slash = path.IndexOf('/');
            if (slash <= 0 || slash >= path.Length - 1)
            {
                Debug.LogError($"[UiKit] Popup path '{path}' must look like \"pageId/popup_name\".");
                return null;
            }

            UiPageBase page = UiKit.Pages.Get(path.Substring(0, slash));
            return page != null ? page.GetPopup(path.Substring(slash + 1)) : null;
        }

        /// <summary>Opens the popup at the path.</summary>
        public void Open(string path)
        {
            Get(path)?.Open();
        }

        /// <summary>Closes the popup at the path.</summary>
        public void Close(string path)
        {
            Get(path)?.Close();
        }

        /// <summary>
        /// Opens the popup as a modal dialog; the task resolves with the clicked button's result
        /// string when the popup closes (see <see cref="PopupView.OpenAsync"/>).
        /// </summary>
        public Task<string> OpenAsync(string path)
        {
            PopupView popup = Get(path);
            return popup != null ? popup.OpenAsync() : Task.FromResult(string.Empty);
        }
    }
}
