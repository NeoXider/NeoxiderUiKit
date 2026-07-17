using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Shop card widget (modeled after panel_upgrade): optional buy button, price label and icon.
    /// Null-tolerant: missing parts leave the corresponding field null and the related calls
    /// degrade silently.
    /// </summary>
    public class ShopItemView : UiSubView
    {
        private bool _purchased;
        private bool _available = true;

        /// <summary>Buy button, or null when the card has no button.</summary>
        public ButtonView Button { get; private set; }

        /// <summary>Price label, or null.</summary>
        public LabelView Price { get; private set; }

        /// <summary>Item icon, or null.</summary>
        public ImageView Icon { get; private set; }

        protected override void OnBind(VisualElement root)
        {
            Button buttonElement = root as Button ?? root.Q<Button>();
            if (buttonElement != null)
            {
                Button = Button ?? new ButtonView();
                Button.Bind(buttonElement);
            }

            Label priceLabel = buttonElement != null ? buttonElement.Q<Label>() : root.Q<Label>();
            if (priceLabel != null)
            {
                Price = Price ?? new LabelView();
                Price.Bind(priceLabel);
            }

            VisualElement iconElement = FindIcon(root, buttonElement);
            if (iconElement != null)
            {
                Icon = Icon ?? new ImageView();
                Icon.Bind(iconElement);
            }

            ApplyState();
        }

        protected override void OnUnwire()
        {
            Button?.Unwire();
            Price?.Unwire();
            Icon?.Unwire();
        }

        /// <summary>Sets the displayed price text (gradient wrapper preserved).</summary>
        public void SetPrice(string price)
        {
            Price?.SetText(price);
        }

        /// <summary>Sets the item icon.</summary>
        public void SetIcon(Sprite sprite)
        {
            Icon?.SetImage(sprite);
        }

        /// <summary>Marks the item as purchased ("is-purchased" class, button disabled).</summary>
        public void SetPurchased(bool purchased)
        {
            _purchased = purchased;
            ApplyState();
        }

        /// <summary>Marks the item as available/unavailable ("is-unavailable" class, button toggled).</summary>
        public void SetAvailable(bool available)
        {
            _available = available;
            ApplyState();
        }

        private void ApplyState()
        {
            if (Root == null)
                return;

            Root.EnableInClassList("is-purchased", _purchased);
            Root.EnableInClassList("is-unavailable", !_available);
            Button?.SetEnabled(_available && !_purchased);
        }

        private static VisualElement FindIcon(VisualElement root, VisualElement excludeScope)
        {
            return root.Query<VisualElement>(className: "fui_type_image")
                .Where(e => excludeScope == null || !IsDescendantOf(e, excludeScope))
                .First();
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            VisualElement current = element;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.parent;
            }

            return false;
        }
    }
}
