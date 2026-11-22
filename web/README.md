# Stavová stránka Spell

Verejná adresa je [spell.arnavigator.site](https://spell.arnavigator.site). Stránka uvádza výsledky skúšok a zoznam vydaných APK.

## Publikovanie z koreňa repozitára

1. Aktualizuj `web/status.md` podľa výsledkov vývoja alebo fyzickej skúšky. Pri každom APK doplň vydanie do `CHANGELOG.md`. Prvé vydanie nahradí úvodnú vetu o príprave APK. Najnovšie vydanie patrí navrch.
2. Spusti `python web/site.py publish` pomocou Pythonu 3.10 alebo novšieho. Príkaz vygeneruje súbor z oboch zdrojov a atómovo nahradí publikovanú stránku. Publikovanie počas bežiaceho servera nevyžaduje reštart.
3. Otvor verejnú adresu a over príslušný údaj o zostave alebo míľniku. Na porovnanie obsahu môžeš použiť nižšie uvedené príkazy.

```powershell
$published = Join-Path $env:LOCALAPPDATA 'Spell\status\index.html'
$download = [IO.Path]::GetTempFileName()
try {
    curl.exe --fail --silent --show-error --output $download 'https://spell.arnavigator.site/'
    if ($LASTEXITCODE -ne 0) { throw 'Verejná stránka je nedostupná.' }
    if ((Get-FileHash $published).Hash -ne (Get-FileHash $download).Hash) {
        throw 'Verejná stránka sa líši od pripravenej verzie.'
    }
} finally {
    Remove-Item -LiteralPath $download
}
```

Zdroje podporujú obyčajné odseky, nadpisy `##` a `###`, odrážky `-`, tučné písmo a krátky kód v spätných apostrofoch. Úvodný nadpis `#` nahrádza šablóna titulkom sekcie.

## Prevádzka na hostiteľovi

Po prihlásení prevádzkového používateľa Windows spúšťa naplánované úlohy `Spell Status Origin` a `Spell Status Tunnel`. Pri chybe zopakujú spustenie. Verejná dostupnosť závisí od zapnutého počítača, aktívnej používateľskej relácie a internetového pripojenia.

- Origin spúšťa `python web/site.py serve` a obsluhuje HTML na `127.0.0.1:18873`. Pre inú lokálnu skúšku možno obom príkazom odovzdať `--page C:\cesta\index.html` a serveru `--port`.
- Samostatný tunel `spell-status` smeruje hostiteľa `spell.arnavigator.site` na tento port. Konfigurácia a poverenia tunela patria do používateľského priečinka mimo Gitu.
- Pri obnove hostiteľa najprv vygeneruj stránku, obnov poverenia tunela zo súkromnej zálohy a spusti origin a konektor. Over lokálnu odpoveď, pripojenie cez `cloudflared tunnel info spell-status` a zhodu verejnej stránky.
