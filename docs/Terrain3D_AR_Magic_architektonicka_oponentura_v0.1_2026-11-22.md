# Terrain3D AR Magic — predbežná architektonická oponentúra v0.1

**Dátum:** 22. 11. 2026  
**Status:** nezávislý technický návrh na diskusiu; **neodsúhlasený vlastníkom**  
**Nadväzujúci dokument:** `Terrain3D_AR_Magic_specifikacia_v0.1_2026-11-22.md`  
**Rozsah:** technické varianty, najväčšie riziká, odporúčaná architektúra, experimentálne dôkazy a ľudské rozhodovacie brány. **Nie implementačný plán ani pokyn začať vývoj.**

## 1. Výrok o uskutočniteľnosti

**Projekt je uskutočniteľný ako experimentálna dvojhráčová Android AR hra.** Neexistuje principiálna prekážka, ktorá by bránila zobrazovať a synchronizovať kúzlo v spoločnej scéne dvoch mobilov na podklade existujúceho výškového modelu DMR5. Samotný herný server, trajektória Fireballu, AoE, HP a výmenné grafické pluginy sú bežné inžinierske úlohy.

**Najväčšie riziko nie je grafika ani sieť.** Je ním **presné, stabilné a navzájom konzistentné prepojenie troch sústav**: (1) lokálne AR súradnice telefónu, (2) fyzické terčíky a druhý hráč, (3) georeferencovaný DMR5 terén. Tento problém treba overiť experimentálne pred rozsiahlou výstavbou hernej platformy.

**Rozhodnutie:** *Podmienečné GO pre technické skúšky a architektonické spresnenie; NO-GO pre veľký paralelný autonómny vývoj celej hry, kým nie je preukázaná kvalita lokalizácie a výmena pluginov.* To nie je odmietnutie projektu. Je to poradie, ktoré minimalizuje riziko vývoja nesprávneho základu.

## 2. Tri hlavné technologické varianty

| Kritérium | A. Unity + AR Foundation | B. Kotlin + ARCore + vlastná/Filament grafika | C. Godot + Android ARCore plugin |
|---|---|---|---|
| Mobilná AR integrácia | Najzrelšia a zdokumentovaná z troch možností | Priama kontrola nad API, robustné Android základy | Existuje, ale závislosť od zrelosti komunitných handheld AR pluginov |
| Hra, efekty a editor | Silné nástroje na VFX, scény, animácie | Grafické efekty vyžadujú viac vlastného vývoja | Silný otvorený herný engine; handheld AR je slabšie miesto |
| Práca s markermi/CV | Vlastný natívny most alebo plugin popri AR Foundation | Výborná kontrola nad kamerou a CV pipeline | Závisí od vlastnej integrácie |
| Modularita | C# assembly, rozhrania, výmenné build-time alebo zabalené implementácie | Kotlin rozhrania, DI, Android moduly, možná natívna ABI | GDScript/C#/GDExtension; pluginy závislé od verzie enginu |
| Open source celého enginu | Nie; open source môže byť vlastný zdrojový kód hry | Väčšina vlastnej platformy môže byť otvorená, ARCore je externý SDK | Áno, Godot je otvorený engine |
| Počiatočná rýchlosť AR hry | Vysoká | Stredná | Nízka až stredná pre handheld AR |
| Budúci automatický build vlastných APK | Možný, ale licencie/build nástroje Unity treba preskúmať | Priamo cez Android/Gradle pipeline | Možný cez Godot export, závisí od AR pluginu |
| Hlavné riziko | Proprietárne nástroje a zmluvné podmienky; komplikácie runtime pluginov/AOT | Náročnejšia tvorba kvalitných efektných kúziel | Zrelosť a udržiavateľnosť handheld AR ekosystému |

### A. Unity + AR Foundation (predbežne preferované pre herný PoC)

