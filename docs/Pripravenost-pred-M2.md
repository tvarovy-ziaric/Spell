# Pripravenosť Spell pred skúškou M2

Audit vychádza z posledného vydaného buildu 3 (commit `c65755a`) a z [roadmapy v0.3](VibeKing_AR_hra_architektura_a_realizacna_roadmapa_Codex_v0.3_2026-11-22.md). Verejný pilot má stále syntetický kopec 7 × 7. Samostatný server aj klient používajú rovnaký povrch. Stavový web a Telegram sú distribučné kanály diagnostiky.

| Míľnik | Stav pri audite | Pripravené pred fyzickou skúškou | Otvorená podmienka |
| --- | --- | --- | --- |
| M0 | Verzia povrchu, hash, lokálny build servera a klienta | Regresia klienta, servera, neplatných dát a zostavenia. Generovaný Android Protobuf sa porovnáva so schémou servera. | Opakovať pri zmene formátu. |
| M1 | Výrez DMR, PROJ, časovanie AR/GNSS, dve značky, geometria, QR a prenos celého balíka. Mobil po párovaní používal HTTP. | Mobilné WSS, kalibrovaná póza, samostatné hodiny GNSS a kamery, strata dôvery, terénny debug, voliteľný vstup smeru účastníka a registrácia zameraných terčíkov. | Potvrdenie optickej identity dobrovoľného spoluhráča a správania senzorov v teréne. |
| M2 | Jeden virtuálny bod medzi terčíkmi, bez výsledku spoločnej fyzickej skúšky. | Poloha kamery v spoločnom rámci, lokálne meranie pri značkách, samostatné vyhodnotenie relatívnej a absolútnej chyby, kontrola prenosu terénu po štarte, tlačiteľné podklady. | **PHYSICAL TEST REQUIRED.** Dva rôzne Androidy, vybrané ihrisko, nezávislé kontrolné body, opakovania, výkon a rozhodnutie vlastníka. |
| M3 | Chýbal rozhodca, správa pravidiel, grafické moduly a žurnál. | Predbežne integrovaný serverový Fireball, dopad, HP, aura, hranice, časovanie a obnova, dve grafiky kúzla, dve témy značiek, zakladateľská administrácia, PostgreSQL a diagnostika. | Prevzatie fyzicky hrateľného duelu nasleduje po prijatí M2. |
| M4 | Bez terénneho výsledku. | Syntetický scenár hrany, neistej polohy, zákrytu hrebeňom, pauzy, obnovy a duplicít. Výkonové profily menia grafické efekty. | **PHYSICAL TEST REQUIRED.** Reálny pohyb, výpadky, čitateľnosť efektov, presnosť zásahov, odozva, zahrievanie a slabší i výkonnejší telefón. |
| M5 | Pilot neprevzatý. | Podpísaný diagnostický distribučný proces, záznamy vydaní a skúšaný postup obnovy databázy a servera. | O výsledku pilotu rozhoduje vlastník po M4. |

## Paralelné zadania a doklady

- **Rozhodca a sieť.** Vetva `prep/referee` pripája výpočet k vstupom relácie. `prep/state-review` pokrýva čerstvosť údajov spoluhráča, oneskorené vzorky, presný zásah hrany a opakovanie udalostí. [Protokol rozhodcu](../contracts/combat-v1.md)
- **Grafika.** Vetva `prep/visual` používa zhodnú predikciu dráhy pre oba vykresľovače. Témy dostávajú kópiu pózy značky, pri strate aktuálneho dôveryhodného pozorovania dekorácia zmizne. Automatické skúšky menia továreň modulu, kontrolujú stav a porovnávajú reálne vykreslené pixely. `prep/observations` skúša jednoznačnosť relácie a vek syntetického smeru účastníka. [Grafické kontrakty](../contracts/visuals.md)
- **Správa a diagnostika.** Vetva `prep/admin` pridáva oprávnenie chránené Android Keystore, históriu a uzamknutie pravidiel pre duel. Diagnostika má ohraničenú kapacitu, dávkové potvrdenia a testovanú obnovu pri strate spojenia alebo procese ukončenom pred potvrdením. Podrobné polohy sú v distribuovanej konfigurácii vypnuté. [Administrácia](administracia.md) · [Žurnál a retencia](../contracts/diagnostics.md) · [Obnova](obnova-servera.md)
- **Terén a integrácia.** Vetva `prep/field` dopĺňa tlač, údaje pre testerov a výpočet P95. Koordinátor prepojil mobilnú sieť, merania, žurnál a registrovaný povrch. Každý pokus používa dve zhodné verzie APK. [Návod pre testerov](M2-skuska.md) · [Vyhodnotenie meraní](../qa/field/README.md)

## Skutočné ihrisko

Na fyzickú skúšku absolútnej polohy treba schválený výrez DMR5 a súradnice terčíkov i nezávislých kontrolných bodov. Nástroj `terrain/arena.py dmr` číta iba zvolené okno zdrojového rastra. Lokálny operátor môže pri spustení servera pridať `--survey <súkromný súbor JSON>`. Súbor má `version: 1`, `arenaSha256` a dve trojice `boardA`, `boardB` v poradí východný posun od začiatku mriežky, výška stredu v Bpv, severný posun. Server overí revíziu, hash, hranice a rozostup pred otvorením relácie. Oba telefóny dostanú zhodné súradnice s lístkom hry. Pri zmene rozostavenia sa prepočíta spoločný rámec existujúcim kalibračným postupom. Skúšobný syntetický profil má rozostup 1,50 m.

Verejne doručovaný syntetický povrch slúži na skúšku spoločného lokálneho rámca. Na absolútnu presnosť voči DMR použite zamerané body a postup pre georeferenciu. Odhady neistoty z fitu sú diagnostické. Ich číselné prahy ani dosah hry neboli potvrdené na ihrisku.

## Opakovanie softvérových kontrol

1. V koreňovom adresári spustiť `python qa/check.py` a `python qa/check_protocol.py`. Overujú geometriu, zhodu formátu, súboj, oboch klientov, duplicity, obnovu, neplatné vstupy a autoritatívny výsledok.
2. `python -m unittest discover -s terrain`, `python -m unittest discover -s qa/field` a `python terrain/test_georeference.py <lokálny priečinok s overenou mriežkou DVRM05>` overia výrezy, softvérový prevod, poradie osí a vyhodnotenie meraní.
3. Nad samostatnou QA databázou podľa návodu administrácie spustiť `python qa/test_admin.py <qa-konfigurácia>` a `python qa/test_diagnostic_api.py <qa-konfigurácia>`. Scenáre zahŕňajú reštart, neprístupnosť cudzích presetov, konflikt opakovanej dávky, retenciu a presnú polohu zapnutú len v izolovanej syntetickej skúške.
4. Unity Editor poskytuje `Spell.Editor.VisualContractChecks.Run` a `Spell.Editor.VisualPixelChecks.Run`. Druhá skúška potrebuje grafické zariadenie. `python tools/test_release.py` overí vydávací proces. Pri zmene scény alebo párovania finálny APK skontrolovať inštaláciou, skutočným prechodom obrazoviek a pripojením dvoch klientov.

**PASS** označuje splnené automatické tvrdenie pri uvedenom vstupe. **FAIL** je nesplnené tvrdenie a vyžaduje opravu. **PHYSICAL TEST REQUIRED** ostáva pri otázkach, na ktoré treba meranie telefónom. Výsledky konkrétneho APK patria do [changelogu](../CHANGELOG.md).
