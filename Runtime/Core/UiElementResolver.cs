using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Resolves page-relative element paths ("element", "popup_x/element", deeper chains).
    /// Element names duplicate across popups in imported UXML, so every segment must be
    /// unambiguous within its scope; ambiguity produces an error listing all candidates.
    /// </summary>
    internal static class UiElementResolver
    {
        /// <summary>
        /// Resolves a path relative to a page root. Returns null and fills <paramref name="error"/>
        /// when the path is missing or ambiguous.
        /// </summary>
        public static VisualElement Resolve(VisualElement pageRoot, string pageId, string relativePath, out string error)
        {
            error = null;
            if (pageRoot == null)
            {
                error = $"[UiKit] Page '{pageId}' is not bound; cannot resolve '{relativePath}'.";
                return null;
            }

            string[] segments = relativePath.Split('/');
            VisualElement scope = pageRoot;

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                {
                    error = $"[UiKit] Empty segment in path '{pageId}/{relativePath}'.";
                    return null;
                }

                List<VisualElement> matches = scope.Query<VisualElement>(segment).ToList();
                matches.Remove(scope);

                if (matches.Count == 0)
                {
                    error = $"[UiKit] Element '{segment}' not found in scope '{DescribeScope(pageId, segments, i)}' " +
                            $"while resolving '{pageId}/{relativePath}'.";
                    return null;
                }

                if (matches.Count > 1)
                {
                    error = $"[UiKit] Element name '{segment}' is ambiguous in scope '{DescribeScope(pageId, segments, i)}' " +
                            $"({matches.Count} matches). Use a scoped path. Candidates: {DescribeCandidates(pageId, matches)}";
                    return null;
                }

                scope = matches[0];
            }

            return scope;
        }

        private static string DescribeScope(string pageId, string[] segments, int index)
        {
            var sb = new StringBuilder(pageId);
            for (int i = 0; i < index; i++)
            {
                sb.Append('/');
                sb.Append(segments[i]);
            }

            return sb.ToString();
        }

        private static string DescribeCandidates(string pageId, List<VisualElement> matches)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < matches.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('\'');
                sb.Append(DescribePath(pageId, matches[i]));
                sb.Append('\'');
            }

            return sb.ToString();
        }

        /// <summary>Builds a readable path for an element: pageId + containing popup (if any) + name.</summary>
        public static string DescribePath(string pageId, VisualElement element)
        {
            string popup = null;
            VisualElement current = element.parent;
            while (current != null)
            {
                if (current.ClassListContains("fui_type_popup") && !string.IsNullOrEmpty(current.name))
                {
                    popup = current.name;
                    break;
                }

                current = current.parent;
            }

            return popup != null ? $"{pageId}/{popup}/{element.name}" : $"{pageId}/{element.name}";
        }
    }
}
