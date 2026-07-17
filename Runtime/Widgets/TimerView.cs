using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Timer display over a label: mm:ss below one hour, h:mm:ss above. Countdown ticks via
    /// the element scheduler against realtime, survives rebinds and raises <see cref="Finished"/>.
    /// </summary>
    public class TimerView : UiSubView
    {
        private Label _label;
        private UiRichText _template;
        private IVisualElementScheduledItem _tickItem;
        private TimeSpan _displayed;
        private bool _hasDisplayed;
        private double _endRealtime = -1d;

        /// <summary>Raised once when a countdown reaches zero.</summary>
        public event Action Finished;

        /// <summary>True while a countdown is running.</summary>
        public bool IsRunning => _endRealtime >= 0d;

        /// <summary>Remaining time of the running countdown, or the last displayed time.</summary>
        public TimeSpan Remaining
        {
            get
            {
                if (!IsRunning)
                    return _hasDisplayed ? _displayed : TimeSpan.Zero;

                double seconds = _endRealtime - Time.realtimeSinceStartupAsDouble;
                return seconds > 0d ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
            }
        }

        protected override void OnBind(VisualElement root)
        {
            _label = root as Label ?? root.Q<Label>();
            if (_label != null)
                _template = UiRichText.Parse(_label.text);

            if (IsRunning)
                StartTicking();
            else if (_hasDisplayed)
                Apply(_displayed);
        }

        protected override void OnUnwire()
        {
            _tickItem?.Pause();
            _tickItem = null;
            _label = null;
        }

        /// <summary>Displays a fixed time (stops any running countdown).</summary>
        public void SetTime(TimeSpan time)
        {
            StopInternal();
            Apply(time);
        }

        /// <summary>Starts (or restarts) a countdown from the given duration.</summary>
        public void StartCountdown(TimeSpan duration)
        {
            _endRealtime = Time.realtimeSinceStartupAsDouble + Math.Max(0d, duration.TotalSeconds);
            Apply(Remaining);
            StartTicking();
        }

        /// <summary>Stops the countdown, keeping the last displayed time.</summary>
        public void Stop()
        {
            StopInternal();
        }

        private void StopInternal()
        {
            _endRealtime = -1d;
            _tickItem?.Pause();
            _tickItem = null;
        }

        private void StartTicking()
        {
            if (_label == null)
                return;

            _tickItem?.Pause();
            _tickItem = _label.schedule.Execute(Tick).Every(100);
        }

        private void Tick()
        {
            if (!IsRunning)
            {
                _tickItem?.Pause();
                return;
            }

            TimeSpan remaining = Remaining;
            Apply(remaining);

            if (remaining <= TimeSpan.Zero)
            {
                StopInternal();
                Finished?.Invoke();
            }
        }

        private void Apply(TimeSpan time)
        {
            _displayed = time;
            _hasDisplayed = true;

            if (_label != null)
                _label.text = _template.Apply(FormatTime(time));
        }

        /// <summary>Formats a time span as mm:ss, or h:mm:ss when one hour or longer.</summary>
        protected virtual string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;

            int totalSeconds = (int)Math.Ceiling(time.TotalSeconds);
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;

            return hours > 0
                ? $"{hours}:{minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
        }
    }
}
