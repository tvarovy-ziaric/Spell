# Android klient Spell

Projekt otvára Unity 6000.3.29f1 s Android Build Support, Android SDK & NDK Tools a OpenJDK. Úvodná scéna `Assets/Spell/Preflight.unity` zobrazí výsledok načítania spoločného skúšobného ihriska. Editor aj Android načítavajú ten istý súbor z `contracts/fixtures`. Pri zostavení ho Unity vloží priamo do APK s očakávaným kontrolným súčtom.

Na podporovanom telefóne tlačidlo **Skontrolovať senzory** spustí diagnostiku ARCore a vyžiada prístup ku kamere a polohe. Obraz kamery, relatívny pohyb od ARCore, pôvodné časy snímok a skutočné GNSS merania slúžia na miestnu kontrolu. Počítadlo odhadov sleduje samostatný takt približne 10 Hz. Kalibráciu ihriska oboma hráčmi a meranie priestorovej zhody preverí ďalší míľnik.

## Lokálny build na vývojovom počítači

### Skúšobný priestor s dvoma terčíkmi

Vytlač [oba terčíky](markers.html) na A4 pri 100 % a zmeraj šírku každého obrázka (160 mm). Pripevnite ich v rovnakej výške, stredy 1,50 m od seba. Po povolení kamery namier postupne na oba obrázky a prepni **Zobraziť kameru**. Pri dostatočnej zhode sa kontrolná guľa objaví 25 cm nad stredom medzi terčíkmi. Pri strate trackingu alebo zastaranom pozorovaní sa skryje. Ide o lokálnu registráciu skúšobnej zostavy. Meranie jej zhody na dvoch telefónoch a absolútna georeferencia ihriska sú ďalšie skúšky.

Vzory obsahujú [AprilTag 36h11, ID 0 a 1](https://github.com/AprilRobotics/apriltag-imgs/tree/f3fd9a7add5bfd82a886fc65240fdb8e3c9ac5a1/tag36h11) na dvoch rozličných textúrach. ARCore rozpoznáva celé referenčné obrázky. Prijímame iba vizuálne pozorovania v stave FULL_TRACKING. Nástroj arcoreimg z verzie 6.3.5 ohodnotil referencie 95 a 100 bodmi zo 100.

### Zostavenie

1. Prihlás sa v Unity Hub pod oprávneným účtom. Pre bezplatnú licenciu Unity Personal vyber príslušnú licenciu podľa svojej spôsobilosti.
2. Otvor priečinok `client-unity`, počkaj na import a cez **File → Build Profiles** prepni cieľ na Android. Pri prvom importe koordinátor skontroluje a uloží vygenerované nastavenia a súbor `Packages/packages-lock.json` do Gitu pred rezerváciou buildu. Ďalšie rezervácie používajú tento záznam závislostí.
3. Koordinátor pripraví rezerváciu cez `tools/release.py` pre čistú zdrojovú revíziu. V lokálnom editore otvor **Spell → Build reserved APK** a vyber JSON rezervácie. Pri ceste nastavenej v `SPELL_BUILD_REQUEST` editor načíta súbor priamo.
4. Editor zapíše verziu a vytvorí APK do rezervovanej cesty. Po úspechu sám spustí `python tools/release.py finish "<rezervácia.json>"` z koreňa repozitára. Po prerušení odovzdania použi rovnaký dokončovací príkaz nad existujúcim APK.

Technické východisko pre lokálnu správu editora opisuje [Unity Hub](https://docs.unity.com/en-us/hub/install-hub-win-mac).
