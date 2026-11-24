# Changelog APK

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
