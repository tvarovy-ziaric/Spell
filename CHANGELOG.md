# Changelog APK

## 0.2.0-dev · build 5

- **Dátum vydania** 2026-11-27
- **Zdrojový commit** `49c78b39e1d3dd9f0d8de9f87b9d215a5acef4ec`
- **APK** `Spell-0.2.0-dev-5.apk`
- **SHA-256** `de108806862247651c05eb87caaa9fa539a4d965f8a453526a7725d307f2a160`
- **Zmeny** Diagnostická verzia pred fyzickou skúškou M2. Pridáva serverový Fireball, auru, pravidlá zakladateľa, vizuálne témy Inferno a Necro, záznam priebehu a meranie spoločného rámca.
- **Overené** Dva testovacie klienty získali zhodný výsledok zásahu. Prešli skúšky siete, kalibrácie, georeferencie, databázy a 33 kontrol grafických kontraktov v Unity. Skontrolované boli aj identifikátor a verzia v manifeste APK.

- **Skúška nainštalovaného APK** Dva emulátory Androidu 15 s rovnakým súborom sa spojili cez verejný server. Hosť nasnímal QR zakladateľa virtuálnou kamerou. Obaja videli pripravené ihrisko, stav 100/100 HP a čakanie na kalibráciu. V aplikácii prešlo prepnutie tém Inferno/Necro a Ember/Rune. Pri prvom pokuse nereagoval náhľad virtuálnej kamery. Opakované skenovanie s udeleným oprávnením prešlo. Po odchode hosťa obaja klienti ukončili hru a ich lokálne dávky sa doručili na server.
- **Administrácia v APK** Zakladateľ odomkol nastavenia, zmenil životy zo 100 na 101, uložil pravidlá, vrátil sa na predvolené a z histórie znova načítal 101 HP. Po odhlásení server zrušil oprávnenie.

## 0.1.1-dev · build 3

- **Dátum vydania** 2026-11-24
- **Zdrojový commit** `0268a3283ca5e0eccc4d86036ec0b5227c9f4681`
- **APK** `Spell-0.1.1-dev-3.apk`
- **SHA-256** `262ed22a82cabde98ac5543f76d719c8bfbca34a4113fc9835879b683349a278`
- **Zmeny** Prvý hráč vytvorí hru a ukáže QR kód. Druhý ho nasníma kamerou v Spell. Aplikácia sa spojí s nastaveným serverom a načíta ihrisko.
- **Overené** Cez verejný server sa z vygenerovaného QR kódu pripojili dve skúšobné relácie hráčov a prevzali zhodné dáta ihriska. Skontrolované boli aj identifikátor a verzia v manifeste APK.
- **Skúška APK** Ten istý podpísaný súbor sme nainštalovali na dva emulátory Android 15. Prvý hráč vytvoril hru, druhý nasnímal zobrazený kód virtuálnou kamerou. Oba Androidy zobrazili „Obaja hráči sú v hre“ a načítali ihrisko do pamäte.

## 0.1.0-dev · build 2

- **Dátum vydania** 2026-11-24
- **Zdrojový commit** `f6c97f316e307e3d2278d5091b22864f7222c296`
- **APK** `Spell-0.1.0-dev-2.apk`
- **SHA-256** `8b24c8f2b8ae4b11da42014148f6178b460a7be71839123c1f569cf930167c38`
- **Zmeny** Prvé diagnostické APK s ihriskom v pamäti, kontrolou AR a GNSS a dvoma kalibračnými terčíkmi.
- **Overené** Prešli hostiteľské skúšky geometrie, kalibrácie a párovania. Podpis APK overil Android apksigner. Skontrolované boli aj identifikátor a verzia v manifeste APK.
- **Skúška APK** Na emulátore Android 15 sa zobrazilo ihrisko s výškou kontrolného bodu 106 m. Build 2 používal ručný testovací formulár. Hráčske párovanie cez kameru v tejto verzii chýbalo.
