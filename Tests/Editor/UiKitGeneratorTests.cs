using System.Collections.Generic;
using System.IO;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class UiKitGeneratorTests
    {
        private const string TempRoot = "Temp/UiKitGeneratorTests";

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
        public void GeneratesExpectedFiles()
        {
            UiKitGenerationResult result = UiKitGenerator.Generate(CreateSettings());

            Assert.AreEqual(3, result.Plans.Count);
            Assert.IsTrue(File.Exists(TempRoot + "/Generated/LoadingView.g.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Generated/MainmenuView.g.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Generated/GameplayView.g.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Generated/UiIds.g.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Generated/UiKitModel.g.txt"));
            Assert.IsTrue(File.Exists(TempRoot + "/Views/LoadingView.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Views/MainmenuView.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/Views/GameplayView.cs"));
            Assert.IsTrue(File.Exists(TempRoot + "/UiKitApi.md"));
            StringAssert.Contains("First generation", result.DiffReport);
        }

        [Test]
        public void GeneratedCodeContainsTypedFieldsAndScopedPaths()
        {
            UiKitGenerator.Generate(CreateSettings());

            string gameplay = File.ReadAllText(TempRoot + "/Generated/GameplayView.g.cs");
            StringAssert.Contains("public partial class GameplayView : UiPageBase", gameplay);
            StringAssert.Contains("public partial class PopupPauseView : PopupView", gameplay);
            StringAssert.Contains("new CounterView { CounterId = \"coin\" }", gameplay);
            StringAssert.Contains("new TimerView()", gameplay);
            StringAssert.Contains("case \"popup_pause\":", gameplay);

            string loading = File.ReadAllText(TempRoot + "/Generated/LoadingView.g.cs");
            StringAssert.Contains("new BarView()", loading);

            string uiIds = File.ReadAllText(TempRoot + "/Generated/UiIds.g.cs");
            StringAssert.Contains("\"gameplay/popup_pause/button_restart\"", uiIds);
            StringAssert.Contains("\"gameplay/popup_endgame/button_restart\"", uiIds);
            StringAssert.Contains("public const string Coin = \"coin\";", uiIds);
        }

        [Test]
        public void ServiceElementsAreExcludedByDefaultAndIncludedOnDemand()
        {
            UiKitGenerator.Generate(CreateSettings());
            string defaultSource = File.ReadAllText(TempRoot + "/Generated/GameplayView.g.cs");
            StringAssert.DoesNotContain("Spacer0001", defaultSource);

            UiKitGenerationSettings settings = CreateSettings();
            settings.includeServiceElements = true;
            UiKitGenerator.Generate(settings);
            string withService = File.ReadAllText(TempRoot + "/Generated/GameplayView.g.cs");
            StringAssert.Contains("Spacer0001", withService);
        }

        [Test]
        public void UserPartialIsCreatedOnceAndNeverOverwritten()
        {
            UiKitGenerationSettings settings = CreateSettings();
            UiKitGenerationResult first = UiKitGenerator.Generate(settings);
            Assert.AreEqual(3, first.CreatedUserFiles.Count);

            const string marker = "// user code marker";
            string userPath = TempRoot + "/Views/GameplayView.cs";
            File.WriteAllText(userPath, File.ReadAllText(userPath) + marker);

            UiKitGenerationResult second = UiKitGenerator.Generate(settings);
            Assert.IsEmpty(second.CreatedUserFiles);
            Assert.AreEqual(3, second.SkippedUserFiles.Count);
            StringAssert.Contains(marker, File.ReadAllText(userPath));
        }

        [Test]
        public void SecondRunIsIdempotent()
        {
            UiKitGenerationSettings settings = CreateSettings();
            UiKitGenerator.Generate(settings);
            UiKitGenerationResult second = UiKitGenerator.Generate(settings);

            Assert.IsEmpty(second.WrittenFiles, "No regenerated file should change on the second run.");
            StringAssert.Contains("no changes", second.DiffReport);
        }
    }
}
