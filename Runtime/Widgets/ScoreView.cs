namespace Neo.UIKit
{
    /// <summary>
    /// Counter specialization for scores; defaults to the "score" counter id.
    /// </summary>
    public class ScoreView : CounterView
    {
        public ScoreView()
        {
            CounterId = "score";
        }

        /// <summary>Sets the displayed score.</summary>
        public void SetScore(long score)
        {
            SetValue(score);
        }
    }
}
