using System;
using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Pluggable integration with the game: adapters connect through narrow interfaces
    /// (<see cref="IUiFlowSource"/>, <see cref="IUiCounterSource"/>, <see cref="IUiClickSound"/>,
    /// <see cref="IUiAudioSettings"/>). Game moments are mapped declaratively to page actions
    /// via the config; the pause popup drives SetPaused (or Time.timeScale as fallback).
    /// </summary>
    public sealed class UiFlow
    {
        private IUiFlowSource _source;

        /// <summary>The connected flow source, or null.</summary>
        public IUiFlowSource Source => _source;

        /// <summary>
        /// Connects an adapter in one call; every supported interface it implements is wired.
        /// </summary>
        public void Connect(object adapter)
        {
            if (adapter == null)
            {
                Debug.LogError("[UiKit] UiKit.Flow.Connect called with null.");
                return;
            }

            bool any = false;

            if (adapter is IUiFlowSource flowSource)
            {
                ConnectFlowSource(flowSource);
                any = true;
            }

            if (adapter is IUiCounterSource counterSource)
            {
                ConnectCounterSource(counterSource);
                any = true;
            }

            if (adapter is IUiClickSound clickSound)
            {
                UiKit.Audio.ConnectClickSound(clickSound);
                any = true;
            }

            if (adapter is IUiAudioSettings audioSettings)
            {
                UiKit.Audio.ConnectSettings(audioSettings);
                any = true;
            }

            if (!any)
            {
                Debug.LogError($"[UiKit] Adapter {adapter.GetType().Name} implements none of the flow interfaces " +
                               "(IUiFlowSource, IUiCounterSource, IUiClickSound, IUiAudioSettings).");
            }
        }

        private void ConnectFlowSource(IUiFlowSource source)
        {
            if (_source != null)
                Debug.LogWarning("[UiKit] Replacing an already connected IUiFlowSource.");

            _source = source;
            source.Win += () => Execute(UiGameMoment.Win);
            source.Lose += () => Execute(UiGameMoment.Lose);
            source.Pause += () => Execute(UiGameMoment.Pause);
            source.Resume += () => Execute(UiGameMoment.Resume);
            source.Menu += () => Execute(UiGameMoment.Menu);
            source.GameStart += () => Execute(UiGameMoment.GameStart);
            source.GameEnd += () => Execute(UiGameMoment.GameEnd);
        }

        private static void ConnectCounterSource(IUiCounterSource source)
        {
            source.CounterChanged += OnCounterChanged;
        }

        private static void OnCounterChanged(string counterId, long value)
        {
            UiKit.Counters.Define(counterId).Set(value);
        }

        /// <summary>
        /// Raises a game moment manually and runs its configured page action (open popup / show
        /// page). Use this when the game has no <see cref="IUiFlowSource"/> adapter connected.
        /// </summary>
        public void Raise(UiGameMoment moment)
        {
            Execute(moment);
        }

        private static void Execute(UiGameMoment moment)
        {
            UiKitConfig config = UiKit.Config;
            if (config == null)
                return;

            for (int i = 0; i < config.flow.Count; i++)
            {
                UiKitConfig.FlowEntry entry = config.flow[i];
                if (entry != null && entry.moment == moment)
                    UiActions.Execute(entry.action, entry.targetId, null, null);
            }
        }

        /// <summary>
        /// Called by popups on open/close. When the popup is the pause popup, forwards the pause
        /// state to the adapter, or toggles Time.timeScale 0/1 when no adapter is connected and
        /// the config allows managing time.
        /// </summary>
        internal void NotifyPopupToggled(string popupPath, bool open)
        {
            if (!IsPausePopup(popupPath))
                return;

            if (_source != null)
                _source.SetPaused(open);
            else if (UiKit.Config != null && UiKit.Config.manageTimeScale)
                Time.timeScale = open ? 0f : 1f;
        }

        private static bool IsPausePopup(string popupPath)
        {
            UiKitConfig config = UiKit.Config;
            if (config != null && !string.IsNullOrEmpty(config.pausePopupPath))
                return string.Equals(config.pausePopupPath, popupPath, StringComparison.Ordinal);

            return popupPath != null && popupPath.IndexOf("pause", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