Dôvody: Google aj Unity dokumentujú AR Foundation + ARCore pre sledovanie polohy, kamerový passthrough, raycasty, anchors, obrazové sledovanie a podľa zariadenia depth/occlusion. Je vhodné pre rýchlu demonštráciu Fireballu ako pevne ukotveného objektu so živým kamerovým obrazom. Systém kúziel, vizuálny modul aj serverové správy možno urobiť cez samostatné C# rozhrania.

Podmienky: od začiatku nepoužívať `MonoBehaviour` odkazy ako jediné rozhranie medzi všetkými modulmi; mať čisté kontrakty nezávislé od scény. Vymeniteľnosť validovať dvoma nezávislými moduly. Nevyžadovať neschválený runtime download ľubovoľných C# DLL na Androide — **pre prvý PoC sa obe implementácie balia do APK a prepínajú cez registráciu/konfiguráciu modulu**. Budúci výber na webe realizovať cez kontrolované nové zostavenie a podpísanie APK.

Dôležité: Unity nie je open-source engine, hoci kód vašej hry môže byť verejný; Unity runtime fee bol oficiálne zrušený, ale aktuálne licenčné podmienky a podmienky automatizovaného build servera treba pred schválením preveriť.

### B. Natívny Android: Kotlin + ARCore + renderer (preferované pre úplnú kontrolu nad platformou)

Dôvody: aplikácia je od prvého dňa Android-only. Priama integrácia ARCore, kamerových parametrov, senzorov, CV markerov, oprávnení a diagnostiky môže byť jednoduchšia. Moduly ako Kotlin/Android knižnice a automatická Gradle zostava sa hodia k budúcemu open-source katalógu vlastných APK. Centrálne rozhrania sa dajú navrhnúť bez závislosti od proprietárneho herného enginu.

Nevýhody: kvalitné a ľahko rozšíriteľné kúzla, particle efekty, škálovateľná renderovacia scéna a nástroje pre grafických autorov by sa museli výraznejšie vytvoriť vlastnými silami. Nezačínať bez rýchlej ukážky „kamera + 3D kúzlo + marker + georeferencia“ na dvoch skutočných telefónoch.

### C. Godot + ARCore plugin (alternatíva s najotvorenejšou hernou platformou)

Dôvody: Godot a jeho pluginové mechanizmy sú otvorené a výrazne vhodné pre komunitných tvorcov grafických modulov. Avšak **podpora Android XR headsetov cez OpenXR nie je automaticky podpora klasickej mobilnej AR kamery**. Oficiálna dokumentácia Godot uvádza, že tradičný ARCore handheld plugin bol dlhšie neaktívny. V októbri 2026 existuje novší komunitný plugin PocketAR (0.0.2) pre Godot 4.7.2+, ale publikované informácie uvádzajú, že zatiaľ **nepodporuje image tracking ani depth occlusion**. Pre požadovanú kalibráciu je to podstatné riziko.

Odporúčanie: nezamietnuť Godot definitívne; ak bude pre vlastníka plne open-source engine najvyššia priorita, vyhradiť krátky izolovaný experiment s aktuálnymi pluginmi pred rozhodnutím.

### Predbežná voľba

**A (Unity + AR Foundation)** pre najrýchlejšie vizuálne funkčné herné PoC, **B (Kotlin + ARCore)** ako najvážnejší konkurent, ak bude rozhodovať budúca otvorená platforma s automatickým buildom APK. **C (Godot)** ponechať ako záložný/experimentálny variant. Konečnú voľbu musí potvrdiť test na cieľovom Androide, nie len porovnávacia tabuľka.

## 3. Odporúčaná logická architektúra bez ohľadu na engine

### 3.1 Klient Android

**Mobile Coordinator (tenké jadro)**: načíta moduly, overí ich verziu a deklarované schopnosti, orchestruje lifecycle a smeruje typované udalosti. Sám nesmie vedieť, ako vyzerá Fireball, ako sa deteguje marker ani ako sa vytvára mesh.

**Hardvérová/AR vrstva**: `DeviceCapabilities`, `CameraAndARTracking`, `GNSSProvider`, `SensorFusion`, `MarkerDetection`, `Calibration`, `SharedWorldTransform`, `TrackingQuality`.

