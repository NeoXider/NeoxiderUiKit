using System.Linq;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class FuiUxmlScannerTests
    {
        private static readonly string[] UxmlFiles =
        {
            "Assets/MY_GAME_DESIGN/UXML/loading.uxml",
            "Assets/MY_GAME_DESIGN/UXML/mainmenu.uxml",
            "Assets/MY_GAME_DESIGN/UXML/gameplay.uxml"
        };

        private UiScanResult _scan;

        [OneTimeSetUp]
        public void ScanRealDesign()
        {
            _scan = FuiUxmlScanner.Scan(UxmlFiles);
        }

        private UiPageModel Page(string pageId)
        {
            UiPageModel page = _scan.Pages.FirstOrDefault(p => p.PageId == pageId);
            Assert.IsNotNull(page, $"Page '{pageId}' not found.");
            return page;
        }

        private static UiElementModel Element(UiPageModel page, string fullPath)
        {
            UiElementModel element = page.Elements
                .Concat(page.Popups.SelectMany(p => p.Elements))
                .FirstOrDefault(e => e.FullPath == fullPath);
            Assert.IsNotNull(element, $"Element '{fullPath}' not found.");
            return element;
        }

        [Test]
        public void FindsAllThreePagesSortedById()
        {
            CollectionAssert.AreEqual(
                new[] { "gameplay", "loading", "mainmenu" },
                _scan.Pages.Select(p => p.PageId).ToArray());
        }

        [Test]
        public void DetectsPopupsPerPage()
        {
            Assert.IsEmpty(Page("loading").Popups);
            CollectionAssert.AreEqual(new[] { "popup_setting" }, Page("mainmenu").Popups.Select(p => p.Name));
            CollectionAssert.AreEqual(new[] { "popup_pause", "popup_endgame" }, Page("gameplay").Popups.Select(p => p.Name));
            Assert.AreEqual("gameplay/popup_pause", Page("gameplay").Popups[0].FullPath);
        }

        [Test]
        public void DuplicateButtonRestartIsScopedPerPopup()
        {
            UiPageModel gameplay = Page("gameplay");
            UiPopupModel pause = gameplay.Popups.First(p => p.Name == "popup_pause");
            UiPopupModel endgame = gameplay.Popups.First(p => p.Name == "popup_endgame");

            UiElementModel inPause = pause.Elements.Single(e => e.Name == "button_restart");
            UiElementModel inEndgame = endgame.Elements.Single(e => e.Name == "button_restart");

            Assert.AreEqual("gameplay/popup_pause/button_restart", inPause.FullPath);
            Assert.AreEqual("gameplay/popup_endgame/button_restart", inEndgame.FullPath);
            Assert.AreEqual(UiWidgetKind.Button, inPause.Widget);
            Assert.IsFalse(inPause.IsAmbiguous);
            Assert.IsFalse(inEndgame.IsAmbiguous);
        }

        [Test]
        public void DuplicateButtonCloseIsScopedPerPage()
        {
            Assert.AreEqual("mainmenu/popup_setting/button_close",
                Element(Page("mainmenu"), "mainmenu/popup_setting/button_close").FullPath);
            Assert.AreEqual(UiWidgetKind.Button,
                Element(Page("gameplay"), "gameplay/popup_pause/button_close").Widget);
        }

        [Test]
        public void LoadingProgressbarIsBarWidget()
        {
            UiElementModel bar = Element(Page("loading"), "loading/progressbar");
            Assert.AreEqual(UiWidgetKind.Bar, bar.Widget);
            Assert.AreEqual("progressbar", bar.RelativePath);
        }

        [Test]
        public void PanelCoinIsCounterOnBothScreens()
        {
            UiElementModel mainmenuCoin = Element(Page("mainmenu"), "mainmenu/panel_coin");
            UiElementModel gameplayCoin = Element(Page("gameplay"), "gameplay/panel_coin");

            Assert.AreEqual(UiWidgetKind.Counter, mainmenuCoin.Widget);
            Assert.AreEqual(UiWidgetKind.Counter, gameplayCoin.Widget);
            Assert.AreEqual("coin", mainmenuCoin.CounterId);
            Assert.AreEqual("coin", gameplayCoin.CounterId);
        }

        [Test]
        public void PanelTimerIsTimerAndWrapperPanelIsNot()
        {
            Assert.AreEqual(UiWidgetKind.Timer, Element(Page("gameplay"), "gameplay/panel_timer").Widget);
            Assert.AreEqual(UiWidgetKind.Element, Element(Page("gameplay"), "gameplay/panel_panel_timer_0001").Widget);
        }

        [Test]
        public void PanelUpgradeIsShopItemAndItsPriceIsNotACounter()
        {
            UiPageModel mainmenu = Page("mainmenu");
            Assert.AreEqual(UiWidgetKind.ShopItem, Element(mainmenu, "mainmenu/panel_upgrade").Widget);
            Assert.AreEqual(UiWidgetKind.Element, Element(mainmenu, "mainmenu/panel_0001").Widget);
            Assert.AreEqual(UiWidgetKind.Element, Element(mainmenu, "mainmenu/panel_button_buy_0001").Widget);
        }

        [Test]
        public void InnermostSemanticPanelsWinCounterDetection()
        {
            UiPageModel gameplay = Page("gameplay");
            Assert.AreEqual(UiWidgetKind.Counter, Element(gameplay, "gameplay/panel_merges").Widget);
            Assert.AreEqual("merges", Element(gameplay, "gameplay/panel_merges").CounterId);
            Assert.AreEqual("reward", Element(gameplay, "gameplay/popup_endgame/panel_reward").CounterId);
            Assert.AreEqual("merges_count", Element(gameplay, "gameplay/popup_endgame/panel_merges_count").CounterId);
            Assert.AreEqual(UiWidgetKind.Element, Element(gameplay, "gameplay/popup_endgame/panel_end_game").Widget);
        }

        [Test]
        public void ServiceElementsAreMarked()
        {
            Assert.IsTrue(Element(Page("gameplay"), "gameplay/spacer_0001").IsService);
            Assert.IsTrue(Element(Page("gameplay"), "gameplay/layout_group_0001").IsService);
            Assert.IsFalse(Element(Page("gameplay"), "gameplay/panel_coin").IsService);
        }

        [Test]
        public void FullElementCoverageIncludesLabelsInsideButtons()
        {
            UiElementModel priceLabel = Element(Page("mainmenu"), "mainmenu/label_button_buy_0001");
            Assert.AreEqual(UiWidgetKind.Label, priceLabel.Widget);

            UiElementModel popupDim = Element(Page("gameplay"), "gameplay/popup_pause/background");
            Assert.AreEqual(UiWidgetKind.Image, popupDim.Widget);
        }
    }
}
