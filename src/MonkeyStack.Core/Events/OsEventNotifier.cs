namespace MonkeyStack.Core.Events;

/// <summary>
/// Az OS eseménymotor közös memóriabeli értesítő osztálya (Reactor minta).
/// Publikus Notify... metódusokat biztosít, hogy mind a háttérben futó valódi OS monitorok,
/// mind a tesztelő (Mock) View csúszkái ugyanazt a memóriabeli példányt tudják meghívni.
/// </summary>
public class OsEventNotifier : IOsEventNotifier
{
    public event EventHandler<CpuEventData>? CpuChanged;
    public event EventHandler<MemoryEventData>? MemoryChanged;
    public event EventHandler<DiskEventData>? DiskChanged;
    public event EventHandler<BatteryEventData>? BatteryChanged;
    public event EventHandler<WindowCountEventData>? WindowCountChanged;
    public event EventHandler<TimeEventData>? TimeChanged;
    public event EventHandler<InactivityEventData>? InactivityChanged;

    public void NotifyCpuChanged(CpuEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnCpuChanged(data);
    }

    public void NotifyMemoryChanged(MemoryEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnMemoryChanged(data);
    }

    public void NotifyDiskChanged(DiskEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnDiskChanged(data);
    }

    public void NotifyBatteryChanged(BatteryEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnBatteryChanged(data);
    }

    public void NotifyWindowCountChanged(WindowCountEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnWindowCountChanged(data);
    }

    public void NotifyTimeChanged(TimeEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnTimeChanged(data);
    }

    public void NotifyInactivityChanged(InactivityEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        OnInactivityChanged(data);
    }

    protected virtual void OnCpuChanged(CpuEventData data) => CpuChanged?.Invoke(this, data);
    protected virtual void OnMemoryChanged(MemoryEventData data) => MemoryChanged?.Invoke(this, data);
    protected virtual void OnDiskChanged(DiskEventData data) => DiskChanged?.Invoke(this, data);
    protected virtual void OnBatteryChanged(BatteryEventData data) => BatteryChanged?.Invoke(this, data);
    protected virtual void OnWindowCountChanged(WindowCountEventData data) => WindowCountChanged?.Invoke(this, data);
    protected virtual void OnTimeChanged(TimeEventData data) => TimeChanged?.Invoke(this, data);
    protected virtual void OnInactivityChanged(InactivityEventData data) => InactivityChanged?.Invoke(this, data);
}
