using System.Collections.Generic;
using System.Linq;
using Neo.UIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Neo.UIKit.Tests
{
    public class UiKitConfigUpdaterTests
    {
        private UiKitConfig _config;

        private static UiScanResult Scan()
        {
            return FuiUxmlScanner.Scan(new[]
            {
                "Assets/MY_GAME_DESIGN/UXML/loading.uxml",
                "Assets/MY_GAME_DESIGN/UXML/mainmenu.uxml",
                "Assets/MY_GAME_DESIGN/UXML/gameplay.uxml"
            });
        }

        [SetUp]
        public void CreateConfig()
        {
            _config = ScriptableObject.CreateInstance<UiKitConfig>();
        }

        [TearDown]
        public void DestroyConfig()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void CreatesPageEntriesWithAssetsAndStartPage()
        {
            UiKitConfigUpdater.Update(_config, Scan());

            Assert.AreEqual(3, _config.pages.Count);
            UiKitConfig.PageEntry loading = _config.GetPage("loading");
            Assert.IsNotNull(loading);
            Assert.IsNotNull(loading.visualTreeAsset, "uxml reference should be assigned");
            Assert.IsNotNull(loading.panelSettings, "PanelSettings under the project folder should be found");
            Assert.IsTrue(loading.isStart, "loading is the default start page");
            Assert.AreEqual(1, _config.pages.Count(p => p.isStart));
            Assert.IsNotNull(_config.uikitStyleSheet, "uikit.uss reference should be assigned");
        }

        [Test]
        public void CreatesCountersWithScannedPathsAndMoneyAlias()
        {
            UiKitConfigUpdater.Update(_config, Scan());

            UiKitConfig.CounterEntry coin = _config.counters.FirstOrDefault(c => c.id == "coin");
            Assert.IsNotNull(coin);
            Assert.IsTrue(coin.paths.Count >= 2, "coin is displayed on mainmenu and gameplay");
            Assert.IsTrue(coin.moneyAlias);
            Assert.AreEqual("coin", _config.MoneyCounterId);
        }

        [Test]
        public void AddsNavigationProposalsAndPopupResults()
        {
            UiKitConfigUpdater.Update(_config, Scan());

            UiKitConfig.NavigationEntry play = _config.navigation.FirstOrDefault(n => n.buttonPath == "mainmenu/button_play");
            Assert.IsNotNull(play);
            Assert.AreEqual(UiNavigationAction.Show, play.action);
            Assert.AreEqual("gameplay", play.targetId);

            UiKitConfig.PopupResultEntry restart =
                _config.popupResults.FirstOrDefault(r => r.buttonPath == "gameplay/popup_pause/button_restart");
            Assert.IsNotNull(restart);
            Assert.AreEqual("restart", restart.result);

            Assert.IsNull(_config.clickSound, "click sound stays empty");
        }

        [Test]
        public void SecondUpdateAddsNothingAndPreservesUserEdits()
        {
            UiScanResult scan = Scan();
            UiKitConfigUpdater.Update(_config, scan);

            UiKitConfig.NavigationEntry play = _config.navigation.First(n => n.buttonPath == "mainmenu/button_play");
            play.action = UiNavigationAction.Push;
            play.targetId = "custom";
            _config.GetPage("gameplay").showPreset = "slide-up";
            _config.GetPage("gameplay").isStart = true;
            _config.GetPage("loading").isStart = false;
            int pages = _config.pages.Count;
            int navigation = _config.navigation.Count;
            int results = _config.popupResults.Count;
            int counters = _config.counters.Count;

            UiKitConfigUpdater.Update(_config, scan);

            Assert.AreEqual(pages, _config.pages.Count);
            Assert.AreEqual(navigation, _config.navigation.Count);
            Assert.AreEqual(results, _config.popupResults.Count);
            Assert.AreEqual(counters, _config.counters.Count);

            play = _config.navigation.First(n => n.buttonPath == "mainmenu/button_play");
            Assert.AreEqual(UiNavigationAction.Push, play.action, "user-edited navigation survives");
            Assert.AreEqual("custom", play.targetId);
            Assert.AreEqual("slide-up", _config.GetPage("gameplay").showPreset);
            Assert.IsTrue(_config.GetPage("gameplay").isStart, "user-chosen start page survives");
        }

        [Test]
        public void ReportsConfigPagesMissingFromScan()
        {
            _config.pages.Add(new UiKitConfig.PageEntry { pageId = "legacy" });

            List<string> report = UiKitConfigUpdater.Update(_config, Scan());

            Assert.IsTrue(report.Any(line => line.Contains("legacy") && line.Contains("missing")),
                "pages that disappeared from the design are reported, not deleted");
            Assert.IsNotNull(_config.GetPage("legacy"));
        }
    }
}
