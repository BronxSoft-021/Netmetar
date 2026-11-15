BRONXSOFT NETMETAR

Jednostavan merač internet saobraćaja za Windows u LCD stilu.

Pokretanje:
- prečica "BronxSoft Netmetar" na desktopu (dist\BronxSoft Netmetar.exe, ~870 KB)
- sa Windowsom se pokreće sam (samo mini prikaz i ikonica)
- treba .NET 10 Desktop Runtime na računaru

Šta je u ovom folderu:
- Program.cs          program (merenje svake sekunde, istorija, tray, jedna kopija programa)
- Merenje.cs          merenje saobraćaja i spisak programa na internetu
- Baza.cs             SQLite istorija i obračunski periodi (SQLite iz samog Windowsa)
- Lcd.cs, Prozori.cs  izgled (LCD displej, mini prikaz, podešavanja)
- Dijalozi.cs         prozori O PROGRAMU i POMOĆ
- Pomoc.cs            tekst pomoći ("srpski | english"); kad se program menja, menja se i ovaj tekst
- Podesavanja.cs      podešavanja, dnevnik grešaka, pokretanje sa Windowsom
- Izmene.cs           istorija verzija (Šta je novo); novi unos uvek na vrh; red je "srpski | english"
- Jezik.cs            srpski ili engleski (podešavanje), Jez.T("sr", "en")
- Netmetar.csproj     projekat (.NET 10, bez dodatnih paketa)
- Napravi exe.bat     pravi dist\BronxSoft Netmetar.exe (pre toga zatvori program: ikonica -> Izlaz)
- Napravi precicu.bat prečica na desktopu
- assets\app.ico      ikonica
- dizajn\             slike izgleda (1_glavni_prikaz.png je odobren) i HTML maketa
- fonts\              segmentni fontovi DSEG7 i DSEG14 i font slova Rajdhani (besplatni, licenca SIL OFL)

Podaci: %APPDATA%\BronxSoft Netmetar\ (netmetar.db, podesavanja.json, greske.log)

