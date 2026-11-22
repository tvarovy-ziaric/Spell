# AR hra VibeKing — architektúra a realizačná roadmapa pre Codex

**Verzia 0.3 · 22. november 2026 · Realizačný plán pre vlastníka a vývojový tím**

Prvým výsledkom bude online duel dvoch hráčov, ktorí sa pohybujú pešo na jednom vopred vymedzenom ihrisku. Pred súbojom si oba Androidy načítajú celé ihrisko do pamäte a zosúladia sa pomocou dvojice fyzických terčíkov. Počas hry mobil okamžite reaguje na pohyb, domáci server rozhoduje o priebehu duelu.

Táto revízia zapracúva tri rozhodnutia vlastníka do špecifikácie v0.2 a následného návrhu architektúry. Vzťah k iným aplikáciám, význam frekvencie 10 Hz a spôsob načítania terénu sa pri realizácii riadia týmto dokumentom. Číselné ciele mobilného výkonu a priestorovej presnosti sú návrhy na meranie. [Pôvodná špecifikácia v0.2](https://docs.google.com/document/d/1Eu81ydmYc_7jm009_7Jzzb_zqofi578h/edit?usp=drivesdk) · [Oponentúra a východiskové merania](https://docs.google.com/document/d/1t2lLwvXR49C_8H5YS7g88WK7_p084oCq/edit?usp=drivesdk)

## 1. Tri pravidlá pre implementáciu

| Rozhodnutie | Čo z neho vyplýva | Ako ho overíme |
| --- | --- | --- |
| **VibeKing vyvíja samostatnú hru.** Jediným zdieľaným projektovým vstupom s Terrain3D a AR Navigatorom sú zdrojové dáta DMR. | Hra vlastní Android aplikáciu, kód na prípravu ihriska, server, databázu, kontrakty, zostavenie a konfiguráciu. K zdrojovým súborom DMR pristupuje vlastným čítaním iba na čítanie. | Z čistého herného repozitára sa zostaví klient a spustí server. Príprava ihriska a skúšobný duel prejdú na izolovanom hostiteľovi s pripojeným DMR. Testové prostredie má aplikácie, API, databázy aj repozitáre ostatných projektov nedostupné. |
| **Približne 10 Hz je frekvencia odosielania odhadu polohy a orientácie.** | GNSS vstupuje pri skutočnom príchode merania. Medzitým odhad aktualizuje vizuálno-inerciálne sledovanie, ktoré kombinuje kameru, akcelerometer a gyroskop. Pevné značky umožňujú vizuálnu opravu driftu. | Pri pomalom, nepravidelnom a na čas úplne vynechanom GNSS sa osobitne vykáže počet nových GNSS meraní, počet odoslaných odhadov a ich neistota. Staré meranie si zachová pôvodný čas. |
| **Celé ihrisko sa načíta pred začiatkom.** | Server aj oba telefóny majú rovnakú verziu povrchu v RAM. Pohyb, otočenie, Fireball a vyhodnotenie dohľadnosti pracujú s týmto povrchom. Herné spojenie ostáva online. | Po stave „hrá sa“ zablokovať prístup k výdaju terénnych súborov a nechať herné spojenie otvorené. Obaja hráči prejdú ihrisko, otočia sa a dokončia duel. Počítadlá musia ukázať **0 požiadaviek na terén a 0 dodatočne prijatých bajtov terénu**. |

## 2. Architektúra hry

| Komponent vo vlastníctve hry | Úloha |
| --- | --- |
| **Android klient** | Unity 6.3 LTS, AR Foundation 6.3.5 a ARCore XR Plugin 6.3.5 ako východisková zostava. Jedna AR relácia vlastní kameru. Klient vykresľuje aktuálny pohľad, spracúva merania, drží celé ihrisko, prijíma ovládanie a zobrazuje odhad dopadu. Pri štarte overí podporu ARCore, oprávnenia, aktívnu kameru, senzory a voliteľnú hĺbku. |
| **Server duelu** | Jeden proces C#/.NET na ASUS ROG. Vlastní členstvo, konfiguráciu zápasu, hodiny, stav hráčov, kúzla, životy, auru, pauzu a obnovu. Východiskom je 20 krokov simulácie za sekundu. Herná geometria je počas duelu v RAM. |
| **Spoločná knižnica geometrie** | Malá knižnica C# používaná výhradne klientom a serverom tejto hry. Zhodne interpretuje polohu a výšku vzoriek, masku platnosti, hranicu ihriska, diagonálu bunky, prienik s povrchom a trajektóriu virtuálneho kúzla. Rovnaké vstupy sa testujú na oboch platformách. |
| **Príprava balíka ihriska** | Predštartová úloha hry získa hranicu vybraného ihriska, pridá okraj potrebný na výpočty a prečíta iba príslušné okno zdrojového DMR5. Navrhovaná lokálna implementácia používa GDAL/Rasterio a PROJ. Výstupom je verzovaný binárny balík. Táto úloha môže dobehnúť pred zápasom alebo pripraviť balík na opakované použitie. |
| **Sieť a úložisko** | Vlastné HTTPS rozhranie hry slúži na párovanie, načítanie balíka pred hrou, presety a diagnostické dávky. Zabezpečený WebSocket nesie herné udalosti a odhady polohy. Domáci PostgreSQL uchováva oprávnenia zakladateľov, históriu presetov a evidenciu prijatých dávok. Domáce služby pobežia v Dockeri, prístup zvonku cez Cloudflare Tunnel. |

### Balík ihriska a podmienka štartu

Zakladateľ na mieste určí hranicu ihriska a potvrdí, že si plochu prešiel. Po získaní referencie pre arénu server vyberie zodpovedajúci georeferencovaný výrez. **Hranica hry a okraj dát majú odlišnú funkciu.** Okraj chráni výpočet blízko hranice. Pri priblížení hráča k hranici príde výstraha, pri jej prekročení alebo neistej polohe spoločná pauza. Let virtuálneho kúzla skončí pri dopade, na maximálnom dolete alebo na hranici povolenej plochy podľa toho, čo nastane skôr.

Manifest balíka nesie ID a verziu datasetu, súradnicový a výškový systém, počiatok a krok mriežky, rozmery, hranicu ihriska, masku NoData, verziu geometrie a kontrolný súčet obsahu SHA-256. Pre herné rozhodnutia sa použije primárna 1 m mriežka. Zjednodušené zobrazenie na slabšom mobile zachová rovnaký kolízny povrch. Pre predstavu má mriežka 201 × 201 vzoriek Float32 s jednobitovou maskou približne **163 KiB** pred hlavičkou, kompresiou a pamäťou vykresľovania. Skutočnú špičku RAM pri rozbalení aj počas hry zmeriame na oboch telefónoch.

Každý mobil overí úplnosť, kontrolný súčet a dostupnú pamäť, načíta povrch do RAM a potvrdí pripravenosť. Server otvorí duel po zhode **oboch potvrdení s jeho vlastným balíkom, revíziou arény a presetom**. Poškodený prenos sa opakuje pri príprave. Neplatné údaje v potrebnej oblasti alebo nedostatok RAM vedú k výberu menšieho či iného ihriska pred štartom. Pri strate RAM po ukončení procesu sa obnovenie rovnakého balíka vykoná v pozastavenej relácii pred pokračovaním.

Herné moduly dostanú úzke lokálne rozhranie na dotaz nad terénom. Jeho jediná implementácia pre tento PoC číta balík z pamäte. Budúci turistický režim môže doplniť iný spôsob dodávania povrchu. Streaming, prednačítavanie ďalšieho územia počas hry a turistický režim sú odložené.

### Kalibrácia, poloha a čas

Každý telefón sa nezávisle zameria na **dva pevné AprilTag terčíky** so známym rozmerom, identitou a výškou stredu nad zemou. Tretie pozorovacie stanovište je miesto pre hráča. Značky definujú spoločný lokálny priestor v metroch, gravitácia určuje zvislicu. GNSS merania pri značkách pridajú georeferenciu s vlastným intervalom neistoty. Server rozlišuje chybu medzi telefónmi a absolútnu chybu arény voči DMR. Zdrojový raster má polohu v JTSK03 (EPSG:8353) a výšku v Bpv (EPSG:8357). Pri staršej kontrole skúšanému prostrediu PROJ chýbala výšková mriežka DVRM05. Herné prostredie musí mať overený prevod z výšky GNSS cez príslušný referenčný rámec do Bpv a nezávislý bod so známou výškou. [Slovenské transformačné mriežky](https://cdn.proj.org/sk_gku_README.txt)

**Čas merania a čas prenosu.** GNSS hlási fixy podľa dostupnosti a možností konkrétneho telefónu. ARCore priebežne odhaduje relatívny pohyb z kamery a IMU. Modul fúzie prijíma tieto vstupy a vizuálne opravy markerov. Približne každých 100 ms odošle najlepší dostupný odhad alebo stav nedostatočnej kvality. Vykresľovanie beží v rytme displeja, serverová simulácia vlastným taktom. Údaje tej istej IMU spracované v ARCore sa započítavajú iba raz. [ARCore a vizuálno-inerciálne sledovanie](https://developers.google.com/ar/develop/fundamentals)

Správa o polohe obsahuje poradové číslo, revíziu arény, čas odhadu, čas posledného skutočného GNSS fixu a posledného vizuálneho merania, stav trackingu a odhad polohovej a uhlovej neistoty. Mobil zaznamenáva časy zdrojov a prevádza ich do overenej lokálnej monotónnej časovej základne. Server odhaduje posun hodín každého telefónu osobitne. Pri zdravom vizuálnom sledovaní môže hráč pokračovať medzi fixmi GNSS. Pri strate opory rastie neistota. Po prekročení dohodnutej hranice sa duel pozastaví obom. [Čas merania Android Location](https://developer.android.com/reference/android/location/Location#getElapsedRealtimeNanos()) · [Čas kamerovej snímky ARCore](https://developers.google.com/ar/reference/java/com/google/ar/core/Frame#getTimestamp())

Vymeniteľný vizuálny modul hlási aj pozorovanie súhlasiaceho protihráča. Výstup zahŕňa smer, čas snímky, kvalitu a potvrdenie identity. Pri prekrytí alebo vstupe tretej osoby sa sporná asociácia zruší. Bežná detekcia postavy sama neposkytuje spoľahlivú vzdialenosť. Prípadnú korekciu polohy podľa pozorovania účastníka možno zapnúť po porovnaní s nezávislým meraním. Video zostáva v telefóne a pri bežnom dueli sa neukladá.

### Priebeh online duelu

Zakladateľ na mobile otvorí administráciu, vyberie preset a ukáže jednorazový QR kód. Hosť vstúpi bez účtu. Server uzamkne pravidlá. Po načítaní ihriska a kalibrácii oboch účastníkov dovolí začať. Na stlačenie tlačidla reaguje telefón okamžite. Server potvrdí virtuálny Fireball, vyhodnotí priamy let, terénny dopad, plošný účinok a HP. Sporný zásah zachová HP. Pri neistej blízkosti hráčov má prednosť výstraha a pauza. DMR poskytuje informáciu o reliéfe, bezpečnosť pešieho pohybu dopĺňa obhliadka miesta a prerušenie súboja pri riziku.

Pri strate spojenia mobil lokálne zablokuje akcie a server zmrazí herný čas i letiace kúzlo. Pri návrate porovná účastníkov, balík a revíziu arény a obom pošle rovnaký stav. Pri obnovenej pôvodnej aréne let pokračuje. Po nutnej zmene jej definície rozbehnuté kúzlo skončí bez poškodenia. Opakovaná požiadavka s rovnakým ID má jediný účinok.

## 3. Ako bude pracovať Codex

Koordinátor vlastní poradie závislostí, verejné kontrakty, integračnú vetvu a výsledky skúšok. Podľa zadania je preferovaný model Astra s najvyššou dostupnou úrovňou uvažovania pre koordináciu. Počet agentov a pridelený výkon sa riadia pripravenou prácou, dostupnou kapacitou a tým, či agenti môžu upravovať oddelené časti. Roly nižšie označujú vlastníctvo práce. Jeden agent môže postupne prevziať viaceré roly.

| Rola | Výhradné pracovné územie v navrhovanom hernom repozitári |
| --- | --- |
| Koordinátor a integrátor | `contracts/`, integračná scéna Unity, zdieľané nastavenia balíkov a zostavenia. Spravuje verzie schém, testovacie vstupy ku kontraktom a rozhrania medzi modulmi. |
| Geodáta | `terrain/`. Vlastná príprava ihriska zo zdrojového rastra, transformácie, manifest a skúšky platnosti dát. |
| Geometria | `shared/geometry/`. Čisté C# výpočty a referenčné výsledky na rovnakých vzorkách pre Unity aj server. |
| Mobilná AR a herné zobrazenie | Oddelené moduly v `client-unity/` pre AR, lokalizáciu, načítanie balíka, ovládanie a grafické pluginy. Vlastník každého súbežného zadania dostane svoj adresár. |
| Server a správa | Oddelené moduly v `server/` pre reláciu, rozhodcu a správu. Jediný vlastník databázových migrácií na konkrétnu zmenu. |
| Testovanie a meranie | `qa/`. Nezávisle určené očakávané výsledky, prehrávanie zaznamenaných udalostí, meranie latencie, pamäte a sieťovej prevádzky. Vlastník s druhým testerom zabezpečí fyzické skúšky. |

Každé zadanie v GitHube dostane vstupnú revíziu kontraktu, adresáre na úpravu, závislosti, výstup a konkrétny akceptačný test. Agenti pracujú vo vlastných vetvách alebo worktree. Zmenu spoločného kontraktu integruje koordinátor spolu s referenčným príkladom a testami oboch konzumentov. Dlhú úlohu integruje po malých použiteľných zmenách. Prideľovanie ďalšej práce má prednosť tam, kde už existuje potrebný kontrakt alebo pripravený balík.

Automatické testy musia ísť spustiť lokálne aj bez platenej CI. Pri odovzdaní sa uvedie commit, príkaz, výsledok a pri fyzickej skúške zariadenie, verzia APK a podmienky. Testovací súhrn má samostatné stavy **overené**, **zlyhalo** a **čaká na fyzickú skúšku**. Vlastník tak môže rozhodnúť z ukážky a meraní aj bez čítania zdrojového kódu.

## 4. Etapy realizácie a ich závislosti

**Kritická cesta** vedie od spoločných kontraktov cez prípravu ihriska a lokalizáciu k prvému spoločnému objektu. Po jeho zmeraní nasleduje stavba úplného duelu, spoločná skúška a prevzatie vlastníkom.

### M0 — Samostatný základ a dohoda o dátach

Koordinátor a testovací agent vychádzajú zo špecifikácie a tejto revízie architektúry.

Pripraviť samostatný herný repozitár pod projektom VibeKing, podpísateľnú kostru Android aplikácie, domáce vývojové prostredie a lokálny testovací príkaz. Definovať formát balíka ihriska, rozhranie hernej geometrie, `PoseSample`, `VisualObservation`, potvrdenie pripravenosti a stavy relácie. Pre herné udalosti použiť verzované schémy Protobuf. Pripraviť drobný syntetický kopec s hranicou a medzerou NoData, stopu nepravidelného GNSS a ochranný test nezávislosti aplikácie.

**Hotovo, keď** klient aj server prečítajú rovnaký vzorový balík, rozpoznajú jeho verziu, odmietnu chybný súčet a lokálny test funguje z čistého herného repozitára. Koordinátor potom otvorí štyri súbežné zadania M1.

### M1 — Štyri paralelné zadania po M0

| Zadanie a vlastník | Výstup | Podmienka prijatia |
| --- | --- | --- |
| **1A · Balík ihriska a georeferencia** — geodáta | Herný nástroj pripraví z DMR jeden zvolený výrez, hranicu, okraj, platnosť a manifest. Zabalí vlastnú overenú konfiguráciu prevodu do JTSK03 a Bpv. | Opakovanie nad rovnakým vstupom dá rovnaký identifikátor obsahu. Kontrolný bod overí výšku aj poradie osí. Príprava funguje s ostatnými aplikáciami nedostupnými. |
| **1B · Mobilná lokalizácia** — AR | Diagnostika telefónu, identifikácia dvoch značiek, samostatná kalibrácia oboch mobilov, odhad z GNSS a ARCore, časové značky, neistota a debug zobrazenie. Základný vizuálny modul vie oznámiť smer a stratu potvrdenej stopy účastníka. | Pri prehratí GNSS napríklad raz za sekundu, potom s nepravidelnými pauzami, sa počty nových fixov zhodujú so vstupom. Odhad sa medzi nimi aktualizuje pohybom z ARCore. Pri strate opory dôvera klesá a staré snímky sa odmietnu. |
| **1C · Povrch v RAM** — geometria a mobilné načítanie v oddelených adresároch | Spoločné vzorkovanie a prienik s terénom, import celého balíka do Unity, debug sieť a počítadlá prípravy, RAM a prístupu k terénnym dátam. | Server aj Unity vrátia rovnaké referenčné výsledky na rovine, svahu, okraji a pri NoData. Zníženie grafického detailu nezmení kolíziu. Telefón potvrdí pripravenosť až po načítaní platného balíka. |
| **1D · Minimálna online relácia** — server | Dve dočasné roly, jednorazové QR, HTTPS na balík, WebSocket na herné správy, synchronizácia času, verzia arény a podmienka spoločného štartu. | Dva testovacie klienty sa pripoja. Pri odlišnom súčte, nedokončenom prenose, chýbajúcej kalibrácii či zlej kvalite server nepovolí štart. Po platných potvrdeniach vydá jediný začiatok relácie. |

### M2 — Prvý dôkaz s dvoma telefónmi

**Závisí od 1A až 1D.** Integrátor spojí prvý úplný skúšobný priebeh. Dva rôzne podporované Androidy sa spoja cez domácu reláciu, načítajú ihrisko, nezávisle sa nakalibrujú a zobrazia jeden virtuálny objekt pri známom bode. Obaja účastníci sa prejdú, otočia, vrátia k terčíkom a zopakujú skúšku pri nepravidelnom GNSS.

**Vlastník vyberie ďalší postup podľa výsledku.** Môže prijať nameranú kvalitu pre pilotný priestor, zmenšiť priestor alebo vrátiť chybu príslušnému agentovi. Ak sa rozchádzajú telefóny, rieši sa lokálna kalibrácia. Ak sa zhodujú navzájom, ale objekt je mimo známeho bodu na teréne, rieši sa georeferencia. Ak po štarte pribúdajú bajty terénu, opraví sa balík alebo jeho životný cyklus. Výstupom je krátka ukážka a tabuľka oboch chýb, odozvy a prenesených bajtov.

### M3 — Úplný duel po prijatí M2

| Súbežné zadanie | Výstup a vlastník | Podmienka prijatia |
| --- | --- | --- |
| **3A · Rozhodca** | Serverový agent implementuje priamy virtuálny Fireball, predikciu a autoritatívny dopad, plošný účinok, HP, auru, terénnu dohľadnosť, hranice ihriska, pauzu a obnovu. | Rovnaká udalosť zmení HP najviac raz. Neistý zásah zachová HP. Hrebeň, voľná spojnica a neznámy reliéf majú odlišné výsledky. Strata spojenia počas letu nezmení férovosť po obnove. |
| **3B · Ovládanie a výmenné moduly** | Mobilný agent rieši tlačidlo, zameriavač, predpokladaný dopad a stav neistoty. Dve nezávislé zadania vytvoria dva vykresľovače Fireballu proti jednému kontraktu, každý vo vlastnom module. | Obe implementácie sa prepnú v tom istom podpísanom APK bez úpravy jadra. Rovnaký záznam udalostí má v oboch prípadoch rovnaký let, čas dopadu a výsledné HP. |
| **3C · Administrácia v mobile** | Agent správy doplní serverové overenie hesla, zapamätané oprávnenie viazané na Android Keystore, stránkovanú históriu presetov v PostgreSQL a nemennú kópiu pravidiel pri štarte duelu. Preset určuje HP, rýchlosť a dolet kúzla, účinok a prahy v povolenom rozsahu. | Zakladateľ vyvolá starší preset. Druhý zakladateľ vidí vlastné presety, hosť vidí iba pravidlá svojho duelu. Opakované použitie alebo vypršanie QR sa korektne spracuje. |
| **3D · Žurnál a adaptácia výkonu** | Agent diagnostiky doplní vymeniteľný lokálny žurnál, dávkový prenos po skončení, obnovu po páde, prístup k diagnostike na serveri a meranie tepelnej záťaže. Mobilný klient prispôsobí nákladné vizuálne efekty a frekvenciu detektora. | Pád pred prenosom aj strata potvrdenia po prijatí dávky vedú k jednej uloženej kópii. Slabší telefón zachová rovnaký fyzikálny výsledok. V logu sa odlíši nový GNSS fix, vizuálne meranie a odoslaný odhad. |

### M4 — Integrovaná skúška na ihrisku

**Závisí od 3A až 3D.** Testovací agent pripraví opakovateľný scenár, vlastník a druhý tester ho vykonajú na slabšom aj výkonnejšom podporovanom telefóne. Zahŕňa normálny duel, chôdzu po ihrisku, zastavenie a otočenie, priblíženie k hranici, nejednoznačnú vizuálnu stopu, stratu trackingu, výpadok hernej siete počas letu a návrat aplikácie z pozadia. Nebezpečný reliéf sa preveruje na testovacom vstupe v bezpečnom prostredí. Jeden beh má vypnutú voliteľnú hĺbku.

**Hotovo, keď** existuje záznam podľa meracieho protokolu nižšie, výsledok zodpovedá na oboch mobiloch a pri chybách je určené, ktorá podmienka štartu, meranie alebo modul zlyhali. Oprava sa vracia do rovnakého scenára s pôvodnými podmienkami.

### M5 — Prevzatie PoC

**Závisí od M4.** Koordinátor predloží vlastníkovi podpísaný skúšobný APK, verzie balíkov a zdrojov, lokálny postup obnovy domáceho servera a krátky report s odchýlkami a otvorenými bodmi. Report musí uviesť zvlášť, čo sa overilo na oboch zariadeniach, pri akej veľkosti ihriska a čo je ešte experimentálne. Vlastník rozhodne o prijatí pilotu alebo o konkrétnej oprave.

## 5. Merací protokol a pravidlá prevzatia

Tri pravidlá z prvej kapitoly platia priamo zo zadania. Nasledujúce výkonnostné a presnostné prahy vychádzajú z oponentúry. Sú to ciele pre pilot, ktoré treba potvrdiť na zariadeniach. P95 je hranica, ktorú spĺňa 95 % meraní. Pri krátkom teste sa vedľa percentilu uvedie počet nezávislých behov a najhorší výsledok.

| Predmet skúšky | Scenár a požadovaný dôkaz |
| --- | --- |
| **Nezávislosť a pripravenosť ihriska** | Spustiť herný systém s jediným externým projektovým vstupom DMR. Pred duelom zaznamenať veľkosť balíka, čas prípravy, prenosu, overenia a maximálnu RAM každého mobilu. Vyskúšať chybný hash, prerušený prenos, medzeru v potrebných dátach aj telefón s nedostatočnou pamäťou. Každý prípad má skončiť zrozumiteľným výsledkom pred štartom. |
| **Terén počas hry** | Po potvrdení oboch klientov zavrieť výdaj balíka. Prejsť plochu aj jej hranicu, vykonať otočenie na mieste, zoslať kúzlo a prerušiť a obnoviť herné spojenie pri zachovanej RAM. Na oboch mobiloch je počet nových terénnych prenosov v aktívnej hre nula. Výmena kvality zobrazenia nesmie vyprázdniť scénu ani zmeniť povrch pre rozhodcu. |
| **Nepravidelný GNSS a strata opory** | Na tej istej stope vyskúšať fixy približne raz za sekundu, rôzne medzery a desaťsekundový výpadok. Ide o testovacie vstupy. Porovnať počty a časy fixov, AR snímok a odoslaných odhadov za sekundu. Pri dobrom AR overiť priebežný pohyb. Pri súčasnej strate vizuálnej opory overiť rast neistoty, odmietnutie zastaraného odhadu a spoločnú pauzu. Zmerať aj reálne intervaly GNSS na každom telefóne. |
| **Spoločný priestor a absolútna výška** | Najmenej tri nezávislé nekolineárne kontrolné body so známou polohou a výškou, oddelené od kalibračných značiek. Návrh je 30 opakovaných kalibrácií počas dvoch dní s výmenou rolí. V testovacom priestore do 30 m navrhujeme P95 vzájomnej odchýlky do 1 m, absolútnej vodorovnej do 3 m a výškovej do 1 m. Aspoň 90 % kalibrácií má byť pripravených do 2 minút. Rozmer ihriska sa zvolí na mieste. |
| **Vizuálne sledovanie účastníka** | Porovnať rovnakú chôdzu a rovnaké kontrolné body s vypnutou a zapnutou korekciou zo smerového pozorovania. Hlásiť aj dostupnosť detekcie, straty a nesprávne priradenia. Na použitie korekcie v hre navrhujeme aspoň 20 % zlepšenie P95 uhlovej chyby bez zhoršenia P95 polohy a nula nesprávnych opráv identity v 50 pripravených kríženiach. Inak modul poskytuje smer a diagnostiku. |
| **Odozva a výkon** | Na dvoch rôzne výkonných mobiloch merať 15 minút pri kamere a aktívnom online dueli. Navrhované P95 sú do 50 ms od stlačenia po prvú zobrazenú odozvu, do 34 ms na snímku a do 2 ms práce terénu na hlavnom vlákne. Zaznamenať tepelné obmedzenie, pamäť a rozdiel pri adaptívnom detaile. Priestorová platnosť a pravidlá zostávajú rovnaké na oboch zariadeniach. |
| **Herná správnosť a sieť** | Pri rovnakých vstupoch má byť predpovedaný a potvrdený dopad od seba najviac 0,25 m. Vyskúšať jednoznačný zásah, tesné minutie a neistý okraj účinku. Pri výpadku potvrdení je cieľ pozastaviť server aj lokálny vstup do 1 s. Po rekonexii overiť rovnaké HP, čas, stav kúzla a jediný účinok udalosti. Osobitne prehrať výstrahu aury aj potvrdené prekrytie. |
| **Moduly, dáta a pád** | Prehrať ten istý duel cez dva výmenné grafické moduly. Ukončiť proces pred odoslaním žurnálu aj po prijatí dávky pred návratom potvrdenia. Overiť jednu prijatú kópiu, zdokumentovanú prípadnú medzeru po páde a opätovnú prípravu po strate RAM. |

**Prístup k údajom a distribúcia.** Pred prvou skúškou s reálnymi trasami vlastník určí účel diagnostiky, prístup a informovanie oboch testerov. Návrh pre pilot je vymazať neodoslané lokálne dávky po 48 hodinách a identifikovateľné diagnostické trasy na serveri po 7 dňoch. Nastavenie treba zosúladiť so zálohami. Verejný herný repozitár má obsahovať vlastný kód, syntetické vstupy a postup zostavenia. Podpisový kľúč APK a presné testovacie trasy zostávajú v súkromnom úložisku. V aplikácii a k odvodeným výrezom uviesť ÚGKK SR, zdroj DMR 5.0, licenciu CC BY 4.0 a vykonané úpravy. Pri Unity, knižnici markerov a prípadných váhach detektora evidovať použitú verziu a jej distribučné podmienky. [Dáta a licencia GKÚ](https://www.gku.sk/gku/produkty-sluzby/na-stiahnutie/zbgis.html) · [Informovanie používateľa ARCore](https://developers.google.com/ar/develop/privacy-requirements) · [Podmienky Unity](https://unity.com/legal/editor-terms-of-service/software)

## 6. Prvé zadanie pre koordinátora pri začatí vývoja

> Začni míľnikom M0 v samostatnom hernom repozitári VibeKing. Priprav vzorové ihrisko, kontrakt balíka, časovaný odhad polohy a podmienku štartu pre dvoch hráčov. Ku každému kontraktu pridaj príklad platného aj neplatného vstupu. Po overení spoločných vstupov rozdeľ 1A až 1D podľa vlastníctva kódu. Prvý výsledok pre vlastníka je jeden spoločný virtuálny objekt na dvoch fyzických telefónoch, s osobitným meraním vzájomnej a absolútnej chyby a s nulovým prenosom ďalšieho terénu počas hry.
