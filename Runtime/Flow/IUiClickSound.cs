namespace Neo.UIKit
{
    /// <summary>
    /// Adapter that plays the button click sound through the project's own audio system
    /// instead of the built-in AudioClip playback.
    /// </summary>
    public interface IUiClickSound
    {
        void PlayClick();
    }
}
