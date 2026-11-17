## Konfigurációs séma és validálás

Ez a dokumentum rögzíti, hogy a [Konfigurációs motor](../components.md#konfigurációs-motor) **mit enged konfigurálni**, milyen formában tárolja (SQLite), és milyen szabályok szerint validálja az adatokat. Erre a sémára épül a másik két komponens is: az [Animációs motor](../components.md#animációs-motor) ebből kapja az animációkat, az [OS esemény értesítő motor](../components.md#os-esemény-értesítő-motor) pedig a mintavételezési beállításokat.

Fogalmak: [Szótár](../../README.md#szótár). Komponens áttekintés: [overview.md](./overview.md). Kapcsolat a többi komponenssel: [Integráció](./integration.md).

---

### Áttekintés

A konfiguráció öt részből áll:

| Rész | Tábla | Mit ír le |
|---|---|---|
| [Animációs forrás](#1-animationsource--animációs-forrás) | `AnimationSource` | Egy [animációs forrás](../../README.md#animációs-forrás) (GIF / tileset / képsorozat-mappa) és lejátszási paraméterei |
| [Mozgási animáció](#2-movementanimation--mozgásállapot-animációja) | `MovementAnimation` | Melyik [mozgásállapothoz](../../README.md#mozgásállapot-alapállapot) melyik animáció tartozik |
| [Viselkedési szabály](#3-behaviorrule--viselkedési-szabály) | `BehaviorRule` | [Triggerfeltétel](../../README.md#triggerfeltétel) → overlay animáció ([viselkedési szabály](../../README.md#viselkedési-szabály)) |
| [Fizikai beállítások](#4-physicssettings--fizikai-paraméterek) | `PhysicsSettings` | Sebesség, gravitáció, méret (egysoros tábla) |
| [Mintavételezés](#5-metricpolling--rendszeresemény-mintavételezés) | `MetricPolling` | [Rendszeresemények](../../README.md#rendszeresemény--metrika-os-check) lekérdezési gyakorisága |

```mermaid
erDiagram
    AnimationSource ||--o{ MovementAnimation : "használja"
    AnimationSource ||--o{ BehaviorRule : "overlay-ként használja"

    AnimationSource {
        int Id PK
        text Name UK
        text SourceType "Gif | SpriteSheet | ImageFolder"
        text Path
        int FrameWidth "csak SpriteSheet"
        int FrameHeight "csak SpriteSheet"
        int Columns "csak SpriteSheet"
        int Rows "csak SpriteSheet"
        int FrameCount "csak SpriteSheet"
        int FrameDurationMs "GIF-nél opcionális felülírás"
        bool Loop
        real Scale
    }
    MovementAnimation {
        text MovementState PK "Idle | Walking | Falling | Dragging"
        int AnimationSourceId FK
    }
    BehaviorRule {
        int Id PK
        text Name
        bool IsEnabled
        text Metric "Cpu | Memory | Disk | Battery | WindowCount | Time | Inactivity"
        text Operator "Gt | Gte | Lt | Lte | Between"
        int Threshold
        int ThresholdUpper "csak Between"
        int AnimationSourceId FK
        int ZOrder
        text ExclusiveGroup "nullable"
        int Priority
    }
    PhysicsSettings {
        int Id PK "mindig 1"
        int WalkSpeedPxPerSec
        int GravityPxPerSec2
        real CharacterScale
        bool FollowCursor
    }
    MetricPolling {
        text Metric PK
        bool IsEnabled
        int IntervalMs
    }
```

Az enum értékek **szövegként** tárolódnak (EF Core `HasConversion<string>()`), így az adatbázis olvasható, és egy enum bővítése nem tolja el a meglévő értékeket.

---

### 1. `AnimationSource` – Animációs forrás

| Mező | Típus | Kötelező | Érvényes érték |
|---|---|---|---|
| `Id` | int | igen | automatikus |
| `Name` | text | igen | 1–64 karakter, egyedi, vezérlőkarakter nélkül, trimmelve |
| `SourceType` | enum | igen | `Gif`, `SpriteSheet`, `ImageFolder` |
| `Path` | text | igen | lásd [Médiafájlok validálása](#médiafájlok-validálása) |
| `FrameWidth`, `FrameHeight` | int | csak `SpriteSheet` | 1–1024 px |
| `Columns`, `Rows` | int | csak `SpriteSheet` | 1–64 |
| `FrameCount` | int | csak `SpriteSheet` | 1 – `Columns × Rows` (az utolsó sor lehet hiányos) |
| `FrameDurationMs` | int | `SpriteSheet`, `ImageFolder`: igen; `Gif`: nem | 16–5000 ms. GIF-nél ha üres, a fájlba ágyazott időzítés érvényes |
| `Loop` | bool | igen | – |
| `Scale` | real | igen | 0.1–4.0 (alapértelmezés 1.0) |

Az overlay animáció a karakterrel azonos helyen, a karakter **fölé rajzolva** jelenik meg (a "karakter fölött" a kirajzolási sorrendre vonatkozik, nem a pozícióra), ezért nincs külön pozíció-eltolás.

A sprite sheet képkockái balról jobbra, majd fentről lefelé haladva olvasódnak. A képsorozat-mappa fájljai természetes sorrendben (`frame2.png` < `frame10.png`) követik egymást.

### 2. `MovementAnimation` – Mozgásállapot animációja

| Mező | Típus | Kötelező | Érvényes érték |
|---|---|---|---|
| `MovementState` | enum (PK) | igen | a [`MovementState`](../../src/MonkeyStack.Core/Character/MovementState.cs) értékei: `Idle`, `Walking`, `Falling`, `Dragging` |
| `AnimationSourceId` | int (FK) | igen | létező `AnimationSource` |

Minden mozgásállapothoz **pontosan egy** sor tartozik. Ha egy állapothoz nincs (érvényes) sor, a motor a beépített alapértelmezett animációt adja vissza, így a karakter sosem tűnik el.

A séma irányonkénti animációt nem tárol: az egérkurzor utáni fordulás (a specifikáció szerinti "utánafordulás") megjelenítési kérdés, az Animációs motor feladata.

### 3. `BehaviorRule` – Viselkedési szabály

| Mező | Típus | Kötelező | Érvényes érték |
|---|---|---|---|
| `Id` | int | igen | automatikus |
| `Name` | text | igen | 1–64 karakter |
| `IsEnabled` | bool | igen | – |
| `Metric` | enum | igen | `Cpu`, `Memory`, `Disk`, `Battery`, `WindowCount`, `Time`, `Inactivity` |
| `Operator` | enum | igen | `Gt` (>), `Gte` (≥), `Lt` (<), `Lte` (≤), `Between` (zárt intervallum) |
| `Threshold` | int | igen | a metrika tartományában (lásd lent) |
| `ThresholdUpper` | int | csak `Between` | a metrika tartományában; `Time` kivételével `≥ Threshold` |
| `AnimationSourceId` | int (FK) | igen | létező `AnimationSource` |
| `ZOrder` | int | igen | 0–100. Kisebb értéket rajzolunk előbb, a nagyobb kerül felülre. A konfigurációs ablak a rétegeket csökkenő `ZOrder` szerint listázza, így a specifikációnak megfelelően a legfelső réteg áll legelöl |
| `ExclusiveGroup` | text | nem | 0–32 karakter. Azonos csoportból egyszerre csak egy szabály lehet aktív |
| `Priority` | int | igen | 0–100. Csoporton belül a nagyobb nyer |

#### Metrikák és értéktartományok

A metrikák a [`OsEventDataRegistry`](../../src/MonkeyStack.Core/Events/OsEventDataRegistry.cs) mezőire képződnek le:

| `Metric` | Forrás mező | Mértékegység | Tartomány |
|---|---|---|---|
| `Cpu` | `Cpu.UsagePercent` | % | 0–100 |
| `Memory` | `Memory.UsagePercent` | % | 0–100 |
| `Disk` | `Disk.UsagePercent` | % | 0–100 |
| `Battery` | `Battery.ChargePercent` | % | 0–100 |
| `WindowCount` | `WindowCount.WindowCount` | db | 0–1000 |
| `Time` | `Time.Hour * 60 + Time.Minute` | perc éjfél óta | 0–1439 (a UI `HH:mm`-ként mutatja) |
| `Inactivity` | `Inactivity.InactivityMinutes` | perc | 0–1440 |

`Time` + `Between` esetén megengedett, hogy `Threshold > ThresholdUpper`: ez **éjfélen átnyúló** intervallum (pl. 20:30–06:00 → `1230`–`360`).

#### Kiértékelés

A séma úgy készült, hogy a README példái (*kis tűz, ha CPU > 60%; nagy tűz, ha CPU > 80%; pizsama, ha este van*) konfigurációval, kód nélkül leírhatók legyenek. Egy adott registry-állapotra a motor így számolja ki az aktív [környezeti állapotokat](../../README.md#környezeti-konfigurálható-állapot):

1. Csak az `IsEnabled` szabályok számítanak.
2. Ha a szabály metrikájához még nem érkezett adat (`null` a registry-ben), a szabály nem teljesül.
3. A teljesülő szabályokat `ExclusiveGroup` szerint csoportosítjuk. Csoportonként csak a legnagyobb `Priority` marad (egyenlőségnél a kisebb `Id`). A csoport nélküli szabályok mind megmaradnak.
4. A maradékot `ZOrder` szerint növekvő sorrendbe rendezzük → ez az overlay-lista.

Példa:

| Name | Metric | Operator | Threshold | ExclusiveGroup | Priority | ZOrder |
|---|---|---|---|---|---|---|
| Kis tűz | Cpu | Gt | 60 | `fire` | 10 | 20 |
| Nagy tűz | Cpu | Gt | 80 | `fire` | 20 | 20 |
| Pizsama | Time | Between | 1230 – 360 | – | 0 | 10 |

CPU = 85%, 22:00 → aktív: *Pizsama* (ZOrder 10), majd *Nagy tűz* (ZOrder 20) → "pizsamában ég a karakter".

### 4. `PhysicsSettings` – Fizikai paraméterek

Egyetlen sor (`Id = 1`); ha hiányzik, az alapértékekkel jön létre.

| Mező | Típus | Tartomány | Alapérték |
|---|---|---|---|
| `WalkSpeedPxPerSec` | int | 10–2000 | 120 |
| `GravityPxPerSec2` | int | 100–10000 | 2000 |
| `CharacterScale` | real | 0.25–4.0 | 1.0 |
| `FollowCursor` | bool | – | igaz |

### 5. `MetricPolling` – Rendszeresemény-mintavételezés

Metrikánként egy sor ("konfigurálható gyakoriságú monitorozás").

| Mező | Típus | Tartomány | Alapérték |
|---|---|---|---|
| `Metric` | enum (PK) | lásd [Metrikák](#metrikák-és-értéktartományok) | – |
| `IsEnabled` | bool | – | igaz |
| `IntervalMs` | int | 250–60000 | `Cpu`, `Memory`: 1000; `Inactivity`, `WindowCount`: 2000; `Disk`, `Battery`, `Time`: 30000 |

Ezeket az [OS esemény értesítő motor](../os_event_engine/overview.md) az `IPollingSettingsProvider` interfészen keresztül kapja meg, így nem függ a Konfigurációs motortól ([részletek](./integration.md#3-fizikai-és-mintavételezési-beállítások)).

---

### Validálás

A validálás **egy helyen** történik (`IConfigurationValidator`), és két ponton fut le:

- **Mentéskor** (konfigurációs ablak): hibás adat nem kerül az adatbázisba, a hibák mezőnként jelennek meg a UI-ban.
- **Betöltéskor**: az adatbázis kívülről is szerkeszthető (pl. DB Browser for SQLite), ezért az indításkor beolvasott adatot is ellenőrizzük.

Betöltéskor a hibás sorokat **kihagyjuk és naplózzuk**, nem állítjuk le miattuk az alkalmazást. A kihagyott elemek az okkal együtt **megjelennek a konfigurációs ablakban**, így a felhasználó látja, miért nem működik egy szabály, és javíthatja vagy törölheti:

| Hiba | Viselkedés |
|---|---|
| Hibás `AnimationSource` | kimarad; az erre hivatkozó szabályok és mozgási animációk is kimaradnak |
| Hibás `BehaviorRule` | kimarad |
| Hiányzó / hibás `MovementAnimation` | beépített alapértelmezett animáció |
| Hiányzó / hibás `PhysicsSettings`, `MetricPolling` sor | alapértékek |
| Ismeretlen enum szöveg (pl. `Metric = "Gpu"`) | a sor kimarad |
| Hiányzó vagy sérült adatbázisfájl | a sérült fájl `config.db.bak`-ként félrekerül, és egy új, alapértékekkel feltöltött adatbázis jön létre |

#### Mezőszintű szabályok

A táblák "Érvényes érték" oszlopai a mezőszintű szabályok. Ezen felül:

- Szöveges mezők: trimmelve tároljuk, vezérlőkarakter (`\0`, `\n` stb.) nem megengedett.
- Kereszthivatkozás: minden `AnimationSourceId` létező forrásra mutat. Egy forrás nem törölhető, amíg szabály vagy mozgási animáció használja (`ON DELETE RESTRICT`), a UI ezt jelzi.
- `Between` esetén `ThresholdUpper` kötelező, más operátornál üres.
- `SpriteSheet` esetén a rács mezők kötelezőek, más típusnál üresek.

#### Médiafájlok validálása

A felhasználó tetszőleges fájlt tallózhat, ezért ezek kezelése biztonsági szempontból is lényeges:

- **Útvonal**: `Path.GetFullPath`-szel normalizáljuk. Hálózati (UNC, `\\szerver\...`) útvonal nem megengedett. Relatív útvonal az alkalmazás `assets` mappájához képest értelmeződik (ez a beépített alapanimációké).
- **Kiterjesztés**: `Gif` → `.gif`. `SpriteSheet` → `.png`, `.jpg`, `.jpeg`, `.bmp`. `ImageFolder` → a mappa ilyen kiterjesztésű fájljai, a többi figyelmen kívül marad.
- **Tartalom**: nem csak a kiterjesztést nézzük, a fájlt ténylegesen dekódoljuk (WPF `BitmapDecoder`). Ha nem dekódolható, a forrás érvénytelen.
- **Méretkorlátok**: fájl ≤ 20 MB, kép ≤ 4096 × 4096 px, legfeljebb 500 képkocka. Ez véd a memóriát elfogyasztó fájlok ellen; a karakter maga nem okozhat "memória > 90%" eseményt.
- **Sprite sheet**: a kép mérete `Columns × FrameWidth` × `Rows × FrameHeight` kell legyen.
- **Képsorozat-mappa**: legalább 1 kép, és minden kép azonos méretű.
- Médiafájlból **soha nem futtatunk kódot** és nem töltünk be más fájlt.

A médiafájlok ellenőrzése lassú lehet, ezért mentéskor a háttérben fut. Betöltéskor csak a létezést és a méretet ellenőrizzük, a dekódolás az első lejátszáskor történik (hibánál alapértelmezett animáció).

#### SQL injection

Az adatbázist kizárólag EF Core-on keresztül érjük el, ami minden lekérdezést paraméterezve küld. Nyers SQL-t string-összefűzéssel nem írunk.

---

### Tárolás

- Fájl: `%LOCALAPPDATA%\MonkeyStack\config.db` (nem kell hozzá rendszergazdai jog).
- Sémaváltozás: EF Core migrációk (`__EFMigrationsHistory` tábla). Indításkor `Database.Migrate()` fut.
- Első indításkor egy alapkonfiguráció jön létre: a beépített mozgási animációk, valamint a [Kiértékelés](#kiértékelés) példájának szabályai a beépített overlay animációkkal.

---

### Alkalmazott minták

A [javasolt minták](../patterns.md) közül:

- **Component Configurator**: a konfiguráció futás közben, újraindítás nélkül cserélhető. A konfigurációs ablakban mentett változtatás után a motor újraolvassa és validálja az adatbázist, és a következő lekérdezéstől már az új szabályok érvényesek ([a mentés útja](./integration.md#2-közös-host-alkalmazás-és-változásértesítés)). Az [`IConfigurationService`](../../src/ConfigurationEngine/IConfigurationService.cs) dokumentációja már ezt a mintát nevezi meg.
- **Wrapper Facade**: az `IConfigurationService` mögé rejtjük az EF Core-t, az SQLite-ot és a fájlrendszert. A hívó csak metrikákat és mozgásállapotot ad át, és animáció-leírókat kap vissza.
- **Scoped Locking** és **Thread-Safe Interface**: az OS események háttérszálról érkeznek, miközben a UI szálon mentés történhet. A betöltött konfigurációt egy **megváltoztathatatlan pillanatkép** (`ConfigurationSnapshot`) tartja. Az olvasók zár nélkül a pillanatkép aktuális referenciáját használják, a mentéseket pedig egy `lock` blokk sorosítja, amely az új pillanatképet atomikusan beállítja. A publikus metódusok így a hívó oldali szinkronizáció nélkül is szálbiztosak.

---

### Továbbfejlesztési lehetőségek

- Összetett feltételek (ÉS / VAGY több metrikára) egy szabályon belül.
- Hiszterézis vagy minimális aktív idő ("legalább N másodpercig teljesül"), hogy a határérték körül ingadozó metrika ne villogtassa az overlay-t.
- Karaktercsomagok importálása és exportálása (zip + manifest), ugyanezzel a validálással.
- Több konfigurációs profil, és váltás köztük.
