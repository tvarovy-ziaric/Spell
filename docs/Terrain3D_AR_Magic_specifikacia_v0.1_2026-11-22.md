# Terrain3D AR Magic — produktová a technická špecifikácia v0.1

**Dátum:** 22. november 2026  
**Projekt:** VibeKing Firma — pracovný názov *Terrain3D AR Magic*  
**Stav:** konsolidácia používateľom nadiktovaných požiadaviek, **nie schválená architektúra**  
**Účel:** nezávislé architektonické posúdenie špičkovým AI modelom s vysokou úrovňou uvažovania; až po ľudskom schválení návrh roadmapy a autonómneho multiagentového vývoja.  
**Zdroj:** hlasová konzultácia 22. 11. 2026 v projekte VibeKing Firma.  
**Dôležité:** Zachovať rozdiel medzi rozhodnutiami vlastníka, predbežnými technickými návrhmi asistenta a otvorenými rizikami. Nepovažovať ukážkové parametre za záväzné hodnoty.

## 1. Vízia a hranice prvej verzie

Vytvoriť otvorenú, modulárnu **multiplayerovú AR hru s magickými súbojmi**, ktorá sa odohráva v **skutočnom teréne Slovenska**. Hráči sledujú okolie cez živý obraz kamery telefónu. Digitálny model terénu zostáva pri bežnej hre neviditeľný; používa sa ako priestorový podklad pre AR objekty, fyziku kolízií, výpočet trajektórií a základné bezpečnostné upozornenia. Hráči sa fyzicky pohybujú v krajine a vidia kúzla ukotvené do spoločných svetových súradníc.

- **Platforma MVP/PoC:** výlučne Android, online-only. iOS, offline režim ani Google Play nie sú súčasťou prvej fázy.
- **Počet hráčov:** presne dvaja v prvom PoC; architektúra sa nesmie blokovať voči neskoršiemu rozšíreniu.
- **Bez registrácie a účtov** v prvej verzii.
- **Bez matchmakingu:** fyzické stretnutie, založenie súboja, párovanie QR kódom.
- **Kde sa hrá:** prakticky kdekoľvek na Slovensku, kde sú dostupné terénne dáta, internet a podmienky na kalibráciu. Nie iba preddefinované arény.
- **Prvé kúzlo:** Fireball; systém kúziel otvorený ďalším modulom.
- **Prvé rozhranie:** AR obraz z kamery, bez minimapy.
- **Prvé distribuovanie:** testovacie APK mimo Google Play, prípadne cez GitHub Releases; obchod až po dostatočnom overení kvality.
- **Server:** zatiaľ na domácom ASUS ROG notebooku používateľa; cloud až vo fáze alfa. Bez dostupného centrálneho servera súboj nefunguje.

## 2. Terénne dáta a renderovanie

1. Využiť existujúcu dátovú infraštruktúru **Terrain3D** a digitálny model reliéfu **DMR 5.0 SR**, s približne 1 × 1 m výškovými vzorkami. Existujúca dátová základňa má podľa používateľa približne 200 GB. **Nekopírovať celý dataset do druhého úložiska**; navrhnúť bezpečné a výkonné napojenie cez existujúcu dátovú službu alebo zdieľanú pipeline.
2. V hernom obraze je terén štandardne **neviditeľný**, ale geometricky aktívny. Debugovacie zapnutie terénnej siete, súradníc, bodov, kalibračných značiek a kolíznych oblastí musí byť možné pri testovaní.
3. Potrebná efektívna mobilná GPU implementácia. Hra má pracovať približne do vzdialenosti **500 m** bez spracúvania miliónov bodov v každom snímku. Použiť predbežne **dlaždice, LOD, odrezávanie neviditeľných oblastí, cache a asynchrónne načítanie**; presné technológie má určiť architekt.
4. **Ilustračný, nezáväzný LOD:** do 30 m ~1 m, od 30–150 m ~4 m, od 150–500 m ~16 m. Je potrebné porovnať vizuálne/kolízne chyby, najmä pri ostrých zmenách reliéfu; kolízna geometria nemusí byť vždy totožná s grafickou LOD geometriou.
5. **Georeferencia:** dôsledne riešiť transformácie medzi súradnicovým systémom DMR/Terrain3D, GNSS/WGS84, lokálnym AR svetom a serverom; výšku antény/telefónu nad zemou, vertikálne referenčné systémy, presnosť a akumuláciu odchýlky.
6. DMR je **model terénu**, nie úplný model budov, vegetácie, múrov či otvorených jám; nepredstierať dokonalú oklúziu ani úplnú bezpečnostnú mapu.

