## Animációs motor

A karakter fizikáját és az overlay renderelését megvalósító komponens.

## Megjelenítés

Minden ablak fölött mozgó animált karakter + overlay animáció

## Belső komponensek

### Fizikai motor

A karakter [mozgásállapotát](../../README.md#mozgásállapot-alapállapot) kezelő komponens.

### Overlay motor

A karakter overlay logikája.
- Eseményeket fogad az [OS esemény értesítő motortól](../components.md#os-esemény-értesítő-motor)
- Lekérdezi az overlay állapotot a [Konfigurációs motortól](../components.md#konfigurációs-motor)
