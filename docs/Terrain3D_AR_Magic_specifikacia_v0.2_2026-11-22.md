# Terrain3D AR Magic — konsolidovaná špecifikácia požiadaviek v0.2

**Dátum:** 22. november 2026  
**Stav:** zadanie vlastníka projektu; podklad pre nezávislý architektonický návrh a oponentúru  
**Charakter:** koncepcia / požiadavky, **nie** schválená architektúra ani pokyn na implementáciu  
**Pracovný názov:** Terrain3D AR Magic (názov produktu zatiaľ nebol schválený)  
**Cieľová platforma MVP/PoC:** Android, Unity + AR Foundation / ARCore, výhradne online, dvaja hráči.
**Aktualizácia v0.2:** záväzne vybrané Unity; DMR5 zostáva na existujúcom disku; rozšírená požiadavka na Visual Localization (terčíky a protihráč).

> **Pravidlo interpretácie:** Formulácie „musí“, „bude“ a „zatiaľ“ uvádzajú používateľom rozhodnuté požiadavky. Technické príklady, predpoklady a odporúčania sú označené samostatne. **Unity je od verzie v0.2 rozhodnutie používateľa**; implementačné detaily AR Foundation, ARCore a počítačového videnia sú predmetom architektonickej kontroly.

## 1. Zámer a herný zážitok

Projekt je otvorená mobilná multiplayerová hra s magickými súbojmi v **skutočnom teréne Slovenska**. Hráč sa pozerá cez kameru Android telefónu na reálnu krajinu; do obrazu sa priestorovo presne vkladajú virtuálne kúzla a ich efekty. Reálny terén tvorí súčasť hernej geometrie. Základ tvorí existujúci digitálny model reliéfu **DMR 5.0 SR s krokom 1 × 1 m**, ktorý má hra používať ako **neviditeľnú** geometriu (kolízie, dopad kúziel, dohľadnosť, analýza rizík), nie ako povinne viditeľnú 3D krajinu. V debug režime sa dá geometria zobraziť, aby sa kontrolovalo zarovnanie so skutočným svetom.

Prvá verzia má byť zjednodušený praktický súboj dvoch hráčov. Do budúcnosti sa má z architektúry stať **open-source platforma pre výmenné moduly a kúzla**, ktorú budú vedieť rozvíjať tretie strany bez zásahov do jadra.

## 2. Záväzné rozhodnutia o rozsahu

- **Len Android**; iOS sa v prvej verzii nevyvíja.
- **Herný engine: Unity** (potvrdené rozhodnutie), primárny návrh mobilnej AR vrstvy cez AR Foundation a ARCore; presné balíky a verzie majú byť overené.
- **Len online**; súboj bez centrálneho servera nebude fungovať a nie je vyžadovaný offline režim.
- **Len dvaja hráči** pre prvý PoC; žiadny matchmaking, sociálna sieť ani účty.
- **Bez registrácie a prihlasovacích účtov hráčov.** Párovanie do duelu cez QR kód.
- **Kdekoľvek na Slovensku**, kde sú dostupné terénne dáta a dá sa uskutočniť kalibrácia. Nie iba v predpripravených arénach.
- **Jedno kúzlo Fireball**, pripravená architektúra na neskoršie kúzla a moduly.
- **Skutočná vymeniteľnosť pluginov už v PoC**; prvé APK obsahuje len vopred schválené interné moduly.
- **Žiadna minimapa v PoC**; do budúcna samostatný mapový modul, s ktorým musí návrh počítať.
- **Administrácia priamo v aplikácii**, nie cez notebook alebo samostatný webový admin.
- **Systém životov (HP)**; parametre sú konfigurovateľné.
- **Distribúcia APK mimo Google Play** počas skúšobnej fázy; Google Play až keď bude produkt dostatočne kvalitný.
- **Všetky počiatočné serverové komponenty doma**, na notebooku ASUS ROG; cloud infraštruktúra až vo fáze alfa.

## 3. Úvodná diagnostika Android zariadenia

Hneď pri prvom spustení aplikácia zistí dostupné parametre a schopnosti zariadenia:

- operačný systém a jeho verziu, triedu a model zariadenia;
- dostupné údaje o CPU, GPU, grafickom API, pamäti a možnostiach vykresľovania;
- veľkosť a rozlíšenie displeja, obnovovaciu frekvenciu, pomer strán;
- kalibračné parametre **aktuálne aktívnej kamery** (intrinsické parametre / FOV), podporované režimy, rozlíšenia, prípadne hĺbkové funkcie;
- kompatibilitu s ARCore, dostupnosť kamery, GNSS, gyroskopu, akcelerometra a prípadne magnetometra;
- výkonové obmedzenia zariadenia, tepelné podmienky a dostupné možnosti merania plynulosti.

Výsledok použije adaptívny systém kvality: detail terénnej siete, vzdialené dlaždice, efektové nastavenia, grafické rozlíšenie, dohľadnosť a dostupnosť náročných funkcií. **Nie každé zariadenie sprístupňuje všetky hardvérové údaje**, preto potrebujeme schopnostné zisťovanie a bezpečné predvolené hodnoty. Výstup diagnostiky má byť dostupný testovaniu a debuggingu.

## 4. Dáta terénu, priestor a výkon

### 4.1 Zdroje a integrácia

