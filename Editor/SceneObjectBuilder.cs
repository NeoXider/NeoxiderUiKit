using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit.Editor
{
    /// <summary>Result of one scene build run.</summary>
    public sealed class SceneBuildReport
    {
        /// <summary>Page ids whose GameObjects were created on this run.</summary>
        public List<string> Created = new List<string>();

        /// <summary>Page ids whose existing GameObjects were updated (assets/config refs only).</summary>
        public List<string> Updated = new List<string>();

        /// <summary>Page ids found in the scene but missing from the config (reported, never deleted).</summary>
        public List<string> Orphaned = new List<string>();

        /// <summary>Non-fatal problems (missing generated types, duplicate bootstraps, ...).</summary>
        public List<string> Errors = new List<string>();

        /// <summary>Human-readable summary of the run.</summary>
        public string Summary =>
            $"[UiKit] Scene build: created {Created.Count}, updated {Updated.Count}" +
            (Orphaned.Count > 0 ? $", orphaned in scene: [{string.Join(", ", Orphaned)}]" : "") +
            (Errors.Count > 0 ? $"\nErrors:\n- {string.Join("\n- ", Errors)}" : "");
    }

    /// <summary>
    /// Creates/updates the scene objects of the kit: a root "UI" GameObject with a single
    /// <see cref="UiKitBootstrap"/> and one child per configured page (PanelRenderer + generated
    /// page component). Idempotency is keyed by the pageId serialized on the page component, not
    /// by GameObject names: existing page objects only get their uxml/PanelSettings/config
    /// references refreshed; transforms, active state and extra components stay untouched.
    /// Pages present in the scene but missing from the config are reported, never deleted.
    /// </summary>
    public static class SceneObjectBuilder
    {
        private const string UndoGroupName = "UiKit Scene Objects";

        /// <summary>
        /// Builds/updates the scene objects for every page of the config. Generated page component
        /// types are resolved by name ("&lt;PageId&gt;View") in the given namespace via reflection.
        /// </summary>
        public static SceneBuildReport Build(UiKitConfig config, string viewNamespace)
        {
            var report = new SceneBuildReport();
            if (config == null)
            {
                report.Errors.Add("No UiKitConfig assigned.");
                return report;
            }

            Undo.SetCurrentGroupName(UndoGroupName);

            GameObject root = EnsureBootstrap(config, report);
            Dictionary<string, UiPageBase> scenePages = CollectScenePages();

            var configuredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiKitConfig.PageEntry entry in config.pages)
            {
                if (entry == null || string.IsNullOrEmpty(entry.pageId))
                    continue;

                configuredIds.Add(entry.pageId);
                if (scenePages.TryGetValue(entry.pageId, out UiPageBase existing))
                {
                    UpdatePage(existing, entry);
                    report.Updated.Add(entry.pageId);
                }
                else
                {
                    CreatePage(root.transform, entry, viewNamespace, report);
                }
            }

            foreach (string pageId in scenePages.Keys)
            {
                if (!configuredIds.Contains(pageId))
                    report.Orphaned.Add(pageId);
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return report;
        }

        private static GameObject EnsureBootstrap(UiKitConfig config, SceneBuildReport report)
        {
            UiKitBootstrap[] bootstraps =
                UnityEngine.Object.FindObjectsByType<UiKitBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            GameObject root;
            UiKitBootstrap bootstrap;
            if (bootstraps.Length > 0)
            {
                if (bootstraps.Length > 1)
                    report.Errors.Add($"Scene contains {bootstraps.Length} UiKitBootstrap components; using '{bootstraps[0].name}'.");

                bootstrap = bootstraps[0];
                root = bootstrap.gameObject;
                Undo.RecordObject(bootstrap, UndoGroupName);
            }
            else
            {
                root = GameObject.Find("UI");
                if (root == null)
                {
                    root = new GameObject("UI");
                    Undo.RegisterCreatedObjectUndo(root, UndoGroupName);
                }
                else
                {
                    Undo.RecordObject(root, UndoGroupName);
                }

                bootstrap = Undo.AddComponent<UiKitBootstrap>(root);
            }

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            return root;
        }

        private static Dictionary<string, UiPageBase> CollectScenePages()
        {
            var pages = new Dictionary<string, UiPageBase>(StringComparer.Ordinal);
            UiPageBase[] found =
                UnityEngine.Object.FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (UiPageBase page in found)
            {
                if (!string.IsNullOrEmpty(page.PageId) && !pages.ContainsKey(page.PageId))
                    pages.Add(page.PageId, page);
            }

            return pages;
        }

        private static void UpdatePage(UiPageBase page, UiKitConfig.PageEntry entry)
        {
            PanelRenderer renderer = page.GetComponent<PanelRenderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, UndoGroupName);
                if (entry.visualTreeAsset != null)
                    renderer.visualTreeAsset = entry.visualTreeAsset;
                if (entry.panelSettings != null)
                    renderer.panelSettings = entry.panelSettings;
            }

            var serialized = new SerializedObject(page);
            serialized.FindProperty("panelRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
        }

        private static void CreatePage(Transform root, UiKitConfig.PageEntry entry, string viewNamespace,
            SceneBuildReport report)
        {
            Type viewType = ResolvePageType(viewNamespace, entry.pageId);
            if (viewType == null)
            {
                report.Errors.Add($"Generated view class '{ExpectedClassName(entry.pageId)}' not found in namespace " +
                                  $"'{viewNamespace}'. Run generation first and let the scripts compile.");
                return;
            }

            var go = new GameObject(entry.pageId);
            Undo.RegisterCreatedObjectUndo(go, UndoGroupName);
            go.transform.SetParent(root, false);

            var page = (UiPageBase)go.AddComponent(viewType);
            PanelRenderer renderer = go.GetComponent<PanelRenderer>();
            if (entry.visualTreeAsset != null)
                renderer.visualTreeAsset = entry.visualTreeAsset;
            if (entry.panelSettings != null)
                renderer.panelSettings = entry.panelSettings;

            var serialized = new SerializedObject(page);
            serialized.FindProperty("pageId").stringValue = entry.pageId;
            serialized.FindProperty("panelRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();

            if (entry.isStart)
                go.AddComponent<UiFakeLoading>();
            else
                go.SetActive(false);

            report.Created.Add(entry.pageId);
        }

        /// <summary>Class name expected for a page id ("gameplay" -> "GameplayView").</summary>
        public static string ExpectedClassName(string pageId)
        {
            return NameSanitizer.ToPascalIdentifier(pageId) + "View";
        }

        /// <summary>
        /// Resolves the generated page component type by name via reflection: an exact
        /// namespace-qualified match first, then any <see cref="UiPageBase"/> subclass of that name.
        /// </summary>
        public static Type ResolvePageType(string viewNamespace, string pageId)
        {
            string className = ExpectedClassName(pageId);
            string fullName = string.IsNullOrEmpty(viewNamespace) ? className : viewNamespace + "." + className;

            Type fallback = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.IsAbstract || !typeof(UiPageBase).IsAssignableFrom(type))
                        continue;

                    if (type.FullName == fullName)
                        return type;

                    if (type.Name == className && fallback == null)
                        fallback = type;
                }
            }

            return fallback;
        }
    }
}