**Terénne moduly**: `TerrainTileClient`, `LODPolicy`, `HeightSampler`, `TerrainRaycast`, `TerrainDebugOverlay`, `TerrainRiskAnalyzer`. Oddeliť výškové vzorkovanie pre kolíziu od renderovacieho LOD.

**Herné moduly**: `InputAdapter`, `SpellCatalog`, `SpellTargeting`, `ProjectilePresentation`, `AoEVFX`, `PlayerPresentation`, `DuelHUD`, `SafetyAuraUI`, voliteľné neskoršie `Minimap`.

**Infra vrstva**: `SessionClient`, `ClockSync`, `Transport`, `TelemetryRecorder`, `LogUpload`, `LocalCrashBuffer`, `GameConfigClient`.

Každý modul má malý verejný kontrakt a explicitne uvedené závislosti. Všetky citlivé zmeny stavu (HP, zásahy, povolené kúzla) sú len **serverom potvrdené**.

### 3.2 Centrálny server

Prvý PoC zvládne **jeden stavový herný proces + zdieľaná terénna služba Terrain3D + lokálny PostgreSQL + pomocný worker** v Docker Compose. Nie je dôvod vytvárať mikroslužby na každú funkciu, samostatný message broker či Kubernetes.

Logické moduly v serverovom procese: `SessionManager`, `PairingTokens`, `AuthoritativeClock`, `WorldState`, `PlayerTracking`, `SpellRules`, `ProjectileSimulation`, `CollisionAndAoE`, `HPState`, `LineOfSight`, `GameConfigAndPresets`, `PauseAndResume`, `AuditEvents`. Hranice modulu majú existovať v kóde a API, aj keď bežia v rovnakom procese.

**Terrain3D poskytuje jediné zdrojové dáta.** Serverový adaptér načíta z existujúceho zdroja výškové vzorky/tiles do cache. Hra nemá duplikovať 183–200 GB národného reliéfu. Pre prenos do Androidu možno servisne vytvárať zmenšené artefakty tiles, ktoré nie sú druhou úplnou databázou Slovenska.

**Transport PoC**: HTTPS na bootstrap/QR/config a **WebSocket** na obojsmerné správy. Cloudflare Tunnel a Cloudflare proxy oficiálne podporujú WebSocket. Stabilitu a latenciu treba reálne zmerať; ak sa niekedy vyžaduje UDP, posúdiť zmenu tunelovej architektúry — bežné verejné HTTP WebSocket nepredstierať za natívne UDP.

### 3.3 Databáza

Vlastný PostgreSQL s tabuľkami (návrhové názvy): `duels`, `session_devices`, `duel_presets`, `calibration_sessions`, `calibration_observations`, `spells_catalog`, `duel_events`, `debug_log_batches`, `module_versions`. Presné polohy a identifikátory vyžadujú bezpečnostné politiky. Live pozície a let projectilov majú byť primárne **v pamäti herného procesu**, nie zapisované ako každoframové SQL transakcie.

## 4. Rozhrania pluginov a udalosťový model

Cieľ: **skutočné prepojenie a výmena implementácií**, nie zdieľanie stoviek interných tried. Rozlišovať:

- **Synchrónne „hot path“ kontrakty** pre render, výšku, pozíciu a kolízne dotazy v rámci lokálneho procesu; bez JSON serializácie v každom snímku.
- **Asynchrónne typované udalosti** pre vstupy, vytvorenie kúzla, zásah, zmeny HP a pauzu (`CastRequested`, `CastAccepted`, `ProjectileSpawned`, `ProjectileStateCorrected`, `ImpactConfirmed`, `HealthChanged`, `TrackingDegraded`, `DuelPaused`, `DuelResumed`).
- **Sieťové správy** so schémou verzií a časom zo servera; Protobuf je vhodný kandidát, ale v dvojhráčovom PoC môže byť čitateľný JSON dostačujúci pre prvé testy. Nespojovať formát prenosu s API modulov.
- **Manifest modulu**: `id`, `version`, `apiVersion`, `capabilities`, `requires`, `permissions`, `platforms`, `signature`, `resourceBudget`. Kontroly kompatibility a neznámych schopností musia byť jednoznačné.

