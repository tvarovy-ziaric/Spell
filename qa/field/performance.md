# M4 · meranie odozvy na dvoch telefónoch

1. Na každom telefóne zapíš model, verziu APK, výkonový režim, zapnutú hĺbku, počiatočný stav batérie, teplotu alebo stav tepelného obmedzenia. Na rovnakom ihrisku spusti kameru aj online duel. Jeden súvislý úsek meraj aspoň **15 minút**. Pri prerušení alebo zmene režimu začni ďalší úsek. Ak máte údaje o pamäti či tepelnom obmedzení počas úseku, prilož ich spolu so zdrojom.
2. Odozvu na stlačenie nakrúť zvonka kamerou s vysokou snímkovou frekvenciou. V jednom zábere musí byť vidieť **dotyk aj prvú viditeľnú reakciu naň**. Pre každý pokus odčítaj čas týchto dvoch snímok. Zapíš použitú časovú základňu a jej rozlíšenie. Pri 240 fps je rozdiel dvoch susedných snímok približne 4,17 ms. Pri variabilnej frekvencii použi skutočné časové značky. Ak odozvu nerozoznáš, zaznamenaj počet takých pokusov v poznámkach a nepriraď im čas nula.
3. Dĺžku snímky odvoď zo skutočných časov prezentácie po sebe idúcich snímok daného herného povrchu, napríklad z identifikovateľných udalostí systémovej stopy. Dlhý interval bez novej snímky patrí do vzorky, keď stopa potvrdzuje, že ide o susedné prezentácie. Pri medzere v zázname iba pokračuj ďalším známym párom. Priemerné FPS neumožňuje dopočítať P95.
4. Čas terénu zbieraj iba vtedy, keď stopa alebo profiler samostatne identifikuje prácu terénu na hlavnom vlákne a jej snímku. Pre jednu snímku s viacerými nesúvislými úsekmi zapíš každý úsek zvlášť pod rovnakým číslom snímky. Vnorené časovače zredukuj na neprekrývajúce sa intervaly. Nulový interval vlož iba pre skutočne pozorovanú snímku bez práce terénu. Ak súčasný APK neposkytuje hodnoverný rozklad časov, nechaj túto metriku nezmeranú.

### Podklady pre koordinátora

Pôvodné videá, merania a mená telefónov uchovaj súkromne. Vytvor CSV s hlavičkou

```csv
run,phone,mode,metric,method,source,sample,start_us,end_us
```

`run` identifikuje úsek, `phone` konkrétny telefón a `mode` zvolený režim. `source` je názov videa alebo súboru stopy a príslušného kanála. `sample` je identifikátor stlačenia alebo snímky, pre celý úsek ľubovoľný jeden názov. `start_us,end_us` sú nezáporné celé mikrosekundy od spoločného začiatku úseku. Časy jedného páru pochádzajú z toho istého zdroja. Pri použití viacerých hodín zaznamenaj synchronizačný bod a preveď ich na spoločnú os. Bez synchronizácie vyhodnoť osobitný úsek bez údajov o celkovom trvaní.

| metric | method | Začiatok a koniec páru |
| --- | --- | --- |
| window | duel_clock | Začiatok a koniec súvislého aktívneho duelu so zapnutou kamerou. Jeden riadok pre telefón, úsek a režim. |
| tap | high_speed_video | Prvý pozorovateľný dotyk a prvá viditeľná odozva na displeji. |
| frame | presentation_times | Dve susedné prezentácie snímok hry. |
| terrain | main_thread_trace | Ohraničený úsek práce terénu na hlavnom vlákne, priradený ku konkrétnej snímke. |

```powershell
python qa/field/performance.py "cesta k súkromnému CSV"
python qa/field/test_performance.py
```

Výstup hodnotí každú kombináciu telefónu, úseku a režimu samostatne. Uvádza počet vzoriek, P95 metódou najbližšieho horného poradia, maximum a počet jednotlivých prekročení. Navrhnuté limity P95 sú **50 ms** pre dotyk, **34 ms** pre prezentované snímky a **2 ms** pre terén na hlavnom vlákne. Stav `WITHIN` alebo `ABOVE` porovnáva len P95 dodaných vzoriek. Pri chýbajúcom zdroji je výsledok `MISSING`. Rozsah vzoriek a pokrytie prezentácií si skontroluj samostatne, pretože pätnásťminútový duel môže mať napríklad len minútu záznamu snímok. Pri interpretácii zohľadni rozlíšenie videa, rozloženie stlačení počas úseku, teplo, pamäť a rozdiely pri adaptívnom detaile. O prijatí M4 rozhoduje vlastník po fyzickej skúške.
