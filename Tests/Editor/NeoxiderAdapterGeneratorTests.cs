using System.IO;
using Neo.UIKit.Editor;
using NUnit.Framework;

namespace Neo.UIKit.Tests
{
    public class NeoxiderAdapterGeneratorTests
    {
        private const string TempPath = "Temp/UiKitAdapterTests/NeoxiderUiAdapter.cs";

        [OneTimeTearDown]
        public void RemoveTempFolder()
        {
            string folder = Path.GetDirectoryName(TempPath);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void NeoxiderWiringIsFullyGuardedByTheDefine()
        {
            string source = NeoxiderAdapterGenerator.GenerateSource("Game.Ui");

            StringAssert.Contains("#if NEOXIDER_TOOLS", source);
            StringAssert.Contains("#else", source);
            StringAssert.Contains("#endif", source);

            // Every NeoxiderTools identifier must appear only inside the define.
            string outside = StripDefinedRegions(source);
            StringAssert.DoesNotContain("Neo.Tools", outside);
            StringAssert.DoesNotContain("Neo.Shop", outside);
            StringAssert.DoesNotContain("Neo.Audio", outside);
            StringAssert.DoesNotContain("GM.I", outside);
            StringAssert.DoesNotContain("EM.I", outside);
            StringAssert.DoesNotContain("Money.I", outside);
            StringAssert.DoesNotContain("AMSettings", outside);
        }

        [Test]
        public void BothVariantsImplementAllFlowInterfaces()
        {
            string source = NeoxiderAdapterGenerator.GenerateSource("Game.Ui");

            StringAssert.Contains("namespace Game.Ui", source);
            Assert.AreEqual(2, CountOccurrences(source,
                "IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings"));
            Assert.AreEqual(2, CountOccurrences(source, "UiKit.Flow.Connect(this);"));
            StringAssert.Contains("public void SetPaused(bool paused)", source);
            StringAssert.Contains("public event Action<string, long> CounterChanged;", source);
            StringAssert.Contains("NEOXIDER_TOOLS", source.Substring(0, source.IndexOf("using System;")),
                "the header README documents the scripting define");
        }

        [Test]
        public void GenerateWritesTheFile()
        {
            string path = NeoxiderAdapterGenerator.Generate(TempPath, "Game.Ui");

            Assert.IsTrue(File.Exists(path));
            StringAssert.Contains("class NeoxiderUiAdapter", File.ReadAllText(path));
        }

        private static string StripDefinedRegions(string source)
        {
            var sb = new System.Text.StringBuilder();
            bool inside = false;
            foreach (string line in source.Split('\n'))
            {
                if (line.StartsWith("#if NEOXIDER_TOOLS"))
                {
                    inside = true;
                    continue;
                }

                if (line.StartsWith("#else") || line.StartsWith("#endif"))
                {
                    inside = false;
                    continue;
                }

                if (!inside)
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            for (int i = text.IndexOf(token); i >= 0; i = text.IndexOf(token, i + token.Length))
                count++;
            return count;
        }
    }
}