**Prísne rozdelenie oprávnení:** alternatívny Fireball renderer môže zmeniť výzor či zvuk, ale nesmie zmeniť trajektóriu a zranenie potvrdené serverom. Nová herná logika musí byť pridaná ako schválený a serverom akceptovaný typ pluginu, nie iba skript bežiaci na jednom telefóne.

**Praktický test výmeny modulov:** dodať `FireballRendererA` a `FireballRendererB` od nezávislých agentov, prepnúť implementáciu cez registráciu bez editácie jadra, spustiť rovnaký replay udalostí, porovnať časy, polohy a výsledky kolízií. V PoC je dostatočné, keď sú obe implementácie súčasťou jedného podpísaného APK. Budúce úplné zostavy pre komunitu sa pripravujú samostatným bezpečným build procesom.

## 5. Súradnicový model, ktorý musí byť jednotný od prvého dňa

Server má poznať autoritatívny geodetický rámec výšok a polôh. Vzhľadom na Terrain3D je potrebné zistiť, ktorý zo systémov údajov sa používa: napríklad DMR5 v **S-JTSK (JTSK03) + Bpv** alebo vo variante **ETRS89-TM34 + príslušná výška**. Oficiálny slovenský portál ponúka viac variantov DMR5 s rozličnými výškovými referenciami; nesmú sa pomiešať.

Na mobile vykresľovať v **lokálnej metrickej sústave** s počiatkom pri spoločnej kalibračnej aréne (napr. ENU alebo ekvivalent orientovaný na východ/sever/hore). Na serveri geografické prevody vykonávať v dostatočnej numerickej presnosti; renderer môže používať lokálne `float` koordináty pri rozumne blízkom počiatku.

Zadefinovať typy ako `GeoPosition`, `LocalWorldPosition`, `CameraPose`, `CalibrationTransform`, `ReferenceFrameId`, `Uncertainty`, `ObservedAt`. Nikde neprenášať neoznačené pole `[x,y,z]`, pri ktorom sa nedá zistiť os, výška, čas alebo referenčný rámec.

**Dva typy chýb sú oddelené:**

1. **Relatívna chyba** — ako presne obaja hráči a kúzla sedia navzájom v spoločnej lokálnej scéne. Marker + ARCore to môžu výrazne zlepšiť.
2. **Absolútna georeferenčná chyba** — ako presne spoločná scéna sedí na DMR5 a fyzickej krajine. Tú samotné dva terčíky s telefónnym GNSS úplne neodstránia.

Toto rozlíšenie je zásadné: vizuálne krásny Fireball medzi hráčmi ešte nedokazuje presný dopad na skutočný svah.

## 6. Najväčšie riziko: dvojmarkerová kalibrácia

### 6.1 Čo dva terčíky naozaj poskytujú

Pri známych fyzických rozmeroch a detegovaných rohoch značiek možno odhadovať **relatívnu polohu/orientáciu** terčíka vzhľadom na kameru a následne odvodzovať spoločný rámec. Dva markerové body zlepšujú priestorovú geometriu, ale presnosť závisí od ich veľkosti, rozostupu, kvality obrazu, ohniska, uhla záberu, viditeľnosti, počasia a schopností AR trackingu.

**Len dve približné GPS polohy nestačia na presný smer.** Ilustračný výpočet: pri nezávislej horizontálnej chybe každého merania ~3 m a rozstupe 20 m môže byť orientačná uhlová neistota z GNSS meraní približne sqrt(3² + 3²) / 20 ≈ 0,21 rad ≈ 12°. Nie je to garantovaná štatistika telefónu, ale demonštrácia, prečo GNSS základňu nesmieme pokladať za presné zameranie azimutu. Pri chybe smeru iba **1°** vznikne na 200 m bočný posun približne **3,5 m**, na 500 m približne **8,7 m**.

