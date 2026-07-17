using System;

namespace Neo.UIKit
{
    /// <summary>
    /// Source of counter values (money, energy, ...). The kit subscribes on
    /// <see cref="UiFlow.Connect"/> and forwards changes into <see cref="CounterRegistry"/>.
    /// </summary>
    public interface IUiCounterSource
    {
        /// <summary>Raised with (counterId, value) whenever a game-side counter changes.</summary>
        event Action<string, long> CounterChanged;
    }
}
