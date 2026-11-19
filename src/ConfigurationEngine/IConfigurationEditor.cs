namespace ConfigurationEngine;

using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Animation;

/// <summary>
/// A konfigurációs ablak (View) homlokzata a Konfigurációs motorhoz (Wrapper Facade minta).
/// Elrejti a tárolót (EF Core, SQLite), a validátorokat, a médiafájlok ellenőrzését és a képkocka-betöltést;
/// a View csak a konfiguráció adatosztályait, a validálási hibákat és kész <see cref="IAnimation"/> objektumokat lát.
/// A <c>ConfigurationService</c> valósítja meg az <see cref="MonkeyStack.Core.Configuration.IConfigurationService"/>
/// és az <see cref="MonkeyStack.Core.Events.IPollingSettingsProvider"/> mellett (Extension Interface minta).
/// A műveletek szálbiztosak és a UI szálról hívhatók; az aszinkron műveletek a lassú munkát a háttérben végzik.
/// Leírás: docs/configuration_engine/gui.md, "Kapcsolat a modellel".
/// </summary>
public interface IConfigurationEditor
{
    /// <summary>
    /// A tárolt konfiguráció szerkeszthető másolata, a betöltéskor kihagyott elemekkel
    /// (<see cref="ConfigurationLoadResult.DroppedItems"/>) és az adatbázis visszaállításának jelzésével
    /// (<see cref="ConfigurationLoadResult.DatabaseWasReset"/>). Az ablak megnyitásakor, Elvetéskor
    /// és mentés után hívja a View.
    /// </summary>
    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mező- és kereszthivatkozási szintű ellenőrzés (<see cref="IConfigurationValidator"/>).
    /// Gyors és mellékhatás nélküli, ezért a View minden módosítás után hívhatja.
    /// </summary>
    ValidationResult Validate(ConfigurationData data);

    /// <summary>
    /// Egy animációs forrás teljes (<see cref="MediaValidationDepth.Full"/>) médiaellenőrzése a háttérben,
    /// tallózás vagy csere után. A hibák kulcsa a forrás <c>Id</c>-ja (negatív ideiglenes azonosítónál is;
    /// 0 azonosítójú új forrásnál a listabeli index, <c>"#index"</c> alakban).
    /// </summary>
    Task<IReadOnlyList<ValidationError>> ValidateMediaAsync(
        AnimationSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Teljes ellenőrzés (validátor és minden forrás <see cref="MediaValidationDepth.Full"/> médiaellenőrzése),
    /// majd mentés: a tárolt állapotot <paramref name="data"/>-ra cseréli, így a vissza nem vett, betöltéskor
    /// kihagyott elemek törlődnek. Az új elemek valódi azonosítóját (a negatív ideiglenes azonosítókra mutató
    /// hivatkozásokkal együtt) a tároló visszaírja <paramref name="data"/> objektumaiba.
    /// Sikeres mentés után a motor újraindítás nélkül újratölt
    /// (Component Configurator), és kiváltja a <c>ConfigurationChanged</c>, illetve szükség esetén a
    /// <c>PollingSettingsChanged</c> eseményt.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">Ha a konfiguráció érvénytelen; ilyenkor semmi nem módosul.</exception>
    Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Azonnal beállítja az animációk egységes méretét (karakter és előnézetek), és egyszer kiváltja a
    /// <c>ConfigurationChanged</c> eseményt. Nem tárol: az adatbázisba a méret a <see cref="SaveAsync"/>
    /// hívással kerül. Elvetéskor a View ezzel állítja vissza a mentett méretet.
    /// </summary>
    void SetCharacterSize(int width, int height);

    /// <summary>
    /// Előnézeti animáció a (még nem mentett) forrásból, az aktuális karakterméretre skálázva, ugyanazzal a
    /// betöltéssel és skálázással, mint a karakter animációi. Külön példány: a Karakter nem kapja meg, de a
    /// motor a karakter animációival együtt lépteti (<c>UpdateAnimations</c>), a View csak rajzolja;
    /// karakterméret-változáskor átméreteződik.
    /// Ugyanannak a forrásnak a képkockái megosztottak.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha a forrás nem tölthető be.</exception>
    Task<IAnimation> CreatePreviewAnimationAsync(
        AnimationSource source,
        double framesPerSecond,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Eldobja a kiadott előnézeti animációkat (Elvetéskor, mentés után és az ablak bezárásakor);
    /// ezután a motor már nem lépteti őket.
    /// </summary>
    void ReleasePreviewAnimations();
}
