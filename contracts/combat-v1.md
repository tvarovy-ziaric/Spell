# Experimentálny rozhodca Fireball v1

`Ready.combat_version = 1` zapínajú obaja účastníci syntetickej skúšky. Pôvodné potvrdzovanie bez tohto poľa funguje v diagnostickom režime. Zmiešaný pár nemôže strieľať. Pred aktiváciou na ihrisku je potrebné prijatie M2 vlastníkom.

**Súradnice.** Východ a sever sú lokálne metre vzhľadom na juhozápadný bod mriežky. Pre boj má zvislá zložka pózy jednotku a výškový systém terénu. Pri DMR je to výška Bpv, pri syntetickom kopci spoločná syntetická výška. Kamera smeruje v lokálnom +Z. Prevod do tohto rámca je zodpovednosťou lokalizácie.

**Vstup.** `ClientFrame.cast` odkazuje na najnovšie platné poradové číslo pózy a rastúce číslo príkazu konkrétneho hráča. Server používa vlastnú kópiu pózy, jej neistotu a pravidlá uzamknuté pri založení hry. Jedna vzorka pózy môže založiť najviac jeden let. Rozpracovaná relácia drží najviac 32 letov. Príkaz s nesprávnou pózou, počas pauzy alebo so zakázaným kúzlom sa spotrebuje bez vzniku letu. Po opätovnom spojení klient pokračuje od posledného spotrebovaného čísla svojho hráča.

**Dráha.** Server a mobil používajú `FireballPath.Trace` nad povrchom v RAM. Let končí na prvom trojuholníku terénu, na hranici ihriska alebo na nastavenom dolete. Neznámy povrch neumožní platnú predikciu. Bod na hranici ukončí let bez poškodenia. Pri konci doletu alebo zásahu terénu server porovná polohu súpera s guľovým polomerom AoE a overí spojnicu od dopadu k súperovi. Odhad chyby zahŕňa neistotu oboch pozícií a odchýlku smeru počas letu. Pri nejednoznačnom prekrytí sféry sa HP zachovajú. Testovacie hodnoty a konzervatívnu toleranciu musí potvrdiť meranie M2. Zásahy v tom istom čase sa vyhodnotia spolu.

**Bezpečnosť.** Aura používa vodorovnú vzdialenosť medzi hráčmi a súčet ich polohových neistôt. Pri možnom prekrytí platí výstraha s pauzou. Pri potvrdenom kontakte v začatom súboji vstúpi raz do platnosti postih oboch z presetu. Oddelenie aúr sa musí potvrdiť aj po započítaní neistoty. Strata spojenia, čerstvej pózy alebo hranice ruší prebiehajúce lety bez poškodenia. Poradie hry a HP zostávajú zachované. Mimo platného sledovania je stav dohľadnosti `unknown`.

**Prenos.** Server kontroluje čas po 50 ms a pri vstupoch. Let používa monotónny herný čas. `Snapshot.combat` má vlastnú verziu, HP oboch hráčov, všetky aktívne lety, výsledky posledných ôsmich letov, stav aury, dohľadnosť a posledné čísla príkazov. Nový snapshot nahrádza predchádzajúci aj pri opätovnom pripojení. Klient spracuje efekt dopadu iba raz pre rovnaké ID letu. Výsledok `interrupted` odstráni vizuál bez dopadového efektu. `finished` pri nulových HP ukončí boj pre obe strany. Obnovu procesu servera táto dočasná relácia neprežíva.

Automatizované kontroly sú v `qa/CombatChecks.cs` a v skúške dvoch skutočných lokálnych WebSocket spojení v `qa/Program.cs`. Spúšťajú sa cez `python qa/check.py`.
