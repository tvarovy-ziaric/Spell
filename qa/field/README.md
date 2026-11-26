# Vyhodnotenie terénnej skúšky

Testerovi stačí [postup na ihrisku](../../docs/M2-skuska.md). Koordinátor prepisuje výsledky do **súkromnej kópie** prázdnych CSV, uloženej mimo Gitu. Každá séria má rovnaké dva telefóny, rovnaký APK a balík ihriska. Uchovaj si ich verzie, modely a svetelné podmienky pri pôvodnom zázname.

- `points.csv` obsahuje P1 až P3 alebo viac nezávislých bodov. Uveď súradnice v JTSK03 E/N (EPSG:8353), výšku Bpv (EPSG:8357) a neistotu referencie v metroch, ktorú dodal geodet. Vzťažným miestom je poloha zadnej kamery v zarážke. Výšku bodu na zemi prepočítaj odmeraným posunom od neho. Ak bod poznáme iba podľa lokálnej značky, vyplň iba jeho meno.
- `attempts.csv` má riadok pre každé zariadenie aj neúspešný pokus. `run` identifikuje jednu spoločnú kalibráciu dvojice, `day` je vo formáte RRRR-MM-DD. `phone` je stále A alebo B, `role` founder alebo guest, `outcome` ready alebo failed. Čas je v sekundách. Posledné dve polia sú **rozdiely počítadiel** nových terénnych požiadaviek a bajtov medzi začiatkom a koncom skutočného úseku hry na danom mobile. Prázdne pole je nezmerané.
- `observations.csv` obsahuje jeden riadok pre telefón na bode v pokuse. X, Y, Z opíš z riadku **M2 · spoločný rámec**. Osi sú smer od A k B, hore a kolmica. Odchýlka fitu terčíkov vyjadruje zhodu ich predpísanej geometrie, nie chybu voči kontrolnému bodu. Ak existuje nezávisle vypočítaný výstup lokalizácie mobilu v EPSG:8353 + Bpv, doplň ho do posledných troch polí. Inak nechaj polia prázdne. Súradnice referencie nikdy nekopíruj do stĺpcov odhadu telefónu.
- Ak po jednej kalibrácii zbieraš viac priechodov tým istým bodom, vyhodnoť ich v samostatných sériách s tou istou identitou kalibrácie. Pri zlúčení nezapočítaj opakované priechody ako nové kalibrácie. Nezamerané body vynechaj a zachovaj poznámku o príčine.

### Voliteľná absolútna georeferencia

1. V mobilnej diagnostike otvor **M2 · súkromné meranie GNSS pri terčíkoch**. Pri stojacom telefóne pri A stlač **Zmerať pri terčíku A**. Počkaj na nový fix, opíš alebo odfoť údaje a zopakuj pri B. Meria sa anténa telefónu. Zmeraj posun od antény k stredu príslušnej dosky. Ak jej polohu v tele telefónu nepoznáš presne, zahrň túto neistotu. Obe merania sú iba v pamäti, možno ich vymazať tlačidlom. Aplikácia ich neposiela do serverového žurnálu. Súradnice obrazovky sú zemepisná dĺžka, šírka a **elipsoidická** výška Android WGS84, s časom fixu v UTC epoch ms a monotónnym časom v mikrosekundách. Chýbajúca vertikálna presnosť ostáva neznáma.
2. Kvalifikovaný spracovateľ doloží použitý referenčný rámec, epochu, transformáciu a jej neistotu. Najprv musí správne previesť pôvodné pozorovania do ETRS89 s elipsoidickou výškou. Existujúci `terrain/georeference.py` potom použije overenú mriežku DVRM05 na prevod **z ETRS89** do JTSK03/Bpv. Náhrada názvu WGS84 za ETRS89 alebo priame porovnanie elipsoidickej výšky s DMR by vydali nesprávny výsledok. Pri nedoloženom prevode alebo chýbajúcej vertikálnej neistote absolútne polia nechaj prázdne.
3. `anchors.csv` je voliteľný. Každý riadok označuje vlastný fix jedného telefónu pri A alebo B. Vpíš `EPSG:8353+8357`, zdroj `phone-fix`, opis prevodu, UTC čas s časovým pásmom, tri prepočítané súradnice antény a odmeraný vektor **z antény k stredu terčíka** (E, N, hore). Pripočítaj chybu prevodu a určenia posunu k odhadom neistoty H/V. Absolútna georeferencia každého telefónu musí pochádzať z nezávislého odhadu daného telefónu. Dosadenie presných zameraných kotiev ako vstupu oboch mobilov by skrylo chybu GNSS.
4. Nástroj z dvojice centrov, stredu úsečky a smeru A → B dopočíta odhad mobilu v rovnakom rámci ako geodetické body. Zohľadní predpísaných 1,50 m a rovnakú výšku terčíkov. Ak interval neistoty umožní ľubovoľný smer, chýba niektorý terčík alebo geometria nezodpovedá montáži, výsledkom je nezmeraná absolútna poloha. Pri bežnej neistote GNSS rádovo v metroch je to na krátkej diagnostickej základni očakávateľné. Citlivosť na chybu určenia kotiev je v samostatných poliach. Ide o model na spracovanie fyzických meraní po skúške, aplikácia ho pri zobrazovaní syntetickej arény nepoužíva.

### Výpočet na pracovnom počítači

```powershell
python qa/field/m2.py "cesta k súkromnej kópii CSV"
python qa/field/test_m2.py
```

Merania sú v metroch, sekundy a desatinné čísla zapisuj s bodkou. Výstup má oddelené P95 párovej 3D odchýlky, absolútnej vodorovnej chyby, absolútnej výškovej chyby a variantov pripočítavajúcich neistotu geodetického bodu. P95 používa metódu najbližšieho horného poradia. Výsledok uvádza počty platných údajov a pokusov, dvojice, ktoré boli skutočne zmerané, merania prevádzky, zastúpenie dní a výmenu rolí. `null` znamená, že chýba meranie. Údaje bez georeferencie ostávajú použiteľné pre vzájomné porovnanie. Rozhodnutie o M2 je na vlastníkovi.

Pre vytvorenie PDF z pôvodných obrázkov spusti `python qa/field/print_markers.py` s nainštalovaným ReportLab. Hotový [tlačový súbor](../../docs/M2-terciky.pdf) má dve strany A4 s rovnakými 640 × 640 textúrami ako zdrojový Unity projekt.