## 3. Automatická diagnostika zariadenia — prvý krok aplikácie

Pri štarte aplikácie spustiť *device capability discovery*:

- Android verzia, model, dostupný CPU/GPU, RAM, dostupné grafické rozhrania a možnosti GPU, prípadné tepelné/výkonové limity;
- rozlíšenie displeja, obnovovacia frekvencia, hustota pixelov a orientácia;
- dostupné kamery a aktuálne **kalibračné parametre kamery** (intrinsics, zorný uhol, režim a rozlíšenie); nie iba nominálne marketingové údaje o optike;
- dostupnosť ARCore a relevantných schopností trackingu/senzorov;
- nastaviť kvalitu renderovania, rozlíšenie, LOD, dohľadnosť a dostupnosť náročných funkcií automaticky, s možnosťou neskoršieho doladenia.

Cieľom je plynulá prevádzka aj na slabších mobiloch; presné limity výkonu, FPS a batérie určí až testovanie. Ak zariadenie nesplní minimum, aplikácia má jasne uviesť obmedzenie.

## 4. Kalibrácia spoločného AR sveta — aktuálne rozhodnutie

**Aktuálne záväzné rozhodnutie vlastníka:** Prvá verzia sa **nekalibruje podľa budov, kostolov ani iných prírodných orientačných bodov**. Použijú sa **dva fyzické kalibračné terčíky/tabuľky/tyčky**, osadené dočasne alebo pevne v teréne. Obaja hráči budú využívať **tú istú dvojicu**. Uvažuje sa aj o výrobe a predaji vlastných kalibračných značiek.

### Asistovaný kalibračný postup v mobilnej aplikácii

1. Zakladateľ v administrácii vyvolá funkciu **Kalibrovať**. Aplikácia vyzve na umiestnenie **prvého terčíka**. Hráč terčík osadí, mobilom zaznamená jeho GPS polohu a čaká na ustálenie aktuálne dostupného odhadu presnosti. Príkladom je ~3 m, nie tvrdo stanovená požiadavka.
2. Aplikácia vyzve na osadenie **druhého terčíka** ďalej v teréne (orientačne **najmenej 20 m**; odporúčaná základňa sa môže adaptovať podľa nameranej neistoty GPS). Znova zaznamená polohu a jej odhadovanú presnosť po stabilizácii.
3. Hráč sa presunie na **tretie pozorovacie stanovište**, približne do konfigurácie trojuholníka s oboma terčíkmi. Tretí bod je **miesto s mobilom, nie tretí terčík**. Kamera musí vidieť a rozpoznať oba kalibračné terčíky.
4. Počítačové videnie (predbežne **AprilTag alebo ArUco**, nie OCR) so známymi rozmermi značiek, aktuálnou kalibráciou kamery a ARCore/VIO určí relatívnu polohu, orientáciu a mierku. Vypočíta sa transformácia do spoločného terénneho sveta, s explicitným odhadom neistoty.
5. Druhý hráč kalibruje svoj telefón **voči tým istým dvom značkám**. Server udržiava spoločný referenčný rámec. Obaja musia dosiahnuť prijateľnú kvalitu lokalizácie pred začatím súboja.
6. Postup má byť v aplikácii vedený jednoduchými pokynmi, kontrolou čitateľnosti značiek a spätnou väzbou o výslednej dôvere v kalibráciu.

**Technické overenie je kritické:** GPS poloha terčíkov s neistotou niekoľko metrov nedáva centimetrickú absolútnu georeferenciu. Vizuálne značky môžu zlepšiť relatívne zarovnanie oboch mobilov, ale samy neodstránia spoločnú globálnu chybu polohy. Potrebné sú terénne experimenty s objektívnym meraním chyby, vrátane chyby výšky a natočenia.

