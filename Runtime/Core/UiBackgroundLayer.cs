using UnityEngine;
using UnityEngine.UI;

namespace Neo.UIKit
{
    /// <summary>
    /// Always-on backdrop behind the UI: a Screen Space - Camera canvas with a far-negative
    /// sorting order (default -1000) holding a single stretched background image. World objects
    /// closer to the camera render in front of it; UI Toolkit panels render on top of everything.
    /// Created by the scene builder when the config has a background sprite.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UiBackgroundLayer : MonoBehaviour
    {
        [Tooltip("Sorting order of the backdrop canvas; far below the game world.")]
        [SerializeField] private int sortingOrder = -1000;
        [SerializeField] private Image image;

        /// <summary>The background image component.</summary>
        public Image Image => image;

        private void OnEnable()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = sortingOrder;
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
            if (canvas.worldCamera != null)
                canvas.planeDistance = canvas.worldCamera.farClipPlane * 0.9f;

            EnsureImage();

            var config = UiKit.Config;
            if (config != null && image != null && image.sprite == null && config.backgroundSprite != null)
                image.sprite = config.backgroundSprite;
        }

        /// <summary>Creates the stretched background image child when missing.</summary>
        public void EnsureImage()
        {
            if (image != null)
                return;

            image = GetComponentInChildren<Image>(true);
            if (image != null)
                return;

            var go = new GameObject("bg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image = go.GetComponent<Image>();
        }
    }
}
