using System.Collections.Generic;
using Neo.UIKit;
using UnityEngine.UIElements;

namespace Game.Ui
{
    /// <summary>
    /// Wires the designer's sound/music switch rows (track image + sliding knob) of the settings
    /// and pause popups to <see cref="UiKit.Audio"/>. The designer shipped the two states as the
    /// rows' initial art: sound row = green track, knob right (on); music row = red track, knob
    /// left (off). Track textures are captured from the resolved styles on first popup open and
    /// swapped per state; the knob slides via classes in uikit-overrides.uss.
    /// </summary>
    internal sealed class AudioToggleWiring
    {
        private sealed class Row
        {
            public VisualElement Track;
            public VisualElement Knob;
            public bool IsSound;
            public ToggleView View;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<(VisualElement element, EventCallback<ClickEvent> callback)> _clicks =
            new List<(VisualElement, EventCallback<ClickEvent>)>();

        private PopupView _popup;
        private Background _onImage;
        private Background _offImage;
        private bool _captured;

        public void Bind(UiPageBase page, string popupName, string imagePrefix)
        {
            _captured = false;
            BindRow(page, popupName, imagePrefix + "_0002", imagePrefix + "_0003", "uikit-audio-knob-right", true);
            BindRow(page, popupName, imagePrefix + "_0005", imagePrefix + "_0006", "uikit-audio-knob-left", false);

            _popup = page.GetPopup(popupName);
            if (_popup != null)
                _popup.Opened += OnPopupOpened;
        }

        public void Unwire()
        {
            if (_popup != null)
            {
                _popup.Opened -= OnPopupOpened;
                _popup = null;
            }

            foreach (Row row in _rows)
                row.View.Unwire();
            _rows.Clear();

            foreach (var (element, callback) in _clicks)
                element.UnregisterCallback(callback);
            _clicks.Clear();
        }

        private void OnPopupOpened()
        {
            if (_rows.Count == 0)
                return;

            // Sound may have been toggled from the other popup while this one was closed:
            // re-read the live state so settings and pause always display the same values.
            foreach (Row row in _rows)
                row.View.SetWithoutNotify(row.IsSound ? UiKit.Audio.SoundOn : UiKit.Audio.MusicOn);

            // Resolved styles are valid once the popup became visible; capture a frame later.
            _rows[0].Track.schedule.Execute(() =>
            {
                CaptureStateImages();
                ApplyAll();
            }).StartingIn(30);
        }

        private void CaptureStateImages()
        {
            if (_captured)
                return;

            Row sound = null, music = null;
            foreach (Row row in _rows)
            {
                if (row.IsSound) sound = row;
                else music = row;
            }

            if (sound == null || music == null)
                return;

            Background on = sound.Track.resolvedStyle.backgroundImage;
            Background off = music.Track.resolvedStyle.backgroundImage;
            if ((on.texture == null && on.sprite == null) || (off.texture == null && off.sprite == null))
                return;

            _onImage = on;
            _offImage = off;
            _captured = true;
        }

        private void ApplyAll()
        {
            foreach (Row row in _rows)
                ApplyVisual(row, row.View.IsOn);
        }

        private void ApplyVisual(Row row, bool on)
        {
            row.Knob.EnableInClassList("is-off", !on);
            row.Track.EnableInClassList("is-off", !on);

            if (!_captured)
                return;

            Background image = on ? _onImage : _offImage;
            row.Track.style.backgroundImage = image.sprite != null
                ? new StyleBackground(image.sprite)
                : new StyleBackground(image.texture);
        }

        private void BindRow(UiPageBase page, string popupName, string trackName, string knobName,
            string knobClass, bool isSound)
        {
            VisualElement track = page.ResolveElement(popupName + "/" + trackName);
            VisualElement knob = page.ResolveElement(popupName + "/" + knobName);
            if (track == null || knob == null)
                return;

            track.AddToClassList("uikit-audio-track");
            knob.AddToClassList("uikit-audio-knob");
            knob.AddToClassList(knobClass);

            var view = new ToggleView();
            view.Bind(track);

            var row = new Row { Track = track, Knob = knob, IsSound = isSound, View = view };
            _rows.Add(row);

            bool current = isSound ? UiKit.Audio.SoundOn : UiKit.Audio.MusicOn;
            view.SetWithoutNotify(current);
            ApplyVisual(row, current);

            view.Changed += value =>
            {
                if (isSound)
                    UiKit.Audio.SoundOn = value;
                else
                    UiKit.Audio.MusicOn = value;
                CaptureStateImages();
                ApplyVisual(row, value);
            };

            EventCallback<ClickEvent> knobClick = _ => view.IsOn = !view.IsOn;
            knob.RegisterCallback(knobClick);
            _clicks.Add((knob, knobClick));
        }
    }
}