Aj známa veľkosť značky pomôže iba vtedy, keď má z kamery dostatok pixelov. Pri príliš vzdialenej alebo malej značke je odhad pozície nestabilný. V teréne treba skúšať rôzne rozmery terčíkov a **tretie pozorovacie miesto nesmie byť determinované iba tvarom trojuholníka**; musíme vyberať geometricky vhodné pozorovania.

### 6.2 Navrhnutý kalibračný experiment (technický dôkaz, nie ešte implementačný task)

- Použiť dva jednoznačné markery s fyzicky zmeranou veľkosťou a odstupe orientačne 20–50 m. Rozstup musí byť parametrom, nie dogmou.
- Pri každom markeri zbierať viac GNSS pozorovaní spolu s Android `accuracy`, časom a prípadne odhadom výšky; dáta filtrovať podľa kvality a priestoru, nie iba pasívne čakať na „ustálenie“.
- Kamera vidí oba markery z tretieho bodu; vytvoriť relatívny priestorový rámec, zaznamenať reprojekčnú chybu, stabilitu yaw a doplniť ARCore pose tracking.
- **Druhý telefón** kalibrovať nezávisle k tým istým markerom. Overiť, ako sa zhodujú pri statickom a pohyblivom teste.
- Porovnať polohu fyzicky známeho kontrolného bodu voči mobilnému AR a terénu; ak treba, na test **dočasne** použiť zameranie metrom/diaľkomerom, bez požiadavky kupovať geodetickú GPS súpravu pre konečného hráča.
- Zámerne testovať zhoršené podmienky: dlhá vzdialenosť, slnečné protisvetlo, občasné zakrytie markeru, šero, otáčanie telefónom, návrat na pôvodné miesto, zmena výšky a rozdielne zariadenia.
- Rozhodnúť na základe **reálnej mediánovej a 95. percentilovej chyby** a jej rastu v čase, nie podľa jedinej peknej ukážky.

### 6.3 Ak experiment neprejde

Konštrukcia hry sa nemusí zahodiť. Možnosti: zväčšiť fyzické značky, doplniť opakovanú vizuálnu rekalibráciu, obmedziť povolené zóny duelu na začiatku, použiť tretí marker ako voliteľnú pomôcku, zaviesť vyznačenú kontrolnú pozíciu, prípadne podporovať presnejšie geolokačné vybavenie ako **voliteľnú** nadstavbu. **Nesľubovať** z telefónneho GPS presnosť, ktorú fyzikálne nemôže dosiahnuť.

## 7. Riešenie konfliktu: presnosť a férovosť AoE

Fireball s rýchlosťou 10 m/s a 200 m doletom nie je problém. **Malý AoE s polohou hráča neistou o 3–10 m problém je.** Ak by mať Fireball polomer len 1 m, server by nemohol poctivo vyhlasovať presné zásahy podľa samotného GNSS.

Potrebujeme oddeľovať:

- smer/pozíciu **kamerového renderu**;
- odhad **pozície hráča a jeho neistoty**;
- serverový **autoritatívny účinok** kúzla.

Možné herné pravidlo: ak hráč nemá dostatočnú kvalitu spoločného trackingu, súboj sa pozastaví alebo sa neumožní presný zásah. Pri väčšej neistote môžu byť potrebné väčšie AoE alebo tolerantné, vopred transparentne definované pravidlá zásahu. **Nerozširovať skryto damage radius len preto, že GPS je zlé**, inak hráči nepochopia výsledok. Vlastník musí schváliť filozofiu férovosti.

Server má spravovať čas začiatku projektílu, počiatočný vektor, rýchlosť, stav dopadu a HP. Klienti môžu deterministicky **interpolovať/predpovedať vizualizáciu**, ale výsledok zásahu overí server. Výška DMR5 pri dopade musí pochádzať z presného, konzistentného výškového samplera, nie z najhrubšieho renderovacieho LOD.

