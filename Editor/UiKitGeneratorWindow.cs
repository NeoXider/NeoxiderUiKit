using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// The UI Kit editor window (Neoxider → UI Kit): FUI project selection, the scanned
    /// page/popup/element tree with inclusion checkboxes and widget-kind overrides, generation
    /// settings, the three action buttons (generate views, build scene objects, create the
    /// Neoxider adapter) and Play Mode dev tools. All generation runs only from here — never
    /// automatically on import.
    /// </summary>
    public sealed class UiKitGeneratorWindow : EditorWindow
    {
        private const string ConfigDefaultPath = "Assets/UiKit/UiKitConfig.asset";

        private UiKitConfig _config;
        private List<string> _projectFolders = new List<string>();
        private int _projectIndex;
        private UiScanResult _scan;
        private string _report = "";
        private Vector2 _scroll;
        private Vector2 _reportScroll;
        private bool _treeFoldout = true;
        private bool _settingsFoldout = true;
        private bool _devFoldout;
        private readonly Dictionary<string, bool> _pageFoldouts = new Dictionary<string, bool>();

        [MenuItem("Neoxider/UI Kit")]
        private static void Open()
        {
            var window = GetWindow<UiKitGeneratorWindow>();
            window.titleContent = new GUIContent("UI Kit");
            window.minSize = new Vector2(420f, 480f);
        }

        private void OnEnable()
        {
            _config = FindConfig();
            DetectProjects();
            Rescan();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawProjectSelection();
            EditorGUILayout.Space();
            DrawTree();
            EditorGUILayout.Space();
            DrawSettings();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            DrawDevTools();
            EditorGUILayout.Space();
            DrawReport();

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- project selection

        private void DrawProjectSelection()
        {
            EditorGUILayout.LabelField("FUI проект", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_projectFolders.Count == 0)
                {
                    EditorGUILayout.HelpBox("Папки с UXML-экранами (fui_type_screen) не найдены в Assets.", MessageType.Warning);
                }
                else
                {
                    int newIndex = EditorGUILayout.Popup(
                        new GUIContent("Папка", "Папка FUI-проекта с импортированными UXML-экранами."),
                        _projectIndex, _projectFolders.ToArray());
                    if (newIndex != _projectIndex)
                    {
                        _projectIndex = newIndex;
                        Rescan();
                    }
                }

                if (GUILayout.Button(new GUIContent("Обновить", "Пересканировать папки и UXML."), GUILayout.Width(80f)))
                {
                    DetectProjects();
                    Rescan();
                }
            }

            _config = (UiKitConfig)EditorGUILayout.ObjectField(
                new GUIContent("UiKitConfig", "Ассет конфигурации; создаётся автоматически при генерации."),
                _config, typeof(UiKitConfig), false);
        }

        private string SelectedProjectFolder =>
            _projectIndex >= 0 && _projectIndex < _projectFolders.Count ? _projectFolders[_projectIndex] : null;

        private void DetectProjects()
        {
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            string assetsPath = Application.dataPath;

            foreach (string file in Directory.GetFiles(assetsPath, "*.uxml", SearchOption.AllDirectories))
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }

                if (!text.Contains("fui_type_screen"))
                    continue;

                string dir = Path.GetDirectoryName(file)?.Replace('\\', '/');
                if (dir == null)
                    continue;

                if (string.Equals(Path.GetFileName(dir), "UXML", StringComparison.OrdinalIgnoreCase))
                    dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');

                if (dir != null && dir.StartsWith(assetsPath.Replace('\\', '/'), StringComparison.Ordinal))
                    folders.Add("Assets" + dir.Substring(assetsPath.Length));
            }

            _projectFolders = folders.ToList();

            string configured = _config != null ? _config.fuiProjectFolder : null;
            int index = string.IsNullOrEmpty(configured) ? -1 : _projectFolders.IndexOf(configured);
            _projectIndex = index >= 0 ? index : 0;
        }

        private void Rescan()
        {
            string folder = SelectedProjectFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _scan = null;
                return;
            }

            List<string> files = Directory.GetFiles(folder, "*.uxml", SearchOption.AllDirectories)
                .Select(f => f.Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            _scan = FuiUxmlScanner.Scan(files);
        }

        // ---------------------------------------------------------------- tree

        private void DrawTree()
        {
            _treeFoldout = EditorGUILayout.Foldout(_treeFoldout, "Страницы и элементы", true);
            if (!_treeFoldout)
                return;

            if (_scan == null || _scan.Pages.Count == 0)
            {
                EditorGUILayout.HelpBox("Экраны не найдены. Выберите папку FUI-проекта.", MessageType.Info);
                return;
            }

            foreach (UiPageModel page in _scan.Pages)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawIncludeToggle(page.PageId, "страницу");
                    if (!_pageFoldouts.TryGetValue(page.PageId, out bool open))
                        open = false;
                    _pageFoldouts[page.PageId] = EditorGUILayout.Foldout(open, page.PageId, true, EditorStyles.foldoutHeader);
                }

                if (!_pageFoldouts[page.PageId])
                    continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (UiElementModel element in page.Elements)
                        DrawElementRow(element);

                    foreach (UiPopupModel popup in page.Popups)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawIncludeToggle(popup.FullPath, "попап");
                            EditorGUILayout.LabelField(new GUIContent(popup.Name, popup.FullPath), EditorStyles.boldLabel);
                        }

                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (UiElementModel element in popup.Elements)
                                DrawElementRow(element);
                        }
                    }
                }
            }
        }

        private void DrawElementRow(UiElementModel element)
        {
            bool showService = _config != null && _config.includeServiceElements;
            if (element.IsService && !showService)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIncludeToggle(element.FullPath, "элемент");
                EditorGUILayout.LabelField(new GUIContent(element.Name, element.FullPath), GUILayout.MinWidth(140f));

                UiWidgetKind effective = EffectiveKind(element);
                var chosen = (UiWidgetKind)EditorGUILayout.EnumPopup(effective, GUILayout.Width(90f));
                if (chosen != effective)
                    SetKindOverride(element, chosen);

                if (IsCounterKind(effective))
                {
                    string id = EffectiveCounterId(element);
                    string newId = EditorGUILayout.DelayedTextField(id, GUILayout.Width(70f));
                    if (newId != id)
                        SetCounterIdOverride(element, newId);
                }
            }
        }

        private void DrawIncludeToggle(string path, string what)
        {
            bool included = !IsExcluded(path);
            bool toggled = EditorGUILayout.Toggle(
                new GUIContent("", $"Включить {what} в генерацию."), included, GUILayout.Width(28f));
            if (toggled != included)
                SetExcluded(path, !toggled);
        }

        // ---------------------------------------------------------------- overrides

        private UiKitConfig.ScanOverrideEntry GetOverride(string path, bool create)
        {
            UiKitConfig config = create ? EnsureConfig() : _config;
            if (config == null)
                return null;

            foreach (UiKitConfig.ScanOverrideEntry entry in config.scanOverrides)
            {
                if (entry != null && entry.elementPath == path)
                    return entry;
            }

            if (!create)
                return null;

            var created = new UiKitConfig.ScanOverrideEntry { elementPath = path };
            config.scanOverrides.Add(created);
            return created;
        }

        private void CompactOverride(UiKitConfig.ScanOverrideEntry entry)
        {
            if (entry != null && !entry.excluded && string.IsNullOrEmpty(entry.widgetKind) &&
                string.IsNullOrEmpty(entry.counterId))
            {
                _config.scanOverrides.Remove(entry);
            }

            EditorUtility.SetDirty(_config);
        }

        private bool IsExcluded(string path)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(path, false);
            return entry != null && entry.excluded;
        }

        private void SetExcluded(string path, bool excluded)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(path, true);
            if (entry == null)
                return;

            Undo.RecordObject(_config, "UiKit Override");
            entry.excluded = excluded;
            CompactOverride(entry);
        }

        private UiWidgetKind EffectiveKind(UiElementModel element)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(element.FullPath, false);
            return entry != null ? UiScanOverrides.ParseKind(entry.widgetKind) ?? element.Widget : element.Widget;
        }

        private string EffectiveCounterId(UiElementModel element)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(element.FullPath, false);
            if (entry != null && !string.IsNullOrEmpty(entry.counterId))
                return entry.counterId;

            return element.CounterId ?? DeriveCounterId(element.Name);
        }

        private void SetKindOverride(UiElementModel element, UiWidgetKind kind)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(element.FullPath, true);
            if (entry == null)
                return;

            Undo.RecordObject(_config, "UiKit Override");
            entry.widgetKind = kind == element.Widget ? "" : kind.ToString();
            CompactOverride(entry);
        }

        private void SetCounterIdOverride(UiElementModel element, string counterId)
        {
            UiKitConfig.ScanOverrideEntry entry = GetOverride(element.FullPath, true);
            if (entry == null)
                return;

            Undo.RecordObject(_config, "UiKit Override");
            entry.counterId = counterId == element.CounterId ? "" : counterId;
            CompactOverride(entry);
        }

        private static bool IsCounterKind(UiWidgetKind kind)
        {
            return kind == UiWidgetKind.Counter || kind == UiWidgetKind.Score || kind == UiWidgetKind.Level;
        }

        private static string DeriveCounterId(string name)
        {
            const string prefix = "panel_";
            return name != null && name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
        }

        // ---------------------------------------------------------------- settings

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Настройки", true);
            if (!_settingsFoldout)
                return;

            UiKitConfig config = _config;
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string ns = EditorGUILayout.TextField(
                    new GUIContent("Namespace", "Namespace сгенерированного кода. Оставьте пустым для генерации без namespace (глобальная область)."),
                    config != null ? config.generatorNamespace : "Game.Ui");
                string output = EditorGUILayout.TextField(
                    new GUIContent("Папка вывода", "Корневая папка сгенерированного кода и документации."),
                    config != null ? config.generatorOutputRoot : "Assets/UiKit");
                bool service = EditorGUILayout.Toggle(
                    new GUIContent("Служебные элементы", "Включать spacer_*/layout_group_* в генерацию."),
                    config != null && config.includeServiceElements);
                string money = EditorGUILayout.TextField(
                    new GUIContent("Money counter id", "Счётчик, доступный как UiKit.Money."),
                    config != null ? config.generatorMoneyCounterId : "coin");

                if (check.changed)
                {
                    config = EnsureConfig();
                    Undo.RecordObject(config, "UiKit Settings");
                    config.generatorNamespace = ns;
                    config.generatorOutputRoot = output;
                    config.includeServiceElements = service;
                    config.generatorMoneyCounterId = money;
                    EditorUtility.SetDirty(config);
                }
            }

            DrawPageSettings();
        }

        private void DrawPageSettings()
        {
            if (_scan == null || _scan.Pages.Count == 0)
                return;

            EditorGUILayout.LabelField("Анимации страниц", EditorStyles.boldLabel);
            string[] presets = UiAnimations.Names.Distinct().ToArray();

            string[] pageIds = _scan.Pages.Select(p => p.PageId).ToArray();
            int startIndex = Array.FindIndex(pageIds, id => _config != null && (_config.GetPage(id)?.isStart ?? false));
            int newStart = EditorGUILayout.Popup(
                new GUIContent("Стартовая страница", "Показывается при запуске."),
                Mathf.Max(startIndex, 0), pageIds);
            if (newStart != startIndex && newStart >= 0)
                SetStartPage(pageIds[newStart]);

            foreach (UiPageModel page in _scan.Pages)
            {
                UiKitConfig.PageEntry entry = _config != null ? _config.GetPage(page.PageId) : null;
                string showPreset = entry?.showPreset ?? "fade";
                string popupPreset = entry?.popupPreset ?? "scale-pop";
                UiAnimationMode mode = entry?.animationMode ?? UiAnimationMode.ForwardAndBackward;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(page.PageId, GUILayout.Width(90f));

                    int presetIndex = Mathf.Max(Array.IndexOf(presets, showPreset), 0);
                    int newPreset = EditorGUILayout.Popup(presetIndex, presets);

                    int popupIndex = Mathf.Max(Array.IndexOf(presets, popupPreset), 0);
                    int newPopup = EditorGUILayout.Popup(popupIndex, presets);

                    var newMode = (UiAnimationMode)EditorGUILayout.EnumPopup(mode, GUILayout.Width(140f));

                    if (newPreset != presetIndex || newPopup != popupIndex || newMode != mode)
                    {
                        entry = GetOrCreatePageEntry(page.PageId);
                        Undo.RecordObject(_config, "UiKit Page Settings");
                        entry.showPreset = presets[newPreset];
                        entry.popupPreset = presets[newPopup];
                        entry.animationMode = newMode;
                        EditorUtility.SetDirty(_config);
                    }
                }
            }

            EditorGUILayout.LabelField(" ", "пресет показа / пресет попапов / режим", EditorStyles.miniLabel);
        }

        private void SetStartPage(string pageId)
        {
            UiKitConfig config = EnsureConfig();
            Undo.RecordObject(config, "UiKit Start Page");
            GetOrCreatePageEntry(pageId);
            foreach (UiKitConfig.PageEntry entry in config.pages)
            {
                if (entry != null)
                    entry.isStart = entry.pageId == pageId;
            }

            EditorUtility.SetDirty(config);
        }

        private UiKitConfig.PageEntry GetOrCreatePageEntry(string pageId)
        {
            UiKitConfig config = EnsureConfig();
            UiKitConfig.PageEntry entry = config.GetPage(pageId);
            if (entry == null)
            {
                entry = new UiKitConfig.PageEntry { pageId = pageId };
                config.pages.Add(entry);
                EditorUtility.SetDirty(config);
            }

            return entry;
        }

        // ---------------------------------------------------------------- actions

        private void DrawActions()
        {
            using (new EditorGUI.DisabledScope(_scan == null || _scan.Pages.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Сгенерировать вьюшки",
                        "Скан UXML, кодогенерация вьюшек, обновление UiKitConfig и diff-отчёт."), GUILayout.Height(28f)))
                {
                    GenerateViews();
                }
            }

            using (new EditorGUI.DisabledScope(_config == null || _config.pages.Count == 0 || EditorApplication.isPlaying))
            {
                if (GUILayout.Button(new GUIContent("Создать объекты на сцене",
                        "Создать/обновить корень UI с UiKitBootstrap и объекты страниц в активной сцене."), GUILayout.Height(28f)))
                {
                    BuildSceneObjects();
                }
            }

            if (GUILayout.Button(new GUIContent("Создать адаптер Neoxider",
                    "Сгенерировать файл-мост к NeoxiderTools (Assets/UiKit/NeoxiderUiAdapter.cs)."), GUILayout.Height(28f)))
            {
                CreateAdapter();
            }
        }

        private void GenerateViews()
        {
            UiKitConfig config = EnsureConfig();
            string folder = SelectedProjectFolder;
            config.fuiProjectFolder = folder;

            string root = string.IsNullOrEmpty(config.generatorOutputRoot) ? "Assets/UiKit" : config.generatorOutputRoot.TrimEnd('/');
            var settings = new UiKitGenerationSettings
            {
                uxmlFolder = folder,
                outputFolder = root + "/Generated",
                userViewsFolder = root + "/Views",
                docFolder = root,
                rootNamespace = config.generatorNamespace,
                includeServiceElements = config.includeServiceElements,
                overrides = config.scanOverrides
            };

            UiKitGenerationResult result = UiKitGenerator.Generate(settings);
            List<string> configChanges = result.Scan.Pages.Count > 0
                ? UiKitConfigUpdater.Update(config, result.Scan)
                : new List<string>();

            AssetDatabase.SaveAssets();

            _report = result.DiffReport;
            if (configChanges.Count > 0)
            {
                _report += "\n\nUiKitConfig:\n" + string.Join("\n", configChanges);
                Debug.Log($"[UiKit] Config updated ({configChanges.Count} change(s)):\n" + string.Join("\n", configChanges));
            }

            Rescan();
        }

        private void BuildSceneObjects()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
                sceneName = "Untitled";

            if (!EditorUtility.DisplayDialog("UI Kit",
                    $"Создать/обновить объекты UI в активной сцене «{sceneName}»?\n\n" +
                    "Существующие страницы получат только ссылки uxml/PanelSettings/конфига; " +
                    "трансформы и добавленные компоненты не изменяются.",
                    "Создать", "Отмена"))
            {
                return;
            }

            SceneBuildReport report = SceneObjectBuilder.Build(_config, _config.generatorNamespace);
            _report = report.Summary;
            Debug.Log(report.Summary);
        }

        private void CreateAdapter()
        {
            string ns = _config != null ? _config.generatorNamespace : "Game.Ui";
            if (File.Exists(NeoxiderAdapterGenerator.DefaultPath) &&
                !EditorUtility.DisplayDialog("UI Kit",
                    $"Файл {NeoxiderAdapterGenerator.DefaultPath} уже существует. Перезаписать?",
                    "Перезаписать", "Отмена"))
            {
                return;
            }

            string path = NeoxiderAdapterGenerator.Generate(NeoxiderAdapterGenerator.DefaultPath, ns);
            _report = $"[UiKit] Адаптер создан: {path}";
            Debug.Log(_report);
        }

        // ---------------------------------------------------------------- dev tools

        private void DrawDevTools()
        {
            _devFoldout = EditorGUILayout.Foldout(_devFoldout, "Dev-инструменты (Play Mode)", true);
            if (!_devFoldout)
                return;

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Доступно только в Play Mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Страницы", EditorStyles.boldLabel);
            foreach (string pageId in UiKit.Pages.Ids.OrderBy(id => id, StringComparer.Ordinal).ToList())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pageId);
                    if (GUILayout.Button("Открыть", GUILayout.Width(90f)))
                        UiKit.Pages.Show(pageId);
                }
            }

            if (_scan == null)
                return;

            EditorGUILayout.LabelField("Попапы", EditorStyles.boldLabel);
            foreach (UiPageModel page in _scan.Pages)
            {
                foreach (UiPopupModel popup in page.Popups)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(popup.FullPath);
                        if (GUILayout.Button("Открыть", GUILayout.Width(90f)))
                            UiKit.Popups.Open(popup.FullPath);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- report + config

        private void DrawReport()
        {
            if (string.IsNullOrEmpty(_report))
                return;

            EditorGUILayout.LabelField("Отчёт", EditorStyles.boldLabel);
            _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.MaxHeight(160f));
            EditorGUILayout.TextArea(_report, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private static UiKitConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:UiKitConfig");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<UiKitConfig>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private UiKitConfig EnsureConfig()
        {
            if (_config != null)
                return _config;

            _config = FindConfig();
            if (_config != null)
                return _config;

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigDefaultPath));
            _config = CreateInstance<UiKitConfig>();
            AssetDatabase.CreateAsset(_config, ConfigDefaultPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UiKit] Created config asset at {ConfigDefaultPath}.");
            return _config;
        }
    }
}