- Primárny podklad: existujúce údaje a spracovateľská infraštruktúra **Terrain3D**, ktorá už pracuje s DMR 5.0 Slovenska. **DMR5 súbory sú už na existujúcom disku**; nebudujeme nové duplicitné úložisko.
- **Nekopírovať celý existujúci dataset** (používateľ ho odhadol rádovo na 200 GB) do ďalšieho samostatného úložiska. Hra má využiť existujúce súbory na disku a existujúcu Terrain3D pipeline cez bezpečné rozhranie/službu; mobil si na požiadanie načíta len potrebný priestorový výrez, napr. dlaždice s LOD, nie celý dataset.
- Zistiť aktuálnu reprezentáciu, presnú licenciu, dátové služby a prístupové obmedzenia; znovupoužitie dát nemusí znamenať priame používanie existujúcej webovej aplikácie.
- Používať konzistentnú transformáciu zemepisných/súradnicových a výškových systémov, jednoznačné jednotky a referenčné rámce. Miestne AR súradnice musia byť previazané s georeferenciou bez neprijateľného driftu a chyby pri väčších vzdialenostiach.

### 4.2 Skrytý terén

- Terén sa vo vizuálnej hre **nevykresľuje ako krajina**, ale funguje ako neviditeľný 3D povrch pre kolízie, plánované dopady a terénnu viditeľnosť.
- Debug režim umožní zobrazovanie siete, bodov, výšok, osí, kalibračných značiek, presnosti a prípadne kolíznych plôch.
- Priestorový rozsah hernej scény môže zahŕňať približne **500 m** a v prípade kúziel ešte vlastné konfigurovateľné limity.
- Neposielať na mobil a nevykresľovať milióny nepotrebných bodov. Použiť **dlaždicovanie, úrovne detailu (LOD), priebežné streamovanie a GPU akceleráciu** podľa možností mobilu.
- Príklad predbežnej politiky LOD (nie pevné zadanie): 0–30 m krok 1 m; 30–150 m krok 4 m; 150–500 m krok 16 m. Architekt má navrhnúť vhodnejšie pravidlo podľa výkonových meraní, kolízií a skutočného vplyvu vzdialenosti na presnosť AR.
- Oddeliť **renderovacie LOD a kolíznu/geometrickú presnosť**; pri hrubom LOD sa nesmú zneplatniť dopady ani detekcia zrázov.

### 4.3 Obmedzenia dát

DMR 5.0 je model terénu, nie úplný model sveta. Sám **nezachytáva spoľahlivo budovy, stromy, pohybujúce sa prekážky, čerstvé stavebné zmeny či malé nebezpečné objekty**. Dohľadnosť a „bezpečnosť“ sa preto musia vyhodnocovať s explicitnou neistotou. Hra nesmie predstierať, že DMR5 garantuje skutočný výhľad alebo bezpečný priechod.

## 5. Kalibračný systém a lokalizácia

### 5.1 Hardvér

- Použiť **dva fyzické pevne umiestnené kalibračné terčíky/tyčky** na rovnakom mieste pre oboch hráčov.
- V PoC **nekalibrovať podľa kostolov, budov ani iných náhodných orientačných objektov**.
- Výroba a prípadný predaj fyzických terčíkov je budúca obchodná možnosť; na začiatku ide o vlastné prototypy.
- Kandidát technológie: vizuálne detegovateľné markery **AprilTag alebo ArUco** s jedinečnými identifikátormi a známymi fyzickými rozmermi. Presný formát určí architekt a testy.

### 5.2 Asistovaná kalibrácia priamo v Androide

1. Zakladateľ súboja zvolí **Kalibrovať** v administrátorskom rozhraní.
2. Aplikácia vyzve na osadenie prvého fyzického terčíka. Pri terčíku sa sleduje GNSS/GPS a jeho uvádzaná presnosť; po dostatočnej stabilizácii sa zaznamená poloha, výška, čas a odhad neistoty.
3. Aplikácia vyzve na osadenie druhého terčíka **približne aspoň 20 m** od prvého. Minimálny odporúčaný odstup sa môže meniť podľa dosahovanej kvality GNSS; nie je to rigidný prah. Opäť sa počká na ustálenie polohy.
4. Následne vyzve hráča, aby sa presunul na **tretie pozorovacie stanovište** tvoriace vhodný trojuholník s dvojicou terčíkov; **nejde o tretí fyzický terčík**.
5. Hráč namieri kameru na obidva terčíky. Počítačové videnie, kamera a AR tracking vypočítajú priestorový vzťah kamery k značkám a vytvoria spoločný kalibrovaný rámec.
6. Obaja hráči sa kalibrujú **voči tej istej dvojici terčíkov** a pracujú s rovnakou georeferenčnou definíciou relácie.
7. Aplikácia vizuálne a numericky ukáže výsledok a kvalitu kalibrácie, umožní kontrolu v debug režime a podľa potreby opätovné zameranie.

### 5.3 Kvalita lokalizácie

