namespace Neo.UIKit
{
    /// <summary>
    /// Adapter delegating sound/music settings to the project's audio system.
    /// When connected it becomes the source of truth (persistence included);
    /// the kit's PlayerPrefs persistence is bypassed.
    /// </summary>
    public interface IUiAudioSettings
    {
        bool SoundOn { get; set; }
        bool MusicOn { get; set; }
    }
}
