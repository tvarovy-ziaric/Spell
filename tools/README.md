# Vydanie testovacieho APK

Na pracovnom počítači používa každá zostava rovnaký lokálny register v `%LOCALAPPDATA%\Spell\releases`. Zálohuj ho spolu s vydaniami. Na ďalší hostiteľ ho prenes pred pridelením verzie. Obsahuje rezervácie, pôvodné APK, kontrolné súčty a stav doručenia. Poverenia Telegramu sa čítajú z operátorom nastavenej cesty `SPELL_TELEGRAM_CONFIG` mimo repozitára. Očakávané vlastnosti sú `TELEGRAM_BOT_TOKEN` a `TELEGRAM_CHAT_ID`.

1. Over testy, commitni zdrojové súbory a z čistého pracovného stromu spusti `python tools/release.py reserve 0.1.0-dev --change "Opis zmeny" --verified "Konkrétny výsledok skúšky" --status "Stručný stav aplikácie"`. Nahraď príklady faktami. Príkaz je spoločným miestom prideľovania čísla a vypíše cestu k súboru `request.json`.
2. Aktuálny postup používa lokálny Unity Editor s Android Build Support a aktívnou licenciou. Vyber **Spell → Build reserved APK**. Cestu k rezervácii môžeš nastaviť cez `SPELL_BUILD_REQUEST` alebo súbor vybrať priamo v editore.
3. Po úspešnom zostavení odovzdá editor rezerváciu príkazu `python tools/release.py finish "cesta k request.json"`. Ten cez Android SDK `aapt` overí manifest APK a vypočíta SHA-256. Zapíše jeden záznam do `CHANGELOG.md`, aktualizuje posledné APK vo `web/status.md`, zverejní stránku a porovná obsah verejnej HTTPS odpovede. Overí identitu bota aj súkromný chat a odošle originálne APK. Úspešnému odoslaniu musí zodpovedať meno, veľkosť, chat a ID správy z Telegramu. Zmeny v changelogu a stave ulož do Gitu.

Ak Android SDK leží mimo bežnej cesty, nastav `ANDROID_SDK_ROOT` alebo úplnú cestu `SPELL_AAPT`. Pre už nakonfigurovaný lokálny Telegram Bot API server podporuje nástroj `SPELL_TELEGRAM_LOCAL_API_PORT` na loopbacku. Verejná Bot API má pre jeden súbor limit 50 MB.

## Obnova prerušeného odovzdania

Po výpadku zopakuj `finish` nad tým istým súborom žiadosti. Potvrdené doručenie sa pri opakovaní preskočí. Zlyhanie webu umožní odoslanie APK a ďalší pokus dokončí publikovanie. Pri výslovnom odmietnutí Telegramom zostáva odoslanie pripravené na ďalší pokus.

Pri strate odpovede po začatí nahrávania skontroluj v histórii súkromného chatu presné APK. Potvrdené doručenie zaeviduj cez `python tools/release.py resolve "cesta k request.json" --message-id ČÍSLO`. Až po potvrdení, že dokument v chate chýba, použi `--confirmed-absent` a zopakuj `finish`. Pri overení nástroja spusti `python tools/test_release.py`; skúšky používajú dočasné súbory a simulovaný Telegram.
