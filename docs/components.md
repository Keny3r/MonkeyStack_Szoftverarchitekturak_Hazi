## Screenmate komponens terv

Az alkalmazást három fő komponensre szeretnénk bontani:
- [Konfigurációs motor](#konfigurációs-motor)
- [OS esemény értesítő motor](#os-esemény-értesítő-motor)
- [Animációs motor](#animációs-motor)

### Konfigurációs motor

End-to-end megvalósítja a konfigurációs beállítások szerkesztését és perzisztenciáját.
A fő célja, hogy a felhasználó konfigurálhassa a [viselkedési szabályokat](../README.md#viselkedési-szabály) és azok perzisztensek maradjanak.

[Részletek](./configuration_engine/overview.md)

### OS esemény értesítő motor

A feladata, hogy a [rendszereseményekről](../README.md#rendszeresemény--metrika-os-check) és azok értékeinek változásáról értesítse az alkalmazás más komponenseit.

[Részletek](./os_event_engine/overview.md)

### Animációs motor

Az animációs motor feladata a karakter [mozgásállapotának](../README.md#mozgásállapot-alapállapot) kezelése és megjelenítése, valamint a konfigurációs állapotból adódó overlay megjelenítése a karakter fölött.
A komponens két kisebb logikai részre bontható. Van egy belső fizikai motor, ami a [mozgásállapotért felel](../README.md#mozgásállapot-alapállapot) és van egy a másik két komponenssel integrálódó motorja, ami az overlay megjelenítéséért felel.

[Részletek](./character_engine/overview.md)

### Kapcsolatok

A rendszer közepén az [Animációs motor](#animációs-motor) áll. Ő tud a másik két komponensről - felhasználja azok interfészeit - azonban a másik két komponens nem tud az animációs motorról, csupán interfészt biztosít.

Az [OS esemény értesítő motor](#os-esemény-értesítő-motor) minden változásról értesítést küld (push).

A [Konfigurációs motor](#konfigurációs-motor) a konfigurációs állapot lekérdezésére tesz lehetőséget megadott pereméterek függvényében (pull).

Működés közben
1. Az [OS esemény értesítő motor](#os-esemény-értesítő-motor) értesítést küld egy érték változásáról
2. Az [Animációs motor](#animációs-motor) ezzel az értékkel változtatja a belső állapotát, majd a teljes állapot függvényében küld egy lekérdezést a
3. [Konfigurációs motor](#konfigurációs-motor) felé, aki visszaadja a megfelelő overlay animációt.
