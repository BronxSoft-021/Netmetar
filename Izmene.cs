// Version history, shown in the "ŠTA JE NOVO" list of the About window.
// Every change that matters to the user = a new entry ON TOP: date, what was NEW and what was a REFINEMENT or fix.
// The number is never written by hand (same rule as Bronxamp): it is 0.Y.Z, where Y counts every new thing since
// the start and Z every refinement or fix, and neither ever goes back to zero. Numerisi works it out from the lists.
// The label (Novo / Dorada) is computed from the lists. The top entry is the current version.
// Keep every line short (about 45 characters): the list does not wrap.
// A line is "srpski | english" (the text after " | " is shown when English is chosen).
namespace Netmetar;

static class Izmene
{
    public sealed record Unos(string Datum, string[] Nove, string[] Dorade)
    {
        /// <summary>The number, worked out from the whole history (0.Y.Z); never typed by hand.</summary>
        public string Broj { get; internal set; } = "";

        /// <summary>All the lines of this version, the new things first.</summary>
        public string[] Stavke => [.. Nove, .. Dorade];

        /// <summary>The lines in the current language.</summary>
        public string[] Linije => [.. Stavke.Select(Jez.Izaberi)];

        /// <summary>„Novo“ and/or „Dorada“, worked out from the lists (translated when shown).</summary>
        public IEnumerable<string> Vrste
        {
            get
            {
                if (Nove.Length > 0) yield return Jez.T("Novo", "New");
                if (Dorade.Length > 0) yield return Jez.T("Dorada", "Refinement");
            }
        }

        /// <summary>What the list shows next to the number: „NOVO · DORADA“.</summary>
        public string Oznaka => string.Join(" · ", Vrste).ToUpperInvariant();
    }