- GPS/GNSS môže mať chybu jednotiek až desiatok metrov. Používateľ uviedol príklad stabilizácie asi na 3 m, **nie povinný limit alebo záruku**.
- Vizuálno-inerciálne sledovanie polohy (kamera + ARCore + gyroskop + akcelerometer) pomáha **relatívnemu** pohybu, ale samo nezaručuje dlhodobú absolútnu polohu bez driftu.
- Dva terčíky významne zlepšujú **vzájomnú orientáciu a relatívne zarovnanie**. Samy automaticky neodstránia metrovú chybu absolútnych GPS súradníc ani nejednoznačnosť výškového systému.
- Navrhnúť model neistoty polohy/orientácie, kontrolu kvality kalibrácie, rekalibráciu a vyhodnocovanie zlyhania. Zohľadniť horizontálnu, vertikálnu a uhlovú neistotu aj výšku/rozmery markerov.
- **Vizuálne rozpoznávanie a priebežné sledovanie** kalibračných terčíkov aj protihráča má byť samostatnou modulárnou schopnosťou, ktorou budeme spresňovať polohu a orientáciu oproti samotnému GNSS. Pri hráčovi treba spoľahlivo priradiť identitu z hernej relácie a výskyt osoby v obraze; samotná detekcia postavy obyčajne poskytuje najmä smer, **nie presnú vzdialenosť**. Na 3D korekciu treba hĺbku alebo ďalšie geometrické informácie (viac časových pozorovaní, trianguláciu, alternatívny marker na hráčovi a pod.).
- OCR nie je primárna technológia na rozpoznávanie postavy alebo markerov; vhodnejšie je počítačové videnie/detekcia značiek.

## 6. Herná relácia a multiplayer

### 6.1 Spárovanie dvoch hráčov

- Hráči sa v úvode **fyzicky stretnú** (analógia historického duelu).
- Jeden hráč na mobile vytvorí hru, nastaví pravidlá a zobrazí **QR kód**.
- Druhý hráč QR kód naskenuje a pripojí sa do rovnakej relácie cez centrálny server.
- Hráči sa vzdialia na bezpečnú hraciu vzdialenosť a začnú súboj. Príbeh „odkrokovania“ je zámer hernej skúsenosti, nie pevná číselná vzdialenosť.
- Bez registračných účtov a matchmakingu. Potrebné sú dočasné identifikátory relácie a bezpečné párovacie tokeny, aby sa nedali svojvoľne prevziať cudzie relácie.

### 6.2 Server je rozhodca

- Každý mobil posiela svoju aktuálnu polohu, kvalitu polohy, relevantné herné udalosti a akcie na spoločný server.
- Server sleduje stav dvoch hráčov, koordinuje hernú reláciu a **autoritatívne** vyhodnocuje HP, zásahy, pravidlá a kolízie.
- Server určuje terénnu **geometrickú** dohľadnosť; hráč môže byť za kopcom aj na veľkú vzdialenosť. Prvé praktické testovanie bude na vzájomnú priamu viditeľnosť.
- Do budúcnosti plugin kúziel môže odhaľovať aj hráčov skrytých za prekážkami; nie je to súčasť úvodného kúzla.
- Mobil má lokálne plynulo animovať herné objekty, server synchronizovať a overovať ich skutočný stav. Riešiť časovú synchronizáciu, oneskorenie siete, kompenzáciu oneskorenia a korekcie predikcie.

### 6.3 Strata polohy a konektivity

- Systém priebežne monitoruje kvalitu GNSS, vizuálno-inerciálneho AR sledovania a sieťového spojenia.
- Pri zhoršení automaticky skúša obnovu pomocou miestneho trackingu a podľa možnosti vizuálneho pozorovania druhým hráčom.
- Ak presnosť alebo konektivita nie je dostatočná pre férový a bezpečný súboj, **pozastaví sa hra obom hráčom**.
- Druhý mobil dostane zrozumiteľné oznámenie: spojenie/priestorová lokalizácia druhého hráča je dočasne nespoľahlivá; počkajte na obnovu.
- Až po získaní prijateľnej kvality môže relácia pokračovať. Číselné prahy majú byť neskôr konfigurovateľné; pri architektonickom návrhu treba stanoviť, čo sa počas pauzy deje s letiacimi kúzlami a herným časom.

## 7. Bojový systém, prvé kúzlo a poškodenie

### 7.1 Modulárne kúzla

- Kúzla majú oddelené moduly **aktivácie**, **zameriavania**, **trajektórie**, **grafického zobrazenia** a **účinku**. Presné hranice sú predmetom návrhu architekta.
- V PoC sa kúzlo aktivuje **tlačidlom na displeji**.
- Budúce vstupy môžu byť **hlasové povely** alebo iné interakcie bez zásahu do herného jadra.
- Zameriavač ukazuje smer zoslania a, ak to geometrii zodpovedá, v AR zobrazuje **predpokladanú dopadovú zónu** na skutočnom teréne (napr. magický kruh, ktorý sa pri pohybe mobilu presúva na svahu).

### 7.2 Fireball

- Prvé a jediné kúzlo v PoC: **Fireball / ohnivá guľa**.
- Má konfigurovateľnú rýchlosť, vizuálny vzhľad a efekt pri zásahu.
- Východiskový **ilustračný príklad**: priamočiary let **10 m/s**, vzdialenosť cieľa cca **200 m**; pri plnej dráhe letí 20 sekúnd.
- **Bez balistickej krivky a gravitácie**; ide po pevnej priamej trajektórii až do kolízie, doletu alebo iného pravidlami definovaného skončenia.
- Fireball je virtuálny objekt s polohou vo **svetových/georeferencovaných súradniciach**. Nie je prilepený na obrazovku. Keď sa hráč presunie bokom, jeho projekcia sa perspektívne zmení, samotný let nie.
- Efekt sa má prirodzene javiť vzdialenejší (menší uhlový rozmer); obaja hráči môžu sledovať rovnaký let zo svojho uhla, ak je podľa pravidiel a prekážok viditeľný.
- Kolízia s neviditeľným terénom; bod dopadu a prípadná projekcia dopadovej zóny. Podľa pravidiel aj interakcia s hráčmi.

