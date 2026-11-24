# Stav vývoja

## Ihrisko a skúšobný duel

V aplikácii prvý hráč stlačí **Vytvoriť hru** a ukáže QR kód. Druhý stlačí **Naskenovať QR kód** a namieri kameru na displej prvého. Spell pozná adresu herného servera.

V builde 3 sme tento priebeh prešli na dvoch emulátoroch Android 15 cez verejný server. Virtuálna kamera druhého zariadenia nasnímala kód z obrazovky prvého. Oba zobrazili **Obaja hráči sú v hre** a každý načítal skúšobné ihrisko s mriežkou 7 × 7 do pamäte.

Na počítači prečítali klientská logika aj server rovnaký skúšobný kopec a odmietli poškodený balík. Prešlo 25 kontrol geometrie ihriska, 23 kontrol výpočtu kalibrácie podľa dvoch terčíkov a šesť testov prípravy terénnych dát. V teste časovania mobilnej polohy systém vydal 16 časových vzoriek pri dvoch rôznych vstupných meraniach GNSS.

Dva simulované klienty sa cez sieť pripojili k duelu. Skúška overila jednorazové párovanie, spoločný štart, zablokovanie ďalších prenosov terénu počas hry aj návrat po výpadku.

## Čo čaká na skúšku

Na dvoch fyzických telefónoch ešte treba odskúšať čítanie QR kódu z displeja a na ihrisku zmerať presnosť kalibrácie pomocou dvoch terčíkov. Hrateľný duel je ďalšou etapou.

## Posledné APK

**0.1.1-dev · build 3**. Párovanie zakladateľa a hosťa cez QR kód. Priestorová kalibrácia na ihrisku a hrateľný súboj čakajú na skúšku.