Verzije (broj se računa iz istorije u Izmene.cs: 0.Y.Z, Y = sve novo, Z = sve dorade; nikad na nulu):
- 0.59.45 beta  (15. 11. 2026) tačna potrošnja po programu: pomoćni program (ETW, admin) + ▼/▲ i DANAS GB u dijalogu veza
- 0.57.45 beta  (15. 11. 2026) klik na program otvara njegove veze (portovi SLUŠA + adrese); tačka za program koji sluša
- 0.55.45 beta  (15. 11. 2026) MAX piše i jedinicu brzine (Mbit/s, MB/s, KB/s)
- 0.55.44 beta  (15. 11. 2026) brzina linka i greške kartice u zaglavlju, % iskorišćenosti veze, PRENOS (koliko i koliko dugo)
- 0.52.44 beta  (15. 11. 2026) Mbit/s je podrazumevana jedinica brzine; MAX (najbrža sekunda danas) piše ispod grafikona
- 0.50.44 beta  (15. 11. 2026) MES: dani perioda, projekcija do kraja perioda, ▼/▲ podela; izvoz perioda u CSV (meni ikonice ili --izvoz)
- 0.47.43 beta  (7. 11. 2026) prosek po danu u istoriji; tastatura: D, N, M i Space (pauza)
- 0.45.43 beta  (5. 11. 2026) ikonice programa, vreme na internetu danas po programu, Čuvar (lampica za program koji mora da ostane na vezi, sa vremenom prekida)
- 0.41.43 beta  (4. 11. 2026) mini prikaz je providan i pun kad miš pređe preko njega, providnost se bira u Opcijama; smanjen gornji deo mini prikaza
- 0.38.42 beta  (4. 11. 2026) download i upload su iste veličine: brojevi, strelice i grafikoni
- 0.38.41 beta  (4. 11. 2026) glavni prozor bez crnog okvira: providan, zaobljene ivice i blaga senka; promena veličine čuva oblik uređaja
- 0.38.38 beta  (4. 11. 2026) red dugmadi se sam raspoređuje prema jeziku: širina prema natpisima, ikonica i natpis centrirani, poravnato sa ekranom
- 0.38.35 beta  (4. 11. 2026) dugme POMOĆ (i F1, i u meniju): šta šta radi i zašto; pregled koda i ispravke: dve kopije odjednom, font natpisa, greška pri promeni jezika tastature, mesto prozora, upis pri gašenju Windowsa, dijalozi kad je prozor smanjen, jezik mini prikaza
- 0.35.29 beta  (3. 11. 2026) ekran PROGRAMI: svi programi na internetu (klik na naslov "NA INTERNETU SADA"), pomeranje točkićem miša; samo broj veza
- 0.32.29 beta  (3. 11. 2026) brojevi u tekstu (datumi, "7 DANA") u novom fontu, čisti brojevi ostaju segmentni; pun naziv programa sa pravilnim pisanjem (Microsoft Edge, Cross Device Service)
- 0.31.28 beta  (3. 11. 2026) novi font slova na LCD natpisima (Rajdhani SemiBold), brojevi ostaju segmentni (DSEG); vraćene kvačice (Š, Č, Ž)
- 0.31.25 beta  (3. 11. 2026) prevod na engleski u podešavanjima (Jezik / Language): prozori, meni, istorija, tooltip, podešavanja i spisak verzija; naslov u O PROGRAMU u fontu zaglavlja
- 0.30.24 beta  (3. 11. 2026) ime UnderGRAND zamenjeno imenom BronxSoft (naslov, exe, prečica); podaci se sami prebacuju iz %APPDATA%\UnderGRAND Netmetar
- 0.29.22 beta  (3. 11. 2026) dugmad OPCIJE i O PROGRAMU kao na slici: nacrtane ikone (zupčanik, info), veća slova, šira dugmad
- 0.29.20 beta  (3. 11. 2026) O PROGRAMU kompaktniji (oko polovine glavnog prozora); spisak svih verzija sa detaljima šta je u kojoj urađeno
- 0.29.18 beta  (3. 11. 2026) prozor O PROGRAMU: glatki zaobljeni uglovi, prevlačenje mišem, manji kada je glavni prozor mali
- 0.28.16 beta  (3. 11. 2026) istorija izmena u prozoru O PROGRAMU (Šta je novo): spisak sa datumom, brojem i vrstom (Novo / Dorada), točkić miša ili strelice za pomeranje; verzija se čita iz Izmene.cs
- 0.25.16 beta (3. 11. 2026) tooltip u tray-u bez imena i verzije programa, samo brzine i dnevni zbir (GB se više ne odseca)
- 0.24.15 beta  (3. 11. 2026) novi O PROGRAMU prozor (ikonica, oval sa verzijom, X, kvačice); glavni prozor bez Windows okvira: oval sa verzijom u zaglavlju, svoja dugmad za smanjivanje i zatvaranje, prevlačenje za zaglavlje, promena veličine sa ivica
- 0.21.14 beta  (2. 11. 2026) O PROGRAMU kao svoj prozor umesto Windows poruke
- 0.19.13 beta  (2. 11. 2026) ispravljene oznake dugmadi OPCIJE i O PROGRAMU, verzija u naslovu
- 0.18.10 beta  (2. 11. 2026) zadržana originalna ikonica u tray-u, dugmad OPCIJE i O PROGRAMU
- 0.16.9 beta  (2. 11. 2026) ispravljen tooltip u tray-u, animirane strelice u ikonici
- 0.15.6 beta  (2. 11. 2026) poravnata statistika u istoriji (četiri jednake kolone)
- 0.14.5 beta  (2. 11. 2026) centrirani natpisi u mini prikazu
- 0.14.2 beta  (2. 11. 2026) mini prikaz ispravno menja KB/s i MB/s
- 0.14.1 beta  (2. 11. 2026) automatsko prebacivanje KB/s na MB/s pri velikom protoku; jedinice se nezavisno prikazuju za download i upload
- 0.11.0 beta  (2. 11. 2026) download grafikon, izbor KB/s ili MB/s u podešavanjima, jedinica se prikazuje u glavnom prozoru, mini prikazu i tray tooltip-u
- 0.8.0 beta   (2. 11. 2026) početak razvoja u C#: brzina uživo sa grafikonom, zbirovi danas/nedelja/mesec,
              obračunski mesec sa danom resetovanja, istorija (DAN/NED/MES), programi na internetu,
              pauza (S/S), mini prikaz iznad sata, ikonica u tray-u, pokretanje sa Windowsom; .exe ~380 KB
- pre toga    (31. 10. - 1. 11. 2026) specifikacija i dizajn, pa probe u Pythonu i C#-u


Changelog: see CHANGELOG.md.