### 7.3 Plošný účinok a životy

- Kúzlo má **Area of Effect (AoE)** s konfigurovateľným polomerom. Ak sa hráč pri účinku nachádza v príslušnej kolíznej oblasti, server spustí stanovený herný účinok.
- Hra má systém **HP / životov**, nie automatické ukončenie po prvom zásahu.
- Počet životov, poškodenie, polomery, sady povolených kúziel, účinky a bezpečnostné prahy sú **nastaviteľné v administrácii na mobile** a záväzné pre súboj na serveri.
- Kým nie je presnosť lokalizácie overená, pravidlá zásahov musia bezpečne a férovo pracovať s neistotou hráčskej polohy. Rozhodnutie o toleranciách a riešení sporných zásahov je pre architekta otvorené.

## 8. Bezpečnosť fyzických hráčov

### 8.1 Bezpečnostná aura

- Každý hráč má virtuálnu bezpečnostnú **auru**, ktorá nemusí byť graficky viditeľná.
- Keď sa hráči k sebe približujú, systém ich varuje, aby sa nestretli v kontaktnej vzdialenosti a nenarušili AR perspektívu alebo fyzickú bezpečnosť.
- Ilustračné číslo zo zadania: varovanie pri vzdialenosti približne **20 m**; definitívne prahy neboli schválené.
- Pri kolízii aúr nastane definovaný herný postih oboch hráčov, napr. herná smrť a efekt výbuchu. Skutočný prah kolízie, varovanie a vizuálne efekty nastavuje administrácia.

### 8.2 Terénna inteligencia

- V teréne vrátane lesov, svahov a krasových oblastí má systém využívať známy reliéf na **varovanie pred rizikovými zrázmi, prudkými sklonmi a veľkými výškovými rozdielmi**, napr. výrazným niekoľkodesiatmetrovým spádom.
- Používateľ nechce, aby sa hráči v zápale súboja zranili; toto je kľúčová požiadavka, nie iba dekoračné upozornenie.
- Algoritmus musí byť konzervatívny a transparentný: DMR5 nie je aktuálna bezpečnostná mapa a nevidí malé jamy, vegetáciu, ploty, vozidlá ani ľudí. **Absencia varovania nikdy nesmie znamenať potvrdenie, že územie je bezpečné.**
- Architekt musí odporučiť aj vhodné pravidlá fyzického pohybu, stav pozastavenia a bezpečný AR režim pri chôdzi, bez predstierania dokonalého rozpoznania rizika.

## 9. Modulárny open-source systém

### 9.1 Hlavný princíp

- Jadrom projektu je **malý koordinačný softvér** („mozog“), ktorý spája komunikáciu medzi mobilnými modulmi a centrálnym serverom.
- Samostatné funkčné moduly: diagnostika zariadenia, kamera/AR, kalibrácia, lokalizácia, terénna služba, viditeľnosť, detekcia hráčov, zameriavanie, simulácia kúziel, vizuálne efekty, audio/vstupy, kolízie, bezpečnostná aura, terénna bezpečnosť, spojenie so serverom, logovanie a pod.
- Moduly komunikujú cez **jasné, rýchle, zdokumentované a verzované API**. Jadro nesmie byť závislé od konkrétneho grafického efektu či implementácie kúzla.
- Odlišovať **klientské vizuálne moduly** od **serverovej autoritatívnej hernej logiky**. Vizuálny plugin nesmie svojvoľne vyhlásiť zásah alebo meniť HP.
- Verejné API umožní inému autorovi vytvoriť alternatívny grafický efekt či úplne novú funkcionalitu; celé na GitHube ako **open source**.

### 9.2 Výmennosť otestovaná už v PoC

- Vybrať aspoň **jednu skutočnú funkcionalitu** a nechať vytvoriť **dve nezávislé implementácie toho istého modulu**, ideálne nezávislými vývojovými agentmi/prostrediami.
- Prakticky ich **prepnúť alebo vymeniť bez úpravy centrálneho jadra** a bez zmeny verejného kontraktu.
- Overiť automaticky (API kontrakty, integrácia) aj v reálnom teréne, že jadro a ostatné moduly korektne spolupracujú.
- „Modularita“ iba v názvoch adresárov či tried nie je akceptovateľná; musí byť výmennosť a zrozumiteľný externý vývojový kontrakt.

### 9.3 Predbežný návrh rozhraní — na architektonické preskúmanie

Nasledujúce je **iba predbežný návrh z diskusie, nie schválená implementácia**:

1. lokálne typované rozhrania modulu v zvolenom engine s minimálnou latenciou;
2. Protobuf alebo porovnateľný verzovateľný binárny kontrakt pre herné udalosti cez sieť;
3. JSON manifest modulu s identitou, verziou, požadovanými schopnosťami a kompatibilitou.

Architekt má preveriť, ako sa externé pluginy bezpečne zostavujú, načítavajú, podpisujú a testujú na Androide. Ak sa zvolí engine vyžadujúci build-time registráciu pluginov, treba to explicitne priznať; nevyhlasovať neexistujúcu možnosť ľubovoľného runtime hot-swappingu.

### 9.4 Budúci katalóg komunitných modulov — nie PoC

