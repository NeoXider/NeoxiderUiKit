using System;

namespace Neo.UIKit
{
    /// <summary>
    /// Bidirectional bridge between the game and the UI: the game raises moment events
    /// (mapped declaratively to page actions in the config) and receives pause requests
    /// when the pause popup opens/closes.
    /// </summary>
    public interface IUiFlowSource
    {
        event Action Win;
        event Action Lose;
        event Action Pause;
        event Action Resume;
        event Action Menu;
        event Action GameStart;
        event Action GameEnd;

        /// <summary>Called by the kit when the pause popup opens (true) or closes (false).</summary>
        void SetPaused(bool paused);
    }
}
