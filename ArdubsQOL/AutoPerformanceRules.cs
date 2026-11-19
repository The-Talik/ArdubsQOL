using System;

namespace ArdubsQOL;

internal sealed class AutoPerformanceCycle
{
    internal bool Paid { get; private set; }
    internal bool Lingering { get; private set; }
    internal TimeSpan PaidAt { get; private set; }
    internal TimeSpan PaidTurn { get; private set; }
    internal TimeSpan RestartAt { get; private set; }

    internal void Begin()
    {
        Paid = false;
        Lingering = false;
    }

    internal void Charge(TimeSpan now, TimeSpan turn)
    {
        Paid = true;
        PaidAt = now;
        PaidTurn = turn;
    }

    internal bool ShouldStop(bool turnBased, TimeSpan now, TimeSpan turn) =>
        Paid && !Lingering && (turnBased ? turn != PaidTurn : now >= PaidAt + TimeSpan.FromSeconds(6));

    internal void Linger(TimeSpan now)
    {
        Lingering = true;
        RestartAt = now + TimeSpan.FromSeconds(12);
    }

    internal bool IsDue(TimeSpan now) => Lingering && now >= RestartAt;

    internal int RoundsUntilRestart(TimeSpan now) =>
        Math.Max(0, Math.Min(2, (int)Math.Ceiling((RestartAt - now).TotalSeconds / 6)));
}