## 5. Kontinuálne určovanie polohy a strata trackingu

- Zlučovať podľa dostupnosti GNSS, ARCore vizuálno-inerciálne sledovanie (kamera + gyroskop + akcelerometer), známe pevné terčíky a **vizuálne pozorovanie druhého hráča** ako voliteľný doplnkový zdroj.
- Keď jeden mobil vidí postavu druhého, systém môže porovnať zistený smer/polohu v obraze s telemetriou druhej strany a zlepšiť relatívny odhad. Je potrebné bezpečné spárovanie správnej identity, odhad hĺbky alebo triangulácia z viacerých pozorovaní; samotná detekcia postavy nestačí na presnú 3D pozíciu.
- Gyroskop a akcelerometer neposkytujú dlhodobo presnú absolútnu polohu; integrovaný odhad má drift. Hodnotiť neistotu a robustnosť fúzie senzorov.
- Pri zhoršení presnosti sa aplikácia najprv pokúsi **automaticky relokalizovať** a spresniť tracking. Ak presnosť prestane byť dostatočná pre férovú/bezpečnú hru, **centrálny server pozastaví súboj pre oboch** a zobrazí im dôvod (strata polohy, nutnosť opätovnej kalibrácie, výpadok siete alebo odpojenie druhého hráča).
- Pokračovanie len po obnovení spojenia a akceptovateľnej lokalizácie. Prahy, časové limity a UX musia byť otestované a konfigurovateľné, nie zafixované bez dát.

## 6. Multiplayer, viditeľnosť a server

- Centrálna serverová autorita prijíma od dvoch mobilov priebežne polohu, neistotu merania, stav trackingu a relevantné herné udalosti. Server koordinuje stav hry, čas, zásahy, životy, kúzla a bezpečnostné pravidlá.
- Podľa digitálneho terénu vie určiť **geometrickú dohľadnosť** dvoch bodov (napr. súper za kopcom); prvé terénne testy sa uskutočnia s hráčmi v priamej viditeľnosti.
- Nie je garantovaná **skutočná viditeľnosť** cez budovy, stromy a iné objekty neobsiahnuté v DMR. Jasne oddeliť serverový výpočet z DMR od prípadného potvrdenia počítačovým videním.
- Hráči môžu byť vzdialení aj ďaleko od seba. Neskorší modul kúziel môže odhaliť súperov za kopcom, mimo pôvodnej geometrickej viditeľnosti.
- Synchronizáciu renderovania riešiť rozdielne od autoritatívnych rozhodnutí: mobil má plynulo interpolovať/predikovať vizuál, zatiaľ čo výsledky zásahov musí potvrdiť server. Zohľadniť latenciu, desynchronizáciu, časové pečiatky a výpadky spojenia.
- Bez servera **žiadna hra**. Online pripojenie je potrebné pri štarte aj počas duelov.

## 7. Štart súboja: fyzické stretnutie a QR

Rituál podobný historickému duelu:

1. Dvaja hráči sa stretnú, umiestnia a spoločne použijú kalibračné značky.
2. Zakladateľ otvorí Android aplikáciu, prípadne vstúpi do administrátorskej konfigurácie, vyberie pravidlá/preset a vytvorí hernú miestnosť.
3. Aplikácia zobrazí **QR kód**, ktorý druhý hráč naskenuje svojím mobilom. Oba mobily sa pripoja na jednu serverovú reláciu; bez matchmakingu.
4. Hráči sa potom fyzicky vzdialia (obrazné „odkrokovanie“ pred duelom) a pripravia sa na súboj. Príklad 30 krokov je metafora, **nie potvrdená pevná vzdialenosť**.
5. Presný spôsob štartového signálu, kontroly pripravenosti a časovania určí budúci návrh.

## 8. Kúzla, zameriavanie, let a zásahy

### Modulárnosť kúziel

- Každé kúzlo je samostatne konfigurovateľná funkcionalita. Oddeliť **spúšťač kúzla**, **zameriavanie**, **simuláciu dráhy a kolízií**, **účinky** a **vizuálne/akustické efekty**.
- MVP spúšťanie tlačidlom; ďalšie pluginy môžu pridať hlas, gestá či iné vstupy.
- Zameriavač zobrazuje smer kúzla. Pri mierení sa na reálnej zemi v AR ukáže **predpokladaná dopadová zóna**, napr. magický kruh, vypočítaná z prieniku trajektórie so skrytým DMR modelom. Pri pohybe kamery sa prepočíta projekcia.

