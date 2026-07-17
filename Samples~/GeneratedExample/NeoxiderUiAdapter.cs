// Neoxider UI Kit adapter. Generated once by the UI Kit window; edit freely.
//
// README:
// NeoxiderTools does not ship a scripting define, so this file uses NEOXIDER_TOOLS:
// when NeoxiderTools (com.neoxider.tools) is installed, add NEOXIDER_TOOLS to
// Project Settings > Player > Scripting Define Symbols to enable the full bridge
// (game state moments, money counter, click sound, sound/music toggles).
// Without the define the plain template below compiles instead - fill in the TODOs
// to connect any other game systems (~30 lines).
//
// Put this component into the scene (e.g. on the UI root); it connects itself
// via UiKit.Flow.Connect in Start.
using System;
using Neo.UIKit;
using UnityEngine;
#if NEOXIDER_TOOLS
using Neo.Audio;
using Neo.Shop;
using Neo.Tools;
#endif

namespace Game.Ui
{
#if NEOXIDER_TOOLS
    /// <summary>
    /// Bridge between NeoxiderTools and the UI Kit: EM game moments feed IUiFlowSource,
    /// Money feeds IUiCounterSource, AM plays the click sound and AMSettings backs the
    /// sound/music toggles.
    /// </summary>
    public sealed class NeoxiderUiAdapter : MonoBehaviour, IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings
    {
        [Tooltip("UiKit counter id fed from Money.CurrentMoney.")]
        [SerializeField] private string moneyCounterId = "coin";
        [Tooltip("Click sound played through AM; leave empty to use the UiKitConfig click sound.")]
        [SerializeField] private AudioClip clickClip;

        private bool _settingPause;

        public event Action Win;
        public event Action Lose;
        public event Action Pause;
        public event Action Resume;
        public event Action Menu;
        public event Action GameStart;
        public event Action GameEnd;

        public bool SoundOn
        {
            get => AMSettings.HasInstance && !AMSettings.I.MuteEfxValue;
            set { if (AMSettings.HasInstance) AMSettings.I.SetEfx(value); }
        }

        public bool MusicOn
        {
            get => AMSettings.HasInstance && !AMSettings.I.MuteMusicValue;
            set { if (AMSettings.HasInstance) AMSettings.I.SetMusic(value); }
        }

        public event Action<string, long> CounterChanged;

        private void Start()
        {
            UiKit.Flow.Connect(this);

            if (EM.HasInstance)
            {
                EM.I.OnWin.AddListener(() => Win?.Invoke());
                EM.I.OnLose.AddListener(() => Lose?.Invoke());
                EM.I.OnPause.AddListener(() => { if (!_settingPause) Pause?.Invoke(); });
                EM.I.OnResume.AddListener(() => { if (!_settingPause) Resume?.Invoke(); });
                EM.I.OnMenu.AddListener(() => Menu?.Invoke());
                EM.I.OnGameStart.AddListener(() => GameStart?.Invoke());
                EM.I.OnEnd.AddListener(() => GameEnd?.Invoke());
            }

            if (Money.HasInstance)
            {
                Money.I.CurrentMoney.AddListener(v => CounterChanged?.Invoke(moneyCounterId, (long)v));
                CounterChanged?.Invoke(moneyCounterId, (long)Money.I.money);
            }
        }

        /// <summary>Called by the kit when the pause popup opens/closes.</summary>
        public void SetPaused(bool paused)
        {
            if (!GM.HasInstance)
                return;

            _settingPause = true;
            try
            {
                if (paused) GM.I.Pause();
                else GM.I.Resume();
            }
            finally
            {
                _settingPause = false;
            }
        }

        public void PlayClick()
        {
            if (!AM.HasInstance)
                return;

            AudioClip clip = clickClip != null ? clickClip
                : UiKit.Config != null ? UiKit.Config.clickSound : null;
            if (clip != null)
                AM.I.Play(clip);
        }
    }
#else
    /// <summary>
    /// Template adapter (NeoxiderTools not detected). Wire the TODOs to your own game
    /// systems, or define NEOXIDER_TOOLS to compile the NeoxiderTools bridge instead.
    /// </summary>
    public sealed class NeoxiderUiAdapter : MonoBehaviour, IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings
    {
#pragma warning disable 67 // raised by your game code, e.g. Win?.Invoke()
        public event Action Win;
        public event Action Lose;
        public event Action Pause;
        public event Action Resume;
        public event Action Menu;
        public event Action GameStart;
        public event Action GameEnd;
        public event Action<string, long> CounterChanged;
#pragma warning restore 67

        public bool SoundOn { get; set; } = true;   // TODO: delegate to your audio system
        public bool MusicOn { get; set; } = true;   // TODO: delegate to your audio system

        private void Start()
        {
            UiKit.Flow.Connect(this);
            // TODO: subscribe to your game events and raise Win/Lose/Pause/... here.
            // TODO: forward counter changes: CounterChanged?.Invoke("coin", value);
        }

        public void SetPaused(bool paused)
        {
            // TODO: pause/resume your game (the kit calls this when the pause popup toggles).
        }

        public void PlayClick()
        {
            // TODO: play the button click through your audio system.
        }
    }
#endif
}
