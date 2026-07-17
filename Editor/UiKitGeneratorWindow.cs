using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// The UI Kit editor window (Neoxider → UI Kit), built with UI Toolkit. Two tabs: "Генерация"
    /// (FUI project, the scanned page/element tree with inclusion + widget-kind overrides, settings,
    /// the colored action buttons and the report) and "Dev-инструменты" (open pages/popups in Play
    /// Mode). All generation runs only from here — never automatically on import.
    /// </summary>
    public sealed class UiKitGeneratorWindow : EditorWindow
    {
        private const string ConfigDefaultPath = "Assets/UiKit/UiKitConfig.asset";

        // Palette.
        private static readonly Color Bg = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color Card = new Color(0.21f, 0.22f, 0.25f);
        private static readonly Color CardBorder = new Color(0.30f, 0.31f, 0.35f);
        private static readonly Color Accent = new Color(0.40f, 0.55f, 0.95f);
        private static readonly Color Green = new Color(0.22f, 0.55f, 0.30f);
        private static readonly Color Blue = new Color(0.16f, 0.42f, 0.78f);
        private static readonly Color Orange = new Color(0.80f, 0.38f, 0.10f);
        private static readonly Color TextDim = new Color(0.66f, 0.68f, 0.72f);

        private UiKitConfig _config;
        private List<string> _projectFolders = new List<string>();
        private int _projectIndex;
        private UiScanResult _scan;
        private string _report = "";

        private int _tab;
        private VisualElement _generationBody;
        private VisualElement _devBody;
        private Label _reportLabel;
        private VisualElement _reportCard;
        private Button _tabGen;
        private Button _tabDev;

        [MenuItem("Neoxider/UI Kit")]
        private static void Open()
        {
            var window = GetWindow<UiKitGeneratorWindow>();
            window.titleContent = new GUIContent("UI Kit");
            window.minSize = new Vector2(460f, 520f);
        }

        private void OnEnable()
        {
            _config = FindConfig();
            DetectProjects();
            Rescan();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.backgroundColor = Bg;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            root.Add(Header());
            root.Add(Tabs());

            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.contentContainer.style.flexShrink = 1;
            scroll.contentContainer.style.maxWidth = Length.Percent(100);
            root.Add(scroll);

            _generationBody = new VisualElement();
            _devBody = new VisualElement { style = { display = DisplayStyle.None } };
            scroll.Add(_generationBody);
            scroll.Add(_devBody);

            _reportCard = BuildCard("Отчёт");
            _reportLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, color = TextDim, fontSize = 11 } };
            _reportCard.Add(_reportLabel);
            _reportCard.style.display = DisplayStyle.None;
            scroll.Add(_reportCard);

            RebuildGenerationTab();
            RebuildDevTab();
            SelectTab(0);

            EditorApplication.playModeStateChanged += _ => RebuildDevTab();
        }

        // ---------------------------------------------------------------- chrome

        private VisualElement Header()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            var dot = new VisualElement
            {
                style =
                {
                    width = 10, height = 10, borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = Accent, marginRight = 8
                }
            };
            row.Add(dot);

            row.Add(new Label("Neoxider UI Kit")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 15, color = Color.white }
            });
            return row;
        }

        private VisualElement Tabs()
        {
            var bar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 8 }
            };

            _tabGen = TabButton("Генерация", 0);
            _tabDev = TabButton("Dev-инструменты", 1);
            bar.Add(_tabGen);
            bar.Add(_tabDev);
            return bar;
        }

        private Button TabButton(string text, int index)
        {
            var btn = new Button(() => SelectTab(index)) { text = text };
            btn.style.flexGrow = 1;
            btn.style.height = 26;
            btn.style.marginLeft = 0;
            btn.style.marginRight = index == 0 ? 4 : 0;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            return btn;
        }

        private void SelectTab(int index)
        {
            _tab = index;
            _generationBody.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _devBody.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _reportCard.style.display = index == 0 && !string.IsNullOrEmpty(_report)
                ? DisplayStyle.Flex : DisplayStyle.None;

            PaintTab(_tabGen, index == 0);
            PaintTab(_tabDev, index == 1);
            if (index == 1)
                RebuildDevTab();
        }

        private static void PaintTab(Button btn, bool active)
        {
            btn.style.backgroundColor = active ? Accent : Card;
            btn.style.color = active ? Color.white : TextDim;
        }

        private static VisualElement BuildCard(string title)
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = Card, borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 8,
                    marginBottom = 8,
                    borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1, borderBottomWidth = 1,
                    borderLeftColor = CardBorder, borderRightColor = CardBorder,
                    borderTopColor = CardBorder, borderBottomColor = CardBorder
                }
            };

            if (!string.IsNullOrEmpty(title))
            {
                card.Add(new Label(title)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold, color = Color.white,
                        marginBottom = 6, fontSize = 12
                    }
                });
            }

            return card;
        }

        private static Button ColorButton(string text, string tooltip, Color color, Action onClick)
        {
            var btn = new Button(onClick) { text = text, tooltip = tooltip };
            btn.style.height = 30;
            btn.style.marginLeft = 0;
            btn.style.marginRight = 0;
            btn.style.marginTop = 3;
            btn.style.marginBottom = 3;
            btn.style.backgroundColor = color;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            Color hover = new Color(color.r + 0.08f, color.g + 0.08f, color.b + 0.08f);
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = hover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = color);
            return btn;
        }

        private static Label Hint(string text)
        {
            return new Label(text)
            {
                style = { color = TextDim, fontSize = 11, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            };
        }

        // ---------------------------------------------------------------- generation tab

        private void RebuildGenerationTab()
        {
            if (_generationBody == null)
                return;

            _generationBody.Clear();
            _generationBody.Add(ProjectCard());
            _generationBody.Add(TreeCard());
            _generationBody.Add(SettingsCard());
            _generationBody.Add(ActionsCard());
        }

        private VisualElement ProjectCard()
        {
            VisualElement card = BuildCard("FUI проект");

            if (_projectFolders.Count == 0)
            {
                card.Add(new HelpBox("Папки с UXML-экранами (fui_type_screen) не найдены в Assets.",
                    HelpBoxMessageType.Warning));
            }
            else
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var popup = new PopupField<string>("Папка", _projectFolders, Mathf.Clamp(_projectIndex, 0, _projectFolders.Count - 1))
                {
                    style = { flexGrow = 1 }
                };
                popup.RegisterValueChangedCallback(evt =>
                {
                    _projectIndex = _projectFolders.IndexOf(evt.newValue);
                    Rescan();
                    RebuildGenerationTab();
                });
                row.Add(popup);

                var refresh = new Button(() => { DetectProjects(); Rescan(); RebuildGenerationTab(); }) { text = "Обновить" };
                refresh.style.width = 84;
                refresh.style.marginLeft = 6;
                row.Add(refresh);
                card.Add(row);
            }

            var configField = new ObjectField("UiKitConfig") { objectType = typeof(UiKitConfig), value = _config };
            configField.tooltip = "Ассет конфигурации. Перетащите сюда свой сохранённый конфиг, чтобы переиспользовать его.";
            configField.RegisterValueChangedCallback(evt =>
            {
                _config = evt.newValue as UiKitConfig;
                RebuildGenerationTab();
            });
            card.Add(configField);

            if (_config == null)
            {
                card.Add(Hint("Конфиг ещё не выбран. Создайте готовый дефолтный (заполнится по текущему проекту) " +
                              "или перетащите свой сохранённый в поле выше. Также его можно создать через " +
                              "Create → Neoxider → UiKit Config."));
                var create = ColorButton("Создать дефолтный конфиг",
                    "Создать UiKitConfig и заполнить дефолтами по выбранному FUI-проекту.", Green, CreateDefaultConfig);
                create.SetEnabled(_scan != null && _scan.Pages.Count > 0);
                card.Add(create);
            }

            return card;
        }

        private void CreateDefaultConfig()
        {
            UiKitConfig config = EnsureConfig();
            string folder = SelectedProjectFolder;
            if (!string.IsNullOrEmpty(folder))
            {
                config.fuiProjectFolder = folder;
                if (_scan != null && _scan.Pages.Count > 0)
                {
                    List<string> changes = UiKitConfigUpdater.Update(config, _scan);
                    ShowReport("Создан дефолтный конфиг:\n" + string.Join("\n", changes));
                }
            }

            AssetDatabase.SaveAssets();
            RebuildGenerationTab();
            Selection.activeObject = config;
        }

        private VisualElement TreeCard()
        {
            VisualElement card = BuildCard("Страницы и элементы");

            if (_scan == null || _scan.Pages.Count == 0)
            {
                card.Add(new HelpBox("Экраны не найдены. Выберите папку FUI-проекта.", HelpBoxMessageType.Info));
                return card;
            }

            bool showService = _config != null && _config.includeServiceElements;

            foreach (UiPageModel page in _scan.Pages)
            {
                var pageFoldout = new Foldout { text = page.PageId, value = false };
                pageFoldout.style.marginBottom = 2;
                var pageHeaderToggle = pageFoldout.Q<Toggle>();
                if (pageHeaderToggle != null)
                    pageHeaderToggle.style.unityFontStyleAndWeight = FontStyle.Bold;

                pageFoldout.Insert(0, IncludeToggle(page.PageId, "страницу"));

                foreach (UiElementModel element in page.Elements)
                {
                    if (element.IsService && !showService)
                        continue;
                    pageFoldout.Add(ElementRow(element));
                }

                foreach (UiPopupModel popup in page.Popups)
                {
                    var popupRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
                    popupRow.Add(IncludeToggle(popup.FullPath, "попап"));
                    popupRow.Add(new Label(popup.Name)
                    {
                        tooltip = popup.FullPath,
                        style = { unityFontStyleAndWeight = FontStyle.Bold, color = Accent }
                    });
                    pageFoldout.Add(popupRow);

                    var popupBox = new VisualElement { style = { marginLeft = 14 } };
                    foreach (UiElementModel element in popup.Elements)
                    {
                        if (element.IsService && !showService)
                            continue;
                        popupBox.Add(ElementRow(element));
                    }

                    pageFoldout.Add(popupBox);
                }

                card.Add(pageFoldout);
            }

            return card;
        }

        private VisualElement ElementRow(UiElementModel element)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 12, overflow = Overflow.Hidden }
            };
            row.Add(IncludeToggle(element.FullPath, "элемент"));

            row.Add(new Label(element.Name)
            {
                tooltip = element.FullPath,
                style =
                {
                    minWidth = 40, flexGrow = 1, flexShrink = 1, color = new Color(0.85f, 0.86f, 0.88f),
                    overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis,
                    unityTextOverflowPosition = TextOverflowPosition.End
                }
            });

            UiWidgetKind effective = EffectiveKind(element);
            var kindField = new EnumField(effective) { style = { width = 92, flexShrink = 0 } };
            kindField.RegisterValueChangedCallback(evt =>
            {
                SetKindOverride(element, (UiWidgetKind)evt.newValue);
                RebuildGenerationTab();
            });
            row.Add(kindField);

            if (IsCounterKind(effective))
            {
                var idField = new TextField { value = EffectiveCounterId(element), isDelayed = true, style = { width = 70, flexShrink = 0 } };
                idField.RegisterValueChangedCallback(evt => SetCounterIdOverride(element, evt.newValue));
                row.Add(idField);
            }

            return row;
        }

        private Toggle IncludeToggle(string path, string what)
        {
            var toggle = new Toggle { value = !IsExcluded(path), tooltip = $"Включить {what} в генерацию." };
            toggle.style.marginRight = 4;
            toggle.RegisterValueChangedCallback(evt => SetExcluded(path, !evt.newValue));
            return toggle;
        }

        private VisualElement SettingsCard()
        {
            VisualElement card = BuildCard("Настройки");

            var ns = new TextField("Namespace") { value = _config != null ? _config.generatorNamespace : "Game.Ui" };
            ns.tooltip = "Namespace сгенерированного кода. Пусто = без namespace (глобальная область).";
            ns.RegisterValueChangedCallback(evt => { EnsureConfig().generatorNamespace = evt.newValue; MarkConfig(); });
            card.Add(ns);

            var output = new TextField("Папка вывода") { value = _config != null ? _config.generatorOutputRoot : "Assets/UiKit" };
            output.tooltip = "Корневая папка сгенерированного кода и документации.";
            output.RegisterValueChangedCallback(evt => { EnsureConfig().generatorOutputRoot = evt.newValue; MarkConfig(); });
            card.Add(output);

            var money = new TextField("Money counter id") { value = _config != null ? _config.generatorMoneyCounterId : "coin" };
            money.tooltip = "Счётчик, доступный как UiKit.Money.";
            money.RegisterValueChangedCallback(evt => { EnsureConfig().generatorMoneyCounterId = evt.newValue; MarkConfig(); });
            card.Add(money);

            var service = new Toggle("Служебные элементы") { value = _config != null && _config.includeServiceElements };
            service.tooltip = "Включать spacer_*/layout_group_* в генерацию.";
            service.RegisterValueChangedCallback(evt => { EnsureConfig().includeServiceElements = evt.newValue; MarkConfig(); RebuildGenerationTab(); });
            card.Add(service);

            var popupCascade = new Toggle("Каскад попапов") { value = _config != null && _config.popupCascadeEnabled };
            popupCascade.tooltip = "Элементы попапа появляются по очереди (бонус-режим).";
            popupCascade.RegisterValueChangedCallback(evt => { EnsureConfig().popupCascadeEnabled = evt.newValue; MarkConfig(); });
            card.Add(popupCascade);

            var pageCascade = new Toggle("Каскад страниц") { value = _config != null && _config.pageCascadeEnabled };
            pageCascade.tooltip = "Элементы страницы появляются по очереди при показе (бонус-режим).";
            pageCascade.RegisterValueChangedCallback(evt => { EnsureConfig().pageCascadeEnabled = evt.newValue; MarkConfig(); });
            card.Add(pageCascade);

            string[] presets = UiAnimations.Names.Distinct().ToArray();
            var pressField = new PopupField<string>("Анимация кнопок", new List<string> { "scale", "sink", "pop", "none" },
                IndexOrZero(new[] { "scale", "sink", "pop", "none" }, _config != null ? _config.buttonPressPreset : "scale"));
            pressField.tooltip = "Пресет нажатия для всех кнопок.";
            pressField.RegisterValueChangedCallback(evt => { EnsureConfig().buttonPressPreset = evt.newValue; MarkConfig(); });
            card.Add(pressField);

            BuildPageAnimationSettings(card, presets);
            return card;
        }

        private void BuildPageAnimationSettings(VisualElement card, string[] presets)
        {
            if (_scan == null || _scan.Pages.Count == 0)
                return;

            card.Add(new Label("Анимации страниц")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, color = Color.white, marginTop = 8, marginBottom = 2 }
            });

            var pageIds = _scan.Pages.Select(p => p.PageId).ToList();
            int startIndex = pageIds.FindIndex(id => _config != null && (_config.GetPage(id)?.isStart ?? false));
            var startField = new PopupField<string>("Стартовая", pageIds, Mathf.Max(startIndex, 0));
            startField.tooltip = "Показывается при запуске.";
            startField.RegisterValueChangedCallback(evt => { SetStartPage(evt.newValue); RebuildGenerationTab(); });
            card.Add(startField);

            card.Add(Hint("страница · пресет показа · пресет попапов · режим"));

            var presetList = presets.ToList();
            foreach (UiPageModel page in _scan.Pages)
            {
                UiKitConfig.PageEntry entry = _config != null ? _config.GetPage(page.PageId) : null;
                string showPreset = entry?.showPreset ?? "fade";
                string popupPreset = entry?.popupPreset ?? "scale-pop";
                UiAnimationMode mode = entry?.animationMode ?? UiAnimationMode.ForwardAndBackward;

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2, overflow = Overflow.Hidden }
                };
                row.Add(new Label(page.PageId)
                {
                    style = { width = 70, flexShrink = 0, color = new Color(0.85f, 0.86f, 0.88f), overflow = Overflow.Hidden }
                });

                var showField = new PopupField<string>(presetList, Mathf.Max(presetList.IndexOf(showPreset), 0)) { style = { flexGrow = 1, flexShrink = 1, minWidth = 40, marginRight = 3 } };
                showField.RegisterValueChangedCallback(evt => { GetOrCreatePageEntry(page.PageId).showPreset = evt.newValue; MarkConfig(); });
                row.Add(showField);

                var popupField = new PopupField<string>(presetList, Mathf.Max(presetList.IndexOf(popupPreset), 0)) { style = { flexGrow = 1, flexShrink = 1, minWidth = 40, marginRight = 3 } };
                popupField.RegisterValueChangedCallback(evt => { GetOrCreatePageEntry(page.PageId).popupPreset = evt.newValue; MarkConfig(); });
                row.Add(popupField);

                var modeField = new EnumField(mode) { style = { flexGrow = 1, flexShrink = 1, minWidth = 40 } };
                modeField.RegisterValueChangedCallback(evt => { GetOrCreatePageEntry(page.PageId).animationMode = (UiAnimationMode)evt.newValue; MarkConfig(); });
                row.Add(modeField);

                card.Add(row);
            }
        }

        private VisualElement ActionsCard()
        {
            VisualElement card = BuildCard("Действия");

            bool canGenerate = _scan != null && _scan.Pages.Count > 0;
            var gen = ColorButton("① Сгенерировать вьюшки",
                "Скан UXML, кодогенерация вьюшек, обновление UiKitConfig и diff-отчёт.", Green, GenerateViews);
            gen.SetEnabled(canGenerate);
            card.Add(gen);

            bool canScene = _config != null && _config.pages.Count > 0 && !EditorApplication.isPlaying;
            var scene = ColorButton("② Создать объекты на сцене",
                "Создать/обновить корень UI с UiKitBootstrap и объекты страниц в активной сцене.", Blue, BuildSceneObjects);
            scene.SetEnabled(canScene);
            card.Add(scene);

            card.Add(ColorButton("Создать адаптер Neoxider",
                "Сгенерировать файл-мост к NeoxiderTools (Assets/UiKit/NeoxiderUiAdapter.cs).", Orange, CreateAdapter));

            if (EditorApplication.isPlaying)
                card.Add(Hint("«Создать объекты на сцене» недоступно в Play Mode."));

            return card;
        }

        // ---------------------------------------------------------------- dev tab

        private void RebuildDevTab()
        {
            if (_devBody == null)
                return;

            _devBody.Clear();

            if (!EditorApplication.isPlaying)
            {
                VisualElement card = BuildCard("Dev-инструменты");
                card.Add(new HelpBox("Доступно только в Play Mode. Запустите игру, чтобы открывать страницы и попапы одним кликом.",
                    HelpBoxMessageType.Info));
                _devBody.Add(card);
                return;
            }

            VisualElement pagesCard = BuildCard("Страницы");
            foreach (string pageId in UiKit.Pages.Ids.OrderBy(id => id, StringComparer.Ordinal))
            {
                string captured = pageId;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
                row.Add(new Label(pageId) { style = { flexGrow = 1, color = new Color(0.85f, 0.86f, 0.88f) } });
                row.Add(ColorButton("Открыть", "Показать страницу.", Blue, () => UiKit.Pages.Show(captured)));
                pagesCard.Add(row);
            }

            _devBody.Add(pagesCard);

            if (_scan != null)
            {
                VisualElement popupsCard = BuildCard("Попапы");
                foreach (UiPageModel page in _scan.Pages)
                {
                    foreach (UiPopupModel popup in page.Popups)
                    {
                        string path = popup.FullPath;
                        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2, overflow = Overflow.Hidden } };
                        row.Add(new Label(path)
                        {
                            tooltip = path,
                            style =
                            {
                                flexGrow = 1, flexShrink = 1, minWidth = 40, color = new Color(0.85f, 0.86f, 0.88f),
                                fontSize = 11, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis
                            }
                        });
                        row.Add(ColorButton("Открыть", "Открыть попап.", Green, () => UiKit.Popups.Open(path)));
                        popupsCard.Add(row);
                    }
                }

                _devBody.Add(popupsCard);
            }
        }

        private void ShowReport(string text)
        {
            _report = text ?? "";
            if (_reportLabel != null)
                _reportLabel.text = _report;
            if (_reportCard != null)
                _reportCard.style.display = _tab == 0 && !string.IsNullOrEmpty(_report) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------- project + scan

        private string SelectedProjectFolder =>
            _projectIndex >= 0 && _projectIndex < _projectFolders.Count ? _projectFolders[_projectIndex] : null;

        private void DetectProjects()
        {
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            string assetsPath = Application.dataPath.Replace('\\', '/');

            foreach (string file in Directory.GetFiles(Application.dataPath, "*.uxml", SearchOption.AllDirectories))
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

                if (dir != null && dir.StartsWith(assetsPath, StringComparison.Ordinal))
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

            MarkConfig();
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

        private static int IndexOrZero(string[] options, string value)
        {
            int index = Array.IndexOf(options, value);
            return index >= 0 ? index : 0;
        }

        // ---------------------------------------------------------------- page settings

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

            MarkConfig();
        }

        private UiKitConfig.PageEntry GetOrCreatePageEntry(string pageId)
        {
            UiKitConfig config = EnsureConfig();
            UiKitConfig.PageEntry entry = config.GetPage(pageId);
            if (entry == null)
            {
                entry = new UiKitConfig.PageEntry { pageId = pageId };
                config.pages.Add(entry);
                MarkConfig();
            }

            return entry;
        }

        // ---------------------------------------------------------------- actions

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

            string report = result.DiffReport;
            if (configChanges.Count > 0)
            {
                report += "\n\nUiKitConfig:\n" + string.Join("\n", configChanges);
                Debug.Log($"[UiKit] Config updated ({configChanges.Count} change(s)):\n" + string.Join("\n", configChanges));
            }

            ShowReport(report);
            Rescan();
            RebuildGenerationTab();
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
            ShowReport(report.Summary);
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
            ShowReport($"[UiKit] Адаптер создан: {path}");
            Debug.Log($"[UiKit] Адаптер создан: {path}");
        }

        // ---------------------------------------------------------------- config

        private void MarkConfig()
        {
            if (_config != null)
                EditorUtility.SetDirty(_config);
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
