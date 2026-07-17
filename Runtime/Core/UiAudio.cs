using System;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// UI audio settings and click sound playback. Three cooperating modes:
    /// (a) AudioMixer with exposed parameter names from the config (mute = -80 dB),
    /// (b) an <see cref="IUiAudioSettings"/> adapter as the source of truth,
    /// (c) plain <see cref="SoundChanged"/>/<see cref="MusicChanged"/> events.
    /// Without an adapter the on/off state persists in PlayerPrefs.
    /// </summary>
    public sealed class UiAudio
    {
        private const string SoundPrefKey = "Neo.UIKit.SoundOn";
        private const string MusicPrefKey = "Neo.UIKit.MusicOn";
        private const float MutedDb = -80f;
        private const float UnmutedDb = 0f;

        private UiKitConfig _config;
        private AudioSource _clickSource;
        private IUiAudioSettings _settings;
        private IUiClickSound _clickSound;
        private bool _soundOn = true;
        private bool _musicOn = true;

        /// <summary>Raised when the sound toggle changes.</summary>
        public event Action<bool> SoundChanged;

        /// <summary>Raised when the music toggle changes.</summary>
        public event Action<bool> MusicChanged;

        /// <summary>Sound effects on/off; delegated to the adapter when connected.</summary>
        public bool SoundOn
        {
            get => _settings != null ? _settings.SoundOn : _soundOn;
            set
            {
                if (_settings != null)
                    _settings.SoundOn = value;
                else
                {
                    _soundOn = value;
                    PlayerPrefs.SetInt(SoundPrefKey, value ? 1 : 0);
                }

                ApplyMixer();
                SoundChanged?.Invoke(value);
            }
        }

        /// <summary>Music on/off; delegated to the adapter when connected.</summary>
        public bool MusicOn
        {
            get => _settings != null ? _settings.MusicOn : _musicOn;
            set
            {
                if (_settings != null)
                    _settings.MusicOn = value;
                else
                {
                    _musicOn = value;
                    PlayerPrefs.SetInt(MusicPrefKey, value ? 1 : 0);
                }

                ApplyMixer();
                MusicChanged?.Invoke(value);
            }
        }

        internal void Configure(UiKitConfig config)
        {
            _config = config;

            if (_settings == null)
            {
                _soundOn = PlayerPrefs.GetInt(SoundPrefKey, 1) == 1;
                _musicOn = PlayerPrefs.GetInt(MusicPrefKey, 1) == 1;
            }

            ApplyMixer();
        }

        internal void AttachClickSource(AudioSource source)
        {
            _clickSource = source;
        }

        internal void ConnectSettings(IUiAudioSettings settings)
        {
            _settings = settings;
            ApplyMixer();
        }

        internal void ConnectClickSound(IUiClickSound clickSound)
        {
            _clickSound = clickSound;
        }

        /// <summary>Plays the button click sound (adapter first, then the config AudioClip).</summary>
        internal void PlayClick()
        {
            if (!SoundOn)
                return;

            if (_clickSound != null)
            {
                _clickSound.PlayClick();
                return;
            }

            if (_clickSource != null && _config != null && _config.clickSound != null)
                _clickSource.PlayOneShot(_config.clickSound);
        }

        private void ApplyMixer()
        {
            if (_config == null || _config.audioMixer == null)
                return;

            bool sound = SoundOn;
            bool music = MusicOn;

            if (!string.IsNullOrEmpty(_config.soundParam))
                _config.audioMixer.SetFloat(_config.soundParam, sound ? UnmutedDb : MutedDb);
            if (!string.IsNullOrEmpty(_config.musicParam))
                _config.audioMixer.SetFloat(_config.musicParam, music ? UnmutedDb : MutedDb);
            if (!string.IsNullOrEmpty(_config.masterParam))
                _config.audioMixer.SetFloat(_config.masterParam, sound || music ? UnmutedDb : MutedDb);
        }
    }
}
