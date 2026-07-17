using System.Collections.Generic;
using System.Linq;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class UiNavigationProposerTests
    {
        private static UiScanResult Scan()
        {
            return FuiUxmlScanner.Scan(new[]
            {
                "Assets/MY_GAME_DESIGN/UXML/loading.uxml",
                "Assets/MY_GAME_DESIGN/UXML/mainmenu.uxml",
                "Assets/MY_GAME_DESIGN/UXML/gameplay.uxml"
            });
        }

        private static UiKitConfig.NavigationEntry Find(List<UiKitConfig.NavigationEntry> entries, string buttonPath)
        {
            return entries.FirstOrDefault(e => e.buttonPath == buttonPath);
        }

        [Test]
        public void ProposesConventionalNavigation()
        {
            List<UiKitConfig.NavigationEntry> proposals = UiNavigationProposer.Propose(Scan());

            UiKitConfig.NavigationEntry play = proposals.First(e => e.buttonPath.EndsWith("button_play"));
            Assert.AreEqual(UiNavigationAction.Show, play.action);
            Assert.AreEqual("gameplay", play.targetId);

            UiKitConfig.NavigationEntry setting = proposals.First(e => e.buttonPath.EndsWith("button_setting"));
            Assert.AreEqual(UiNavigationAction.OpenPopup, setting.action);
            Assert.AreEqual("mainmenu/popup_setting", setting.targetId);

            UiKitConfig.NavigationEntry pause = proposals.First(e => e.buttonPath.EndsWith("button_pause"));
            Assert.AreEqual(UiNavigationAction.OpenPopup, pause.action);
            Assert.AreEqual("gameplay/popup_pause", pause.targetId);

            UiKitConfig.NavigationEntry mainMenu = proposals.First(e => e.buttonPath.Contains("button_main_menu"));
            Assert.AreEqual(UiNavigationAction.Show, mainMenu.action);
            Assert.AreEqual("mainmenu", mainMenu.targetId);
        }

        [Test]
        public void CloseButtonsCloseTheirPopupOrPopThePage()
        {
            List<UiKitConfig.NavigationEntry> proposals = UiNavigationProposer.Propose(Scan());

            foreach (UiKitConfig.NavigationEntry close in proposals.Where(e => e.buttonPath.EndsWith("button_close")))
            {
                bool insidePopup = close.buttonPath.Contains("/popup_");
                Assert.AreEqual(insidePopup ? UiNavigationAction.ClosePopup : UiNavigationAction.Pop, close.action,
                    close.buttonPath);
                Assert.IsEmpty(close.targetId ?? "", close.buttonPath);
            }
        }

        [Test]
        public void UnconventionalButtonsGetNoProposal()
        {
            List<UiKitConfig.NavigationEntry> proposals = UiNavigationProposer.Propose(Scan());

            Assert.IsNull(Find(proposals, "gameplay/popup_pause/button_restart"),
                "button_restart resolves through the popup result mapping, not navigation.");
            Assert.IsNull(proposals.FirstOrDefault(e => e.buttonPath.EndsWith("button_buy")));
            Assert.IsNull(proposals.FirstOrDefault(e => e.buttonPath.EndsWith("button_left")));
        }

        [Test]
        public void EveryProposalPointsAtAScannedButton()
        {
            UiScanResult scan = Scan();
            var buttonPaths = new HashSet<string>();
            foreach (UiPageModel page in scan.Pages)
            {
                foreach (UiElementModel element in page.Elements)
                    buttonPaths.Add(element.FullPath);
                foreach (UiPopupModel popup in page.Popups)
                {
                    foreach (UiElementModel element in popup.Elements)
                        buttonPaths.Add(element.FullPath);
                }
            }

            foreach (UiKitConfig.NavigationEntry entry in UiNavigationProposer.Propose(scan))
                Assert.IsTrue(buttonPaths.Contains(entry.buttonPath), entry.buttonPath);
        }
    }
}
