# Diagnostická relácia v1

Každá dvojica má vlastnú reláciu. Jeden proces servera pri štarte overí SHA-256 ihriska, načíta ho do pamäte a zdieľa jeho povrch medzi reláciami. Aplikácia používa HTTPS a WSS na `spell.arnavigator.site`. Tunel smeruje `/v1/` na herný server, úvodnú stránku na web so stavom vývoja.

| Rozhranie | Vstup a výsledok |
| --- | --- |
| POST /v1/session | V režime hráčov založí aplikácia duel s daným ihriskom a pravidlami pomocou hlavičky `Idempotency-Key`. Súkromný režim vyžaduje aj Bearer kľúč hostiteľa. Odpoveď obsahuje miesto hráča, identitu balíka a QR PNG s päťminútovým pozvaním. |
| GET /v1/session | Bearer token hráča vráti `{"paired": true}` po obsadení oboch miest. |
| DELETE /v1/session | Hráč s platným tokenom ukončí svoju reláciu obom telefónom. Oprávnenia a nepoužité pozvanie stratia platnosť. |
| POST /v1/invite | Platné oprávnenie zakladateľa vydá nové pozvanie pred obsadením druhého miesta. Starý kód tým stratí platnosť. |
| POST /v1/join | JSON s poľom `code` z pozvania `spell://join/v1/<code>`. Platný kód získa jedno miesto hosťa. Ďalšie použitie dostane HTTP 401. |
| GET /v1/arena | Bearer token miesta hráča oprávňuje stiahnuť binárny balík v stave prípravy alebo pauzy. Počas hry odpovie HTTP 403. |
| GET /v1/events | Každé miesto má jedno WebSocket spojenie s Bearer tokenom v hlavičke. Každá binárna správa je jeden Protobuf ClientFrame alebo ServerFrame, veľkosť vstupu najviac 4096 bajtov. |

Zakladateľ odošle pri vytvorení relácie hlavičku `Idempotency-Key` s náhodným GUID vo forme 32 hexadecimálnych znakov. Aplikácia drží tento údaj v súkromí rovnako ako token hráča. Pri opakovaní s rovnakým identifikátorom získa pôvodné miesto. Vypršané pozvanie obnoví cez `/v1/invite`. Hosť môže pri pripojení pridať do JSON pole `attempt` s vlastným náhodným GUID v rovnakom formáte. Pri strate odpovede zopakuje pôvodný kód s tým istým identifikátorom a získa pôvodné miesto. Rovnaký kód s iným identifikátorom dostane HTTP 401.

Verejný pilot povoľuje desať požiadaviek na vytvorenie hry za minútu z jednej IP adresy a najviac 128 súbežných relácií. Relácia bez spojenia či autorizovanej aktivity zanikne po pätnástich minútach. Reštart procesu ukončí rozbehnuté diagnostické relácie.

Poradie je pripojenie, prijatie snímky stavu, overenie celého balíka, kalibrácia a potvrdenie pripravenosti. `Ready` pre revíziu 1 obsahuje hash oznámený serverom, preset `diagnostic-v1` a potvrdenia pamäte a kalibrácie. Nesprávny súčet alebo odvolaná kalibrácia zruší pripravenosť. Prechod do hry vyžaduje dve pripravené miesta s čerstvým sledovaním. Obe strany dostanú tú istú revíziu stavu. Opakované potvrdenie platí pre ten istý stav.

`PoseSample` pracuje v lokálnych metroch ihriska, vodorovne na východ a sever od juhozápadnej vzorky, so zvislicou nahor. Zariadenie odošle odhad s rastúcim poradím a monotónnym časom. Chýbajúci GNSS fix sa vynechá. Pri opakovanom použití starého fixu si zachová čas merania. Čas vizuálneho merania protokolu musí byť prevedený na rovnakú časovú základňu ako odhad. Mobilná diagnostika eviduje aj pôvodný čas kamery a samostatný čas prijatia. `Ping/Pong` vráti čas klienta a monotónny čas servera v mikrosekundách na meranie spiatočného času a odhadu posunu hodín.

Pre syntetickú skúšku sú pracovné maximá neistoty 3 m a 10°. Skutočné prahy pre fyzický pilot vyžadujú meranie. Vzorka mimo ihriska, neisté sledovanie alebo chýbajúca vizuálna opora zrušia pripravenosť. Po 800 ms bez novej použiteľnej vzorky server pri najbližšej 100 ms kontrole pozastaví spoločnú reláciu. Odpojenie invaliduje potvrdenie daného miesta. Po pripojení rovnakým tokenom treba obnoviť pripravenosť aj sledovanie.

`VisualObservation` je rozhranie pre lokálny vizuálny modul. Smer je jednotkový vektor. Pre potvrdenú značku je prípustné ID známej značky s časom snímky a kvalitou v rozsahu 0 až 1. Stratená identita účastníka zruší oprávnenie použiť pozorovanie na korekciu. Mobilná diagnostika používa dve vopred známe dosky. Identifikácia osôb čaká na ďalšiu etapu.

Voliteľný zdroj smeru protihráča v1 prijíma iba dočasnú referenciu, ktorú účastník potvrdil pre konkrétnu reláciu. Čas snímky aj príjmu vstupuje v mikrosekundách na tej istej monotónnej osi. `PoseTimeline` publikuje jednotkový smer iba pri jedinom potvrdenom kandidátovi, čerstvej snímke a dôveryhodnom sledovaní kamery. Žiadny zdroj alebo chýbajúce pozorovanie ponechá smer prázdny. Odhad vzdialenosti, polohy, fyzika a kalibrácia sa týmto údajom nemenia. Automatické skúšky používajú výlučne syntetické identifikátory a smery. Zapojenie fyzickej dobrovoľnej značky hráča a jej asociáciu s reláciou musí overiť skúška na mieste.

| Príklad | Očakávané správanie |
| --- | --- |
| Odhad pohybu s časom 1 200 000 µs, posledný GNSS fix 1 080 000 µs a čerstvá AR snímka | Akceptovať príslušný odhad, čas pôvodného fixu ostáva 1 080 000 µs. |
| Rovnaký odhad s časom vizuálneho merania 1 300 000 µs | Odmietnuť pripravenosť, meranie je neskoršie ako samotný odhad. |
| Známa značka s overeným ID, kladným časom a jednotkovým smerom (0, 0, 1) | Lokálny modul ju môže použiť na výpočet kalibrácie. |
| Pozorovanie postavy s nepotvrdenou identitou alebo nulovým vektorom smeru | Nepoužiť na opravu polohy žiadneho hráča. |
| Kontrola pauzy po oneskorenom doručení čakajúcich správ | Server vyhodnotí aj dodatočné oneskorenie oproti najnižšiemu zistenému časovému posunu konkrétneho spojenia. Starý odhad nemôže obnoviť štart iba tým, že práve dorazil. Absolútne sieťové oneskorenie prvého merania treba odmerať v pilote. |
