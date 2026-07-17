using System.Collections.Generic;
using System.Text;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Emits UiKitApi.md — a Russian cheatsheet of all generated pages, popups, fields, paths
    /// and counters with ready-to-copy code examples. Regenerated on every run.
    /// </summary>
    public static class ApiDocGenerator
    {
        /// <summary>Emits the UiKitApi.md content for all planned pages.</summary>
        public static string Generate(IReadOnlyList<UiPagePlan> plans, UiKitGenerationSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# UiKit API — шпаргалка");
            sb.AppendLine();
            sb.AppendLine("Файл сгенерирован автоматически (UiKitGenerator) и обновляется при каждой генерации — не редактируйте вручную.");
            sb.AppendLine();
            sb.AppendLine($"Namespace сгенерированного кода: `{settings.EffectiveNamespace}`.");
            sb.AppendLine();

            AppendQuickStart(sb, plans);

            sb.AppendLine("## Страницы");
            sb.AppendLine();
            foreach (UiPagePlan plan in plans)
                AppendPage(sb, plan);

            AppendCounters(sb, plans);

            sb.AppendLine("## Правила");
            sb.AppendLine();
            sb.AppendLine("- Поля страниц перепривязываются при каждом reload панели: подписки на события делайте в `OnBind()` partial-класса страницы.");
            sb.AppendLine("- Кнопочные поля (`ButtonView`) переиспользуют автоматическую вьюшку страницы (анимация нажатия, звук, троттлинг) и валидны только пока страница привязана.");
            sb.AppendLine("- Пользовательский код пишите в partial-файле `<Page>View.cs` — он создаётся один раз и не перезаписывается генератором.");
            sb.AppendLine("- Сырые строки путей — fallback; используйте константы `UiIds`, тогда переименование в дизайне превратится в ошибку компиляции.");

            return sb.ToString();
        }

        private static void AppendQuickStart(StringBuilder sb, IReadOnlyList<UiPagePlan> plans)
        {
            UiPagePlan firstPage = plans.Count > 0 ? plans[0] : null;
            UiPopupPlan firstPopup = null;
            UiPagePlan popupPage = null;
            foreach (UiPagePlan plan in plans)
            {
                if (plan.Popups.Count > 0)
                {
                    popupPage = plan;
                    firstPopup = plan.Popups[0];
                    break;
                }
            }

            sb.AppendLine("## Быстрый старт");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            if (firstPage != null)
            {
                string pageClass = NameSanitizer.ToPascalIdentifier(firstPage.Model.PageId);
                sb.AppendLine($"UiKit.Pages.Show(UiIds.{pageClass}.Id);                 // показать страницу");
                sb.AppendLine($"var page = UiKit.Get<{firstPage.ClassName}>();          // типизированная вьюшка страницы");
            }

            if (firstPopup != null && popupPage != null)
            {
                string pageClass = NameSanitizer.ToPascalIdentifier(popupPage.Model.PageId);
                sb.AppendLine($"UiKit.Get<{popupPage.ClassName}>().{firstPopup.FieldName}.Open();  // открыть попап");
                sb.AppendLine($"var result = await UiKit.Popups.OpenAsync(UiIds.{pageClass}.{firstPopup.FieldName}.Path); // модальный попап");
            }

            sb.AppendLine("UiKit.Money.Set(1500);                                 // счётчик денег (алиас в конфиге)");
            sb.AppendLine("UiKit.OnClick(\"pageId/button_x\", () => { });           // клик по пути (переживает reload)");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        private static void AppendPage(StringBuilder sb, UiPagePlan plan)
        {
            sb.AppendLine($"### `{plan.Model.PageId}` — {plan.ClassName}");
            sb.AppendLine();
            sb.AppendLine($"UXML: `{plan.Model.UxmlPath}`");
            sb.AppendLine();

            if (plan.Fields.Count > 0)
            {
                AppendFieldTable(sb, plan.Fields, plan.ClassName);
                sb.AppendLine();
            }

            foreach (UiPopupPlan popup in plan.Popups)
            {
                sb.AppendLine($"#### Попап `{popup.Model.Name}` — поле `{popup.FieldName}` ({popup.ClassName})");
                sb.AppendLine();
                sb.AppendLine($"Путь: `{popup.Model.FullPath}` — `UiKit.Popups.Open(...)` / `OpenAsync(...)`.");
                sb.AppendLine();
                if (popup.Fields.Count > 0)
                {
                    AppendFieldTable(sb, popup.Fields, $"{plan.ClassName}.{popup.FieldName}");
                    sb.AppendLine();
                }
            }
        }

        private static void AppendFieldTable(StringBuilder sb, List<UiFieldPlan> fields, string owner)
        {
            sb.AppendLine("| Поле | Тип | Путь |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (UiFieldPlan field in fields)
                sb.AppendLine($"| `{owner}.{field.FieldName}` | `{field.TypeName}` | `{field.Element.FullPath}` |");
        }

        private static void AppendCounters(StringBuilder sb, IReadOnlyList<UiPagePlan> plans)
        {
            List<KeyValuePair<string, string>> counters = UiIdsGenerator.CollectCounters(plans);
            if (counters.Count == 0)
                return;

            sb.AppendLine("## Счётчики");
            sb.AppendLine();
            sb.AppendLine("| Id | Константа | Пример |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (KeyValuePair<string, string> counter in counters)
            {
                sb.AppendLine($"| `{counter.Value}` | `UiIds.Counters.{counter.Key}` | `UiKit.Counters[UiIds.Counters.{counter.Key}].Set(10);` |");
            }

            sb.AppendLine();
        }
    }
}