## 8. Viditeľnosť, skryté prekážky a bezpečnosť

- **DMR5 dohľadnosť:** geometrické tienenie reliéfom — dá sa počítať vzorkovaním výšok pozdĺž spojnice medzi bodmi, s korekciou výšky očí/kamery/objektu. Ak je hráč za kopcom, dá sa označiť ako geometricky neviditeľný.
- **Nie „skutočne vidím človeka“:** DMR5 nezachytáva presne budovy, vegetáciu a ďalšie prekážky. Pre reálne rozpoznanie treba počítačové videnie, ale aj to môže zlyhať.
- **Vizuálne spresnenie podľa druhého hráča:** identifikovanie postavy v obraze samo dá hlavne smer a priebežné sledovanie, **nie jednoznačnú vzdialenosť ani identitu**. Pre PoC je vhodnejšie zamerať sa najprv na markery a vizuálne rozpoznávanie človeka odložiť, kým nie je potrebné pre samotný súboj. Ak sa testuje, zvážiť voliteľný marker na hráčovi alebo robustnú identifikáciu; herná logika nesmie brať neurčitú detekciu osoby za presný 3D údaj.
- **Terénne bezpečnostné varovania:** DMR5 vie odhaliť niektoré prudké svahy a zrázy, ale **nie všetky fyzické nástrahy**, napr. malé jamy, mokré skaly, dopravu či ploty. Hra nesmie deklarovať terén za bezpečný len na základe DEM. Zvážiť bezpečné zastavenie pohybu pri manipulácii s mobilom a jasné upozornenie na obmedzenia.
- **Bezpečnostná aura:** server vyhodnocuje konfigurovateľné vzdialenosti, ktoré sú veľké vzhľadom na typickú chybu lokalizácie. Varovný prah a prah hernej kolízie sa nemajú zamieňať.

## 9. Prevádzka doma a sieť

### Na ASUS ROG

`Docker Compose`: herný server, PostgreSQL, worker (log processing a príprava herných tiles), Cloudflare Tunnel agent; Terrain3D dataset sa neposiela do ďalšej databázy. Overiť presnú konfiguráciu ASUS ROG a reálnu spotrebu RAM. Dve pripojené mobilné zariadenia a niekoľko desiatok správ za sekundu nevyžadujú masívny cloud.

### Bezpečné párovanie bez registrácie

- Zakladateľ vytvorí duel, server vydá krátkodobý QR join token s obmedzením na konkrétnu reláciu a expirácou.
- Druhý klient sa pripojí a dostane dočasný serverom autorizovaný session token. Bez registrácie stále musíme overovať oprávnenie posielať herné udalosti, inak by ktokoľvek mohol falšovať zásahy.
- „Zapamätané admin heslo“ riešiť Android Keystore/bezpečným dlhodobejším tokenom a serverovou kontrolou práv, nie uložením hesla v čitateľnom texte.
- Presety uložené na serveri bez účtu naviazať na bezpečný identifikátor oprávneného zariadenia/zakladateľa s možnosťou obnovy podľa neskôr určených pravidiel.
- Cloudflare Tunnel nevystavuje nutne databázu, iba určené endpointy; DB a interné rozhrania nemajú byť prístupné z internetu.

### Logy a ochrana súkromia

Vyhovieť požiadavke na výmenný logovací modul (v PoC upload po súboji; neskôr stream), ale **lokálne spoolovanie musí prežiť crash** a obnoviť nedoručené dávky neskôr. Bezlimitná retencia polôh a úplne neoznámené zaznamenávanie môžu byť neprimerané; to je povinné právne a bezpečnostné rozhodnutie ešte pred verejným testovaním. Dôležité sú minimalizácia, prístupové práva, šifrovanie spojenia a možnosť mazania po overení debuggingu.

## 10. Tenké jadro verzus zamýšľaný „veľký mozog“

