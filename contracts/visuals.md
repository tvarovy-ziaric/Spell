# Vizuálne moduly v1

Mobilná diagnostika ponúka v **Nastaveniach grafiky** samostatnú voľbu vzhľadu terčíkov, vzhľadu Fireballu, grafického profilu a čiary trajektórie. Výkonové profily ovládajú počet vykreslených iskier a cieľových 30 alebo 60 FPS. Závažný tepelný stav Androidu pri aktívnej diagnostike prepne efekty na úsporný profil. Dosiahnutú snímkovú frekvenciu treba zmerať.

`IMarkerPoseSource.TryGetMarkerPose` odovzdáva hodnotovú kópiu `Pose` fyzickej dosky v súradniciach Unity v metroch. Každá nová snímka začína prázdnym zoznamom pozorovaní. ARCore musí pre príslušnú známu dosku potvrdiť plné vizuálne sledovanie. Chýbajúca doska, prerušenie kamery, pauza alebo snímka staršia než 800 ms dekoráciu skryje. Dvanásťsekundové okno existujúcej kalibrácie slúži na postupné zameranie oboch dosiek. Téma nedostáva AR trackable, transformáciu kalibrácie ani oprávnenie zapisovať do zdroja polohy.

`IMarkerVisualTheme` a `IFireballRenderer` implementujú `ApiVersion = 1`. Vstupy pre Fireball sú poloha, smer letu, čas od začiatku a priebeh serverom potvrdeného efektu dopadu. Oba vykresľovače čítajú tú istú interpoláciu medzi koncovými bodmi snímky. Identita letu obmedzí opakovanú snímku na jednu inštanciu. Pri strate spojenia či platného rámca volajúci zruší náhľad aj lety.

Predikcia používa spoločný `FireballPath.Trace` a uloženú mriežku. Kružnicu predpokladanej zóny vykreslí po povrchu len pri terénnom dopade s dostupnými výškami celej kružnice. Rámec prepočtu vychádza z kalibrácie oboch dosiek a nezávislej registrácie ich stredov voči balíku ihriska.

Pre meraný priestor dodá prevádzkovateľ zdroj `Assets/Spell/Resources/SpellSurvey.json`. Schéma v1 má `version` (1), `arenaSha256` (presný hash balíka), `boardA` a `boardB` (každá hodnota je pole troch čísel). Súradnice stredov majú poradie východný posun od pôvodu balíka, absolútna výška vo výškovom systéme balíka, severný posun. Chýbajúca alebo neplatná registrácia ponechá zameranie nedostupné. Predvolená registrácia je obmedzená na presný SHA-256 syntetického kopca. Na tomto dátovom súbore nemožno merať absolútnu presnosť voči skutočnému DMR.

Skúšku spúšťa Unity Editor cez `-executeMethod Spell.Editor.VisualContractChecks.Run`. Používa skutočné triedy oboch grafických tém, prepínač vykresľovačov, opakované udalosti, neplatný kontrakt, časové hrany, zmenu vstupnej polohy aj mriežku ihriska. Vzhľad v kamere, čitateľnosť na slnku, termálne správanie a presnosť fyzického ukotvenia vyžadujú dva telefóny.
