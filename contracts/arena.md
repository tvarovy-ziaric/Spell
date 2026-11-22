# Balík ihriska v1

Jeden súbor `.bytes` obsahuje celú vzorkovanú plochu a nezávislú hranicu hry. V prvej verzii má ihrisko tvar obdĺžnika orientovaného podľa osí mriežky. Formát používa little-endian. Revízia arény a pravidlá zápasu patria do hernej relácie. Identita obsahu je SHA-256 nad **všetkými bajtmi** balíka vrátane hlavičky. Príjemca dostane očakávaný hash z dôveryhodnej konfigurácie alebo relácie, overí ho pred čítaním a potom drží výšky aj masku v pamäti.

| Poradie | Pole | Typ alebo význam |
| --- | --- | --- |
| 1 | Magic | Osem ASCII bajtov `SPLARENA` |
| 2 | Verzia formátu, verzia geometrie | Dve UInt16, obe 1 |
| 3 | Počet stĺpcov, počet riadkov | Dve UInt32, každá 2 až 2048 |
| 4 | Vodorovné a výškové EPSG | Dve Int32. DMR používa 8353 a 8357, syntetický priestor 0 a 0 |
| 5 | Easting, northing prvej vzorky, krok | Tri Float64 v metroch. Počiatkom je stred juhozápadnej vzorky. Krok je 1 m |
| 6 | Západ, juh, východ, sever hracej plochy | Štyri Float64 v metroch **od počiatku balíka**, vrátane hranice |
| 7 | Dĺžka ID datasetu, dĺžka revízie | Dve UInt16 s počtom bajtov od 1 do 256 |
| 8 | ID datasetu a jeho revízia | Dva po sebe idúce reťazce v UTF-8 s uvedenými dĺžkami |
| 9 | Výšky | W × H vzoriek Float32. Riadky rastú od juhu na sever, stĺpce od západu na východ. Pre DMR sú to absolútne metre Bpv |
| 10 | Maska platnosti | ceil(W × H / 8) bajtov, index vzorky i má bit `1 << (i % 8)` v bajte `i / 8`. Platnosť označuje 1. Zvyšné bity majú hodnotu nula |

Chýbajúca vzorka má nulovú výšku a nulový bit. Platnosť určuje výhradne maska. Za maskou sa súbor končí. Čítačka odmietne chybný súčet, veľkosť, rozmer, verziu, nekonečné či nečíselné výšky a neplatné metadáta. Na spustenie duelu musí byť `PlayableAreaValid` pravda. Overuje všetky vzorky v obdĺžniku ohraničujúcom hernú plochu po zaokrúhlení smerom von na mriežku. Medzera v dodatočnom okraji môže zostať medzi diagnostickými dátami.

## Volanie z C#

Unity aj .NET používajú rovnakú `Spell.Geometry.ArenaSurface` z [balíka geometrie](../shared/geometry). Po `ArenaSurface.Load(bytes, expectedSha256)` si oba procesy držia vlastnú overenú kópiu. Funkcia `TryHeight(e, n, out z)` berie lokálne metre od juhozápadnej vzorky. Výsledok má absolútnu výšku datasetu. V Unity preto treba pri prechode na scénu zohľadniť aj výškovú referenciu arény.

Každá bunka má diagonálu z juhozápadu na severovýchod. Výšky sa interpolujú po trojuholníkoch. Ak k výsledku kladnou váhou prispieva chýbajúca vzorka alebo dopyt opustí oblasť dát, funkcia vráti `false`. Výsledok nezávisí od detailu vykreslenia. `IsInPlayArea(e,n)` osobitne overuje povolený pohyb. `IntersectSegment` berie koncové body v poradí easting, northing, absolútna výška. Postupne skúša bunky a diagonály a pri prvom styku s povrchom vráti `Hit` a podiel dĺžky v intervale [0,1]. Pri celej známej voľnej dráhe vráti `Clear`. Chýbajúce dáta na skúšanej časti alebo koncový bod mimo mriežky znamenajú `Unknown`. Klient aj server môžu pred dopytom skrátiť úsek po hranicu ihriska.

## Referenčné ihrisko

### Kalibrácia lokálneho rámca telefónu

`MarkerCalibration.Fit` dostáva stredy dvoch známych terčíkov v dohodnutom priestore arény, ich pozorovania v spoločnom trackingovom rámci jedného telefónu, smer gravitácie nadol a prahy minimálneho vodorovného rozstupu a povolenej chyby. `MarkerPoint` nesie ID terčíka a polohu v metroch. V aréne je +Y nahor. Lokálne osi arény treba pri spojení s DMR výslovne zaregistrovať voči východu a severu. Absolútnu výšku určuje georeferencia ihriska.

