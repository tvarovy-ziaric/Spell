# Pravidlá duelu v mobile

Pred vytvorením hry otvor **Nastavenia zakladateľa**. Zadaj heslo od prevádzkovateľa. Zaškrtnutím „Zapamätať oprávnenie“ uložíš na zariadení 30-dňové oprávnenie. Heslo ostáva iba počas odomknutia. Android Keystore chráni šifrovací kľúč k oprávneniu a náhodnej identite inštalácie. Odhlásenie aktuálne oprávnenie zruší. Po opätovnom odomknutí na tom istom zariadení vidíš svoju históriu. Po vymazaní dát aplikácie alebo reinštalácii vznikne nová identita.

Uprav hodnoty a stlač **Uložiť a vybrať pravidlá** alebo otvor históriu a ťukni na dátum. Každé uloženie pridá verziu. V histórii sa zobrazí desať najnovších záznamov a tlačidlo na staršie. Tlačidlom **Vytvoriť hru** uzamkneš vybranú verziu pre oboch hráčov. Hosť načíta QR a dostane kópiu účinných pravidiel. Bez vlastnej voľby hra použije základné skúšobné pravidlá.

| Parameter | Povolený rozsah |
| --- | --- |
| Životy | 1–10 000 |
| Poškodenie a postih aury | 0–10 000 |
| Rýchlosť | 0,1–100 m/s |
| Dosah | 0,1–300 m |
| Polomer účinku | 0–30 m |
| Varovanie aury | 0,1–30 m |
| Kolízia aury | Od 0,1 m po vzdialenosť varovania |

## Lokálny server

Príkaz `python tools/provision_admin.py --directory "$env:LOCALAPPDATA/Spell/server"` vytvorí pre Spell lokálny PostgreSQL v Dockeri. Kontajner počúva na 127.0.0.1:18875, dáta má v samostatnom zväzku. Súkromný priečinok obsahuje `admin.config.json` a `founder-password.txt`. Zakladateľovi poskytni heslo súkromne. Pri rotácii hesla zruš v databáze aj aktívne oprávnenia. Na obnovu uchovaj zálohu databázy a súkromnej konfigurácie.

Server načíta konfiguráciu zo štandardného adresára Spell/server pre aktuálneho používateľa alebo z cesty v `SPELL_ADMIN_CONFIG`. Pripojenie a hash hesla možno nahradiť premennými `SPELL_POSTGRES` a `SPELL_ADMIN_PASSWORD_HASH`. Pri štarte v kontajneri pripoj konfiguráciu ako súbor iba na čítanie a použi adresu lokálnej databázy dosiahnuteľnú zo siete kontajnera. Verejné vytvorenie hry so základnými pravidlami funguje aj počas výpadku databázy. Oprávnenie vytvoriť súkromnú reláciu v serverovom režime s `SPELL_FOUNDER_KEY` ostáva samostatné.

## Kontrakt a automatická skúška

`POST /v1/admin/grant` prijme heslo a 256-bitovú identitu inštalácie cez HTTPS. Server vráti dočasný token. V databáze uchováva iba odtlačky identity a tokenu. `DELETE /v1/admin/grant` zruší aktuálny token. `POST /v1/admin/presets` s tokenom zakladateľa obsahuje `version=1`, UUID `requestId` a úplné `rules`. Rovnaký pokus pri prerušení spojenia vráti pôvodnú verziu; rozdielny obsah pri rovnakom identifikátore vráti 409. História je zoradená zostupne podľa poradia uloženia, používa `GET /v1/admin/presets?before=...`. Výber pri vytvorení duelu používa hlavičky `Spell-Preset` a `Spell-Grant`. Obe vstupenky dostanú `rulesVersion`, `preset` a `rules`.

Pre testy vytvor samostatnú inštanciu s názvom `spell-admin-qa`, portom 18876 a priečinkom `$env:LOCALAPPDATA/Spell/qa/admin`. Po Release builde servera spusti `python qa/test_admin.py "$env:LOCALAPPDATA/Spell/qa/admin/admin.config.json"`. Každý beh uloží syntetické presety dvoch nových testovacích identít. Zahŕňa reštart testovacieho procesu, pripojenie oboch hráčov a kontrolu oddelenej histórie.
