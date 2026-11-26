# Obnova herného servera

Domáci server obsluhuje port 18874. Zostavenie `3b8a5482` bolo nasadené medzi reláciami po overení nulového počtu miestností v registri pôvodného procesu. HTTPS aj dve sieťové spojenia WebSocket prešli skúškou po prepnutí. Predchádzajúce zostavenie `aaf5acb` je uložené vedľa neho.

V privátnom adresári `%LOCALAPPDATA%\Spell\server` sú súbory `task-current.xml` a `task-previous.xml` pre túto konkrétnu výmenu. Prevádzkovateľ má zachovať oba príslušné adresáre so serverom. Pri ďalšej výmene sa dá pred zásahom uložiť aktuálna úloha pomocou `Export-ScheduledTask`. Úloha beží v prihlásenej relácii vlastníka PC.

## Návrat na predchádzajúci server

1. Ukonči skúšobné hry. Rozbehnutá herná relácia je v pamäti procesu a na iné zostavenie sa neprenesie.
2. Over, že `task-previous.xml` odkazuje na očakávaný adresár so súborom `Spell.Server.dll`. V PowerShelli rovnakého používateľa spusti nasledujúce príkazy.

```powershell
$folder = Join-Path $env:LOCALAPPDATA 'Spell\server'
$previous = Get-Content -Raw -LiteralPath (Join-Path $folder 'task-previous.xml')
Stop-ScheduledTask -TaskName 'Spell Duel API'
Register-ScheduledTask -TaskName 'Spell Duel API' -Xml $previous -Force
Start-ScheduledTask -TaskName 'Spell Duel API'
Invoke-RestMethod 'http://127.0.0.1:18874/healthz'
```

3. Po spustení musí lokálna kontrola vrátiť `ok`. Over vytvorenie hry a pripojenie hosťa cez QR. Pri návrate použi APK kompatibilné so zvoleným serverom. Pre opätovné nasadenie tohto vydania použi `task-current.xml`.
4. Pri obnove PC vráť zo súkromnej zálohy aj poverenia Cloudflare, konfiguráciu PostgreSQL, aplikačný adresár Data Protection a prístupové práva. Strata kľúčov Data Protection zneplatní už vydané oprávnenia čakajúcich diagnostických dávok.

## Obnova databázy

PostgreSQL má samostatný kontajner `spell-postgres` a pomenovaný dátový zväzok. Záloha obsahuje presety, odtlačky oprávnení a diagnostiku. Uchovávaj ju iba v súkromnom úložisku. Dobu uchovávania záloh zosúlaď so sedemdňovou retenciou diagnostiky pred skúškou s osobnými údajmi.

V kontajneri vytvor zálohu pomocou `pg_dump -U spell -d spell --format custom --file /tmp/spell.dump`. Príkazom `docker cp spell-postgres:/tmp/spell.dump <súkromný-súbor>` prenes zálohu mimo kontajnera. Pri obnove vytvor novú databázu s odlišným názvom a cez `pg_restore -U spell -d <nová-databáza> --exit-on-error /tmp/spell.dump` obnov jej obsah. Porovnaj počet záznamov v tabuľkách `spell_presets`, `spell_founder_grants`, `spell_diagnostic_batches` a `spell_diagnostic_events`. Až po skúške vyber obnovenú databázu v súkromnej konfigurácii servera a reštartuj ho medzi reláciami. V samostatnej lokálnej QA inštancii bola obnova týmto postupom vykonaná a počty v štyroch tabuľkách sa zhodovali.