- Vývojári budú môcť zverejňovať, popisovať, skúšať a vylepšovať moduly na webe hry.
- Hráč si na webe vyskladá kombináciu kompatibilných modulov a stiahne **vlastnú zostavu APK** s vybranými modulmi.
- Prvá aplikácia obsahuje len schválenú pevnú sadu modulov; **webový katalóg, komunitné funkcie a automatické zostavovanie variantov APK sa zatiaľ nevyvíjajú**.
- Už pri návrhu treba počítať s kompatibilitou API, oprávneniami, bezpečným build procesom a podpisovaním APK.

## 10. Administrácia a herné predvoľby

- Administrácia je dostupná **priamo v mobilnej aplikácii zakladateľa súboja** po zadaní administrátorského hesla.
- Overenie hesla sa má dať **zapamätať**, aby sa nemuselo opakovať pred každým súbojom; bezpečný spôsob uchovania oprávnenia navrhne architekt.
- Zakladateľ na mieste nastaví pravidlá; druhý hráč sa následne pripojí cez QR kód a akceptuje serverom spravovaný stav relácie.
- Nastaviteľné: životy, sila a povolenie kúziel, polomery zásahov, parametre aury, ďalšie budúce nastavenia.
- Konfigurácie súbojov sa ukladajú **na server** do histórie **viacerých presetov**. Nesmie sa prepísať iba posledný stav.
- Presety môžu byť identifikované **dátumom a časom**; nepotrebujú povinné vlastné názvy.
- **Bez vopred zavedeného limitu počtu presetov.** Ich zoznam zobrazuje len zakladateľ na vlastnom mobile.
- Po novom spustení sa dá vyvolať posledný alebo skorší preset bez nastavovania od nuly.
- Hromadné mazanie a export presetov sú **mimo prvého rozsahu**.
- Bez registrácie treba pre architektonickú oponentúru vyriešiť, ako server bezpečne rozpozná právo zakladateľa zobraziť svoje presety bez vytvárania klasických používateľských účtov.

## 11. Logovací modul a debugging

- Logovanie je **samostatný vymeniteľný modul** so stabilným rozhraním.
- Logovať technické udalosti, priebeh kalibrácie, kvalitu a zmeny polohy, herné akcie, výstrely, zásahy, HP, pozastavenia, sieťové problémy, stav výmenných modulov a **polohy oboch hráčov**.
- **Prvý implementovaný logovací modul zhromažďuje údaje počas súboja a odošle ich na server až na konci súboja.**
- API musí umožniť **alternatívny modul s priebežným streamovaním logov**, ale ten v prvej verzii nie je potrebný.
- Logy sa prezerajú **iba na serveri** (napríklad serverový výpis/diagnostické nástroje), nie v samostatnom mobilnom UI.
- Používateľ neurčil retenčný limit a chce zatiaľ zbierať logy bez pevného počtového limitu.
- Treba robustné lokálne zapisovanie a obnovenie neodoslaných logov po páde mobilu či prerušení spojenia. Presná stratégia, ako zachytiť aj posledné dáta pri havárii pred koncom duelu, patrí do architektonického návrhu.
- **Dôležitá oponentúrna výhrada:** záznamy presných polôh sú osobné údaje; pred reálnym používaním treba navrhnúť transparentnosť, ochranu, prístupové práva, retenciu a primerané spracúvanie v súlade s právom. V diskusii sa používateľ rozhodol nezaoberať teraz samostatným upozornením; to **nie je výnimka z prípadných právnych povinností**.

## 12. Prvá serverová infraštruktúra

- Počiatočný centrálny server pobeží na **domácom notebooku ASUS ROG**. Používateľ predpokladá približne **16 GB RAM**, presnú konfiguráciu treba pred rozhodnutím o veľkosti procesov overiť.
- **Docker** kontajnery s herným serverom, lokálnou databázou **PostgreSQL alebo podobným samostatne hostovaným riešením**, potrebným workerom a obslužnými službami.
- **Cloudflare Tunnel** pre sieťový prístup zvonku (konkrétne limity protokolu, bezpečnosť a typ konektivity preveriť).
- **Žiadna externá spravovaná databáza** (napr. SaaS Postgres) v úvodnej verzii. Existujúcu infraštruktúru Terrain3D nevytvárať duplicitne.
- Bez dostupného vlastného servera **hra nebude fungovať**; nevyžaduje sa automatický cloudový failover.
- Presun/rozšírenie servera do cloudu **až vo fáze alfa**, a to bez potreby prestavať všetky moduly.
- GitHub repozitár má byť **verejný**, otvorený pre komunitu. Licencia projektu, pravidlá príspevkov a licencia zdrojových geodát sa musia vyhodnotiť a rozhodnúť samostatne.

## 13. Autonómny multiagentový vývoj — až po schválení návrhu

