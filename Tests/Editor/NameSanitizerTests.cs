using System.Collections.Generic;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class NameSanitizerTests
    {
        [TestCase("button_restart", "ButtonRestart")]
        [TestCase("spacer_0001", "Spacer0001")]
        [TestCase("spacer_0001_2", "Spacer00012")]
        [TestCase("button-play", "ButtonPlay")]
        [TestCase("panel coin", "PanelCoin")]
        [TestCase("image_panel_coin_0002", "ImagePanelCoin0002")]
        [TestCase("BUTTON_OK", "BUTTONOK")]
        public void ToPascalIdentifier_ConvertsToPascalCase(string input, string expected)
        {
            Assert.AreEqual(expected, NameSanitizer.ToPascalIdentifier(input));
        }

        [TestCase("9lives", "_9lives")]
        [TestCase("0001", "_0001")]
        public void ToPascalIdentifier_PrefixesLeadingDigit(string input, string expected)
        {
            Assert.AreEqual(expected, NameSanitizer.ToPascalIdentifier(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("___")]
        public void ToPascalIdentifier_EmptyInputBecomesUnderscore(string input)
        {
            Assert.AreEqual("_", NameSanitizer.ToPascalIdentifier(input));
        }

        [Test]
        public void Unique_AppendsDeterministicSuffixOnDuplicates()
        {
            var used = new HashSet<string>();
            Assert.AreEqual("ButtonRestart", NameSanitizer.Unique("ButtonRestart", used));
            Assert.AreEqual("ButtonRestart_2", NameSanitizer.Unique("ButtonRestart", used));
            Assert.AreEqual("ButtonRestart_3", NameSanitizer.Unique("ButtonRestart", used));
        }
    }
}
