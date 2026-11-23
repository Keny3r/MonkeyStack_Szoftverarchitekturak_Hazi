namespace MonkeyStack.Core.Timing;

/// <summary>
/// Egy <see cref="IFrameLoop.Tick"/> ütem adatai.
/// </summary>
/// <param name="deltaTime">Az előző ütem (az első ütemnél az indítás) óta eltelt idő.</param>
public sealed class FrameTickEventArgs(TimeSpan deltaTime) : EventArgs
{
    /// <summary>Az előző ütem (az első ütemnél az indítás) óta eltelt idő.</summary>
    public TimeSpan DeltaTime { get; } = deltaTime;
}
