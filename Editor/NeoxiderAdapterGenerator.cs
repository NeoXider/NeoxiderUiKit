using System.IO;
using System.Text;
using UnityEditor;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Generates the NeoxiderTools bridge adapter (Assets/UiKit/NeoxiderUiAdapter.cs). The
    /// NeoxiderTools wiring (GM/EM/Money/AM/AMSettings) is wrapped entirely in
    /// #if NEOXIDER_TOOLS — NeoxiderTools ships no scripting define of its own, so the consumer
    /// adds NEOXIDER_TOOLS to the Player scripting defines (documented in the file header).
    /// Without the define a plain template adapter compiles instead, so the file is always valid
    /// in projects without NeoxiderTools.
    /// </summary>
    public static class NeoxiderAdapterGenerator
    {
        /// <summary>Default output path of the adapter file.</summary>
        public const string DefaultPath = "Assets/UiKit/NeoxiderUiAdapter.cs";

        /// <summary>Writes the adapter file and refreshes the asset database.</summary>
        public static string Generate(string path, string ns)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, GenerateSource(ns), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return path;
        }

        /// <summary>Emits the adapter source.</summary>
        public static string GenerateSource(string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Neoxider UI Kit adapter. Generated once by the UI Kit window; edit freely.");
            sb.AppendLine("//");
            sb.AppendLine("// README:");
            sb.AppendLine("// NeoxiderTools does not ship a scripting define, so this file uses NEOXIDER_TOOLS:");
            sb.AppendLine("// when NeoxiderTools (com.neoxider.tools) is installed, add NEOXIDER_TOOLS to");
            sb.AppendLine("// Project Settings > Player > Scripting Define Symbols to enable the full bridge");
            sb.AppendLine("// (game state moments, money counter, click sound, sound/music toggles).");
            sb.AppendLine("// Without the define the plain template below compiles instead - fill in the TODOs");
            sb.AppendLine("// to connect any other game systems (~30 lines).");
            sb.AppendLine("//");
            sb.AppendLine("// Put this component into the scene (e.g. on the UI root); it connects itself");
            sb.AppendLine("// via UiKit.Flow.Connect in Start.");
            sb.AppendLine("using System;");
            sb.AppendLine("using Neo.UIKit;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("#if NEOXIDER_TOOLS");
            sb.AppendLine("using Neo.Audio;");
            sb.AppendLine("using Neo.Shop;");
            sb.AppendLine("using Neo.Tools;");
            sb.AppendLine("#endif");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("#if NEOXIDER_TOOLS");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Bridge between NeoxiderTools and the UI Kit: EM game moments feed IUiFlowSource,");
            sb.AppendLine("    /// Money feeds IUiCounterSource, AM plays the click sound and AMSettings backs the");
            sb.AppendLine("    /// sound/music toggles.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public sealed class NeoxiderUiAdapter : MonoBehaviour, IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings");
            sb.AppendLine("    {");
            sb.AppendLine("        [Tooltip(\"UiKit counter id fed from Money.CurrentMoney.\")]");
            sb.AppendLine("        [SerializeField] private string moneyCounterId = \"coin\";");
            sb.AppendLine("        [Tooltip(\"Click sound played through AM; leave empty to use the UiKitConfig click sound.\")]");
            sb.AppendLine("        [SerializeField] private AudioClip clickClip;");
            sb.AppendLine();
            sb.AppendLine("        private bool _settingPause;");
            sb.AppendLine();
            sb.AppendLine("        public event Action Win;");
            sb.AppendLine("        public event Action Lose;");
            sb.AppendLine("        public event Action Pause;");
            sb.AppendLine("        public event Action Resume;");
            sb.AppendLine("        public event Action Menu;");
            sb.AppendLine("        public event Action GameStart;");
            sb.AppendLine("        public event Action GameEnd;");
            sb.AppendLine();
            sb.AppendLine("        public bool SoundOn");
            sb.AppendLine("        {");
            sb.AppendLine("            get => AMSettings.HasInstance && !AMSettings.I.MuteEfxValue;");
            sb.AppendLine("            set { if (AMSettings.HasInstance) AMSettings.I.SetEfx(value); }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool MusicOn");
            sb.AppendLine("        {");
            sb.AppendLine("            get => AMSettings.HasInstance && !AMSettings.I.MuteMusicValue;");
            sb.AppendLine("            set { if (AMSettings.HasInstance) AMSettings.I.SetMusic(value); }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public event Action<string, long> CounterChanged;");
            sb.AppendLine();
            sb.AppendLine("        private void Start()");
            sb.AppendLine("        {");
            sb.AppendLine("            UiKit.Flow.Connect(this);");
            sb.AppendLine();
            sb.AppendLine("            if (EM.HasInstance)");
            sb.AppendLine("            {");
            sb.AppendLine("                EM.I.OnWin.AddListener(() => Win?.Invoke());");
            sb.AppendLine("                EM.I.OnLose.AddListener(() => Lose?.Invoke());");
            sb.AppendLine("                EM.I.OnPause.AddListener(() => { if (!_settingPause) Pause?.Invoke(); });");
            sb.AppendLine("                EM.I.OnResume.AddListener(() => { if (!_settingPause) Resume?.Invoke(); });");
            sb.AppendLine("                EM.I.OnMenu.AddListener(() => Menu?.Invoke());");
            sb.AppendLine("                EM.I.OnGameStart.AddListener(() => GameStart?.Invoke());");
            sb.AppendLine("                EM.I.OnEnd.AddListener(() => GameEnd?.Invoke());");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (Money.HasInstance)");
            sb.AppendLine("            {");
            sb.AppendLine("                Money.I.CurrentMoney.AddListener(v => CounterChanged?.Invoke(moneyCounterId, (long)v));");
            sb.AppendLine("                CounterChanged?.Invoke(moneyCounterId, (long)Money.I.money);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Called by the kit when the pause popup opens/closes.</summary>");
            sb.AppendLine("        public void SetPaused(bool paused)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!GM.HasInstance)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            _settingPause = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                if (paused) GM.I.Pause();");
            sb.AppendLine("                else GM.I.Resume();");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                _settingPause = false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void PlayClick()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!AM.HasInstance)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            AudioClip clip = clickClip != null ? clickClip");
            sb.AppendLine("                : UiKit.Config != null ? UiKit.Config.clickSound : null;");
            sb.AppendLine("            if (clip != null)");
            sb.AppendLine("                AM.I.Play(clip);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("#else");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Template adapter (NeoxiderTools not detected). Wire the TODOs to your own game");
            sb.AppendLine("    /// systems, or define NEOXIDER_TOOLS to compile the NeoxiderTools bridge instead.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public sealed class NeoxiderUiAdapter : MonoBehaviour, IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings");
            sb.AppendLine("    {");
            sb.AppendLine("#pragma warning disable 67 // raised by your game code, e.g. Win?.Invoke()");
            sb.AppendLine("        public event Action Win;");
            sb.AppendLine("        public event Action Lose;");
            sb.AppendLine("        public event Action Pause;");
            sb.AppendLine("        public event Action Resume;");
            sb.AppendLine("        public event Action Menu;");
            sb.AppendLine("        public event Action GameStart;");
            sb.AppendLine("        public event Action GameEnd;");
            sb.AppendLine("        public event Action<string, long> CounterChanged;");
            sb.AppendLine("#pragma warning restore 67");
            sb.AppendLine();
            sb.AppendLine("        public bool SoundOn { get; set; } = true;   // TODO: delegate to your audio system");
            sb.AppendLine("        public bool MusicOn { get; set; } = true;   // TODO: delegate to your audio system");
            sb.AppendLine();
            sb.AppendLine("        private void Start()");
            sb.AppendLine("        {");
            sb.AppendLine("            UiKit.Flow.Connect(this);");
            sb.AppendLine("            // TODO: subscribe to your game events and raise Win/Lose/Pause/... here.");
            sb.AppendLine("            // TODO: forward counter changes: CounterChanged?.Invoke(\"coin\", value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void SetPaused(bool paused)");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: pause/resume your game (the kit calls this when the pause popup toggles).");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void PlayClick()");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: play the button click through your audio system.");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("#endif");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
