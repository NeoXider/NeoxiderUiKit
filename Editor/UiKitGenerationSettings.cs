using System;
using System.Collections.Generic;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Settings of one generation run. Used by <see cref="UiKitGenerator.Generate"/> from the
    /// editor window and from tests; generation never runs automatically on import.
    /// </summary>
    [Serializable]
    public sealed class UiKitGenerationSettings
    {
        /// <summary>Explicit UXML file paths; when empty, <see cref="uxmlFolder"/> is scanned for *.uxml.</summary>
        public List<string> uxmlPaths = new List<string>();

        /// <summary>Folder scanned recursively for *.uxml when <see cref="uxmlPaths"/> is empty.</summary>
        public string uxmlFolder = "";

        /// <summary>Output folder of regenerated files (*.g.cs, UiIds.g.cs, model snapshot).</summary>
        public string outputFolder = "Assets/UiKit/Generated";

        /// <summary>Folder of user partial classes; files there are created once and never overwritten.</summary>
        public string userViewsFolder = "Assets/UiKit/Views";

        /// <summary>Folder of the generated UiKitApi.md cheatsheet.</summary>
        public string docFolder = "Assets/UiKit";

        /// <summary>Namespace of the generated code.</summary>
        public string rootNamespace = "Game.Ui";

        /// <summary>Optional FUI project name; adds an output subfolder and a sub-namespace.</summary>
        public string projectName = "";

        /// <summary>When true, spacer_*/layout_group_* elements also become fields.</summary>
        public bool includeServiceElements;

        /// <summary>Scanner overrides applied after the scan (exclusions, widget kinds, counter ids).</summary>
        public List<UiKitConfig.ScanOverrideEntry> overrides = new List<UiKitConfig.ScanOverrideEntry>();

        /// <summary>When false, UiKitApi.md is not written.</summary>
        public bool writeApiDoc = true;

        /// <summary>When true, AssetDatabase.Refresh is called after writing files under Assets.</summary>
        public bool refreshAssets = true;

        /// <summary>Namespace including the optional project sub-namespace.</summary>
        public string EffectiveNamespace =>
            string.IsNullOrEmpty(projectName)
                ? rootNamespace
                : rootNamespace + "." + NameSanitizer.ToPascalIdentifier(projectName);

        /// <summary>Output folder including the optional project subfolder.</summary>
        public string EffectiveOutputFolder =>
            string.IsNullOrEmpty(projectName)
                ? outputFolder
                : outputFolder.TrimEnd('/') + "/" + NameSanitizer.ToPascalIdentifier(projectName);
    }
}
