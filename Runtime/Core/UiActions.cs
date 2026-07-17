using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Executes declarative page/popup actions shared by button navigation and flow mappings.
    /// </summary>
    internal static class UiActions
    {
        /// <summary>
        /// Runs an action. <paramref name="contextPage"/>/<paramref name="contextPopup"/> qualify
        /// short popup targets and empty ClosePopup targets.
        /// </summary>
        public static void Execute(UiNavigationAction action, string targetId, UiPageBase contextPage, PopupView contextPopup)
        {
            switch (action)
            {
                case UiNavigationAction.Show:
                    UiKit.Pages.Show(targetId);
                    break;

                case UiNavigationAction.Push:
                    if (!string.IsNullOrEmpty(targetId) && targetId.Contains("/") ||
                        !string.IsNullOrEmpty(targetId) && targetId.StartsWith("popup_"))
                        UiKit.Popups.Open(Qualify(targetId, contextPage));
                    else
                        UiKit.Pages.Push(targetId);
                    break;

                case UiNavigationAction.Pop:
                    UiKit.Pages.Pop();
                    break;

                case UiNavigationAction.OpenPopup:
                    UiKit.Popups.Open(Qualify(targetId, contextPage));
                    break;

                case UiNavigationAction.ClosePopup:
                    if (string.IsNullOrEmpty(targetId))
                    {
                        if (contextPopup != null)
                            contextPopup.Close();
                        else
                            Debug.LogError("[UiKit] ClosePopup without a target requires the button to live inside a popup.");
                    }
                    else
                    {
                        UiKit.Popups.Close(Qualify(targetId, contextPage));
                    }

                    break;
            }
        }

        private static string Qualify(string targetId, UiPageBase contextPage)
        {
            if (string.IsNullOrEmpty(targetId) || targetId.Contains("/") || contextPage == null)
                return targetId;

            return contextPage.PageId + "/" + targetId;
        }
    }
}