**Pozor na overengineering:** mobilný koordinátor nesmie byť centrálne obrovský objekt, do ktorého pluginy zasahujú ľubovoľne. Mal by iba (a) zistiť schopnosti, (b) pripojiť vybrané implementácie, (c) doručovať konkrétne udalosti, (d) riadiť lifecycle a diagnostiku, (e) overovať API verzie a práva. Samotná „inteligencia“ je v samostatných subsystémoch a autoritatívnom serveri.

Nepoužívať veľký univerzálny event-bus na každé GPU vykreslenie alebo polohový výpočet. Renderovacie moduly volajú malé typované lokálne API; nízkofrekvenčné stavové zmeny idú cez udalosti; sieťové správy cez presne definovanú hranicu. To umožní moduly naozaj vymeniť a udržať nízku latenciu.

## 11. Experimentálny dôkaz pred veľkým vývojom

Poradie dôkazov (nie definitívna roadmapa):

**Dôkaz 1 — spoločný priestor.** Dve fyzické značky, dva telefóny, jeden 3D virtuálny bod. Obe obrazovky zobrazia virtuálny bod na tom istom fyzickom mieste; pohyb a návrat na pôvodné miesto preukážu drift a neistotu. Bez tohto dôkazu nie je rozumné počítať zásahy.

**Dôkaz 2 — terén a presnosť dopadu.** Jeden importovaný výškový výrez existujúceho Terrain3D, skrytý kolízny povrch, AR zameriavač. Porovnať dopad virtuálneho lúča s reálnym svahom v bezpečnej testovacej oblasti. Zdokumentovať odchýlku aj pri nerovnom teréne.

**Dôkaz 3 — skutočné pluginové API.** Dva nezávislé vykresľovače Fireballu v tom istom APK, jeden koordinátor, bez editácie jadra, automatizované kontraktové testy a jednoduchá výmena v teréne.

**Dôkaz 4 — autoritatívny online duel.** Dva Androidy pripojené cez QR a Cloudflare Tunnel k domácemu serveru; porovnanie synchronizácie a pozastavenia po kontrolovanom zhoršení spojenia/AR trackingu.

**Dôkaz 5 — výkon a bezpečnosť.** Zmerať FPS, teplotu, pamäť a kolíznu presnosť v teréne; simulovať varovania a situácie s obmedzenou viditeľnosťou. Testovať na minimálne dvoch výkonnostných triedach telefónov, ideálne aj na treťom zariadení.

Úspešnosť neurčovať abstraktným pocitom „funguje“. Pre každú skúšku potrebujeme tolerancie a merania vyhovujúce hernému zážitku. Prahy presnosti, FPS a stabilizačného času majú byť schválené pred implementáciou akceptačných testov.

## 12. Rozhodovacie brány vlastníka

**GATE A — Platforma.** Schváliť Unity/AR Foundation vs. natívny Kotlin/ARCore vs. Godot po krátkych dôkazoch na mobiloch. Posúdiť váhu otvoreného enginu oproti rýchlosti tvorby efektov.

**GATE B — Kalibrácia a férovosť.** Podľa výsledkov dvojmarkerového experimentu určiť prijateľnú presnosť, veľkosť markerov, toleranciu AoE a pravidlá, kedy sa duel smie začať alebo musí pozastaviť.

**GATE C — Pluginové kontrakty.** Schváliť hranice modulu, verziovanie, formu vymeniteľnosti a politiku komunitných doplnkov po demonštrácii výmeny A/B.

**GATE D — Reálny PoC duel.** Osobne otestovať spárovanie, kalibráciu, Fireball, HP, varovania, pozastavenie a obnovenie; pustiť ďalšiu etapu iba po praktickom dôkaze.

**GATE E — Verejná alfa.** Až po posúdení práv a licencie DMR5, ochrany polôh hráčov, bezpečnosti a stability zvoliť cloud deployment, verejnú distribúciu a komunitné príspevky.

