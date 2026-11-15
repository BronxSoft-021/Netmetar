// Text of the Help window: what each part does and why. Every line is "srpski | english".
// When the program changes, change this text too: it must describe what the program really does.
namespace Netmetar;

static class Pomoc
{
    public sealed record Odeljak(string Naslov, string[] Stavke);

    public static readonly Odeljak[] Spisak =
    [
        new("ŠTA JE NETMETAR | WHAT NETMETAR IS",
        [
            "Meri koliko podataka prolazi kroz mrežnu karticu ovog računara: brzinu uživo i zbir po danu, nedelji i mesecu. | It measures how much data passes through the network card of this computer: live speed and totals per day, week and month.",
            "Zašto: da znaš koliko si potrošio i koliko je ostalo do kraja obračunskog meseca, bez čekanja na podatke provajdera. | Why: so you know how much you used and how long the billing month still lasts, without waiting for your provider's figures.",
            "Ništa ne šalje na internet i ne traži prava administratora. Svi podaci ostaju na ovom računaru. | It sends nothing to the internet and needs no administrator rights. All data stays on this computer.",
        ]),
        new("BRZINA I GRAFIKON | SPEED AND GRAPH",
        [
            "DOWNLOAD (▼) je ono što stiže na računar, UPLOAD (▲) ono što računar šalje. Brojevi se osvežavaju jednom u sekundi. | DOWNLOAD (▼) is what arrives at the computer, UPLOAD (▲) what it sends. The numbers refresh once a second.",
            "Stubići pokazuju poslednjih 30 sekundi. Najviši stubić je najveća brzina u tih 30 sekundi, da bi se i mali saobraćaj video. Zato visina stubića nije stalna mera. | The bars show the last 30 seconds. The tallest bar is the highest speed in those 30 seconds, so that small traffic is visible too. That is why bar height is not a fixed scale.",
            "Jedinica (KB/s, MB/s ili Mbit/s) bira se u Opcijama; podrazumevano je Mbit/s, kako brzinu pišu provajderi i speed testovi. U KB/s prikazu brzina od 1000 KB/s i više sama prelazi na MB/s, jer displej ima tri cifre. | The unit (KB/s, MB/s or Mbit/s) is chosen in Options; Mbit/s is the default, the way providers and speed tests write a speed. In KB/s mode a speed of 1000 KB/s or more switches to MB/s by itself, because the display has three digits.",
            "Ispod grafikona piše MAX: najveća brzina danas (sa jedinicom) i vreme kada je zabeležena. Ostaje i posle zatvaranja programa, a počinje iznova u ponoć. | Under the graph MAX shows the highest speed today (with its unit) and when it was recorded. It survives a restart and starts again at midnight.",
            "Uz DOWNLOAD piše koliko je veza iskorišćena (npr. 12% VEZE), a ako ih je bilo, i greške mrežne kartice od pokretanja programa. | Next to DOWNLOAD it shows how much of the link is used (e.g. 12% LINK) and, if there were any, network card errors since the program started.",
            "Gore desno u zaglavlju piše brzina koju je kartica dogovorila sa prekidačem (1 GBIT). Ako padne na manju (npr. 100 MBIT zbog lošeg kabla), ceo natpis postaje crven. | The header on the right shows the speed the card negotiated with the switch (1 GBIT). If it drops (e.g. 100 MBIT because of a bad cable), the whole text turns red.",
            "Kad nešto prenosi mnogo podataka, levo u zaglavlju piše PRENOS i koliko je preneto i koliko traje; nestaje posle nekoliko mirnih sekundi. | When something moves a lot of data, the header on the left shows TRANSFER with how much has moved and for how long; it goes away after a few quiet seconds.",
        ]),
        new("DANAS, NEDELJA, MESEC | TODAY, WEEK, MONTH",
        [
            "Svaki zbir je download i upload zajedno, jer se potrošnja obično tako računa. | Every total is download and upload together, because that is how usage is usually counted.",
            "NEDELJA ide od ponedeljka do nedelje. MESEC je obračunski mesec (vidi ispod), a ne kalendarski. | WEEK runs from Monday to Sunday. MONTH is the billing month (see below), not the calendar month.",
            "1 GB je 1000 MB, kao kod provajdera. Windows računa 1 GB = 1024 MB, pa kod njega isti saobraćaj izgleda malo manji. | 1 GB is 1000 MB, as with providers. Windows counts 1 GB = 1024 MB, so the same traffic looks a little smaller there.",
        ]),
        new("OBRAČUNSKI MESEC | BILLING MONTH",
        [
            "U Opcijama upiši dan kada ti provajder počinje novi obračun. Period traje od tog dana do dana pre sledećeg resetovanja. | In Options enter the day your provider starts a new billing period. The period runs from that day until the day before the next reset.",
            "Zašto: da se zbir MESEC poklapa sa obračunom provajdera, a ne sa kalendarom. Ako ti je obračun kalendarski, ostavi 1. | Why: so the MONTH total follows your provider's billing, not the calendar. If your billing follows the calendar, leave it at 1.",
            "Ako mesec nema taj dan (npr. 31. u novembru), reset je poslednjeg dana tog meseca. | If a month has no such day (e.g. the 31st in November), the reset is on the last day of that month.",
            "Stari periodi se ne brišu. I kad promeniš dan resetovanja, završeni periodi ostaju kakvi su bili. | Old periods are not deleted. Even when you change the reset day, finished periods stay as they were.",
        ]),
        new("DUGMAD DAN, NED, MES I S/S | DAY, WEEK, MONTH AND S/S BUTTONS",
        [
            "DAN, NED i MES otvaraju istoriju: veliki zbir i tabelu poslednjih 7 dana, 7 nedelja ili 7 obračunskih perioda, sa trakom za poređenje. Dole su ova i prošla nedelja i dva prethodna perioda. | DAY, WEEK and MONTH open the history: a big total and a table of the last 7 days, 7 weeks or 7 billing periods, with a bar for comparing them. Below are this and last week and the two previous periods.",
            "Isto dugme još jednom ili taster Esc vraća živi prikaz. Kod MES prvi klik daje obračunske periode, drugi dane u tekućem periodu, treći vraća živi prikaz. | The same button again, or the Esc key, returns to the live display. On MONTH the first click shows the billing periods, the second the days of the current period, the third returns to the live display.",
            "Dani perioda: tabela ide dan po dan (točkić miša ili strelice je pomeraju). Gore desno piše PROJEKCIJA - dokle bi period stigao na dosadašnjem tempu. Uz naslov tabele je podela ▼ primljeno i ▲ poslato za ceo period. | Days of the period: the table goes day by day (the mouse wheel or the arrows scroll it). The top right shows PROJECTED - where the period would end at the current pace. Next to the table heading is the split of the whole period into ▼ received and ▲ sent.",
            "Prosek po danu: na DAN piše prosek poslednjih 7 dana, a na NED i MES zbir podeljen brojem proteklih dana u toj nedelji ili obračunskom periodu. | Average per day: on DAYS it is the average of the last 7 days, and on WEEKS and MONTHS the total divided by the days that have passed in that week or billing period.",
            "S/S je pauza: dok je uključena, saobraćaj se ne broji i ne ulazi u zbirove. Posle ponovnog pokretanja programa pauza nije uključena, da merenje ne bi ostalo ugašeno greškom. | S/S is pause: while it is on, traffic is not counted and does not enter the totals. After the program restarts the pause is off, so metering cannot stay off by mistake.",
        ]),
        new("NA INTERNETU SADA | ONLINE NOW",
        [
            "Spisak programa koji upravo imaju otvorenu vezu ka drugom računaru. Početni ekran pokazuje prva četiri, a klik na naslov NA INTERNETU SADA otvara sve (točkić miša ili strelice pomeraju, Esc vraća). | The list of programs that have an open connection to another computer right now. The main screen shows the first four; a click on the ONLINE NOW heading opens all of them (mouse wheel or arrow keys scroll, Esc goes back).",
            "VEZE je broj veza, a ne količina podataka: program sa jednom vezom može da skida mnogo više od programa sa deset veza. | LINKS is the number of connections, not the amount of data: a program with one connection can download much more than one with ten.",
            "DANAS je koliko je program danas bio povezan sa internetom. To je vreme, a ne potrošnja: program koji samo drži otvorenu vezu skuplja vreme i kad ništa ne skida. Broji se i dok su prozori sakriveni, a ne broji se u pauzi. | TODAY is how long the program was connected to the internet today. It is time, not usage: a program that only keeps a connection open collects time even when it downloads nothing. It is counted while the windows are hidden too, but not during a pause.",
            "Zašto nema MB po programu: Windows to daje samo programima sa pravima administratora, a Netmetar radi bez njih. Procena po broju veza bi bila netačna, pa je nema. | Why there are no MB per program: Windows gives that only to programs with administrator rights, and Netmetar runs without them. An estimate from the number of connections would be wrong, so there is none.",
            "Procesi koji ne mogu da se pročitaju bez tih prava se preskaču. | Processes that cannot be read without those rights are skipped.",
            "Klik na program otvara njegove veze: portove na kojima sluša (SLUŠA) i otvorene veze sa adresama i portovima. Imena sajtova se ne traže, jer program ne ide na internet. | A click on a program opens its connections: the ports it listens on (LISTENS) and open connections with their addresses and ports. Website names are not looked up, because the program never goes online.",
            "Mala tačka posle imena znači da program sluša na nekom portu i može da primi vezu (server). Programi bez tačke se samo kače na druge (klijent). | A small dot after the name means the program listens on a port and can accept a connection (server). Programs without the dot only connect to others (client).",
        ]),
        new("TAČNA POTROŠNJA PO PROGRAMU | EXACT TRAFFIC PER PROGRAM",
        [
            "Uključi se u Opcijama. Windows tada jednom pita za dozvolu, jer se tačni bajtovi po programu mogu pročitati samo sa pravima administratora; glavni program ih ne traži. | Turn it on in Options. Windows then asks for permission once, because exact bytes per program can be read only with administrator rights; the main program does not ask for them.",
            "Pomoćni program se posle pokreće sam (zakazani zadatak) i šalje brojeve glavnom programu; kada se glavni program zatvori, gasi se i on. | The helper then starts by itself (a scheduled task) and sends the numbers to the main program; when the main program closes, it stops too.",
            "Klik na program pokazuje ▼ i ▲ brzinu i koliko je taj program preneo danas; brojevi se pamte po danu, kao i vreme na mreži. | A click on a program shows the ▼ and ▲ speed and how much that program moved today; the numbers are kept per day, like the time online.",
            "Isključivanjem u Opcijama program se zaustavlja. Uklanjanje zadatka: \"BronxSoft Netmetar Merac.exe\" --remove. | Turning it off in Options stops it. Removing the task: \"BronxSoft Netmetar Merac.exe\" --remove.",
        ]),
        new("ČUVAR | WATCH",
        [
            "Čuvar pazi na program koji mora da ostane na internetu, na primer na program za emitovanje. Uključuje se klikom na krug u koloni ČUVAR, na spisku svih programa, a isto tako se i isključuje. | The Watch looks after a program that must stay online, for example a broadcasting program. Turn it on with a click on the circle in the WATCH column of the full program list, and turn it off the same way.",
            "Pun krug znači da program ima vezu. Ako je izgubi na duže od 20 sekundi, a i dalje radi, krug postaje crven, a u mini prikazu i na ikonici kod sata piše PREKID. Zato se vidi i dok je veliki prozor sakriven. | A full circle means the program is connected. If it loses the connection for more than 20 seconds while it keeps running, the circle turns red, and DOWN is shown on the mini display and on the icon by the clock. So you see it even while the big window is hidden.",
            "Kratak prekid se ne računa, jer se programi sami ponovo povežu. Ni program koji si sam ugasio nije prekid: on piše kao UGAŠEN. Vreme poslednjeg prekida ostaje zapisano pored programa. | A short break does not count, because programs reconnect by themselves. A program you closed yourself is not an outage either: it is shown as NOT RUNNING. The time of the last outage stays written next to the program.",
            "Programi pod Čuvarom stoje na vrhu spiska, pa se vide i na početnom ekranu. | Watched programs stand at the top of the list, so they are visible on the main screen too.",
            "Čuvar vidi samo TCP veze. Veza znači da je program povezan, ali ne i da zaista šalje zvuk ili podatke. | The Watch sees TCP connections only. A connection means the program is connected, not that it really sends sound or data.",
        ]),
        new("MINI PRIKAZ | MINI DISPLAY",
        [
            "Mali displej uvek na vrhu, sa brzinama i današnjim zbirom, da stanje vidiš bez otvaranja velikog prozora. | A small display that is always on top, with the speeds and today's total, so you can see the state without opening the big window.",
            "Klik otvara veliki prozor, prevlačenje ga pomera (mesto se pamti), a desni klik otvara meni. Uključuje se i isključuje u meniju ili u Opcijama. | A click opens the big window, dragging moves it (the place is remembered) and a right click opens the menu. It is turned on and off in the menu or in Options.",
            "Kućište i ekran su providni, da ne štrči preko drugih programa, a brojevi ostaju puni. Kad miš pređe preko njega, ceo postaje pun. Providnost se bira u Opcijama. | The case and the screen are see-through, so it does not stand out over other programs, while the numbers stay solid. When the mouse moves over it, all of it turns solid. The transparency is chosen in Options.",
        ]),
        new("IKONICA KOD SATA I ZATVARANJE | ICON BY THE CLOCK AND CLOSING",
        [
            "Program meri i kad je prozor zatvoren. X samo sakriva prozor, jer bi se inače brojanje zaustavilo. Program se potpuno gasi sa Izlaz u meniju ikonice. | The program measures even when the window is closed. X only hides the window, because counting would stop otherwise. The program is fully closed with Exit in the icon's menu.",
            "Klik na ikonicu otvara prozor, desni klik meni, a kad zadržiš miša vidiš brzine i današnji zbir. | A click on the icon opens the window, a right click the menu, and hovering shows the speeds and today's total.",
            "Strelice na ikonici trepću kad ima saobraćaja: ▼ zeleno za download, ▲ svetlo za upload. | The arrows on the icon blink when there is traffic: ▼ green for download, ▲ bright for upload.",
        ]),
        new("OPCIJE | OPTIONS",
        [
            "Pokreni sa Windowsom: preporučeno, jer se saobraćaj broji samo dok program radi. | Start with Windows: recommended, because traffic is counted only while the program runs.",
            "Računaj i VPN adaptere: saobraćaj kroz VPN prolazi i kroz mrežnu karticu, pa se sa ovom opcijom broji dva puta. Zato je podrazumevano isključena. | Count VPN adapters too: traffic through a VPN also passes through the network card, so with this option it is counted twice. That is why it is off by default.",
            "U Opcijama piše i koje se mrežne kartice mere. | Options also show which network cards are measured.",
        ]),
        new("ZAŠTO SE BROJ RAZLIKUJE OD PROVAJDEROVOG | WHY THE NUMBER DIFFERS FROM THE PROVIDER'S",
        [
            "Broji se sav saobraćaj kroz karticu, pa i onaj u kućnoj mreži (npr. kopiranje na drugi računar), koji provajder ne računa. | All traffic through the card is counted, also traffic inside your home network (e.g. copying to another computer), which the provider does not count.",
            "Ne broji se saobraćaj drugih uređaja na istoj mreži: telefona, televizora, drugih računara. | Traffic of other devices on the same network is not counted: phones, TVs, other computers.",
            "Ne broji se ništa dok je program ugašen, dok računar ne radi i dok je uključena pauza. | Nothing is counted while the program is closed, while the computer is off or while pause is on.",
            "Virtuelni adapteri (VirtualBox, VMware, Hyper-V, WSL, Bluetooth) se ne broje, da se isti saobraćaj ne bi sabrao dva puta. | Virtual adapters (VirtualBox, VMware, Hyper-V, WSL, Bluetooth) are not counted, so the same traffic is not added twice.",
        ]),
        new("PODACI | DATA",
        [
            "Istorija i podešavanja su u folderu koji otvara dugme Otvori folder u Opcijama. Ne briši taj folder ako želiš da sačuvaš istoriju. | The history and settings are in the folder opened by the Open folder button in Options. Do not delete that folder if you want to keep the history.",
            "U bazu se upisuje jednom u minuti, da disk ne radi bez potrebe, i još jednom pri izlasku ili gašenju Windowsa. | The database is written once a minute, so the disk is not used needlessly, and once more on exit or when Windows shuts down.",
            "Podaci po minutu stariji od 90 dana sažimaju se u dnevne zbirove, da baza ostane mala. Dnevni zbirovi ostaju. | Per-minute data older than 90 days is folded into daily totals, so the database stays small. The daily totals remain.",
            "Izvezi period u CSV (meni ikonice): fajl sa svakim danom perioda, zbirom, prosekom i projekcijom, za Excel. | Export the period to CSV (icon menu): a file with every day of the period, the total, the average and the projection, for Excel.",
        ]),
        new("TASTERI I PROZOR | KEYS AND WINDOW",
        [
            "F1 otvara ovu pomoć. Esc vraća živi prikaz i zatvara ovaj prozor. | F1 opens this help. Esc returns to the live display and closes this window.",
            "Tasteri D, N i M otvaraju istoriju dana, nedelja i meseci (M drugi put dane u periodu), a Space uključuje i isključuje pauzu — isto kao dugmad ispod ekrana. U tabelama strelice gore i dole pomeraju listu. | D, N and M open the days, weeks and months history (M again the days of the period) and Space turns pause on and off - the same as the buttons under the display. In the tables the up and down arrows scroll.",
            "Prozor se pomera prevlačenjem zaglavlja, a veličina se menja prevlačenjem ivice. Mesto i veličina se pamte. | The window is moved by dragging its header and resized by dragging an edge. Its place and size are remembered.",
            "Klik na natpis sa stanjem u zaglavlju (npr. ● MERI) otvara Opcije. | A click on the status text in the header (e.g. ● METERING) opens Options.",
        ]),
    ];
}
