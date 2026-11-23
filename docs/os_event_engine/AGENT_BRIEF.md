# 🤖 AGENT BRIEF – OS esemény értesítő motor

> **AI agenteknek: ezt olvasd először, és általában elég is.** Tömör, tokentakarékos összefoglaló.
> Részletek (indoklás, diagramok, ADR-ek) **csak szükség esetén**: [plan.md](./plan.md) – lásd a [Mélyebb részletek](#mélyebb-részletek-planmd) táblát, és csak a releváns fejezetet olvasd be.
> Projekt szabályok: [AGENTS.md](../../AGENTS.md). Fogalmak: [Szótár](../../README.md#szótár).

## Mi ez?
Windows rendszermetrikákat mér (CPU, memória, háttértár, akku, ablakszám, idő, inaktivitás), és **változáskor** C# eseményt küld a Karakternek. Tartozik hozzá egy csúszkás teszt-View, ami valódi terhelés nélkül szimulál. Küszöböket és szabályokat **nem** ismer (az a Konfigurációs motoré).

## Állapot (frissítsd, ha változik)
- ✅ Publikus szerződés és belső interfészek (szerződés szintű kód, mint a Konfigurációs motornál, #1). A `Metric` a Core-ból jön.
- ✅ Döntések a #3 review-ján: host bekötés, `Simulation`-alapú Mock út, Observer/Reactor szóhasználat, platform-szolgáltató nálunk (A opció).
- ✅ M1–M4: valódi Win32 mérés, monitorok, lánc, kiküldés; `Start`, `Reconfigure`, `UsePollingSettings`, `GetSnapshot`, `GetMonitorStatus`, `Dispose` működik. Saját terhelés ~0,15% (16 mag, 10 s mérés).
- ✅ M5–M7: szimuláció (`Simulation`, `IsMockModeEnabled`), csúszkás OS Mock View (MVVM, presetek), 35 unit teszt (`tests/OsEventEngine.Tests`).
- ✅ M9 Reactor (`Win32EventReactor`: ablak, tápellátás, idő, kijelző értesítések → `TriggerNow`), M11 platform-szolgáltató (`OsEventMonitorService.Platforms`), M10 minta-dokumentáció ([patterns_applied.md](./patterns_applied.md)). 70 unit teszt.
- ⏳ Hátravan (nem a mi kódunk): M8 host bekötés a Karakter motorban: `Start()`/`Dispose()`, `UsePollingSettings`, és a fizika tickben `SharedOsMonitorService.Platforms.Current.Platforms` ([plan.md 9.](./plan.md#9-megvalósítási-lépések-mérföldkövek)).
- Nincs több `NotImplementedException`. Tesztek: `dotnet test tests/OsEventEngine.Tests` (a CI minden PR-on futtatja).

## Fájltérkép
| Fájl | Szerep |
|---|---|
| `src/MonkeyStack.Core/Events/IOsEventNotifier.cs`, `OsEventNotifier.cs` | **Közös szerződés, nem a miénk, ne módosítsd.** 7 esemény + `Notify…Changed`. |
| `src/OsEventEngine/OsEventMonitorService.cs` | **Egyetlen publikus belépési pont** (Facade). |
| `src/OsEventEngine/OsEventEngineOptions.cs` | `MonitorOptions`, `OsEventEngineOptions` (intervallum, deadband). |
| `src/MonkeyStack.Core/Character/IPlatformProvider.cs`, `PlatformSnapshot.cs`, `ScreenArea.cs`, `ScreenRect.cs` | **Közös, ne módosítsd.** Platform-szolgáltató szerződés (#7); az OS motor valósítja meg (M11, `Win32PlatformProvider`), a Karakter fizika olvassa. |
| `src/MonkeyStack.Core/Events/Metric.cs`, `IPollingSettingsProvider.cs` | **Közös, ne módosítsd.** Metrika enum; a mintavételi beállítások forrása (a Konfigurációs motor valósítja meg). |
| `src/OsEventEngine/Monitoring/MetricCatalog.cs` | Tartományok, alap intervallumok, UI-leírók (egyeznek a `ConfigurationLimits`-szel; később a Core-ba). |
| `src/OsEventEngine/Monitoring/MetricSample.cs`, `MonitorInterfaces.cs` | Belső minta + `IMetricMonitor`, `IConfigurableComponent`, `ISimulatable`, `MonitorStatus`. |
| `src/OsEventEngine/Sources/*` | `IMetricSource` + 7 élő forrás (`CpuSource`, `SystemSources.cs`). A natív lekérdezés konstruktorban cserélhető (tesztek). |
| `src/OsEventEngine/Native/*` | `NativeMethods` (DllImport), `Win32System`, `WindowEnumerator`, `Win32DesktopScanner` + `DesktopScene` (Wrapper Facade), `Win32EventReactor` (Reactor). Win32 hívás csak itt. |
| `src/OsEventEngine/Notifications/*` | `OsNotification` + `IOsNotificationSource` (a Reactor szerződése), `Throttle` (értesítések összevonása). |
| `src/OsEventEngine/Platforms/*` | `PlatformCalculator` (tiszta: takarás, fullscreen, talaj, DPI), `Win32PlatformProvider` (`IPlatformProvider`, újraszámolás a Reactor jelzésére). |
| `src/OsEventEngine/Monitoring/MetricMonitor.cs`, `MonitorRepository.cs` | Mintavételező ciklus (`Init`/`Suspend`/`Resume`/`Fini`, `TriggerNow`, hiba → backoff). |
| `src/OsEventEngine/State/MetricSnapshotStore.cs` | Pillanatkép + `EventDataMapper` (`MetricSample` → Core `*EventData` / `Notify…`). |
| `src/OsEventEngine/Pipeline/*` | `IEventInterceptor`, `Interceptors.cs` (Smoothing → ChangeFilter → Snapshot → Logging), `EventDispatcher` (csatorna + 1 fogyasztó). |
| `src/OsEventEngine/Simulation/*` | `ISimulationControl`, `SourceMode`, `MetricDescriptor`, `SwitchableSource` (élő/szimulált, `ISimulatable`), `SimulationController`. |
| `src/OsEventEngine/ViewModels/*` | `OsMockViewModel`, `MetricRowViewModel` (MVVM; a csúszkák a `Simulation`-t hívják). |
| `tests/OsEventEngine.Tests/*` | Unit tesztek; `FakeSource` + `Eventually` segédek a `TestHelpers.cs`-ben. |
| `src/OsEventEngine/MainWindow.xaml(.cs)` | OS Mock View: csúszkák, presetek, élő/kiküldött érték (konstruktor: `MainWindow(OsEventMonitorService?)`, ne változtasd). |

## Adatfolyam (1 sor)
`MetricMonitor` (`Task.Delay(TimeProvider)` ciklus, `IMetricSource`) → `Channel<MetricSample>` → interceptor lánc (1 fogyasztó) → `SynchronizationContext.Post` → `OsEventNotifier.Notify…Changed` → `CharacterEventCoordinator`.

## Szerződés – ezt garantáljuk
1. Események **sorosítva** jönnek; ha a `Start()`-kor volt `SynchronizationContext`, azon. A fogyasztó ne építsen a UI szálra.
2. `Start()` után 1 kezdeti esemény minden elérhető metrikára (CPU: az első periódus után, mert két minta kell), utána csak ha `|Δ| ≥ Deadband`.
3. Nem elérhető vagy letiltott metrika (pl. nincs akku) → nincs esemény.
4. Minden eseményhez új `*EventData` példány jön.
5. Time = `Hour`/`Minute` (belül perc éjfél óta, 0–1439). Inaktivitás percben.
6. Sorrend a hostban: létrehozás → feliratkozás → `Start()` → `Dispose()`.
7. `Platforms.Current`: lock nélkül, minden fizika tickben olvasható, WPF egységben (DIP); a talaj (tálca teteje / képernyő alja) is `PlatformSegment`. A `PlatformsChanged` háttérszálon jön.

## Szabályok agenteknek
- **Csak `src/OsEventEngine/` és `docs/os_event_engine/` módosítható szabadon.** Core, `CharacterEngine`, `ConfigurationEngine`, `docs/mvc_architecture.md`: előbb szólj a felhasználónak, hogy egyeztessen a komponens felelősével.
- A `Notify…Changed`-et **csak** az `OsEventMonitorService` hívja. Teszteléshez a `Simulation` való, ne a notifier közvetlen hívása.
- Új osztály legyen `internal`, kivéve, ha a host vagy a View használja.
- Időt és időzítőt csak `TimeProvider`-en keresztül használj (tesztelhetőség). Win32 hívás csak a `Native/NativeMethods` mögött.
- Nevezd meg a használt mintát a kódkommentben ([patterns.md](../patterns.md)), ahogy a meglévő fájlok teszik.
- Meglévő dokumentáció szövegét ne írd át engedély nélkül. Új tartalom új fájlba, linkkel.

## Minta → hely
Wrapper Facade: `Sources/*`, `Native/*` · Component Configurator: `IConfigurableComponent`, `Reconfigure` · Interceptor: `Pipeline/*` · Extension Interface: `IMetricMonitor` + `IConfigurableComponent` (monitor), `ISimulatable` (`SwitchableSource`) · Active Object jelleg: `SimulationController` → `TriggerNow` · MVVM: `ViewModels/*` · Half-Sync/Half-Async: monitorok → Channel → fogyasztó · Monitor Object / Thread-Safe Interface / Scoped Locking: `MetricSnapshotStore` · Reactor (2. fázis): `Win32EventReactor` · DCLO: `Lazy<T>` · Strategy + Proxy: `SwitchableSource`. Observer = a C# események (az MVC terv ezt Reactornak hívja).

## Mélyebb részletek (plan.md)
| Ha ezt kell tudnod… | Olvasd |
|---|---|
| Miért így (minták indoklása, elvetett minták) | [3. fejezet](./plan.md#3-alkalmazott-architekturális-minták) |
| Melyik Win32 API, képlet, intervallum | [4. fejezet](./plan.md#4-metrikák-mérése-windows) |
| Szimuláció, View felépítése, régi API sorsa | [5. fejezet](./plan.md#5-szimuláció-és-os-mock-view) |
| Teljes osztálylista és szignatúrák | [6. fejezet](./plan.md#6-belső-felépítés-és-publikus-api) |
| Hibakezelés, leállítás, teljesítmény | [7. fejezet](./plan.md#7-hibakezelés-életciklus-teljesítmény) |
| Tesztek | [8. fejezet](./plan.md#8-tesztelési-terv) |
| Mit kell egyeztetni, kivel | [10. fejezet](./plan.md#10-egyeztetendő-a-többi-komponens-felelőseivel) |
| Döntések (ADR) | [11. fejezet](./plan.md#11-architekturális-döntések-adr-összefoglaló) |
| Ablakperemek / fullscreen / takarás (kész, M11) | [platform_provider_proposal.md](./platform_provider_proposal.md), [patterns_applied.md 3.](./patterns_applied.md#3-platform-szolgáltató-m11) |
| Melyik minta hol van a kész kódban | [patterns_applied.md](./patterns_applied.md) |
