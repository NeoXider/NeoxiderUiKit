namespace Neo.UIKit
{
    /// <summary>
    /// Counter specialization for the level number. The text prefix found in the original
    /// label ("LEVEL 5" → "LEVEL ") is preserved by the base template logic; level numbers
    /// are never compacted.
    /// </summary>
    public class LevelView : CounterView
    {
        public LevelView()
        {
            CounterId = "level";
            UseCompactFormat = false;
            PulseOnChange = false;
        }

        /// <summary>Sets the displayed level number.</summary>
        public void SetLevel(int level)
        {
            SetValue(level);
        }
    }
}