- Kód budú vytvárať **Codex a prípadne Claude Code** alebo ďalšie AI prostredia, pracujúce nad spoločným verejným GitHub repozitárom.
- Hlavný AI **koordinátor** má byť podľa predstavy používateľa **Astra s najvyššou dostupnou úrovňou uvažovania (Extra High/Very High podľa konkrétneho produktu)**.
- Koordinátor má **autonómne vyberať počet agentov** a ich odborné úlohy; používateľ **nechce vopred stanoviť umelý početný limit**. Sám má podľa náročnosti práce voliť model/úroveň uvažovania pre jednotlivé podúlohy.
- Agenti majú pracovať paralelne, kde je to technicky vhodné, s jasným vlastníctvom úloh, kvalitnými integračnými kontrolami, dokumentovaným stavom a minimalizáciou konfliktov v Gite.
- Reálnu súbežnosť však limitujú dostupné práva, kvóty, náklady, infraštruktúra a závislosti; **„bez umelého limitu“ neznamená neobmedzené fyzické zdroje**.
- Automatizované testy sú povinné aj bez platenej GitHub CI. Praktické terénne a vizuálne testy vykoná človek.
- Autonómny vývoj bude obsahovať **human gates**, pri ktorých je nevyhnutné schválenie vlastníkom. Používateľ rozhoduje o architektúre, funkciách, správaní a reálnom testovaní; nemusí čítať ani upravovať zdrojový kód.
- **Teraz nespúšťať implementáciu.** Najprv nezávislá architektonická oponentúra, používateľské schválenie, až potom roadmapa a implementačné zadanie.

## 14. Navrhovaný používateľský priebeh PoC

1. Hráč spustí Android aplikáciu; prebehne diagnostika hardvéru, oprávnení a aktuálnych AR možností.
2. Zakladateľ otvorí administráciu, vyvolá uložený preset alebo upraví pravidlá.
3. Spustí asistovanú kalibráciu: prvý marker (stabilizácia GNSS), druhý marker s odporúčaným rozostupom, tretie pozorovacie stanovište a vizuálne zameranie oboch markerov.
4. Druhý hráč pripojí svoj Android mobil cez QR kód a dokončí kalibráciu voči tej istej dvojici značiek.
5. Oba mobily a server potvrdia, že zdieľajú dostatočne spoľahlivý priestorový rámec.
6. Hráči sa vzdialia na rozumnú bezpečnú vzdialenosť a spustia duel.
7. Na obrazovkách vidia kamerový obraz a AR efekty. Digitálny terén ostáva skrytý, dostupný v debug režime.
8. Tlačidlo Fireball aktivuje priamočiary svetovo ukotvený projektíl; zameriavač môže ukázať predpovedanú zónu dopadu na terén.
9. Server overuje stav kúziel a AoE zásahy, mení HP, vyhodnocuje auru a dohľadnosť; mobily vykresľujú spoločný výsledok.
10. Pri strate kalibrácie, signálu alebo bezpečnej kvality súboj pozastaví na oboch mobiloch; pokračuje po obnove.
11. Pri skončení súboja logger odošle zozbierané údaje na vlastný server; konfigurácia a história presetov ostávajú uložené na serveri.

*Poznámka:* Konkrétne poradie párovania a kalibrácie môže architekt navrhnúť efektívnejšie. Tento prechod je ilustračný, nie nemenné používateľské workflow.

## 15. Akceptačné skúšky pre prvý PoC

1. **Dve skutočné Android zariadenia** sa spárujú QR kódom bez účtov a bez matchmakingu cez domáci server.
2. Pracujú s jednou fyzickou dvojicou markerov; aplikácia vedie používateľa kalibráciou a ukazuje **odhad kvality**, nie nepreukázateľnú absolútnu presnosť.
3. Na overenej testovacej lokalite sa neviditeľný DMR5 povrch v debug režime zobrazí zarovnaný s reálnym terénom v medziach merateľnej chyby; výsledky sa logujú a vyhodnocujú.
4. Fireball s definovanou rýchlosťou zostáva vo svetovom priestore pri pohybe kamery a na oboch zariadeniach vykazuje konzistentnú časovú trajektóriu.
5. Predpovedaná dopadová zóna a serverová skutočná kolízia sa porovnajú. Musí byť zdokumentované, aké odchýlky vznikajú pri GNSS/AR neistote a rôznych LOD.
6. Server rozhoduje o AoE a HP podľa jednej autoritatívnej konfigurácie.
7. Ak sa hráči priblížia, funguje aura/varovanie/konfigurovaný herný postih; pri relevantných terénnych rizikách sa zobrazí varovanie.
8. Nasimulovaná strata trackingu alebo konektivity vyvolá pozastavenie obom hráčom a korektné obnovenie relácie.
9. **Dve nezávislé implementácie jedného modulu** sa dajú zameniť bez editácie koordinačného jadra. Automatické kontraktové testy aj praktický test prejdú.
10. Debug logy sa uchovávajú, pri riadnom ukončení sa odošlú na server, neodoslané logy po páde sa bezpečne obnovia.
11. Automatická adaptácia detailov má merateľný dopad na plynulosť pri slabšom aj výkonnejšom kompatibilnom telefóne; presné minimálne FPS a hardvérové požiadavky stanoví oponentúra a test.
12. Server si pamätá viacero presetov, zakladateľ vyvolá predchádzajúcu konfiguráciu; bez webového admina.

## 16. Podstatné technické a právne otázniky pre nezávislého architekta

