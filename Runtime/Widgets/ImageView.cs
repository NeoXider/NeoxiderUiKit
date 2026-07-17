using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Wrapper over image_* elements: swap the background image, tint it and toggle visibility
    /// without touching styles by hand. All values are cached and re-applied on bind.
    /// </summary>
    public class ImageView : UiSubView
    {
        private Sprite _sprite;
        private Texture2D _texture;
        private Color _tint = Color.white;
        private bool _hasImage;
        private bool _hasTint;
        private bool _visible = true;
        private bool _hasVisibility;

        protected override void OnBind(VisualElement root)
        {
            if (_hasImage)
                ApplyImage();
            if (_hasTint)
                ApplyTint();
            if (_hasVisibility)
                ApplyVisibility();
        }

        /// <summary>Sets the background image from a sprite.</summary>
        public void SetImage(Sprite sprite)
        {
            _sprite = sprite;
            _texture = null;
            _hasImage = true;
            ApplyImage();
        }

        /// <summary>Sets the background image from a texture.</summary>
        public void SetImage(Texture2D texture)
        {
            _texture = texture;
            _sprite = null;
            _hasImage = true;
            ApplyImage();
        }

        /// <summary>Tints the background image.</summary>
        public void SetTint(Color color)
        {
            _tint = color;
            _hasTint = true;
            ApplyTint();
        }

        /// <summary>Makes the image participate in layout and rendering.</summary>
        public void Show()
        {
            SetVisible(true);
        }

        /// <summary>Removes the image from layout and rendering.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>Toggles display of the image element.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            _hasVisibility = true;
            ApplyVisibility();
        }

        private void ApplyImage()
        {
            if (Root == null)
                return;

            if (_sprite != null)
                Root.style.backgroundImage = new StyleBackground(_sprite);
            else if (_texture != null)
                Root.style.backgroundImage = new StyleBackground(_texture);
            else
                Root.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
        }

        private void ApplyTint()
        {
            if (Root != null)
                Root.style.unityBackgroundImageTintColor = _tint;
        }

        private void ApplyVisibility()
        {
            if (Root != null)
                Root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
