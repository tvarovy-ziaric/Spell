# Spell na dvoch telefónoch

Cieľom prvej skúšky je zistiť, či obaja vidíte tú istú virtuálnu guľu na tom istom mieste a či tam po chôdzi a otočení zostane. Na prvý pokus stačia dva podporované telefóny s rovnakým APK, dva vytlačené listy, meter, stopky a ceruzka.

### 1. Pripravte miesto

- Stiahnite a vytlačte [obe strany PDF s terčíkmi](M2-terciky.pdf) na papier A4, pri mierke **100 % / skutočná veľkosť**. Pravítkom overte dĺžku úsečky **100 mm** a celú štvorcovú vzorku **160 × 160 mm** vrátane sivého okraja. Toto sú kalibračné dosky. Párovací QR kód zobrazí prvý telefón až pri založení hry.
- Na rovnom prehľadnom mieste upevnite dosky zvislo, označením nahor. Stredy majú byť v rovnakej výške, presne **1,50 m od seba**. Tento rozmer patrí ku geometrii skúšobného APK. Počas skúšky doskami nehýbte. Na tyčke si označte miesto 25 cm **nad stredom spojnice** dosiek. Tam očakávate stred kontrolnej gule.
- Ak chcete aj číselné porovnanie telefónov, vopred vyznačte tri miesta P1, P2 a P3, ktoré neležia na jednej priamke. Pri každom nastavte zarážku pre zadnú kameru vo fixnej výške. Zaznačte si aj smer, ktorým má kamera pri porovnaní mieriť. Miesta sú samostatné, mimo dosiek. Na prvú skúšku ich umiestnite tak, aby od nich bolo vidno terčíky.
- Účel snímok a kto ich bude vidieť si dohodnite s oboma testermi. Presné polohy a záznamy uschovajte súkromne.

### 2. Spárujte telefóny

Na prvom stlačte **Vytvoriť hru**. Na druhom stlačte **Naskenovať QR kód** a namierte kameru na displej prvého. Na oboch očakávajte „Obaja hráči sú v hre“ a načítané ihrisko. Zapíšte modely telefónov, verziu APK, dátum a ktorý telefón hru založil. Skúšobná mriežka 7 × 7 predstavuje syntetický povrch.

### 3. Nezávisle nakalibrujte každý telefón

Na každom stlačte **Skontrolovať senzory**, povoľte kameru a presnú polohu a spustite stopky. Postavte sa pred dosky na samostatné pozorovacie miesto, odkiaľ vidíte obe. Každým telefónom zamerajte A aj B v priebehu **12 sekúnd**. Pri prijatí spoločného rámca si zapíšte čas od začiatku pokusu. Pri neúspechu zapíšte „nepodarilo sa“, čas a hlásenie.

Stlačte **Zobraziť kameru**. Pozrite, či na oboch displejoch leží stred gule pri značke 25 cm nad spojnicou. Urobte snímku z každého telefónu. Cez **Zobraziť diagnostiku → Nastavenia grafiky → Terčíky** vyberte **Inferno** alebo **Necro** a vráťte sa do kamery. Vyskúšajte obe témy. Zapíšte, či dekorácie sedia na terčíkoch a čo sa stane, keď kameru odvrátite alebo terčík zakryjete.

### 4. Prejdite, otočte sa a vráťte

Postupne priložte zadnú kameru prvého aj druhého telefónu na rovnakú zarážku P1, do rovnakej výšky a smeru. Každý si odfoťte riadok **M2 · spoločný rámec (X, Y, Z)** a riadok s GNSS. Zapíšte čísla ku konkrétnemu telefónu, pokusu a bodu. Riadok „Lokálna poloha“ používa vlastný počiatok každého telefónu. Pre porovnanie potrebujete hodnoty zo spoločného rámca.

Zopakujte na P2 a P3. Prejdite niekoľko krokov, zastavte, otočte kameru od dosiek a vráťte sa k nim. Nový okruh zapíšte pod novým označením pokusu alebo samostatnej série. Sledujte správu o strate a obnovení opory, viditeľnosť gule a prípadný skok pri opätovnom nájdení. Ak rámec chýba, zapíšte „nedostupný“. Zvlášť si poznačte úsek s nepravidelným GPS a vek posledného fixu. Pri chôdzi kontrolujte okolie, zatiaľ čo druhý človek sleduje trasu.

### 5. Odovzdajte výsledok

Stačia snímky alebo opis na papieri. Jeden riadok pre každé meranie môže vyzerať takto:

| Pokus a deň | Telefón a rola | Bod | Kalibrácia v s / neúspech | Spoločné X, Y, Z v m | Čo ste videli |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

Pre porovnanie dvoch telefónov použijeme vzdialenosť medzi ich odhadmi na rovnakom bode. Pre **absolútnu chybu voči terénu** treba osobitne získať georeferenciu telefónov a nezávislé geodetické súradnice kontrolných bodov na úrovni zadnej kamery. Pri prvej skúške je v poriadku odovzdať iba vzájomné porovnanie a video. Bez podkladov absolútna chyba ostáva nezmeraná.

Pre širší pilot roadmapa navrhuje **30 opakovaných kalibrácií počas dvoch dní** s výmenou rolí na priestore do 30 m. Do výsledku patria aj zlyhané kalibrácie. Ciele sú P95 vzájomnej odchýlky do 1 m, absolútnej vodorovnej do 3 m, výškovej do 1 m a aspoň 90 % kalibrácií pripravených do dvoch minút. P95 označuje hodnotu, ktorú neprekročilo približne 95 % meraní. Po porovnaní na spoločnej zarážke sa každý vráťte na vlastné pozorovacie miesto. Ak aplikácia hlási blízkosť hráčov, pomaly zväčšite odstup vnútri ihriska. Keď sa duel spustí, na každom telefóne otvorte **Zobraziť diagnostiku**. Odfoťte riadok **Terén od štartu** po štarte a po návrate z chôdze. Na oboch očakávame `0 požiadaviek, 0 B`. Ak sa duel nespustil alebo riadok chýba, zapíšte „nezmerané“. Výsledok prijíma vlastník po preštudovaní meraní.

Technická príloha opisuje meranie voči DMR, formuláre a automatický výpočet. [Vyhodnotenie M2](../qa/field/README.md)
