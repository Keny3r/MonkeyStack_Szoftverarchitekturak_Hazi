namespace CharacterEngine.Windowing;

/// <summary>A folyamat DPI-tudatossága: ettől függ, milyen egységben adja a Windows a kurzor koordinátáit.</summary>
public enum DpiAwareness
{
    /// <summary>A Windows a koordinátákat 96 DPI-re virtualizálja: a kapott érték már WPF egység (DIP).</summary>
    Unaware,

    /// <summary>Fizikai pixel, egyetlen, rendszerszintű váltószámmal.</summary>
    SystemAware,

    /// <summary>Fizikai pixel, monitoronként a monitor saját DPI-jével (PerMonitorV2).</summary>
    PerMonitorAware,
}

/// <summary>
/// A globális egérbemenethez szükséges nyers operációs rendszer-lekérdezések. A valódi megvalósítás a
/// <see cref="Win32InputSource"/> (Wrapper Facade); a tesztek hamis forrást adnak, így a <see cref="GlobalMouseInput"/>
/// logikája (gombcsere, DPI-átváltás) natív hívás nélkül ellenőrizhető.
/// </summary>
public interface INativeInputSource
{
    /// <summary>A kurzor pozíciója a Windows által adott egységben (lásd <see cref="DpiAwareness"/>).</summary>
    /// <returns>Hamis, ha a pozíció nem kérdezhető le (pl. zárolt asztal).</returns>
    bool TryGetCursorPosition(out int x, out int y);

    /// <summary>Le van-e nyomva a fizikai bal egérgomb.</summary>
    bool IsLeftButtonDown();

    /// <summary>Le van-e nyomva a fizikai jobb egérgomb.</summary>
    bool IsRightButtonDown();

    /// <summary>Fel van-e cserélve a két egérgomb (balkezes beállítás).</summary>
    bool AreMouseButtonsSwapped();

    /// <summary>A hívó szál DPI-tudatossága.</summary>
    DpiAwareness GetDpiAwareness();

    /// <summary>A rendszerszintű DPI (alapértelmezetten 96).</summary>
    uint GetSystemDpi();

    /// <summary>A megadott pontot tartalmazó (vagy ahhoz legközelebbi) monitor DPI-je.</summary>
    /// <returns><c>null</c>, ha nem kérdezhető le.</returns>
    uint? GetMonitorDpi(int x, int y);
}
