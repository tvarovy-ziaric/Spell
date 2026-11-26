# Stav vývoja

## Príprava pred M2

Zakladateľ vytvorí hru, druhý hráč naskenuje QR kód. Oba telefóny dostávajú tú istú verziu ihriska a pravidiel. Verejný pilot používa malý syntetický kopec.

Dva testovacie klienty sa cez verejný server pripojili do rovnakej hry. Po výpadku sa spojenie obnovilo a opakované zoslanie toho istého Fireballu odpočítalo HP iba raz. Lokálne skúšky rozhodcu preverili aj zákryt terénom, auru a hranice ihriska. Na verejnom serveri prešlo uloženie presetov zakladateľa a opätovné doručenie diagnostických udalostí do vlastného PostgreSQL.

## Grafika a diagnostika

V Unity prešla výmena vykresľovačov Fireballu aj tém terčíkov Inferno a Necro. Pri nedôveryhodnom sledovaní sa dekorácia skryje. Samostatné skúšky overili zameriavač, terénnu sieť, stratu opory, syntetické merania GNSS a prevod výšky do Bpv.

## Čo čaká na ihrisko

**M2 zostáva otvorený.** Dvaja ľudia na rozdielnych Androidoch odmerajú polohu spoločného objektu, presnosť po chôdzi a otočení, reakciu na stratu značky a prenos terénu po štarte. Absolútna chyba voči DMR vyžaduje vybraný výrez a nezávislé kontrolné body. Na skutočných telefónoch treba zvlášť overiť čitateľnosť efektov, odozvu, plynulosť a zahrievanie.

## Posledné APK

**0.1.1-dev · build 3**. Párovanie zakladateľa a hosťa cez QR kód. Priestorová kalibrácia na ihrisku a hrateľný súboj čakajú na skúšku.
