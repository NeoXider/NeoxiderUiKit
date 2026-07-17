using System.Collections.Generic;
using System.Text;

namespace Neo.UIKit.Editor
{
    /// <summary>How a generated field is created and bound.</summary>
    public enum UiFieldBinding
    {
        /// <summary>Own widget instance (LabelView, CounterView, ...) bound to the resolved element.</summary>
        WidgetInstance = 0,

        /// <summary>Reuses the page's automatic ButtonView (press animation, sound, throttle).</summary>
        AutoButton,

        /// <summary>Raw VisualElement fallback field.</summary>
        RawElement
    }

    /// <summary>A planned generated field for one named element.</summary>
    public sealed class UiFieldPlan
    {
        public UiElementModel Element;
        public string FieldName;
        public string TypeName;
        public UiFieldBinding Binding;
        public string Initializer;
    }

    /// <summary>A planned nested popup class of a page view.</summary>
    public sealed class UiPopupPlan
    {
        public UiPopupModel Model;
        public string ClassName;
        public string FieldName;
        public List<UiFieldPlan> Fields = new List<UiFieldPlan>();
    }

    /// <summary>A planned page view: class name plus all fields and popup classes.</summary>
    public sealed class UiPagePlan
    {
        public UiPageModel Model;
        public string ClassName;
        public List<UiFieldPlan> Fields = new List<UiFieldPlan>();
        public List<UiPopupPlan> Popups = new List<UiPopupPlan>();
    }

    /// <summary>
    /// Plans and emits per-page view code: the regenerated &lt;Page&gt;View.g.cs partial
    /// (typed fields for every named element, nested popup classes with scoped queries,
    /// BindUi/UnwireUi wiring) and the user partial created once and never overwritten.
    /// </summary>
    public static class ViewCodeGenerator
    {
        private static readonly string[] PageReservedNames =
        {
            "Root", "ScreenRoot", "PageId", "PanelRenderer", "Popups", "IsBound", "Bound",
            "AnimationMode", "BindUi", "UnwireUi", "OnBind", "OnShow", "OnHide", "Unwire",
            "CreatePopupView", "BindGenerated", "GetPopup", "ResolveElement", "GetButtonView", "Id"
        };

        private static readonly string[] PopupReservedNames =
        {
            "Root", "IsBound", "Name", "Path", "Owner", "IsOpen", "Opened", "Closed",
            "Open", "Close", "OpenAsync", "OnBind", "OnUnwire", "Bind", "Unwire",
            "BindGeneratedFields", "BindGenerated", "FindElement",
            "PlayShowAnimation", "PlayHideAnimation"
        };

        /// <summary>Builds the deterministic naming plan for one page.</summary>
        public static UiPagePlan Plan(UiPageModel page, UiKitGenerationSettings settings)
        {
            var plan = new UiPagePlan
            {
                Model = page,
                ClassName = NameSanitizer.ToPascalIdentifier(page.PageId) + "View"
            };

            var used = new HashSet<string>(PageReservedNames) { plan.ClassName };

            foreach (UiElementModel element in page.Elements)
            {
                if (element.IsService && !settings.includeServiceElements)
                    continue;

                plan.Fields.Add(PlanField(element, used));
            }

            foreach (UiPopupModel popup in page.Popups)
            {
                var popupPlan = new UiPopupPlan
                {
                    Model = popup,
                    FieldName = NameSanitizer.Unique(NameSanitizer.ToPascalIdentifier(popup.Name), used)
                };
                popupPlan.ClassName = NameSanitizer.Unique(popupPlan.FieldName + "View", used);

                var popupUsed = new HashSet<string>(PopupReservedNames) { popupPlan.ClassName };
                foreach (UiElementModel element in popup.Elements)
                {
                    if (element.IsService && !settings.includeServiceElements)
                        continue;

                    popupPlan.Fields.Add(PlanField(element, popupUsed));
                }

                plan.Popups.Add(popupPlan);
            }

            return plan;
        }