    public static readonly Unos[] Spisak = Numerisi(
    [
        new("16.11.2026",
        [
            "Zum prozora: Ctrl+točkić i dupli klik | Window zoom: Ctrl+wheel and a double click",
            "Ctrl+0 vraća prozor na 100% | Ctrl+0 puts the window back to 100%",
        ],
        [
            "Prozor: najmanje 700, najviše 2× | Window: at least 700, at most 2×",
        ]),
        new("15.11.2026",
        [
            "Tačna potrošnja po programu (merač) | Exact traffic per program (the meter)",
            "▼/▲ i DANAS GB u dijalogu veza | ▼/▲ and TODAY GB in the connections dialog",
        ],
        []),
        new("15.11.2026",
        [
            "Klik na program: spisak njegovih veza | Click a program: its connections",
            "Tačka za program koji sluša port | A dot for a program that listens",
        ],
        []),
        new("15.11.2026",
        [],
        [
            "MAX piše i jedinicu brzine | MAX shows the speed unit too",
        ]),
        new("15.11.2026",
        [
            "Brzina linka i greške kartice | Link speed and adapter errors",
            "Iskorišćenost veze u procentima | How much of the link is used",
            "PRENOS: koliko je preneto i koliko traje | TRANSFER: how much has moved, how long",
        ],
        []),
        new("15.11.2026",
        [
            "Mbit/s kao jedinica brzine | Mbit/s as a speed unit",
            "MAX: najbrža sekunda danas | MAX: today's fastest second",
        ],
        []),
        new("15.11.2026",
        [
            "MESEC: dani perioda, dan po dan | MONTH: days of the period, day by day",
            "Projekcija potrošnje do kraja perioda | Projection to the end of the period",
            "Izvoz perioda u CSV (meni ikonice) | Export the period to CSV (icon menu)",
        ],
        [
            "Tabela istorije se pomera točkićem | History table scrolls with the wheel",
        ]),
        new("07.11.2026",
        [
            "Prosek po danu u istoriji | Average per day in history",
            "Tastatura: D, N, M i Space | Keys: D, N, M and Space",
        ],
        []),
        new("05.11.2026",
        [
            "Ikonice programa na spisku | Program icons on the list",
            "Koliko je program danas bio na internetu | Time online today for each program",
            "Čuvar: pazi da program ostane na vezi | Watch: keeps a program online in sight",
            "Prekid se vidi i u mini prikazu | An outage shows on the mini display",
        ],
        []),
        new("04.11.2026",
        [
            "Mini prikaz je providan | The mini display is see-through",
            "Pun je kad miš pređe preko njega | It turns solid under the mouse",
            "Providnost se bira u Opcijama | Transparency is set in Options",
        ],
        [
            "Mini prikaz: smanjen gornji deo | Mini display: smaller top part",
        ]),
        new("04.11.2026",
        [],
        [
            "Download i upload su iste veličine | Download and upload are the same size",
        ]),
        new("04.11.2026",
        [],
        [
            "Glavni prozor bez crnog okvira | Main window without the black frame",
            "Zaobljene ivice i blaga senka | Rounded edges and a soft shadow",
            "Promena veličine čuva oblik uređaja | Resizing keeps the shape of the device",
        ]),
        new("04.11.2026",
        [],
        [
            "Lepši red dugmadi na oba jezika | Nicer button row in both languages",
            "Širina dugmadi prati natpise | Button widths follow their labels",
            "Ikonica i natpis centrirani u dugmetu | Icon and label centered in the button",
        ]),
        new("04.11.2026",
        [
            "Dugme POMOĆ (i F1): šta šta radi i zašto | HELP button (and F1): what does what and why",
            "Dve kopije više ne mogu da rade odjednom | Two copies can no longer run at once",
            "Pamti se mesto i veličina velikog prozora | Big window place and size are remembered",
        ],
        [
            "Natpisi su uvek u istom fontu | Labels always use the same font",
            "Nema greške pri promeni jezika tastature | No error when the keyboard language changes",
            "Upis i pri gašenju Windowsa | Data is saved when Windows shuts down",
            "Mini prikaz odmah menja jezik | Mini display changes language at once",
            "Dijalog se vidi i kad je prozor smanjen | Dialogs show when the window is minimized",
            "Ikonica u tray-u troši manje | Tray icon uses less CPU",
        ]),
        new("03.11.2026",
        [
            "Ekran PROGRAMI: svi programi na internetu | PROGRAMS screen: all programs online",
            "Klik na „NA INTERNETU SADA“ | Click the “ONLINE NOW” heading",
            "Pomeranje točkićem miša | Scroll with the mouse wheel",
        ],
        []),
        new("03.11.2026",
        [
            "Pun naziv programa, pravilno pisanje | Full program names, written properly",
        ],
        [
            "Brojevi u tekstu (datumi, 7 DANA) u novom fontu | Digits in text (dates, 7 DAYS) use the new font",
        ]),
        new("03.11.2026",
        [],
        [
            "Novi font slova na LCD natpisima | New letter font on the LCD labels",
            "Brojevi ostaju segmentni | Digits stay in segments",
            "Vraćene kvačice (Š, Č, Ž...) | Diacritics are back (Š, Č, Ž...)",
        ]),
        new("03.11.2026",
        [
            "Prevod na engleski u podešavanjima | English translation in the settings",
        ],
        [
            "Naslov u O PROGRAMU u fontu zaglavlja | ABOUT title in the header font",
        ]),
        new("03.11.2026",
        [
            "Podaci se sami prebacuju u novi folder | Data moves to the new folder by itself",
        ],
        [
            "Ime UnderGRAND zamenjeno imenom BronxSoft | Name UnderGRAND replaced by BronxSoft",
            "Novi exe: BronxSoft Netmetar.exe",
        ]),
        new("03.11.2026",
        [],
        [
            "Dugmad OPCIJE i O PROGRAMU kao na slici | OPTIONS and ABOUT buttons as in the mockup",
            "Nacrtane ikone (zupčanik, info) i veća slova | Drawn icons (gear, info), larger labels",
        ]),
        new("03.11.2026",
        [],
        [
            "O PROGRAMU je manji i kompaktniji | ABOUT is smaller and more compact",
            "Vidi se istorija svih verzija sa detaljima | Full version history with details",
        ]),
        new("03.11.2026",
        [
            "Prozor se može pomerati mišem | The window can be dragged with the mouse",
        ],
        [
            "O PROGRAMU: glatki zaobljeni uglovi | ABOUT: smooth rounded corners",
            "Manji je kada je glavni prozor mali | Smaller when the main window is small",
        ]),
        new("03.11.2026",
        [
            "Istorija izmena u prozoru O PROGRAMU | Change history in the ABOUT window",
            "Datum, broj i vrsta (Novo / Dorada) | Date, number and kind (New / Refinement)",
            "Pomeranje točkićem miša ili strelicama | Scroll with the mouse wheel or arrow keys",
        ],
        []),
        new("03.11.2026",
        [
            "Tooltip u tray-u bez imena programa | Tray tooltip without the program name",
        ],
        [
            "„GB“ se više ne odseca u tooltip-u | “GB” is no longer cut off in the tooltip",
        ]),
        new("03.11.2026",
        [
            "Glavni prozor bez Windows okvira | Main window without the Windows frame",
            "Oval sa verzijom i svoja dugmad u zaglavlju | Version pill and own buttons in the header",
            "Prevlačenje za zaglavlje, veličina sa ivica | Drag by the header, resize from the edges",
        ],
        [
            "Novi izgled O PROGRAMU (ikonica, kvačice) | New ABOUT look (icon, diacritics)",
        ]),
        new("02.11.2026",
        [
            "Svoj O PROGRAMU umesto Windows poruke | Own ABOUT window, not a Windows message",
            "Dugme ZATVORI i verzija u LCD stilu | CLOSE button and version in LCD style",
        ],
        [
            "Uklonjen ružan „\\n“ tekst iz prozora | Removed the stray “\\n” text",
        ]),
        new("02.11.2026",
        [
            "Verzija u naslovu prozora | Version in the window title",
        ],
        [
            "Ispravljeni preveliki natpisi na dugmadima | Fixed oversized button labels",
            "Kompaktnija slova, prave širine dugmadi | Compact lettering, proper button widths",
            "Zadržan Classic izgled i srednja dugmad | Classic look and middle buttons kept",
        ]),
        new("02.11.2026",
        [
            "Dugme OPCIJE dole levo | OPTIONS button bottom left",
            "Dugme O PROGRAMU dole desno | ABOUT button bottom right",
        ],
        [
            "Zadržana originalna ikonica | Original icon kept",
        ]),
        new("02.11.2026",
        [
            "Strelice trepere kada ima saobraćaja | Arrows blink when there is traffic",
        ],
        [
            "Ispravljen tekst tooltip-a u tray-u | Fixed the tray tooltip text",
            "Tooltip ograničen na 63 znaka (Windows) | Tooltip limited to 63 characters",
            "Download strelica zelena, upload narandžasta | Download arrow green, upload orange",
        ]),
        new("02.11.2026",
        [
            "Donja statistika u istoriji: četiri kolone | History bottom statistics: four columns",
        ],
        [
            "Natpisi i vrednosti na istoj osi | Labels and values on the same axis",
        ]),
        new("02.11.2026",
        [],
        [
            "Natpisi i jedinice centrirani u mini prikazu | Labels and units centered in the mini display",
            "Malo veći natpisi i strelice | Slightly larger labels and arrows",
            "„DANAS GB“ centrirano iznad vrednosti | “TODAY GB” centered above the value",
        ]),
        new("02.11.2026",
        [],
        [
            "Mini prikaz ispravno menja KB/s i MB/s | Mini display switches KB/s and MB/s correctly",
        ]),
        new("02.11.2026",
        [
            "KB/s se automatski prebacuje na MB/s | KB/s switches to MB/s automatically",
            "Download i upload biraju jedinicu nezavisno | Download and upload pick their unit separately",
            "Isto u prozoru, mini prikazu i tooltip-u | Same in the window, mini display, tooltip",
        ],
        [
            "Preciznije male MB/s vrednosti (npr. 8.75) | More precise small MB/s values (e.g. 8.75)",
        ]),
        new("02.11.2026",
        [
            "Download grafikon pored upload grafikona | Download graph next to the upload graph",
            "Podešavanje: prikaz u KB/s ili MB/s | Setting: show KB/s or MB/s",
            "Jedinica i u mini prikazu i tooltip-u | Unit in the mini display and tooltip too",
        ],
        []),
        new("02.11.2026",
        [
            "Brzina uživo (download, upload) i grafikon | Live speed (download, upload) and graph",
            "Zbirovi: danas, nedelja, mesec | Totals: today, week, month",
            "Obračunski mesec sa danom resetovanja | Billing month with a reset day",
            "Istorija (DAN / NED / MES) | History (DAY / WEEK / MONTH)",
            "Programi trenutno na internetu | Programs currently online",
            "Pauza (S/S) i ikonica u tray-u | Pause (S/S) and tray icon",
            "Mini prikaz iznad sata | Mini display above the clock",
            "Pokretanje sa Windowsom | Start with Windows",
        ],
        []),
    ]);

    /// <summary>
    /// 0.Y.Z counted from the whole history: Y is every new thing since the start, Z every refinement or fix.
    /// Neither ever goes back to zero (the same rule as Bronxamp), so the top entry carries the totals.
    /// </summary>
    static Unos[] Numerisi(Unos[] sve)
    {
        int novih = 0, dorada = 0;
        foreach (var u in sve.Reverse())
        {
            novih += u.Nove.Length;
            dorada += u.Dorade.Length;
            u.Broj = $"0.{novih}.{dorada}";
        }
        return sve;
    }
}