### Fireball — prvý prototyp

- Jeden typ projektilu **Fireball** s vlastnou rýchlosťou, dráhou, vizuálnym efektom a oblasťou plošného účinku (**AoE**).
- Predbežná ilustračná rýchlosť **10 m/s**, uvažovaná vzdialenosť **200 m**; pri takejto dráhe čas letu ~20 s. Parametre **nie sú pevne určené**, majú byť nastaviteľné.
- Fireball letí **po priamej línii**, bez balistického poklesu/gravitácie; je to mágia.
- Po vystrelení dostane 3D svetovú polohu, smer a čas. **Nie je prilepený k displeju ani ku kamere.** Z perspektívy hráča sa pri vzďaľovaní javí menší a pri bočnom pohybe hráča sa zobrazuje z nového uhla, no nemení svoju trajektóriu.
- Let končí platnou kolíziou (terén, hráč alebo iná podmienka pravidiel). Na dopade vzniká AoE s konfigurovateľným polomerom; ak sa v jej oblasti podľa serverového výpočtu nachádza súper, nastáva herný účinok.
- Uvažovať neistotu polohy, čas dopadu a pravidlá zásahu pri hraniciach oblasti; predchádzať nefér výsledkom pre chybu GPS.

### Životy a nastavenia

- Hra má **systém životov (HP)**, nie automatické ukončenie po jedinom zásahu.
- V mobilnej administrácii bude nastaviteľný počet HP, poškodenie kúziel, dostupné kúzla, rýchlosť, rádius AoE a ďalšie parametre; server ich počas duelu vynucuje.

## 9. Bezpečnosť hráčov a terénna inteligencia

- Každý hráč má abstraktnú **bezpečnostnú auru**, ktorá nemusí byť viditeľná. Pri priblížení na nastavený prah systém oboch varuje; pri kolízii aúr nastane definovaný **herný** efekt (napr. smrť oboch postáv/výbuch), aby sa hráči nepribližovali do kontaktnej vzdialenosti.
- **Ilustračné** varovanie okolo 20 m. Presné hranice upozornenia, kolízie, vizuály a prípadné sankcie **nastaviteľné v administrácii**; nefixovať ich teraz.
- Implementovať základnú **terénnu bezpečnostnú inteligenciu**, ktorá z DMR a polohy upozorňuje na extrémne svahy, zrázy a prudké výškové rozdiely (napr. 30 m pád). Používateľ nechce úrazy v zápale hry.
- **Kritická výhrada:** DMR5 nevidí všetky skaly, jamy, vegetáciu, prekážky ani aktuálny stav povrchu; GPS môže mať metrovú chybu. Bezpečnostné upozornenia sú len doplnkové, **nie záruka bezpečnosti**. Architekt musí navrhnúť bezpečné UI a režim pozastavenia pri nedôveryhodnej polohe; zohľadniť, že hráč sa pri pohľade do mobilu pohybuje skutočným terénom.

## 10. Administrácia, presety a používateľské rozhranie

- **Administrácia priamo v Android aplikácii**, nie povinný webový admin. Zakladateľ súboja zadá **administrátorské heslo**, nastaví pravidlá na svojom telefóne a druhý hráč sa pridá cez QR.
- Zadanie administrátorského hesla má byť **jednorazové a zapamätané** vhodným bezpečným spôsobom; nie nutnosť písať znova pri každej hre.
- **Presety a konfigurácie súbojov sa uchovávajú na centrálnom serveri** v lokálnej databáze. Zakladateľ si môže znovu vyvolať posledné nastavenie aj históriu viacerých presetov, takže nezačína od nuly.
- Presety stačí identifikovať **dátumom/časom**, bez povinných vlastných názvov. Zobrazuje ich zakladateľ na svojom mobile; druhý hráč nepotrebuje vlastný zoznam.
- Zatiaľ bez limitu počtu presetov, bez exportu do súboru, bez tlačidla na hromadné mazanie všetkých presetov.
- V prvej verzii iba AR pohľad z kamery; **minimapa** je predvídaný samostatný modul, **nevyvíja sa teraz**.

