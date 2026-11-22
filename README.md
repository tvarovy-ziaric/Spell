# Spell

AR hra pre dvoch hráčov na Androide.

## Spustenie vývojovej skúšky

Z koreňa repozitára spusti `python qa/check.py` s Pythonom 3.10+ a .NET SDK 10. Pri prvej obnove knižníc je potrebný prístup k NuGet. Ak má SDK vlastné umiestnenie, nastav `SPELL_DOTNET` na jeho spustiteľný súbor.

Príkaz znovu pripraví syntetický kopec, overí jeho zhodu s uloženým balíkom, spustí testy geometrie a prehrá nepravidelné merania polohy. Dvaja testovací klienti prejdú párovaním a zmenami pripravenosti cez skutočný lokálny server. Vstupom je syntetické ihrisko uložené v repozitári.

Android projekt je v [client-unity](client-unity). Pre prvé zostavenie potrebuje Unity 6.3, Android Build Support a aktívnu licenciu. Klient obsahuje mobilnú diagnostiku a dva [terčíky na vytlačenie](client-unity/markers.html). Priebeh v Unity a priestorovú presnosť na telefónoch overujeme oddelene od hostiteľských testov.

Koordinátor pridelí každému APK vlastné číslo a po zostavení overí doručenie do súkromného chatu codexer. [Postup vydania a obnovenia prenosu](tools/README.md)

Ručné spustenie servera vyžaduje súkromný náhodný `SPELL_FOUNDER_KEY` a parametre `--arena` a `--digest`. Pre lokálny vývoj používaj `--urls http://127.0.0.1:5080`. Docker obraz sa zostavuje z koreňa cez `docker build -f server/Dockerfile -t spell-server .`. Pri spustení sa balík pripojí iba na čítanie a kľúč sa odovzdá cez prostredie. [Protokol relácie](contracts/session.md)

Verejný stav vývoja a chronologický prehľad APK sú na [spell.arnavigator.site](https://spell.arnavigator.site).

## Dokumentácia

Vývoj hry vychádza z dokumentu [Architektúra a realizačná roadmapa pre Codex v0.3](docs/VibeKing_AR_hra_architektura_a_realizacna_roadmapa_Codex_v0.3_2026-11-22.md).

| Verzia | Špecifikácia | Architektonická oponentúra |
| --- | --- | --- |
| v0.2 | [Špecifikácia v0.2](docs/Terrain3D_AR_Magic_specifikacia_v0.2_2026-11-22.md) | [Oponentúra v0.2](docs/Terrain3D_AR_Magic_architektonicka_oponentura_v0.2_2026-11-22.md) |
| v0.1 | [Špecifikácia v0.1](docs/Terrain3D_AR_Magic_specifikacia_v0.1_2026-11-22.md) | [Oponentúra v0.1](docs/Terrain3D_AR_Magic_architektonicka_oponentura_v0.1_2026-11-22.md) |