- **Kalibrácia:** dá sa z dvojice značiek s telefónnym GNSS a vizuálnym sledovaním vytvoriť dostatočne stabilný spoločný AR rámec pre súboj a AoE? Aká je **absolútna** chyba voči DMR5 a ako ju zmerať?
- **Vzájomná lokalizácia:** čo je reálne možné odvodiť z vizuálneho sledovania druhého hráča bez osobitného markeru, hĺbkového senzora alebo druhej kalibrovanej perspektívy?
- **Súradnicové systémy:** správny horizontálny rámec, výškový systém, lokálna tangentová sústava, výška kamery/markerov, aritmetická presnosť a drift na 500 m.
- **Dohľadnosť:** skutočný terén vs. vegetácia/budovy, rozdiel between terrain-only line of sight a vizuálna detekcia.
- **Kolízie a férovosť:** neistota GPS rádovo metre vs. kúzla a hráči s potenciálne menšou AoE; čo môže byť spoľahlivo rozhodované serverom?
- **Bezpečnosť:** fyzický pohyb hráčov cez kameru, riziká prudkých svahov a skryté prekážky; čo sa musí zabezpečiť pre bezpečné testovanie.
- **AR platforma:** Unity už bolo používateľom vybrané; architekt má kriticky overiť kombináciu Unity + AR Foundation/ARCore, podporované zariadenia, licencovanie, Android build/podpisovanie, výmenu pluginov, mobilné výkonnostné limity a testovanie na dvoch fyzických telefónoch. Alternatívny engine odporučiť len pri podstatnom, preukázanom blokéri.
- **Open-source licencia:** podmienky engine, pluginov, publikovaných závislostí a používania geodát; licenčná separácia dát a kódu.
- **Distribúcia:** vlastné zostavy APK, ich podpisovanie, aktualizácie, ochrana proti škodlivým pluginom a kompatibilita pri budúcej komunitnej platforme.
- **Sieť:** zvolená transportná vrstva cez Cloudflare Tunnel, priepustnosť, latencia, prerušenie, časová synchronizácia, bezpečnosť bez registrácie.
- **Prevádzka:** RAM/CPU domáceho ASUS ROG, Docker, PostgreSQL, worker, prístup k existujúcim DMR5 dátam bez ich duplikácie, bezpečné zálohy.
- **Ochrana súkromia:** dôsledky zberu a dlhodobého uchovávania presných polôh, účel/debugging, minimalizácia údajov, informovanie a pravidlá uchovávania.
- **Budúca modulárnosť:** ako otestovať skutočnú výmennosť bez jadrových zmien a ako oddeliť dôveryhodné serverové pravidlá od komunitných vizuálnych doplnkov.

## 17. Pokyn na nasledujúcu fázu: architektúra, nie kód

Predložiť tento dokument najvýkonnejšiemu dostupnému modelu na **nezávislú, kritickú architektonickú oponentúru**. Model nesmie iba potvrdiť želania; musí oddeliť realizovateľné od experimentálnych, explicitne označiť fyzikálne/technické limity a navrhnúť overovacie experimenty.

Požadované výstupy architekta:

1. **Minimálne tri odlišné technické/architektonické varianty v rámci schváleného Unity stacku** (napríklad skladba komponentov lokalizácie, pluginov a sieťovej synchronizácie), porovnanie a odporúčaný variant. Samotný výber Unity už nepovažovať za otvorenú používateľskú otázku.
2. Odporúčaná architektúra klienta, servera, pluginov a zdieľaných kontraktov — hranice modulov a typy udalostí.
3. Návrh súradnicového modelu a reálny postup kalibrácie (vrátane toho, čo z dvoch markerov a GNSS **nie je** zaručené).
4. Návrh renderovania/streamovania a systému kolízií so zreteľom na rozsah 500 m a mobily rôznych tried.
5. Návrh multiplayerovej synchronizácie, fairness pri nepresnej polohe, viditeľnosti a rekonexie.
6. Návrh výkonovej, bezpečnostnej a súkromnoprávnej stratégie s merateľnými limitmi PoC.
7. **Prototypové technické dôkazové skúšky** (spikes) na najväčšie riziká, najmä kalibráciu, spresnenie polohy a výmennosť pluginov.
8. Prehľad technického dlhu, nákladov, externých závislostí a licenčných rizík.
9. **Rozhodovacie brány vlastníka (human gates)** s variantmi a odporúčaniami v ľudsky zrozumiteľnej forme.
10. Až po schválení architektúry pripraviť **etapovú roadmapu** pre autonómny multiagentový vývoj cez GitHub. **Bez okamžitého kódovania alebo produkčného nasadenia.**

## 18. Explicitne mimo prvého PoC

- iOS a offline režim;
- matchmaking, verejné kontá, sociálne profily a komunitné siete;
- hotový webový katalóg pluginov a používateľsky skladané APK;
- ľubovoľné dodatočné kúzla vrátane odhaľovania hráča za kopcom;
- hlasové zosielanie kúziel;
- minimapa a pokročilé mapové UI;
- automatický cloudový failover alebo produkčný cloud hosting;
- registrácia, platby, obchod s terčíkmi a reklamy;
- geodeticky presná lokalizácia alebo zaručená centimetrová presnosť (nič také nebolo dokázané).

## 19. Potvrdené po architektonickej diskusii – aktualizácia v0.2 (22. 11. 2026)

Táto časť spresňuje a v prípade rozporu **prepisuje staršie predbežné varianty** z v0.1. Zachovať pri ďalšom návrhu; neprikladať ako oddelený dokument, ide o súčasť tejto konsolidovanej špecifikácie.

### 19.1 Herný engine – rozhodnuté

- **Použiť Unity** ako engine pre prvú Android AR hru. Predbežná integračná voľba je **AR Foundation + ARCore**; architekt má preveriť kompatibilné stabilné verzie, reálne zariadenia, licencie, diagnostiku a pluginový model.
- Nevracať sa k výberu Unity vs. Godot vs. Kotlin bez zásadného technického dôvodu alebo výslovného pokynu vlastníka.
- Počas prvej fázy ponechať vymeniteľné moduly oddelené od Unity koordinačného jadra; pri PoC dokázať výmenu dvoch implementácií rovnakého rozhrania bez zmeny jadra.