## 11. Modulárna architektúra a open-source pluginový ekosystém

**Toto je základný, nie voliteľný princíp projektu.** Cieľom je vytvoriť komunitne rozšíriteľnú hernú platformu, nie monolitickú hru:

- **Mobilné koordinačné jadro** riadi lokálne moduly, ich životný cyklus, prenos relevantných dát a spojenie so serverom. Samo nemusí vykresľovať ani implementovať kúzla.
- **Serverové jadro** koordinuje relácie, synchronizáciu, pravidlá, stav sveta, životy a autoritatívne kolízie.
- Moduly pre terén, AR/kameru, lokalizáciu/kalibráciu, hráčov, kúzla, spúšťače, zameriavanie, vizuál, bezpečnostné funkcie, logovanie, správu presetov atď. majú mať jasne oddelené zodpovednosti.
- **Verzované stabilné API a dátové kontrakty**: externe implementovateľné a testovateľné, s minimálnou latenciou. Grafický modul dostáva pozíciu, smer, čas a typ kúzla; môže úplne zmeniť vzhľad bez zmeny gameplay pravidiel.
- **Predbežný návrh, ktorý ešte nie je záväzný:** rýchle typované lokálne API v hernom engine; Protobuf pre klient/server; JSON manifest s názvom, verziou, kompatibilitou a schopnosťami pluginu. Hlavný architekt môže zvoliť lepšiu alternatívu.
- Dôsledne oddeliť **dôveryhodné serverové pravidlá** od neautorizovaných vizuálnych pluginov; riešiť kompatibilitu, verzie, oprávnenia, bezpečné spúšťanie a platformové pravidlá Androidu.
- **Povinný PoC test skutočnej vymeniteľnosti:** dve nezávisle vyvinuté implementácie modulu pre tú istú funkcionalitu (môžu ich vytvoriť rôzni AI agenti/prostredia), prepnutie medzi nimi bez úpravy jadra, automatické kontraktové testy **aj živé terénne testy**.
- **Budúca komunita:** verejný open-source GitHub repozitár; vývojári publikujú pluginy, dokumentujú ich a umožňujú ostatným skúšať/vylepšovať ich. Používateľ si na webe hry vyberie kompatibilnú kombináciu a stiahne **vlastnú zostavu Android aplikácie** s týmito modulmi. Webový katalóg, build služba a sociálne funkcie **nepatria do prvého PoC**, ale návrh ich nemá vylučovať.
- **Prvá zostava má fixný schválený súbor modulov**, hoci ich technicky musí byť možné vymieňať v rámci projektu.

## 12. Logovanie, debugging, súkromie

- **Logovací modul musí byť samostatný a vymeniteľný.** Rozhranie má podporovať **(A) hromadné odoslanie po skončení súboja** a **(B) priebežné streamovanie počas súboja**.
- **Prvá implementácia používa iba režim A:** počas hry zbiera logy lokálne, **odosiela ich až po skončení súboja**; priebežný prenos zostáva možným budúcim modulom.
- Logovať technické udalosti, kalibráciu, stav presnosti a tracking, chyby siete, pauzy, zásahy a **polohy oboch hráčov**, keďže cieľom je debugging a analýza.
- Logy sa ukladajú/prehliadajú cez **serverový výpis**, bez obrazovky logov v mobilnej aplikácii. Vlastník zatiaľ nepožaduje limit na počet či históriu logov.
- Pri páde aplikácie/odpojení je potrebné bufferovanie a bezpečné opätovné odoslanie, aby sa diagnostika nezahodila.
- **Významná položka pre architektonickú oponentúru:** presné geolokačné záznamy sú osobné údaje; neobmedzená retencia a logovanie bez informovania hráčov vytvárajú právne a bezpečnostné riziká. Vlastník v rozhovore nechcel riešiť používateľské upozornenie v PoC; architekt má navrhnúť zákonné a primerané minimum (transparentnosť, prístupové práva, zabezpečenie, retenčná politika), nie automaticky predpokladať, že neoznámené bezlimitné uchovávanie je vhodné pre verejnú prevádzku.

