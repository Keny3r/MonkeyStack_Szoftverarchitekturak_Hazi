## Szállított alapkarakter, overlay-ek és alapszabályok

Ez a dokumentum az alkalmazással szállított [animációs forrásokat](../../README.md#animációs-forrás) és az első indításkor létrejövő [viselkedési szabályokat](../../README.md#viselkedési-szabály) írja le. A tárolás és a kiértékelés általános szabályai: [Séma és validálás](./schema.md).

---

### Alapkarakter: Mangó

A [karakter](../../README.md#screenmate-karakter) alapanimációi egy kis majmot, Mangót mutatják. Minden [mozgásállapothoz](../../README.md#mozgásállapot-alapállapot) egy képsorozat-mappa tartozik az `assets/movement` alatt (100×100 px-es, átlátszó hátterű `frameN.png` képkockák, jobbra néző karakterrel; balra a Karakter View tükröz).

| Mozgásállapot | Mappa | Képkocka |
|---|---|---|
| `Idle` | `movement/idle` | 16 |
| `Walking` | `movement/walking` | 8 |
| `Falling` | `movement/falling` | 6 |
| `Dragging` | `movement/dragging` | 12 |

A források azonosítója és útvonala nem változott (1–4, [`ConfigurationDefaults.CreateMovementSources`](../../src/ConfigurationEngine/Model/ConfigurationDefaults.cs)), csak a képek.

---

### Overlay-ek és régiók

Az overlay-ek a [környezeti állapotok](../../README.md#környezeti-konfigurálható-állapot) animációi az `assets/overlays` alatt, Mangóhoz igazítva. Az overlay a karakterrel azonos dobozba, a karakter fölé rajzolódik, és nem követi a mozgásállapotot ([schema.md](./schema.md#3-behaviorrule--viselkedési-szabály)). Ezért minden overlay a doboz egy **régiójában** él, ahol a karakter néhány pixeles mozgása nem zavar: a fej fölött, a karakter két oldalán vagy a felső sarkokban.

Ha sok [triggerfeltétel](../../README.md#triggerfeltétel) teljesül egyszerre, az overlay-ek egymásra kerülnének. Ezt a meglévő [kizáró csoport és prioritás](./schema.md#kiértékelés) oldja meg, új kód nélkül: **a kizáró csoport neve a régió neve**. Egy régióban egyszerre csak a legnagyobb prioritású teljesülő szabály overlay-e látszik, így ha minden szabály igaz, akkor is legfeljebb négy overlay jelenik meg, egymást nem takarva. A prioritás a fontosságot fejezi ki (a tűz fontosabb, mint a hálósapka).

| Régió (`ExclusiveGroup`) | Hely | `ZIndex` |
|---|---|---|
| `fejtető` | a fej fölött | 20 |
| `oldalak` | a karakter két oldalán | 10 |
| `jobb felső` | jobb felső sarok | 30 |
| `bal felső` | bal felső sarok | 30 |

Saját overlay-t a konfigurációs ablakban egy meglévő régió nevével lehet ugyanígy beilleszteni: a csoport mező felajánlja a már használt neveket ([Konfigurációs ablak](./view.md#részletek)).

---

### Alapszabályok

Első indításkor az alábbi szabályok jönnek létre, mind engedélyezve, 10 képkocka/s sebességgel ([`ConfigurationDefaults.CreateDefaultRules`](../../src/ConfigurationEngine/Model/ConfigurationDefaults.cs)). A mértékegységek: [Metrikák és értéktartományok](./schema.md#metrikák-és-értéktartományok).

| Szabály | Feltétel | Forrás (`overlays/…`) | Régió | Prioritás |
|---|---|---|---|---|
| Kis tűz | CPU > 60 % | `kis_tuz` (8) | `fejtető` | 40 |
| Ablakkáosz | ablakok száma > 10 | `ablakkaosz` (12) | `fejtető` | 30 |
| Floppytorony | háttértár > 99 % | `floppytorony` (8) | `fejtető` | 20 |
| Hálósapka | idő 22:00–06:00 | `halosapka` (12) | `fejtető` | 10 |
| Nagy tűz | CPU > 80 % | `nagy_tuz` (8) | `oldalak` | 20 |
| Banánhéjak | memória > 90 % | `bananhejak` (12) | `oldalak` | 10 |
| Unatkozás | inaktivitás > 5 perc | `unatkozas` (24) | `jobb felső` | 20 |
| Álmosság | idő 22:00–06:00 | `zzz` (12) | `jobb felső` | 10 |
| Lemerülőben | akkumulátor < 10 % | `lemerulo` (10) | `bal felső` | 10 |

- **A kis és a nagy tűz nem zárja ki egymást:** CPU > 80 %-nál a fejtetőn a kis tűz, oldalt a nagy tűz ég, a kettő együtt adja a nagy tüzet.
- **A pizsama két szabály** ugyanarra az éjfélen átnyúló időfeltételre: ha a hálósapkát egy fontosabb fejtető-overlay kiszorítja, az álmosság (Zzz) még jelzi az éjszakát.
- **Az unatkozás** buboréka a jobb felső sarokban van, a légy viszont szándékosan az egész karaktert körberepüli.
- **Szállított, de szabály nélküli forrás:** `overlays/cetlik` (Cetlik, 12 képkocka) a memóriához, a banánhéjak alternatívájaként; a forrás a konfigurációs ablakban kiválasztható.

A források azonosítói rögzítettek (5–14), a szabályoké 1–9. A források és a szabályok a [`AddDefaultOverlays`](../../src/ConfigurationEngine/Storage/Migrations/) migrációval kerülnek az adatbázisba, így egy törölt alapszabály nem jön vissza újra: a migráció csak egyszer fut.

---

### Alkalmazott minták

A [javasolt minták](../patterns.md) közül:

- **Component Configurator:** az alapviselkedés kód helyett adatként, migrációval érkezik, és a felhasználó a konfigurációs ablakban felülírhatja; mentés után újraindítás nélkül érvényesül ([schema.md](./schema.md#alkalmazott-minták)).

---

### Ismert korlátok

- Az overlay nem követi a mozgásállapotot, ezért eséskor és húzáskor a fejtető-overlay-ek néhány pixellel elcsúszhatnak a fejhez képest.
- Más alakú karakterhez (más fejmagassággal) a `fejtető` régió overlay-eit újra kell igazítani; a többi régió overlay-e karaktertől függetlenül használható.
