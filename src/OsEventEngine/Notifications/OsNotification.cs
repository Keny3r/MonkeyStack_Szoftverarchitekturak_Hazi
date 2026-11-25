namespace OsEventEngine.Notifications;

/// <summary>
/// Az operációs rendszer értesítései, amelyekre a motor azonnali mintavétellel vagy újraszámolással reagál.
/// A <see cref="Native.Win32EventReactor"/> a különböző Win32 forrásokat (WinEvent hookok, ablaküzenetek) ezekre képezi le.
/// </summary>
internal enum OsNotification
{
    /// <summary>Ablak megjelent, eltűnt, megszűnt, minimalizálódott / visszaállt, vagy előtérbe került (Z-sorrend).</summary>
    WindowsChanged,

    /// <summary>Ablak elmozdult vagy átméreteződött.</summary>
    WindowMoved,

    /// <summary>Tápellátás változott (akkumulátor, hálózati táp).</summary>
    PowerStatusChanged,

    /// <summary>A gép felébredt alvásból.</summary>
    Resumed,

    /// <summary>A rendszeridő megváltozott.</summary>
    TimeChanged,

    /// <summary>Kijelző, felbontás, DPI vagy munkaterület (tálca) változott.</summary>
    DisplayChanged,
}

/// <summary>
/// OS értesítések forrása (Reactor). A handlerek a Reactor szálán futnak, ezért gyorsak legyenek:
/// csak jelet adjanak (<c>TriggerNow</c>, újraszámolás kérése), ne számoljanak.
/// </summary>
internal interface IOsNotificationSource
{
    /// <summary>Handler regisztrálása egy értesítésfajtára. A visszaadott objektum <c>Dispose</c>-a leiratkoztat.</summary>
    IDisposable Register(OsNotification notification, Action handler);
}
