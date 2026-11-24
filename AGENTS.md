# Pracovné pravidlá projektu Spell

Na začiatku každého vlákna si prečítaj tento súbor, [README](README.md) a aktuálnu roadmapu uvedenú v README. Tieto pravidlá platia pre celý repozitár, koordinátora aj agentov. Pri odovzdaní rozpracovanej úlohy zachovaj informáciu o poslednej verzii APK, doručení a stave publikovania.

## Každý úspešný build APK

Hráči používajú server nastavený v aplikácii. Prvý hráč vytvorí hru a ukáže QR kód. Druhý ho nasníma kamerou v aplikácii a pripojí sa. V hráčskom rozhraní sa adresa servera ani pozvánka nevypisujú ručne.

1. Každé novo zostavené APK musí mať vlastnú verziu. Prideľ mu rastúci Android `versionCode` a uveď čitateľnú verziu aj číslo buildu v názve súboru. Platí to aj pre skúšobné buildy. Pri súbežnej práci prideľuje čísla jeden spoločný proces.
2. Po každom úspešnom builde odošli vlastníkovi samotný súbor APK ako dokument do dohodnutého súkromného chatu **codexer** cez telegramového bota `@codexer_komunikacia_bot`. Pripoj verziu a stručný opis zmien. Vlastník týmto pravidlom schválil opakované doručovanie. Za dôkaz odoslania považuj potvrdenie Telegramu pre príslušný súbor. Token a identifikátor súkromného chatu čítaj z lokálnej konfigurácie mimo Gitu.
3. Pre každé APK vytvor jeden záznam v koreňovom `CHANGELOG.md`. Uveď dátum, verziu, číslo buildu, zdrojový commit, názov APK, čo sa zmenilo a čo bolo overené. Pri opakovaní prenosu použi ten istý súbor, verziu aj záznam. Pri chybe doručenia uchovaj APK a pokračuj v doručení.

## Verejný changelog a stav aplikácie

4. Publikuj changelog aj ako jednoduchú webstránku. Webový prehľad verzií generuj z toho istého changelogu, aby obe podoby uvádzali rovnaké zmeny.
5. Web sprístupni cez **Cloudflare Tunnel** na **https://spell.arnavigator.site**. Po publikovaní na tejto verejnej adrese over zobrazenie očakávanej verzie.
6. Na úvodnej stránke udržuj stručný opis aktuálneho stavu Spell. Uveď, čo už funguje, čo bolo skutočne odskúšané a čo ešte čaká na overenie. Skontroluj a aktualizuj ho pri každom APK aj pri zmene výsledku skúšky alebo míľnika medzi buildmi.

## Samostatná práca

7. Trvalé dohody vlastníka o práci na Spell zapisuj sem a ulož do hlavnej vetvy repozitára. Nové vlákno musí vedieť nadviazať podľa obsahu repozitára.
8. Keď vlastník zadá realizáciu, pokračuj samostatne cez implementáciu, testy, opravy a dohodnuté odovzdanie. Bežné technické rozhodnutia, opakovanie prenosu a opraviteľné zlyhania vyrieš sám. Zastav sa až pri kroku, ktorý skutočne vyžaduje človeka, napríklad fyzickú skúšku na ihrisku, chýbajúci prístup alebo rozhodnutie vlastníka o prijatí míľnika. Najskôr dokonči dostupnú nezávislú prácu a potom si vypýtaj konkrétny potrebný zásah. Otázku o pripravenosti považuj za žiadosť o informáciu.