### 19.2 Terénne dáta – rozhodnuté

- **DMR5 už máme fyzicky uložený na disku**, je napojený na existujúci projekt Terrain3D. Nové kopírovanie celého datasetu nie je žiaduce.
- Navrhnúť čítanie existujúcich súborov cez serverovú službu s právami iba na čítanie, streamovanie menších výrezov potrebných pre hernú polohu a oblasť vplyvu a jednotnú georeferenciu pre herný server aj mobil.
- DMR5 je podklad pre kolízie, predpovedanie dopadu, line-of-sight a základnú analýzu reliéfu; nesmie sa zamieňať za model skutočných objektov (vegetácia, budovy, ľudia).

### 19.3 Nová výslovná požiadavka – vizuálna lokalizácia a sledovanie

Používateľ potvrdil záujem o **rozpoznávanie obrazu a časové sledovanie protihráča aj kalibračných terčíkov**, predovšetkým ako ďalší zdroj presnosti lokalizácie.

Požiadavky na vymeniteľný modul **Visual Localization / Object Tracking**:

1. Rozpoznávať a identifikovať oba pevné kalibračné terčíky (kandidáti AprilTag/ArUco), vypočítať pózu voči kamere pri známej veľkosti, reportovať kvalitu a neistotu merania.
2. Priebežne sledovať ich obrazovú polohu a využívať opätovné pozorovanie na opravu driftu lokálneho AR rámca po pohybe hráča.
3. Rozpoznávať a sledovať **protihráča** v obraze; z detekcie poskytovať aspoň smerovú informáciu a časovú sekvenciu pozorovaní. Na prepojenie s konkrétnym hráčom z dvojice musí byť bezpečná dátová asociácia.
4. Navrhnúť fúziu údajov **GNSS + vizuálne markery + ARCore visual-inertial tracking + IMU + voliteľná dostupná hĺbka + pozorovania protihráča**. Vizuálne rozpoznanie protivníka **nie je zárukou presnej vzdialenosti** a samo nesmie vynucovať neodôvodnené presné korekcie.
5. Oddeliť *relatívnu* presnosť voči terčíkom a druhému hráčovi od *absolútnej* presnosti digitálneho DMR5 voči geodetickému svetu; neistota GPS neprestane existovať tým, že kamera spoľahlivo pozná dva terčíky.
6. Exportovať jednoduché verzované rozhranie: identifikátor sledovaného objektu, čas merania, poloha/póza alebo smer, priestorový referenčný rámec, intervaly neistoty či confidence, stav trackingu a udalosť straty/opätovného získania cieľa. Odporúčaná interná reprezentácia je na úrovni dátových kontraktov, nie konkrétneho CV frameworku.
7. Pri slabom mobile používať nastaviteľnú frekvenciu detekcie, ľahší tracker medzi detekciami, obmedzenie ROI a rozumné GPU/CPU rozpočty. Počas straty trackingu degradovať kvalitu, skúsiť obnovu a pri kritickej neistote pozastaviť duel na oboch mobiloch.
8. V debug zobrazení vedieť vizualizovať rozpoznané značky, bounding box/smer hráča, merania, korekcie transformácií, drift a kvalitu spoločného AR zarovnania.

**Dôležité:** V prvej verzii je hlavnou oporou dvojica fyzických terčíkov; sledovanie hráča je doplnkový zdroj. Nepovažovať odhad vzdialenosti k hráčovi za vyriešený pred terénnym meraním.

### 19.4 Navrhovaný prvý experiment – na odsúhlasenie architektom

Pripraviť dva telefóny, dva označené fyzické terčíky a **jeden spoločný virtuálny objekt** ukotvený na známom mieste zvoleného testovacieho terénu. Obaja hráči sa nezávisle kalibrujú a pri pohybe z rôznych strán overia, že objekt aj debugovaný neviditeľný terén ostávajú v konzistentnej pozícii. Zdokumentovať rozdiel relatívnej zhody medzi telefónmi a absolútneho nesúladu voči DMR5. Porovnať zlepšenie po opätovnom pozorovaní markerov a po pridaní vizuálneho sledovania protihráča. Stanoviť merateľné akceptačné limity pred prácami na ďalších kúzlach.

### 19.5 Pokyn pre nezávislú AI architektonickú oponentúru

Architekt má považovať **Unity za zvolené**, **existujúce DMR5 na disku za zdieľaný dátový zdroj** a **modul vizuálnej lokalizácie za súčasť architektúry**. Má kriticky preveriť dosiahnuteľnú presnosť, návrh API modulu, výkonnosť na mobiloch, licencie knižníc, bezpečnostné a súkromnoprávne dopady a riziko, že hráča nemožno spoľahlivo 3D lokalizovať z jednej kamery bez hĺbky. Žiadne ďalšie kódovanie ani roadmapu vývoja nespúšťať pred schválením architektúry vlastníkom.

---

---

**Koniec špecifikácie v0.2.** Tento text konsoliduje používateľské požiadavky a otvorené otázky zo zadávacieho rozhovoru. Technické odporúčania podliehajú architektonickej oponentúre a schváleniu vlastníkom.
