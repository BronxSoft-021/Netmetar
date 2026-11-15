# BronxSoft Netmetar

**[Srpski](#srpski) · [English](#english)**

![Netmetar](dizajn/1_glavni_prikaz.png)

---

## Srpski

Jednostavan merač internet saobraćaja za Windows, u izgledu starog LCD displeja, bez gomile opcija.
Program je na srpskom i engleskom (Opcije).

**Status: beta.**

### Šta radi

- Brzina preuzimanja i slanja uživo, jednom u sekundi, sa grafikonom poslednjih 30 sekundi i najvećom brzinom
  danas (KB/s, MB/s ili Mbit/s).
- Zbirovi za danas, ovu nedelju i obračunski mesec, sa danom resetovanja koji biraš sam i projekcijom do kraja
  perioda.
- Istorija po danima, nedeljama i obračunskim periodima, sa izvozom u CSV za Excel.
- Programi koji su upravo na internetu, sa ikonicama, vremenom provedenim na mreži danas, portovima i vezama.
  **Čuvar** javlja kad program pod nadzorom izgubi vezu.
- Tačna potrošnja po programu preko pomoćnog programa (`BronxSoft Netmetar Merac.exe`, traži administratorska
  prava jednom; uključuje se u Opcijama).
- Mali prikaz iznad sata, ikonica pored sata i pokretanje sa Windowsom.

Program ne šalje ništa na internet; samo meri saobraćaj na ovom računaru.

### Preuzimanje

Gotov program je na strani [Releases](../../releases): raspakuj zip i pokreni `BronxSoft Netmetar.exe`.

- Windows 10 ili 11, 64-bitni.
- Treba [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows ga ponudi pri
  prvom pokretanju ako ga nema).
- Program nije digitalno potpisan, pa Windows SmartScreen pri prvom pokretanju može da pita
  („Više informacija“ → „Svejedno pokreni“).
- Podaci programa (istorija i podešavanja) su u `%APPDATA%\BronxSoft Netmetar\`.

### Pravljenje iz koda

Treba [.NET 10 SDK](https://dotnet.microsoft.com/download). Program ne koristi dodatne pakete.

```bat
dotnet build Netmetar.csproj
```

`Napravi exe.bat` pravi `dist\BronxSoft Netmetar.exe` i pomoćni `dist\BronxSoft Netmetar Merac.exe`
(projekat u folderu `Merac/`). Istorija izmena je u [CHANGELOG.md](CHANGELOG.md) i u samom programu
(„Šta je novo“).

### Licenca

Kod Netmetra je pod licencom **MIT** (vidi [LICENSE](LICENSE)). Fontovi DSEG i Rajdhani su pod SIL Open Font
License 1.1; spisak je u [Licence.txt](Licence.txt).

### Autor

**BronxSoft**, Novi Sad. Program je besplatan i nema reklama.

---

## English

A simple internet traffic meter for Windows in the look of an old LCD display, without a pile of options.
The program speaks Serbian and English (Options).

**Status: beta.**

### Features

- Live download and upload speed, once a second, with a graph of the last 30 seconds and today's top speed
  (KB/s, MB/s or Mbit/s).
- Totals for today, this week and the billing month, with a reset day you choose and a projection to the end
  of the period.
- History by days, weeks and billing periods, with export to CSV for Excel.
- The programs that are on the internet right now, with icons, time spent online today, ports and connections.
  The **Watch** feature tells you when a watched program loses its connection.
- Exact traffic per program through a small helper (`BronxSoft Netmetar Merac.exe`; it asks for administrator
  rights once and is turned on in Options).
- A mini view above the clock, a tray icon and start with Windows.

The program sends nothing to the internet; it only measures the traffic on this computer.

### Download

The ready program is on the [Releases](../../releases) page: unzip it and run `BronxSoft Netmetar.exe`.

- Windows 10 or 11, 64-bit.
- Needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows offers it on
  the first run if it is missing).
- The program is not code-signed, so Windows SmartScreen may ask on the first run
  ("More info" → "Run anyway").
- Program data (history and settings) is in `%APPDATA%\BronxSoft Netmetar\`.

### Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The program uses no extra packages.

```bat
dotnet build Netmetar.csproj
```

`Napravi exe.bat` builds `dist\BronxSoft Netmetar.exe` and the helper `dist\BronxSoft Netmetar Merac.exe`
(project in the `Merac/` folder). The change history is in [CHANGELOG.md](CHANGELOG.md) and in the program
itself (the "Šta je novo" window, "What's new"). Names in the code are in Serbian.

### License

Netmetar's own code is licensed under **MIT** (see [LICENSE](LICENSE)). The DSEG and Rajdhani fonts are under
the SIL Open Font License 1.1; the list is in [Licence.txt](Licence.txt).

### Author

**BronxSoft**, Novi Sad. The program is free and has no ads.