## 13. Prvé hostovanie a databáza

- Všetky serverové služby sa v prvej fáze prevádzkujú **na vlastnom domácom notebooku ASUS ROG**, nie na verejnom cloud serveri.
- Notebook má podľa slov používateľa **pravdepodobne 16 GB RAM**; presné parametre CPU/GPU/RAM/disku treba ešte overiť, nepredpokladať ako fakt.
- Použiť **Docker kontajner(y)**, lokálny **PostgreSQL alebo podobnú DB**, pracovníka/workera na spracovanie úloh a nevyhnutné API/relé služby. **Žiadna externá spravovaná databáza**.
- V rozhovore bol uvedený **Cloudflare Tunnel** ako možný spôsob bezpečného prístupu cez internet; **zatiaľ len predbežná možnosť**, architekt má overiť prevádzkové vhodnosti, limity, websocket/UDP/latenciu a autentifikáciu.
- Všetko musí zostať funkčné pri bežnom internetovom pripojení oboch Android telefónov k domácemu serveru; bez neho sa duel nehrá.
- Cloudové hostovanie sa plánuje **až pre fázu alfa**. Riešenie má uľahčiť neskoršiu migráciu dát/služieb bez duplikácie celého Terrain3D datasetu.

## 14. Vývoj, AI agenti a rozhodovacie brány

- Projekt bude mať verejný **GitHub open-source repozitár**. **Codex** a potenciálne **Claude Code** budú pracovať nad tým istým repozitárom; architekt má navrhnúť bezpečný proces vetvenia, PR, code review, CI a prevencie konfliktov medzi agentmi.
- Cieľ je **autonómny multiagentový vývoj** na zrýchlenie realizácie. Hlavný koordinátor má byť podľa želania vlastníka čo najsilnejší dostupný model (**Astra / najvyššie uvažovanie**); podľa potreby sám prideľuje úlohy podriadeným agentom, vyberá ich počet, špecializácie a úroveň uvažovania.
- Vlastník **nechce vopred obmedzovať počet agentov**. Architekt musí rozlíšiť tento zámer od reálnych súbehov, kvót, nákladov, prístupových práv a kapacity 16 GB notebooku; odporučiť praktické riadenie limitov, nie sľub neobmedzenej realizácie.
- Majiteľ chce rozhodovať o **funkcionalite, architektúre a akceptácii správania**, nie osobne analyzovať zdrojový kód. Technické kontroly a testy majú robiť AI agenti a automatizácia.
- **Poradie je záväzné:** (1) konsolidovať tento dokument; (2) architektonický návrh a kritická oponentúra silným modelom; (3) **human gate — vlastník osobne schváli architektúru**; (4) až potom detailná roadmapa, implementačné úlohy, multiagentová orchestrácia, iterácie a ďalšie brány pri rizikových rozhodnutiach.
- **Tento dokument nie je pokyn na kódovanie, založenie repozitára, nasadenie alebo zmenu existujúceho Terrain3D.**

## 15. Otvorené otázky a experimenty pre architekta

Namiesto ďalšieho zahlcovania používateľa drobnými otázkami spracovať technickú oponentúru týchto bodov:

