namespace MonkeyStack.Core.Events;

/// <summary>
/// Az OS eseménymotor által biztosított esemény-előfizetési interfész (Reactor minta).
/// A Karakter koordinátor erre az interfészre iratkozik fel a közös memóriában.
/// </summary>
public interface IOsEventNotifier
{
    event EventHandler<CpuEventData>? CpuChanged;
    event EventHandler<MemoryEventData>? MemoryChanged;
    event EventHandler<DiskEventData>? DiskChanged;
    event EventHandler<BatteryEventData>? BatteryChanged;
    event EventHandler<WindowCountEventData>? WindowCountChanged;
    event EventHandler<TimeEventData>? TimeChanged;
    event EventHandler<InactivityEventData>? InactivityChanged;
}
