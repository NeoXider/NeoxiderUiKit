using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class UiScanOverridesTests
    {
        private const string TempRoot = "Temp/UiScanOverridesTests";

        private static UiKitGenerationSettings CreateSettings()
        {
            return new UiKitGenerationSettings
            {
                uxmlPaths = new List<string>
                {
                    "Assets/MY_GAME_DESIGN/UXML/loading.uxml",
                    "Assets/MY_GAME_DESIGN/UXML/mainmenu.uxml",
                    "Assets/MY_GAME_DESIGN/UXML/gameplay.uxml"
                },
                outputFolder = TempRoot + "/Generated",
                userViewsFolder = TempRoot + "/Views",
                docFolder = TempRoot,
                rootNamespace = "Game.Ui",
                refreshAssets = false
            };
        }

        [SetUp]
        public void CleanTempFolder()
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, true);
        }

        [OneTimeTearDown]
        public void RemoveTempFolder()
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, true);
        }

        [Test]
        public void ExcludedElementDisappearsFromGeneratedCode()
        {
            UiKitGenerationSettings settings = CreateSettings();
            settings.overrides.Add(new UiKitConfig.ScanOverrideEntry
            {
                elementPath = "mainmenu/button_play",
                excluded = true
            });

            UiKitGenerationResult result = UiKitGenerator.Generate(settings);

            UiPageModel mainmenu = result.Scan.Pages.First(p => p.PageId == "mainmenu");
            Assert.IsFalse(mainmenu.Elements.Any(e => e.FullPath == "mainmenu/button_play"));
            StringAssert.DoesNotContain("public ButtonView ButtonPlay",
                File.ReadAllText(TempRoot + "/Generated/MainmenuView.g.cs"));
        }

        [Test]
        public void ExcludedPageIsNotGenerated()
        {
            UiKitGenerationSettings settings = CreateSettings();
            settings.overrides.Add(new UiKitConfig.ScanOverrideEntry { elementPath = "loading", excluded = true });

            UiKitGenerationResult result = UiKitGenerator.Generate(settings);

            Assert.AreEqual(2, result.Plans.Count);
            Assert.IsFalse(File.Exists(TempRoot + "/Generated/LoadingView.g.cs"));
        }

        [Test]
        public void WidgetKindOverrideChangesTheGeneratedField()
        {
            UiKitGenerationSettings settings = CreateSettings();
            settings.overrides.Add(new UiKitConfig.ScanOverrideEntry
            {
                elementPath = "gameplay/panel_coin",
                widgetKind = nameof(UiWidgetKind.Score),
                counterId = "gold"
            });

            UiKitGenerationResult result = UiKitGenerator.Generate(settings);

            UiElementModel coin = result.Scan.Pages.First(p => p.PageId == "gameplay")
                .Elements.First(e => e.FullPath == "gameplay/panel_coin");
            Assert.AreEqual(UiWidgetKind.Score, coin.Widget);
            Assert.AreEqual("gold", coin.CounterId);

            string source = File.ReadAllText(TempRoot + "/Generated/GameplayView.g.cs");
            StringAssert.Contains("new ScoreView { CounterId = \"gold\" }", source);
        }

        [Test]
        public void InvalidKindNameKeepsTheScannedKind()
        {
            Assert.IsNull(UiScanOverrides.ParseKind("NotAKind"));
            Assert.IsNull(UiScanOverrides.ParseKind(""));
            Assert.AreEqual(UiWidgetKind.Bar, UiScanOverrides.ParseKind("Bar"));
        }
    }
}