Pozorovania sa spárujú podľa ID bez ohľadu na poradie. Výpočet zarovná vertikálu podľa gravitácie, azimut podľa vodorovného priemetu spojnice terčíkov a posun podľa ich stredu. Výsledkom pri stave `Ready` je obojsmerný tuhý prevod `ToShared` a `ToTracking` a najväčšia odchýlka medzi transformovaným pozorovaným stredom a príslušným referenčným stredom v metroch. Pri chybných ID, slabej základni alebo prekročenej odchýlke výsledný prevod chýba. Odchýlka meria vzájomný súlad dvoch pozorovaní. Spoločný posun alebo nesprávnu identifikáciu rovnako vysokých fyzických terčíkov nemožno zo samotného rezídua odhaliť. Identitu, kvalitu obrazu, spoločný trackingový rámec a aktuálnosť pozorovaní musí overiť klient. Samotné funkcie nesnímajú kameru.

### Syntetický povrch

[hill.bytes](fixtures/hill.bytes) má 7 × 7 vzoriek, lokálny počiatok (0, 0), krok 1 m a povolenú hru v rozsahu 1 až 5 m na každej osi. Vrchol v bode (3, 3) má 106 m. Vzorka (0, 0) je NoData. Balík má 312 bajtov a [kontrolný súčet](fixtures/hill.sha256). Pre poškodený vstup zmeň bajt na offsete 8 a zachovaj pôvodný očakávaný hash. Klient aj server majú vstup odmietnuť.

Z koreňa repozitára možno referenciu obnoviť a skontrolovať nasledujúcimi príkazmi.

```text
python terrain/arena.py fixture contracts/fixtures/hill.bytes
dotnet run --project terrain/tests/GeometryChecks.csproj -- contracts/fixtures/hill.bytes
```

## Príprava skutočného ihriska

Pythonová príprava číta zdrojový DMR len na čítanie a získa iba pôvodné okno v plnom rozlíšení. Vstupom je raster s metrovým krokom a jeho priestorové metadáta. Pri staršom TIFF s vodorovným LOCAL_CS a autoritou EPSG:8353 sa výšková referencia číta z poľa VCSWKID v sprievodnom súbore `.tif.xml`. Pri compound CRS sa kontroluje výškové EPSG priamo. Nástroj odmietne neznámu výškovú referenciu aj NoData v potrebných vzorkách.

```text
python -m pip install -r terrain/requirements.txt
python terrain/test_arena.py
python terrain/arena.py dmr FIELD.bytes --source DMR.tif --bounds WEST SOUTH EAST NORTH --margin 2 --revision RELEASE
```

Hranice nahraď absolútnymi súradnicami JTSK03 v poradí easting, northing v metroch. Výstup ulož mimo verejného repozitára. Pri distribúcii odvodeného balíka prilož jeho súbor `.source.txt` s uvedením zdroja ÚGKK SR, licencie CC BY 4.0 a vykonaných úprav. [Podklad DMR 5.0](https://www.gku.sk/gku/produkty-sluzby/na-stiahnutie/zbgis.html)

## Transformácia referenčného bodu

```text
python terrain/georeference.py GRID_DIR provision
python terrain/georeference.py GRID_DIR point LONGITUDE LATITUDE ELLIPSOID_HEIGHT
python terrain/test_georeference.py GRID_DIR
```

Prvý príkaz pripraví oficiálnu mriežku DVRM05 od GKÚ Bratislava a overí jej SHA-256 z katalógu PROJ. Druhý pracuje offline, zakazuje približné náhradné transformácie a používa bod už vyjadrený v ETRS89 (EPSG:4937), zemepisnú dĺžku a šírku v stupňoch a elipsoidickú výšku v metroch. Vypíše JTSK03 easting, northing a Bpv výšku v metroch. Mobilnú GNSS výšku treba zaradiť do správneho rámca a vyhodnotiť jej neistotu pred použitím tohto kroku. Tretí príkaz kontroluje poradie osí, znamienko výškovej opravy a interpoláciu vzhľadom na publikovaný raster DVRM05. Terénne overenie voči nezávislým zameraným bodom patrí do spoločnej skúšky. [Príručka a licencia transformačnej mriežky](https://cdn.proj.org/sk_gku_README.txt)
