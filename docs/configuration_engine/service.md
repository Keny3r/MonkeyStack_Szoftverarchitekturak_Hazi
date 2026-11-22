## Konfigurációs motor – futó konfiguráció

Ez a dokumentum a [Konfigurációs motor](../components.md#konfigurációs-motor) [`ConfigurationService`](../../src/ConfigurationEngine/ConfigurationService.cs) osztályát írja le: hogyan lesz a tárolt konfigurációból futó animáció, hogyan értékeli ki a szabályokat, és hogyan cseréli a konfigurációt újraindítás nélkül. Mit tárolunk és mi érvényes: [Séma és validálás](./schema.md); a tárolás és a betöltés: [implementáció](./implementation.md); a képkockák betöltése: [Médiaréteg](./media.md). Fogalmak: [Szótár](../../README.md#szótár).

---

### Felépítés

| Fájl | Szerep |
|---|---|
| [`ConfigurationService.cs`](../../src/ConfigurationEngine/ConfigurationService.cs) | Az [`IConfigurationService`](../../src/MonkeyStack.Core/Configuration/IConfigurationService.cs) megvalósítása: betöltés, újratöltés, méretváltás, léptetés, lekérdezések |
| [`Rules/RuleEvaluator.cs`](../../src/ConfigurationEngine/Rules/RuleEvaluator.cs) | A szabályok kiértékelése egy registry-állapotra; állapot nélküli, tiszta függvények |
| [`Runtime/AnimationSetBuilder.cs`](../../src/ConfigurationEngine/Runtime/AnimationSetBuilder.cs) | A betöltött konfigurációból felépíti az animációkat (betöltéskor és méretváltáskor) |
| [`Runtime/AnimationSet.cs`](../../src/ConfigurationEngine/Runtime/AnimationSet.cs) | A futó konfiguráció pillanatképe: animációk, szabályok, kikapcsolt metrikák, betöltési hibák |
| [`Media/MediaLoadFailure.cs`](../../src/ConfigurationEngine/Media/MediaLoadFailure.cs) | Egy forrás, amely a felépítéskor nem töltődött be |
| [`tests/ConfigurationEngine.Tests`](../../tests/ConfigurationEngine.Tests) | `RuleEvaluatorTests`, `ConfigurationServiceTests` |

A paraméter nélküli konstruktor a felhasználó adatbázisát ([`SqliteConfigurationStore`](../../src/ConfigurationEngine/Storage/SqliteConfigurationStore.cs), gyors médiaellenőrzéssel) és a szállított képeket ([`FrameLoader`](../../src/ConfigurationEngine/Media/FrameLoader.cs)) használja. A konstruktor nem végez I/O-t, a konfiguráció az `InitializeAsync` hívásakor töltődik be. A tesztek a második konstruktorral saját tárolót és betöltőt adnak át.

---

### Betöltés és újratöltés

```mermaid
sequenceDiagram
    participant H as Hívó / IConfigurationStore.Changed
    participant C as ConfigurationService
    participant S as IConfigurationStore
    participant B as AnimationSetBuilder
    participant L as IFrameLoader

    H->>C: InitializeAsync() / ReloadAsync()
    C->>C: újratöltési zár (SemaphoreSlim)
    C->>S: LoadAsync()
    S-->>C: érvényes konfiguráció
    C->>B: BuildAsync(konfiguráció, mentett méret)
    loop minden használt forrás egyszer
        B->>L: LoadFramesAsync(forrás, méret)
    end
    B-->>C: új AnimationSet
    C->>C: lock: a régi pillanatkép cseréje
    C-->>H: ConfigurationChanged
```

- Az első betöltés az `InitializeAsync`. Amíg nem fejeződik be, a `GetMovementAnimation` `null`-t, a `GetOverlayAnimations` üres listát ad.
- A tároló minden sikeres mentés után kiváltja a `Changed` eseményt. A motor erre a háttérben újratölt ([Component Configurator](#alkalmazott-minták)). Ha közben már vár egy újratöltés, amely még nem kezdte olvasni a tárolót, a további mentések nem indítanak újat, mert a várakozó már a legfrissebb adatot olvassa majd.
- Az új animációk a motor zárján kívül készülnek, így a lassú képbetöltés közben a Karakter a régieket olvassa és a motor azokat lépteti. A csere a zár alatt egy lépésben történik.
- Ha a tároló vagy a felépítés hibát dob (pl. zárolt adatbázis), a futó konfiguráció és a karakterméret változatlan marad. A `ReloadAsync` a hibát továbbadja, a mentés utáni háttér-újratöltés naplózza.
- Az újratöltés a mentett karakterméretet állítja be, de csak a sikeres csere után, így a `CharacterWidth` és a `CharacterHeight` mindig a futó animációk méretét mutatja. A még nem mentett `SetCharacterSize` ilyenkor felülíródik; ha viszont a méretet a felépítés közben állítják, az új méret marad.

---

### Animációk felépítése

A [séma szerint](./schema.md#animációk-előállítása) minden mozgásállapot és minden bekapcsolt szabály saját animációt kap a saját sebességével, a szabályok a saját `ZIndex`-ükkel is. A képkockák forrásonként egyszer töltődnek be, és az azonos forrású animációk megosztják őket. A kikapcsolt szabályok forrásait nem töltjük be.

Ha egy forrás nem tölthető be (pl. a fájlt a mentés óta törölték, vagy a karakter méretén túl sok képkockát foglalna, lásd [Skálázás](./media.md#skálázás)):

| Felhasználás | Viselkedés |
|---|---|
| Mozgásállapot | a szállított alapanimáció, annak alapsebességével (a beállított sebesség a felhasználó saját képeihez készült) |
| Mozgásállapot, és a szállított alapanimáció sem tölthető be | üres animáció (`CurrentFrame` = `null`), hogy az állapot ne maradjon animáció nélkül |
| Szabály | a szabály nem jelenik meg |

A nem betölthető forrásokat a `ConfigurationService.MediaLoadFailures` sorolja fel, a hibaüzenettel és a hibás mezővel együtt.

A kimaradt szabály a kizáró csoportjában sem vesz részt. A [Kiértékelés](./schema.md#kiértékelés) példájában, ha a *Nagy tűz* képe hiányzik, 85%-os CPU-nál a *Kis tűz* jelenik meg: a karakter a megjeleníthető legerősebb állapotot mutatja, ahelyett hogy semmit.

---

### Kiértékelés

A `GetOverlayAnimations` a [séma kiértékelési szabályait](./schema.md#kiértékelés) követi. A séma szövegét két ponton pontosítja a megvalósítás:

- A kikapcsolt mintavételezésű metrika szabályai nem teljesülnek akkor sem, ha a registry-ben még van korábbi érték ([`MetricPolling`](./schema.md#5-metricpolling--rendszeresemény-mintavételezés)).
- Azonos `ZIndex` esetén a kisebb `Id` kerül előbb (alulra), így a sorrend minden hívásnál ugyanaz.

A kiértékelés a pillanatképen, a motor zárján kívül fut, mert a pillanatkép a csere után sem változik.

---

### Méretváltás

A `SetCharacterSize` (és a `CharacterWidth`, `CharacterHeight` beállítása):

1. a méretet a [`CharacterSettings`](./schema.md#4-charactersettings--karakter-beállítások) tartományára korlátozza; a nem pozitív érték nem változtat, a változatlan méret nem vált ki eseményt;
2. a zár alatt minden animáció azonnal az új méretet kapja (`SetSize`), és tüzel a `ConfigurationChanged`;
3. a háttérben az új méretre újraépíti az animációkat ugyanabból a konfigurációból, mint egy újratöltés (a tárolót nem olvassa újra), majd a csere után a `ConfigurationChanged` még egyszer tüzel.

Amíg a 3. lépés tart, a régi képkockák az új méretre nyújtva látszanak. Mivel az újraépítés ugyanazokat a szabályokat követi, mint a betöltés, egy forrás, amely az új méreten túl sok képkockát foglalna, ugyanúgy a szállított alapanimációt kapja, illetve kimarad, és megjelenik a `MediaLoadFailures`-ben. Gyors egymás utáni méretváltásnál (pl. csúszka) az új méret megszakítja az előző, még futó újraépítést. A `SetCharacterSizeAsync` ugyanezt teszi, és a visszaadott feladat az újraépítés végén (vagy megszakításakor) fejeződik be.

---

### Szálkezelés

| Művelet | Szál | Megjegyzés |
|---|---|---|
| `GetMovementAnimation`, `GetOverlayAnimations`, `UpdateAnimations`, `SetCharacterSize` | bármelyik | rövid, a motor zárja alatt |
| `InitializeAsync`, `ReloadAsync`, újraépítés méretváltáskor | háttérszál (`Task.Run`) | egymás után futnak (újratöltési zár); a hívó szál szinkronizációs környezetét nem használják, így a UI szálról blokkolva is várhatók |
| `ConfigurationChanged` | a műveletet végző szál | sosem a motor zárja alatt; a feliratkozónak kell a UI szálra váltania. A feliratkozók egymástól függetlenül futnak: egy feliratkozó kivételét a motor naplózza, és nem teszi sikertelenné a már megtörtént cserét |
| `Dispose` | bármelyik | leiratkozik a tárolóról, és megszakítja a folyamatban lévő munkát; utána már nem cserélődik a konfiguráció, és nem tüzel esemény |

---

### Alkalmazott minták

A [javasolt minták](../patterns.md) közül:

- **Component Configurator**: a tároló `Changed` eseményére a motor újraindítás nélkül újratölt, és a `ConfigurationChanged` eseménnyel jelzi a Karakternek, hogy kérdezze le újra az animációkat.
- **Monitor Object** és **Scoped Locking**: a publikus műveletek egy közös zár (`lock (_syncRoot)`) alatt olvassák és cserélik a pillanatképet; a lassú munka (betöltés, újraépítés) a záron kívül, egy `SemaphoreSlim` mögött sorban fut, amelyet `try`/`finally` old fel.
- **Thread-Safe Interface**: a motor bármelyik szálról hívható; a pillanatkép a csere után nem változik, és a képkockák fagyasztottak, így a Karakter zár nélkül olvashatja a kapott animációkat.
- **Wrapper Facade**: a Karakter csak metrikákat és mozgásállapotot ad át, és kész `IAnimation` objektumokat kap; a tároló, a képbetöltés és a kiértékelés rejtve marad.
- **Extension Interface**: a motor belül [`IMutableAnimation`](../../src/MonkeyStack.Core/Animation/IMutableAnimation.cs)-ként kezeli az animációkat (méret, képkockák, léptetés), kifelé csak az olvasható [`IAnimation`](../../src/MonkeyStack.Core/Animation/IAnimation.cs)-t adja.