1. **Výber enginu:** Unity + AR Foundation/ARCore, Godot alebo iná technológia — kompatibilita s Androidom, GPU výkonom, natívnou kamerou, rozšíreniami, licenčnými podmienkami open-source projektu a budúcim skladaním APK z pluginov. Pozor na limity dynamického kódu na Android/Google Play a bezpečnosť pluginov.
2. **Presnosť georeferencie:** čo reálne dosiahneme s dvoma GPS-osadenými terčíkmi a tretím pozorovacím stanovišťom? Navrhnúť merateľné testy relatívnej/absolútnej chyby, kalibrácie výšky, stability po 20–500 m a rôznych mobiloch.
3. **Jednotná súradnicová sústava:** presné transformácie DMR–GPS–AR, lokálny floating origin, vertikálny datum a robustnosť voči GPS posunom.
4. **Efektívny terrain streaming:** existujúca Terrain3D pipeline, potrebné API, tile/mesh cache, kompromisy LOD a presnosti kolízií, nulová duplicita 200 GB dát.
5. **Férovosť a kolízie:** serverové AoE pri metrových odchýlkach polohy, časové synchronizovanie, latency compensation a anti-cheat v hre bez používateľských účtov.
6. **Herná bezpečnosť:** úskalia DMR a AR pri fyzickom pohybe, identifikácia nebezpečných svahov, bezpečnostná aura, prednostné pauzovanie pri neistote; neurčovať falošný pocit bezpečnosti.
7. **Modulová hranica a kontrakty:** ktoré časti budú binárne/pluginové, ktoré zostavené do APK, ktoré iba konfigurované? Rozhrania, API versioning, manifest, bezpečnostný model, test dvoch zameniteľných implementácií.
8. **Server a sieť:** protokol, správa relácií, QR párovanie, vlastná lokálna databáza, observabilita a prístup z internetu na domáci notebook.
9. **Logy a súkromie:** lokálny buffer, upload po zápase, legálne spracúvanie presných lokalizačných dát, vhodná retencia a prístupová kontrola.
10. **Vývojový proces:** rozdeliť fázy PoC, alfa, beta; rizikové human gates; simulátor bez fyzických hráčov; device test matrix; CI; práca viacerých AI agentov cez GitHub.

## 16. Požadovaný výstup architektonickej oponentúry (pre ďalší AI model)

**Úloha pre architekta:** Nezačínaj implementovať. Skontroluj úplnosť, konzistenciu a technickú uskutočniteľnosť tejto špecifikácie. Identifikuj rozpory a kritické predpoklady; čo možno odvodiť, uveď ako vlastný návrh, nie ako rozhodnutie majiteľa.

Požadujeme:

- 2–3 **realistické architektonické alternatívy** s výberom odporúčanej a odôvodnením;
- komponentový diagram klient/server, zodpovednosti jadra a pluginov, dátové kontrakty a interakčné toky;
- detailnú schému kalibrácie a spoločného AR sveta vrátane **realistickej presnosti a overiteľných experimentov**;
- návrh serverovej autority, telemetrie, protokolu, výkonu a synchronizácie pri dvoch hráčoch;
- návrh prístupov k existujúcemu Terrain3D DMR dátovému úložisku bez plošného kopírovania;
- technické a právne riziká, licencie, bezpečnosť modulov a konzervatívny návrh bezpečnosti ľudí v teréne;
- PoC akceptačné kritériá vrátane **výmeny dvoch nezávislých pluginov bez úpravy jadra** a testov na dvoch reálnych Androide zariadeniach;
- návrh **human gates** s otázkami, ktoré naozaj vyžadujú rozhodnutie vlastníka;
- **návrh fáz a závislostí na schválenie**, ale nie ešte konečnú implementačnú roadmapu ani autonómne spustenie Codex/Claude Code.

**Rozhodovací princíp:** Vlastník najprv osobne schváli architektúru; až potom samostatne poverí AI vytvorením roadmapy a vývojom.

---

### Zhrnutie stavu rozhodnutí

**Schválené používateľom v rozhovore:** Android-only, online-only, dvaja hráči, bez účtov, QR párovanie, zdieľané dva fyzické terčíky a asistované osadenie podľa vlastnej GPS, využitie existujúceho Terrain3D DMR, neviditeľný terén, Fireball v AR, HP, AoE, bezpečnostná aura a terénne varovania, admin priamo v mobile, presety na serveri, vymeniteľné pluginy od prvého PoC, logovací modul po skončení súboja, server najprv doma s Docker a vlastnou DB, neskôr cloud, komunitné pluginy neskôr.

**Predbežné technické varianty, ešte neschválené:** Unity/AR Foundation, AprilTag/ArUco, konkrétna LOD krivka, Protobuf, JSON manifest, Cloudflare Tunnel, konkrétne výkonnostné a presnostné limity.

**Neuzavreté, zámerne odložené:** presná rýchlosť a dosah Fireballu, prahy aúr, HP a sila kúziel, konkrétny typ enginu, dátové a pluginové protokoly, UI detaily, bezpečnosť a retenčné politiky logov, presná konfigurácia notebooku, obchodný/licenčný model vlastných kalibračných tabuliek.