Až po schválení GATE A/B/C má zmysel autonómna paralelná implementácia s koordinátorom Astra Extra High (ak je daný režim dostupný), Codex/Claude Code a ľudským schvaľovaním zásadných zmien. Počet subagentov môže určiť koordinátor bez vopred stanoveného umelého počtového stropu, ale musí rešpektovať reálne kvóty, konflikty v Gite a závislosti. Vydávať „maximálny počet agentov“ za najrýchlejšiu stratégiu by bolo nesprávne, kým nie sú pripravené nezávislé úlohy a testy.

## 13. Finálne predbežné stanovisko

1. **Neodporúčam začať implementáciou kompletného magického boja ani komunitného katalógu.** Prvé dve technické otázky sú: presnosť shared-world AR na Slovensku a skutočná vymeniteľnosť modulov.
2. **Odporúčam podmienečne Unity + AR Foundation ako herný PoC**, ale konečný výber nech rozhodne meraná pilotná skúška s natívnym Kotlin + ARCore. Ak je plná otvorenosť enginu absolútna priorita, Godot je kandidát až po preukázaní stabilnej handheld AR integrácie.
3. **Server môže byť jednoduchý a lokálny.** PostgreSQL, jeden herný proces a Terrain3D data adapter v Dockeri sú vzhľadom na prvých dvoch hráčov primerané; Cloudflare Tunnel + WebSocket je realistický transport pre PoC.
4. **Nespájať render presnosť s presnosťou lokalizácie.** Aj dokonale renderovaná AR guľa môže dopadnúť geometricky nesprávne, ak je chybná transformácia AR→geodézia.
5. **Open source platforma je realizovateľná**, ak sa API modulu verzuje, podpisuje, testuje a oddeľuje autoritatívna logika od grafiky. Plne ľubovoľné runtime pluginy na Androide nateraz nevyžadovať.
6. **Pred verejným testom treba bezpečnostné a právne minimum**, najmä lokalizačné logy, fyzická bezpečnosť, serverové prístupové práva a uvedenie licenčného zdroja DMR5.

**Stav rozhodnutia:** Necháva sa otvorený na osobné schválenie vlastníkom. **Žiadna roadmapa ani produkčný kód zatiaľ neboli týmto dokumentom autorizované.**

## 14. Primárne technické zdroje a ich význam

- Google, AR Foundation a ARCore capabilities: https://developers.google.com/ar/develop/unity-arf/features
- Unity, AR Foundation occlusion platform support: https://docs.unity.cn/Packages/com.unity.xr.arfoundation%406.2/manual/features/occlusion/platform-support.html
- Google, ARCore supported devices: https://developers.google.com/ar/devices/
- Google, ARCore Geospatial a VPS availability: https://developers.google.com/ar/develop/c/geospatial/check-vps-availability
- Godot, ARCore plugin lifecycle: https://docs.godotengine.org/en/latest/tutorials/xr/arcore_intro.html
- PocketAR pre mobilné Godot AR, stav pluginu (2026-10): https://store.godotengine.org/asset/double-symmetry/pocketar/
- Unity, stav licencie Runtime Fee: https://unity.com/products/pricing-updates
- Cloudflare, WebSocket support: https://developers.cloudflare.com/network/websockets/
- Cloudflare Tunnel, WebSocket FAQ: https://developers.cloudflare.com/cloudflare-one/faq/cloudflare-tunnels-faq/
- GKÚ, poskytovanie DMR5, systémy a CC BY 4.0: https://www.gku.sk/gku/produkty-sluzby/na-stiahnutie/zbgis.html
- ÚGKK SR Geoportál, podmienky atribúcie ÚGKK SR: https://www.skgeodesy.sk/geoportal/zbgis/lls/poskytovanie/

**Poznámka k licencii DMR5:** Oficiálna stránka GKÚ aj Národný geoportál uvádzajú DMR5 z 1. cyklu ako **CC BY 4.0**, čo je priaznivé pre otvorený projekt; treba korektne uvádzať „ÚGKK SR“ a skontrolovať presnú licenciu prevzatých súborov/dátovej verzie. Nevydávať toto overenie za automatické povolenie použiť ľubovoľný iný dataset.
