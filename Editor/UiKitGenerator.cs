using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Neo.UIKit.Editor
{
    /// <summary>Result of one generation run.</summary>
    public sealed class UiKitGenerationResult
    {
        /// <summary>Regenerated files that were written or updated on this run.</summary>
        public List<string> WrittenFiles = new List<string>();

        /// <summary>Regenerated files whose content did not change.</summary>
        public List<string> UnchangedFiles = new List<string>();

        /// <summary>User partial files created on this run.</summary>
        public List<string> CreatedUserFiles = new List<string>();

        /// <summary>User partial files left untouched because they already exist.</summary>
        public List<string> SkippedUserFiles = new List<string>();

        /// <summary>Human-readable diff of the model against the previous generation.</summary>
        public string DiffReport = "";

        /// <summary>Scan result the code was generated from.</summary>
        public UiScanResult Scan;

        /// <summary>Page plans (naming) the code was generated from.</summary>
        public List<UiPagePlan> Plans = new List<UiPagePlan>();
    }

    /// <summary>
    /// Entry point of the view generator. Generation runs only when explicitly invoked
    /// (editor window button or tests) — never automatically on import.
    /// </summary>
    public static class UiKitGenerator
    {
        private const string SnapshotFileName = "UiKitModel.g.txt";

        /// <summary>
        /// Scans the configured UXML files and (re)generates per-page views, user partials,
        /// UiIds.g.cs and UiKitApi.md. Logs and returns a diff against the previous run.
        /// </summary>
        public static UiKitGenerationResult Generate(UiKitGenerationSettings settings)
        {
            var result = new UiKitGenerationResult();

            List<string> uxmlFiles = ResolveUxmlFiles(settings);
            result.Scan = FuiUxmlScanner.Scan(uxmlFiles);
            UiScanOverrides.Apply(result.Scan, settings.overrides);

            foreach (string warning in result.Scan.Warnings)
                Debug.LogWarning($"[UiKit] {warning}");

            if (result.Scan.Pages.Count == 0)
            {
                result.DiffReport = "[UiKit] Generation skipped: no fui_type_screen pages found.";
                Debug.LogWarning(result.DiffReport);
                return result;
            }

            foreach (UiPageModel page in result.Scan.Pages)
                result.Plans.Add(ViewCodeGenerator.Plan(page, settings));

            string outputFolder = settings.EffectiveOutputFolder;
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(settings.userViewsFolder);

            string snapshotPath = Path.Combine(outputFolder, SnapshotFileName).Replace('\\', '/');
            string[] previousSnapshot = File.Exists(snapshotPath) ? File.ReadAllLines(snapshotPath) : null;
            List<string> snapshot = BuildSnapshot(result.Plans);
            result.DiffReport = BuildDiffReport(previousSnapshot, snapshot);

            foreach (UiPagePlan plan in result.Plans)
            {
                string generatedPath = $"{outputFolder}/{plan.ClassName}.g.cs".Replace('\\', '/');
                WriteGenerated(generatedPath, ViewCodeGenerator.GeneratePageSource(plan, settings), result);

                string userPath = $"{settings.userViewsFolder}/{plan.ClassName}.cs".Replace('\\', '/');
                if (File.Exists(userPath))
                {
                    result.SkippedUserFiles.Add(userPath);
                }
                else
                {
                    WriteFile(userPath, ViewCodeGenerator.GenerateUserPartialSource(plan, settings));
                    result.CreatedUserFiles.Add(userPath);
                }
            }

            string uiIdsPath = $"{outputFolder}/UiIds.g.cs".Replace('\\', '/');
            WriteGenerated(uiIdsPath, UiIdsGenerator.Generate(result.Plans, settings), result);

            if (settings.writeApiDoc)
            {
                Directory.CreateDirectory(settings.docFolder);
                string docPath = $"{settings.docFolder}/UiKitApi.md".Replace('\\', '/');
                WriteGenerated(docPath, ApiDocGenerator.Generate(result.Plans, settings), result);
            }

            WriteFile(snapshotPath, string.Join("\n", snapshot) + "\n");

            Debug.Log($"[UiKit] Generated {result.Plans.Count} page view(s) into '{outputFolder}'. " +
                      $"Written: {result.WrittenFiles.Count}, unchanged: {result.UnchangedFiles.Count}, " +
                      $"user partials created: {result.CreatedUserFiles.Count}.\n{result.DiffReport}");

            if (settings.refreshAssets)
                AssetDatabase.Refresh();

            return result;
        }

        private static List<string> ResolveUxmlFiles(UiKitGenerationSettings settings)
        {
            var files = new List<string>();

            if (settings.uxmlPaths != null && settings.uxmlPaths.Count > 0)
                files.AddRange(settings.uxmlPaths);
            else if (!string.IsNullOrEmpty(settings.uxmlFolder) && Directory.Exists(settings.uxmlFolder))
                files.AddRange(Directory.GetFiles(settings.uxmlFolder, "*.uxml", SearchOption.AllDirectories));

            return files.Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        private static void WriteGenerated(string path, string content, UiKitGenerationResult result)
        {
            content = content.Replace("\r\n", "\n");
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == content)
            {
                result.UnchangedFiles.Add(path);
                return;
            }

            WriteFile(path, content);
            result.WrittenFiles.Add(path);
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        private static List<string> BuildSnapshot(List<UiPagePlan> plans)
        {
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UiPagePlan plan in plans)
            {
                lines.Add($"page|{plan.Model.PageId}");
                foreach (UiFieldPlan field in plan.Fields)
                    lines.Add($"element|{field.Element.FullPath}|{field.Element.Widget}");
                foreach (UiPopupPlan popup in plan.Popups)
                {
                    lines.Add($"popup|{popup.Model.FullPath}");
                    foreach (UiFieldPlan field in popup.Fields)
                        lines.Add($"element|{field.Element.FullPath}|{field.Element.Widget}");
                }
            }

            foreach (KeyValuePair<string, string> counter in UiIdsGenerator.CollectCounters(plans))
                lines.Add($"counter|{counter.Value}");

            return lines.ToList();
        }

        private static string BuildDiffReport(string[] previousLines, List<string> currentLines)
        {
            if (previousLines == null)
                return "[UiKit] First generation (no previous model to diff against).";

            var previous = ParseSnapshot(previousLines);
            var current = ParseSnapshot(currentLines);

            var sb = new StringBuilder();
            int changes = 0;

            foreach (KeyValuePair<string, string> entry in current)
            {
                if (!previous.TryGetValue(entry.Key, out string oldValue))
                {
                    sb.AppendLine($"+ {Describe(entry.Key, entry.Value)}");
                    changes++;
                }
                else if (oldValue != entry.Value)
                {
                    sb.AppendLine($"~ {entry.Key} ({oldValue} -> {entry.Value})");
                    changes++;
                }
            }

            foreach (KeyValuePair<string, string> entry in previous)
            {
                if (!current.ContainsKey(entry.Key))
                {
                    sb.AppendLine($"- {Describe(entry.Key, entry.Value)}");
                    changes++;
                }
            }

            return changes == 0
                ? "[UiKit] Model diff: no changes since the previous generation."
                : $"[UiKit] Model diff ({changes} change(s)):\n{sb.ToString().TrimEnd()}";
        }

        private static Dictionary<string, string> ParseSnapshot(IEnumerable<string> lines)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('|');
                string key = parts.Length >= 2 ? parts[0] + "|" + parts[1] : line;
                map[key] = parts.Length >= 3 ? parts[2] : "";
            }

            return map;
        }

        private static string Describe(string key, string value)
        {
            string entry = key.Replace("|", " ");
            return string.IsNullOrEmpty(value) ? entry : $"{entry} ({value})";
        }
    }
}