        private static UiFieldPlan PlanField(UiElementModel element, HashSet<string> used)
        {
            var field = new UiFieldPlan
            {
                Element = element,
                FieldName = NameSanitizer.Unique(NameSanitizer.ToPascalIdentifier(element.Name), used)
            };

            switch (element.Widget)
            {
                case UiWidgetKind.Button:
                    field.TypeName = "ButtonView";
                    field.Binding = UiFieldBinding.AutoButton;
                    break;
                case UiWidgetKind.Toggle:
                    field.TypeName = "ToggleView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new ToggleView()";
                    break;
                case UiWidgetKind.Label:
                    field.TypeName = "LabelView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new LabelView()";
                    break;
                case UiWidgetKind.Image:
                    field.TypeName = "ImageView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new ImageView()";
                    break;
                case UiWidgetKind.Counter:
                    field.TypeName = "CounterView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = $"new CounterView {{ CounterId = \"{element.CounterId}\" }}";
                    break;
                case UiWidgetKind.Score:
                    field.TypeName = "ScoreView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = $"new ScoreView {{ CounterId = \"{element.CounterId}\" }}";
                    break;
                case UiWidgetKind.Level:
                    field.TypeName = "LevelView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = $"new LevelView {{ CounterId = \"{element.CounterId}\" }}";
                    break;
                case UiWidgetKind.Timer:
                    field.TypeName = "TimerView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new TimerView()";
                    break;
                case UiWidgetKind.Bar:
                    field.TypeName = "BarView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new BarView()";
                    break;
                case UiWidgetKind.ShopItem:
                    field.TypeName = "ShopItemView";
                    field.Binding = UiFieldBinding.WidgetInstance;
                    field.Initializer = "new ShopItemView()";
                    break;
                default:
                    field.TypeName = "VisualElement";
                    field.Binding = UiFieldBinding.RawElement;
                    break;
            }

            return field;
        }

        /// <summary>Emits the regenerated &lt;Page&gt;View.g.cs content.</summary>
        public static string GeneratePageSource(UiPagePlan plan, UiKitGenerationSettings settings)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, plan.Model.UxmlPath);
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UIElements;");
            sb.AppendLine("using Neo.UIKit;");
            sb.AppendLine();
            AppendNamespaceOpen(sb, settings);
            sb.AppendLine($"    /// <summary>Generated typed view of the \"{plan.Model.PageId}\" page.</summary>");
            sb.AppendLine($"    public partial class {plan.ClassName} : UiPageBase");
            sb.AppendLine("    {");

            bool first = true;
            foreach (UiFieldPlan field in plan.Fields)
            {
                AppendFieldDeclaration(sb, field, "        ", ref first);
            }

            foreach (UiPopupPlan popup in plan.Popups)
            {
                if (!first)
                    sb.AppendLine();
                first = false;
                sb.AppendLine($"        /// <summary>Popup \"{popup.Model.Name}\" at \"{popup.Model.FullPath}\".</summary>");
                sb.AppendLine($"        public {popup.ClassName} {popup.FieldName} {{ get; }} = new {popup.ClassName}();");
            }

            if (plan.Popups.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        protected override PopupView CreatePopupView(string popupName)");
                sb.AppendLine("        {");
                sb.AppendLine("            switch (popupName)");
                sb.AppendLine("            {");
                foreach (UiPopupPlan popup in plan.Popups)
                {
                    sb.AppendLine($"                case \"{popup.Model.Name}\":");
                    sb.AppendLine($"                    return {popup.FieldName};");
                }

                sb.AppendLine("                default:");
                sb.AppendLine("                    return base.CreatePopupView(popupName);");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }

            sb.AppendLine();
            sb.AppendLine("        protected override void BindUi(VisualElement root)");
            sb.AppendLine("        {");
            foreach (UiFieldPlan field in plan.Fields)
                AppendPageFieldBind(sb, field);
            foreach (UiPopupPlan popup in plan.Popups)
                sb.AppendLine($"            {popup.FieldName}.BindGeneratedFields(this);");
            sb.AppendLine("        }");

            sb.AppendLine();
            sb.AppendLine("        protected override void UnwireUi()");
            sb.AppendLine("        {");
            foreach (UiFieldPlan field in plan.Fields)
                AppendFieldUnwire(sb, field, "            ");
            sb.AppendLine("        }");

            if (HasWidgetInstances(plan.Fields))
            {
                sb.AppendLine();
                sb.AppendLine("        private void BindGenerated(UiSubView view, string relativePath)");
                sb.AppendLine("        {");
                sb.AppendLine("            VisualElement element = ResolveElement(relativePath);");
                sb.AppendLine("            if (element != null)");
                sb.AppendLine("                view.Bind(element);");
                sb.AppendLine("        }");
            }

            foreach (UiPopupPlan popup in plan.Popups)
                AppendPopupClass(sb, popup);

            sb.AppendLine("    }");
            AppendNamespaceClose(sb, settings);
            return sb.ToString();
        }

        /// <summary>Emits the user partial content (created once, never overwritten).</summary>
        public static string GenerateUserPartialSource(UiPagePlan plan, UiKitGenerationSettings settings)
        {
            var sb = new StringBuilder();
            AppendNamespaceOpen(sb, settings);
            sb.AppendLine($"    public partial class {plan.ClassName}");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override void OnBind()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            AppendNamespaceClose(sb, settings);
            return sb.ToString();
        }

        /// <summary>Opens the namespace block when configured; global scope emits nothing.</summary>
        internal static void AppendNamespaceOpen(StringBuilder sb, UiKitGenerationSettings settings)
        {
            if (settings.HasNamespace)
            {
                sb.AppendLine($"namespace {settings.EffectiveNamespace}");
                sb.AppendLine("{");
            }
        }

        /// <summary>Closes the namespace block opened by <see cref="AppendNamespaceOpen"/>.</summary>
        internal static void AppendNamespaceClose(StringBuilder sb, UiKitGenerationSettings settings)
        {
            if (settings.HasNamespace)
                sb.AppendLine("}");
        }

        private static void AppendHeader(StringBuilder sb, string sourcePath)
        {
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine($"//     Generated by Neo.UIKit from {sourcePath}.");
            sb.AppendLine("//     Manual changes will be lost on the next generation; use the user partial instead.");
            sb.AppendLine("// </auto-generated>");
        }

        private static bool HasWidgetInstances(List<UiFieldPlan> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Binding == UiFieldBinding.WidgetInstance)
                    return true;
            }

            return false;
        }

        private static void AppendFieldDeclaration(StringBuilder sb, UiFieldPlan field, string indent, ref bool first)
        {
            if (!first)
                sb.AppendLine();
            first = false;

            sb.AppendLine($"{indent}/// <summary>{DescribeField(field)}</summary>");
            switch (field.Binding)
            {
                case UiFieldBinding.WidgetInstance:
                    sb.AppendLine($"{indent}public {field.TypeName} {field.FieldName} {{ get; }} = {field.Initializer};");
                    break;
                case UiFieldBinding.AutoButton:
                    sb.AppendLine($"{indent}public ButtonView {field.FieldName} {{ get; private set; }}");
                    break;
                default:
                    sb.AppendLine($"{indent}public VisualElement {field.FieldName} {{ get; private set; }}");
                    break;
            }
        }

        private static string DescribeField(UiFieldPlan field)
        {
            UiElementModel element = field.Element;
            string description;
            switch (element.Widget)
            {
                case UiWidgetKind.Counter:
                case UiWidgetKind.Score:
                case UiWidgetKind.Level:
                    description = $"{element.Widget} \"{element.Name}\" (counter id \"{element.CounterId}\") at \"{element.FullPath}\".";
                    break;
                case UiWidgetKind.Element:
                    description = $"Element \"{element.Name}\" ({element.UxmlTag}) at \"{element.FullPath}\".";
                    break;
                default:
                    description = $"{element.Widget} \"{element.Name}\" at \"{element.FullPath}\".";
                    break;
            }

            if (element.Widget == UiWidgetKind.Button)
                description += " Rebound on every reload; subscribe in OnBind.";

            return description;
        }

        private static void AppendPageFieldBind(StringBuilder sb, UiFieldPlan field)
        {
            switch (field.Binding)
            {
                case UiFieldBinding.WidgetInstance:
                    sb.AppendLine($"            BindGenerated({field.FieldName}, \"{field.Element.RelativePath}\");");
                    break;
                case UiFieldBinding.AutoButton:
                    sb.AppendLine($"            {field.FieldName} = GetButtonView(ResolveElement(\"{field.Element.RelativePath}\"));");
                    break;
                default:
                    sb.AppendLine($"            {field.FieldName} = ResolveElement(\"{field.Element.RelativePath}\");");
                    break;
            }
        }

        private static void AppendFieldUnwire(StringBuilder sb, UiFieldPlan field, string indent)
        {
            if (field.Binding == UiFieldBinding.WidgetInstance)
                sb.AppendLine($"{indent}{field.FieldName}.Unwire();");
            else
                sb.AppendLine($"{indent}{field.FieldName} = null;");
        }

        private static void AppendPopupClass(StringBuilder sb, UiPopupPlan popup)
        {
            sb.AppendLine();
            sb.AppendLine($"        /// <summary>Generated typed view of popup \"{popup.Model.Name}\" at \"{popup.Model.FullPath}\".</summary>");
            sb.AppendLine($"        public partial class {popup.ClassName} : PopupView");
            sb.AppendLine("        {");

            bool first = true;
            foreach (UiFieldPlan field in popup.Fields)
                AppendFieldDeclaration(sb, field, "            ", ref first);

            sb.AppendLine();
            sb.AppendLine("            internal void BindGeneratedFields(UiPageBase page)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (Root == null)");
            sb.AppendLine("                    return;");
            if (popup.Fields.Count > 0)
                sb.AppendLine();
            foreach (UiFieldPlan field in popup.Fields)
            {
                switch (field.Binding)
                {
                    case UiFieldBinding.WidgetInstance:
                        sb.AppendLine($"                BindGenerated({field.FieldName}, \"{field.Element.RelativePath}\");");
                        break;
                    case UiFieldBinding.AutoButton:
                        sb.AppendLine($"                {field.FieldName} = page.GetButtonView(FindElement(\"{field.Element.RelativePath}\"));");
                        break;
                    default:
                        sb.AppendLine($"                {field.FieldName} = FindElement(\"{field.Element.RelativePath}\");");
                        break;
                }
            }

            sb.AppendLine("            }");

            sb.AppendLine();
            sb.AppendLine("            protected override void OnUnwire()");
            sb.AppendLine("            {");
            foreach (UiFieldPlan field in popup.Fields)
                AppendFieldUnwire(sb, field, "                ");
            sb.AppendLine("                base.OnUnwire();");
            sb.AppendLine("            }");

            if (HasWidgetInstances(popup.Fields))
            {
                sb.AppendLine();
                sb.AppendLine("            private void BindGenerated(UiSubView view, string relativePath)");
                sb.AppendLine("            {");
                sb.AppendLine("                VisualElement element = FindElement(relativePath);");
                sb.AppendLine("                if (element != null)");
                sb.AppendLine("                    view.Bind(element);");
                sb.AppendLine("            }");
            }

            sb.AppendLine();
            sb.AppendLine("            private VisualElement FindElement(string relativePath)");
            sb.AppendLine("            {");
            sb.AppendLine("                VisualElement current = Root;");
            sb.AppendLine("                string[] segments = relativePath.Split('/');");
            sb.AppendLine("                for (int i = 0; i < segments.Length && current != null; i++)");
            sb.AppendLine("                    current = current.Q<VisualElement>(segments[i]);");
            sb.AppendLine();
            sb.AppendLine("                if (current == null)");
            sb.AppendLine("                    Debug.LogError($\"[UiKit] Element '{Path}/{relativePath}' was not found (renamed in the design?).\");");
            sb.AppendLine();
            sb.AppendLine("                return current;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
    }
}
