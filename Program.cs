// BronxSoft Netmetar - internet traffic meter in the style of an old LCD display.
//   "BronxSoft Netmetar.exe"              big window (and the mini display, tray icon)
//   "BronxSoft Netmetar.exe" --skriveno   only the mini display and tray (start with Windows)
//   "BronxSoft Netmetar.exe" --snimak F   saves pictures of every screen into folder F and quits
//   "BronxSoft Netmetar.exe" --izvoz F    writes the current billing period to the CSV file F and quits
//   "BronxSoft Netmetar.exe" --programi F writes today's exact bytes per program (from the meter) to F and quits
using System.Drawing.Imaging;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Netmetar;

static class Program
{
    // A second start only asks the running copy, through this named pipe, to show its window
    static readonly string Kanal = "BronxSoft-Netmetar-" + Environment.UserName
        + (string.IsNullOrEmpty(Podesavanja.TestFolder) ? "" : "-test");

    /// <summary>Pipe of the helper that counts exact traffic per program (the same test rule).</summary>
    public static string KanalMeraca => "BronxSoft-Netmetar-Merac-" + Environment.UserName
        + (string.IsNullOrEmpty(Podesavanja.TestFolder) ? "" : "-test");

    // Held while the program runs. The pipe alone is not enough: it opens only after the start-up,
    // so two quick starts both missed it and both counted the same traffic into the database.
    static Mutex jedinaKopija;

    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);

    // Keeps the original app icon. Only its existing down/up arrows pulse.
    public static Icon IkonicaAktivnosti(Icon original, bool downloadAktivan, bool uploadAktivan, bool faza, Size velicina)
    {
        using var source = original.ToBitmap();
        int w = source.Width, h = source.Height;
        using var recolored = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        Color downPulse = downloadAktivan
            ? (faza ? Color.FromArgb(255, 70, 210, 95) : Color.FromArgb(255, 35, 105, 50))
            : Color.FromArgb(255, 244, 163, 56);
        Color upPulse = uploadAktivan
            ? (faza ? Color.FromArgb(255, 255, 245, 210) : Color.FromArgb(255, 135, 125, 105))
            : Color.FromArgb(255, 245, 232, 205);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color c = source.GetPixel(x, y);
            // Orange down arrow area; do not touch the similarly colored outer border.
            if (x >= w * 0.12 && x <= w * 0.49 && y >= h * 0.25 && y <= h * 0.62
                && c.A > 0 && c.R > 180 && c.G > 80 && c.G < c.R && c.B < 130)
            {
                float lum = c.R / 255f;
                recolored.SetPixel(x, y, Color.FromArgb(c.A,
                    (int)(downPulse.R * lum), (int)(downPulse.G * lum), (int)(downPulse.B * lum)));
            }
            // Cream up arrow area.
            else if (x >= w * 0.50 && x <= w * 0.90 && y >= h * 0.18 && y <= h * 0.60
                && c.A > 0 && c.R > 190 && c.G > 180 && c.B > 150)
            {
                float lum = c.R / 255f;
                recolored.SetPixel(x, y, Color.FromArgb(c.A,
                    (int)(upPulse.R * lum), (int)(upPulse.G * lum), (int)(upPulse.B * lum)));
            }
            else recolored.SetPixel(x, y, c);
        }

        using var small = new Bitmap(velicina.Width, velicina.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(recolored, 0, 0, velicina.Width, velicina.Height);
        }
        IntPtr hIcon = small.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        // Numbers and dates on the display do not follow the regional settings (decimal point, yyyy-MM-dd in the database).
        // Not InvariantGlobalization: with it WinForms throws when the keyboard language changes.
        CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        bool skriveno = args.Contains("--skriveno");
        int i = Array.IndexOf(args, "--snimak");
        string snimak = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        int j = Array.IndexOf(args, "--izvoz");
        string izvoz = j >= 0 && j + 1 < args.Length ? args[j + 1] : null;
        // per-program bytes of today, written to a file (for checking without the window)
        int k = Array.IndexOf(args, "--programi");
        if (k >= 0 && k + 1 < args.Length)
        {
            using (var b = new Baza(Podesavanja.Baza))
            {
                var redovi = b.ProgramiDanaBajtovi(DateOnly.FromDateTime(DateTime.Today))
                    .OrderByDescending(x => x.Value.d + x.Value.u)
                    .Select(x => $"{x.Key};{x.Value.d};{x.Value.u}");
                File.WriteAllLines(args[k + 1], redovi);
            }
            return 0;
        }

        if (snimak == null && izvoz == null)
        {
            jedinaKopija = new Mutex(true, Kanal, out bool prva);
            if (!prva)
            {
                JaviPrvojKopiji(skriveno);
                return 0;
            }
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Podesavanja.Log("Neuhvaćena greška", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Podesavanja.Log("Neuhvaćena greška", e.ExceptionObject as Exception);
        ApplicationConfiguration.Initialize();

        Netmetar n;
        try
        {
            Crtac.UcitajFontove();
            n = new Netmetar(skriveno || snimak != null || izvoz != null);
        }
        catch (Exception e)
        {
            // e.g. the database cannot be opened: say so instead of disappearing without a word
            Podesavanja.Log("Program nije mogao da se pokrene", e);
            if (snimak == null)
                MessageBox.Show(Jez.T($"Program ne može da se pokrene. Detalji su u fajlu greske.log u folderu:\n{Podesavanja.Folder}",
                                      $"The program cannot start. Details are in the file greske.log in the folder:\n{Podesavanja.Folder}"),
                    Podesavanja.Ime, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        if (snimak != null)
        {
            n.Snimi(snimak);
            return 0;
        }
        if (izvoz != null)
        {
            // the same file the menu writes, without the dialog (for checking and for scripts)
            return n.Izvezi(izvoz) ? 0 : 1;
        }
        Slusaj(n);
        Application.Run(n);
        return 0;
    }

    /// <summary>Another copy runs: ask it to show its window (it may still be starting, so it gets a few seconds).</summary>
    static void JaviPrvojKopiji(bool skriveno)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", Kanal, PipeDirection.Out);
            c.Connect(5000);
            AllowSetForegroundWindow(-1); // we were started by the user, so the running copy may come to the front
            var b = System.Text.Encoding.ASCII.GetBytes(skriveno ? "skriveno" : "prikazi");
            c.Write(b, 0, b.Length);
            c.Flush();
        }
        catch { /* the first copy is closing: nothing to ask */ }
    }

    static void Slusaj(Netmetar n)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(Kanal, PipeDirection.In, 1);
                    s.WaitForConnection();
                    var buf = new byte[32];
                    int k = s.Read(buf, 0, buf.Length);
                    if (System.Text.Encoding.ASCII.GetString(buf, 0, k).Trim() == "prikazi")
                        n.UNiti(n.PrikaziGlavni);
                }
                catch (Exception e)
                {
                    Podesavanja.Log("Kanal za jednu kopiju programa", e);
                    Thread.Sleep(5000);
                }
            }
        }) { IsBackground = true, Name = "kanal" };
        t.Start();
    }
}

/// <summary>Measuring, totals and history; the windows only read from here.</summary>
sealed class Netmetar : ApplicationContext, IStanje
{
    readonly Podesavanja p = Podesavanja.Ucitaj();
    readonly Baza baza;
    readonly Merac merac;
    readonly Pracenje pracenje;
    readonly MeracPrograma meracPrograma;
    Dictionary<string, (long d, long u)> bajtoviDanas = [];                    // exact traffic today, per program
    Dictionary<(string dan, string ime), (long d, long u)> bajtoviNaCekanju = [];
    readonly Queue<(double, double)> grafikon = new();
    Dictionary<string, long[]> naCekanju = [];   // minute -> [down, up], written once a minute
    string minut = KljucMinuta();
    int otkucaj;
    DateOnly? sazeto;
    DateOnly dan;
    (DateOnly pocetak, DateOnly kraj) period, ned;
    long kesDanas, kesNedelja, kesMesec;
    readonly Dictionary<string, Istorija> istorija = [];
    double maxDown, maxUp;              // today's best speeds and when they happened (table stanje)
    DateTime? maxDownKada, maxUpKada;
    bool maxPrljav;
    const double PragPrenosa = 5e6;     // bytes per second: about 40 Mbit/s starts a "transfer"
    bool prenos;
    long prenosOd, prenosBajtova;
    int prenosMirovanje;

    readonly Icon ikona;
    readonly GlavniProzor glavni;
    readonly MiniProzor mini;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip meni = new();
    ToolStripMenuItem akcijaMini, akcijaPauza, stavkaOtvori, stavkaPodesavanja, stavkaIzvoz, stavkaPomoc, stavkaIzlaz;
    readonly System.Windows.Forms.Timer tajmer = new() { Interval = 1000 };
    readonly Dictionary<(bool, bool, bool), Icon> ikoneTray = [];   // a few variants, made once
    (bool, bool, bool) ikonaTray;
    Form otvorenProzor;   // Options, Help or About: one at a time
    bool zavrseno;

    public bool Pauza { get; private set; }
    public int DanReseta => p.DanReseta;
    public string JedinicaBrzine => p.JedinicaBrzine is "KB/s" or "Mbit/s" ? p.JedinicaBrzine : "MB/s";
    public int MiniProvidnost => p.MiniProvidnost;
    public double BrzinaDown { get; private set; }
    public double BrzinaUp { get; private set; }
    public double MaxDown => maxDown;
    public double MaxUp => maxUp;
    public DateTime? MaxDownKada => maxDownKada;
    public DateTime? MaxUpKada => maxUpKada;
    public ulong VezaBrzina => merac.Brzina;
    public ulong VezaNajveca => merac.NajvecaBrzina;
    public long VezaGreske => merac.Greske;
    public bool Prenos => prenos;
    public long PrenosBajtova => prenosBajtova;
    public long PrenosSekundi => prenos ? (Environment.TickCount64 - prenosOd) / 1000 : 0;
    public IReadOnlyCollection<(double d, double u)> Grafikon => grafikon;
    public List<RedPrograma> Programi => pracenje.Redovi;
    public string UPrekidu => pracenje.UPrekidu;

    static string KljucMinuta() => DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    public Netmetar(bool skriveno)
    {
        baza = new Baza(Podesavanja.Baza);
        merac = new Merac(p.Vpn);
        pracenje = new Pracenje(p, baza);
        meracPrograma = new MeracPrograma(Program.KanalMeraca);
        try { merac.Ocitaj(); } catch (Exception e) { Podesavanja.Log("Prvo čitanje brojača", e); }  // baseline
        OsveziZbirove();
        UcitajMaksimume();

        using (var s = typeof(Netmetar).Assembly.GetManifestResourceStream("app.ico"))
            ikona = new Icon(s);
        glavni = new GlavniProzor(this, ikona);
        glavni.Pritisak += Dugme;
        glavni.TraziPodesavanja += OtvoriPodesavanja;
        glavni.TraziOProgramu += OtvoriOProgramu;
        glavni.TraziPomoc += OtvoriPomoc;
        glavni.TraziCuvanje += ime => { pracenje.Prebaci(ime); glavni.Osvezi(); };
        glavni.TraziVeze += OtvoriVeze;
        glavni.TraziMeni += m => meni.Show(m);
        glavni.Sakrivanje += ZapamtiProzor;
        // the window really closes only when Windows asks it to (shutdown, Task Manager): the program then ends cleanly
        glavni.FormClosed += (_, _) => Izlaz();
        PostaviGeometriju();
        _ = glavni.Handle; // created now, so calls from the channel thread can be passed to the UI thread
        try { bajtoviDanas = baza.ProgramiDanaBajtovi(dan); } catch (Exception e) { Podesavanja.Log("Bajtovi programa nisu pročitani", e); }
        meracPrograma.Primljeno += sek => UNiti(() => DodajMereno(sek));
        meracPrograma.Slusaj();
        // the helper is started after the window exists, so a possible UAC question does not block the start-up
        glavni.BeginInvoke(() => { if (p.TacnaPotrosnja) UskladiMerac(); });

        mini = new MiniProzor(this);
        mini.Otvori += PrikaziGlavni;
        mini.Pomeren += (x, y) => { p.MiniPozicija = [x, y]; p.Sacuvaj(); };
        mini.TraziMeni += m => meni.Show(m);
        PostaviMini();

        NapraviMeni();
        using (var s = typeof(Netmetar).Assembly.GetManifestResourceStream("app.ico"))
            tray = new NotifyIcon { Icon = new Icon(s, SystemInformation.SmallIconSize), Text = Podesavanja.Ime, ContextMenuStrip = meni, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) PrikaziGlavni(); };

        if (p.MiniPrikaz) mini.Show();
        if (!skriveno) PrikaziGlavni();

        tajmer.Tick += (_, _) => Tik();
        tajmer.Start();
        Application.ApplicationExit += (_, _) => Kraj();
        glavni.BeginInvoke(p.UskladiPokretanje);
    }

    public void UNiti(Action a)
    {
        try { glavni.BeginInvoke(a); } catch { /* closing */ }
    }

    // ---- measuring ----

    void Tik()
    {
        try
        {
            var (d, u, sek) = merac.Ocitaj();
            var kljuc = KljucMinuta();
            if (kljuc != minut)
            {
                Upisi();
                minut = kljuc;
                OsveziZbirove();
            }
            if (Pauza) d = u = 0;
            BrzinaDown = sek > 0 ? d / sek : 0;
            BrzinaUp = sek > 0 ? u / sek : 0;
            if (!Pauza)
            {
                // today's records: the highest one-second speed and when it happened
                bool nov = false;
                if (BrzinaDown > maxDown) { maxDown = BrzinaDown; maxDownKada = DateTime.Now; nov = true; }
                if (BrzinaUp > maxUp) { maxUp = BrzinaUp; maxUpKada = DateTime.Now; nov = true; }
                if (nov) maxPrljav = true;

                // a "transfer": something is moving a lot of data; it ends after five quiet seconds
                var ukupno = BrzinaDown + BrzinaUp;
                if (!prenos && ukupno > PragPrenosa)
                {
                    prenos = true;
                    prenosOd = Environment.TickCount64;
                    prenosBajtova = 0;
                    prenosMirovanje = 0;
                }
                if (prenos)
                {
                    prenosBajtova += d + u;
                    if (ukupno < PragPrenosa / 2) prenosMirovanje++;
                    else prenosMirovanje = 0;
                    if (prenosMirovanje >= 5) prenos = false;
                }
            }
            else
            {
                prenos = false;
                prenosBajtova = 0;
            }
            grafikon.Enqueue((BrzinaDown, BrzinaUp));
            while (grafikon.Count > 30) grafikon.Dequeue();
            if (d != 0 || u != 0)
            {
                if (!naCekanju.TryGetValue(kljuc, out var c)) naCekanju[kljuc] = c = new long[2];
                c[0] += d;
                c[1] += u;
            }
            otkucaj++;
            if (otkucaj % 3 == 1) pracenje.Osvezi(Pauza);   // also while hidden: the Watch and the time online never stop
            if (glavni.Visible && glavni.WindowState != FormWindowState.Minimized) glavni.Osvezi();
            if (mini.Visible) mini.Osvezi();
            var down = Format.BrzinaPrikaz(BrzinaDown, JedinicaBrzine);
            var up = Format.BrzinaPrikaz(BrzinaUp, JedinicaBrzine);
            // NotifyIcon tooltip is limited to 63 characters; use separators, not escaped \\n.
            var tekst = $"▼{down.vrednost} {down.jedinica}  ▲{up.vrednost} {up.jedinica}  |  {Jez.T("Danas", "Today")} {Format.Gb(Danas)} GB" + (Pauza ? Jez.T(" | Pauza", " | Paused") : "");
            if (UPrekidu != null) tekst = $"{Jez.T("PREKID", "DOWN")}: {UPrekidu}  |  {tekst}";
            if (tekst.Length > 63) tekst = tekst[..63];
            if (tray.Text != tekst) tray.Text = tekst;

            OsveziIkonuTray();
            if (Environment.GetEnvironmentVariable("NM_DIAG") == "1")
                Podesavanja.Log($"diag down={BrzinaDown:F0} up={BrzinaUp:F0} link={merac.Brzina} pct={100.0 * 8 * (BrzinaDown + BrzinaUp) / Math.Max(1, merac.Brzina):F3}");
        }
        catch (Exception e) { Podesavanja.Log("Greška u merenju", e); }
    }

    /// <summary>Activity arrows blink independently when traffic is detected.</summary>
    void OsveziIkonuTray()
    {
        bool down = BrzinaDown > 500, up = BrzinaUp > 500;
        var kljuc = (down, up, (down || up) && otkucaj % 2 == 0);
        if (kljuc == ikonaTray) return;
        if (!ikoneTray.TryGetValue(kljuc, out var ikonica))
            ikoneTray[kljuc] = ikonica = Program.IkonicaAktivnosti(ikona, kljuc.Item1, kljuc.Item2, kljuc.Item3, SystemInformation.SmallIconSize);
        tray.Icon = ikonica;
        ikonaTray = kljuc;
    }

    void Upisi()
    {
        pracenje.Upisi();
        UpisiMaksimume();
        if (bajtoviNaCekanju.Count > 0)
        {
            try
            {
                baza.UpisiProgrameBajtove(bajtoviNaCekanju);
                bajtoviNaCekanju = [];
            }
            catch (Exception e) { Podesavanja.Log("Bajtovi programa nisu upisani, pokušavam ponovo za minut", e); }
        }
        if (naCekanju.Count == 0) return;
        try
        {
            baza.Upisi(naCekanju);
            naCekanju = [];
        }
        catch (Exception e) { Podesavanja.Log("Upis u bazu nije uspeo, pokušavam ponovo za minut", e); }
    }

    /// <summary>Exact bytes from the helper: added to today's per-program totals and queued for the database.</summary>
    void DodajMereno(Dictionary<string, (long d, long u)> sek)
    {
        var danas = dan.ToString("yyyy-MM-dd");
        foreach (var (ime, v) in sek)
        {
            var ima = bajtoviDanas.GetValueOrDefault(ime);
            bajtoviDanas[ime] = (ima.d + v.d, ima.u + v.u);
            var kljuc = (danas, ime);
            var cek = bajtoviNaCekanju.GetValueOrDefault(kljuc);
            bajtoviNaCekanju[kljuc] = (cek.d + v.d, cek.u + v.u);
        }
    }

    public bool MeracRadi => meracPrograma.Radi;
    public (long d, long u) BrzinaPrograma(string ime) => meracPrograma.Sekund.GetValueOrDefault(ime);
    public (long d, long u) DanasPrograma(string ime) => bajtoviDanas.GetValueOrDefault(ime);

    /// <summary>Today's records from the stanje table; a record of an older day is ignored.</summary>
    void UcitajMaksimume()
    {
        try
        {
            var danas = DateOnly.FromDateTime(DateTime.Today);
            (maxDown, maxDownKada) = CitajMaks(baza.Stanje("max_down"), danas);
            (maxUp, maxUpKada) = CitajMaks(baza.Stanje("max_up"), danas);
        }
        catch (Exception e) { Podesavanja.Log("Maksimumi nisu pročitani", e); }
    }

    /// <summary>The value is "yyyy-MM-dd;bytes per second;HH:mm"; anything else means no record.</summary>
    static (double v, DateTime? kada) CitajMaks(string tekst, DateOnly danas)
    {
        if (string.IsNullOrEmpty(tekst)) return (0, null);
        var delovi = tekst.Split(';');
        if (delovi.Length != 3 || !DateOnly.TryParse(delovi[0], out var dan) || dan != danas) return (0, null);
        if (!double.TryParse(delovi[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return (0, null);
        return (v, TimeOnly.TryParse(delovi[2], out var vreme) ? danas.ToDateTime(vreme) : null);
    }

    void UpisiMaksimume()
    {
        if (!maxPrljav) return;
        try
        {
            var danas = DateOnly.FromDateTime(DateTime.Today);
            baza.PostaviStanje("max_down", $"{danas:yyyy-MM-dd};{maxDown.ToString("F2", CultureInfo.InvariantCulture)};{maxDownKada?.ToString("HH:mm")}");
            baza.PostaviStanje("max_up", $"{danas:yyyy-MM-dd};{maxUp.ToString("F2", CultureInfo.InvariantCulture)};{maxUpKada?.ToString("HH:mm")}");
            maxPrljav = false;
        }
        catch (Exception e) { Podesavanja.Log("Maksimumi nisu upisani", e); }
    }

    void OsveziZbirove()
    {
        var noviDan = DateOnly.FromDateTime(DateTime.Today);
        if (dan != default && dan != noviDan)
        {
            // a new day starts a new record and new per-program totals
            maxDown = maxUp = 0;
            maxDownKada = maxUpKada = null;
            maxPrljav = true;
            bajtoviDanas = [];
        }
        dan = noviDan;
        ned = Baza.NedeljaZa(dan);
        try
        {
            if (sazeto != dan)
            {
                baza.Sazmi(dan);
                sazeto = dan;
            }
            period = baza.UskladiPeriode(p.DanReseta, dan);
            kesDanas = Zbir(baza.Zbir(dan, dan));
            kesNedelja = Zbir(baza.Zbir(ned.pocetak, ned.kraj));
            kesMesec = Zbir(baza.Zbir(period.pocetak, period.kraj));
        }
        catch (Exception e)
        {
            Podesavanja.Log("Zbirovi nisu osveženi", e);
            if (period.pocetak == default) period = Baza.PeriodZa(dan, p.DanReseta);
        }
        istorija.Clear();
    }

    static long Zbir((long d, long u) x) => x.d + x.u;

    // ---- values for the windows ----

    (long d, long u) Cekanje()
    {
        long d = 0, u = 0;
        foreach (var v in naCekanju.Values) { d += v[0]; u += v[1]; }
        return (d, u);
    }

    public long Danas => kesDanas + Zbir(Cekanje());
    public long Nedelja => kesNedelja + Zbir(Cekanje());
    public long Mesec => kesMesec + Zbir(Cekanje());
    public int ResetZa => Math.Max(1, period.kraj.AddDays(1).DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber);

    /// <summary>Data for a history screen; the database part is cached until the next minute.</summary>
    public Istorija Istorija(string rezim)
    {
        if (!istorija.TryGetValue(rezim, out var h))
        {
            try { h = istorija[rezim] = IstorijaIzBaze(rezim); }
            catch (Exception e)
            {
                Podesavanja.Log("Istorija nije pročitana", e);
                return new Istorija();
            }
        }
        // the first row (today / this week / this period) and "this week" are live
        var (cd, cu) = Cekanje();
        var r = new Istorija
        {
            Kuciste = h.Kuciste, Naslov = h.Naslov, DesnoNaslov = h.DesnoNaslov, DesnoVrednost = h.DesnoVrednost,
            DesnoMesta = h.DesnoMesta, DesnoJedinica = h.DesnoJedinica, TabelaNaslov = h.TabelaNaslov, Kolona = h.Kolona,
            DaniPerioda = h.DaniPerioda, Down = h.Down, Up = h.Up,
            Redovi = [.. h.Redovi], Dole = [.. h.Dole],
        };
        if (r.Redovi.Count > 0)
        {
            var prvi = r.Redovi[0];
            r.Redovi[0] = (prvi.natpis, prvi.d + cd, prvi.u + cu);
        }
        r.Zbir = h.Zbir + cd + cu;
        if (r.DaniPerioda)
        {
            r.Down = h.Down + cd;
            r.Up = h.Up + cu;
            // the period so far, stretched to its last day: what DU Meter calls "Projected"
            int proteklo = Math.Max(1, dan.DayNumber - period.pocetak.DayNumber + 1);
            int ukupno = Math.Max(proteklo, period.kraj.DayNumber - period.pocetak.DayNumber + 1);
            long proj = r.Zbir * ukupno / proteklo;
            (r.DesnoNaslov, r.DesnoVrednost, r.DesnoMesta, r.DesnoJedinica) = (Jez.T("PROJEKCIJA", "PROJECTED"), Format.Gb(proj), 4, "GB");
        }
        if (r.Dole.Count > 0) r.Dole[0] = (r.Dole[0].natpis, r.Dole[0].bajtova + cd + cu);
        Prosek(r, rezim);
        return r;
    }

    /// <summary>
    /// Average per day: on the DAYS screen the average of the seven days listed, on WEEKS and MONTHS
    /// the total divided by the days that have passed in that week or billing period.
    /// </summary>
    void Prosek(Istorija h, string rezim)
    {
        try
        {
            if (rezim == "dan")
            {
                long zbir = 0;
                foreach (var (_, d, u) in h.Redovi) zbir += d + u;
                h.ProsekNatpis = Jez.T("PROSEK 7 DANA", "AVERAGE OF 7 DAYS");
                h.Prosek = zbir / Math.Max(1, h.Redovi.Count);
            }
            else
            {
                int dana = rezim == "ned"
                    ? dan.DayNumber - ned.pocetak.DayNumber + 1
                    : dan.DayNumber - period.pocetak.DayNumber + 1;
                h.ProsekNatpis = Jez.T("PROSEK PO DANU", "AVERAGE PER DAY");
                h.Prosek = h.Zbir / Math.Max(1, dana);
            }
            if (h.Zbir == 0) h.ProsekNatpis = "";   // nothing measured yet: no average to show
        }
        catch (Exception e) { Podesavanja.Log("Prosek nije izračunat", e); }
    }

    Istorija IstorijaIzBaze(string rezim)
    {
        var mes = Format.Meseci;
        var pon = ned.pocetak;
        var prosla = baza.Zbir(pon.AddDays(-7), pon.AddDays(-1));
        var prethodni = baza.PrethodniPeriodi(period.pocetak, 6, p.DanReseta);
        var h = new Istorija();
        h.Dole.Add((Jez.T("OVA NEDELJA GB", "THIS WEEK GB"), Zbir(baza.Zbir(ned.pocetak, ned.kraj))));
        h.Dole.Add((Jez.T("PROŠLA NED GB", "LAST WEEK GB"), Zbir(prosla)));
        foreach (var (s, e) in prethodni.Take(2)) h.Dole.Add(($"{mes[s.Month - 1]} GB", Zbir(baza.Zbir(s, e))));

        if (rezim == "dan")
        {
            var poDanima = baza.PoDanima(dan.AddDays(-6), dan);
            for (int i = 0; i < 7; i++)
            {
                var d = dan.AddDays(-i);
                var (x, y) = poDanima.GetValueOrDefault(d);
                h.Redovi.Add(($"{Format.Dan(d)} {d:dd.MM}", x, y));
            }
            var juce = poDanima.GetValueOrDefault(dan.AddDays(-1));
            var danas = poDanima.GetValueOrDefault(dan);
            h.Zbir = danas.d + danas.u;
            h.Kuciste = Jez.T("ISTORIJA · DANI", "HISTORY · DAYS");
            h.Naslov = $"{Jez.T("DANAS", "TODAY")} · {Format.Dan(dan)} {dan:dd.MM}";
            (h.DesnoNaslov, h.DesnoVrednost, h.DesnoMesta, h.DesnoJedinica) = (Jez.T("JUČE", "YESTERDAY"), Format.Gb(Zbir(juce)), 4, "GB");
            (h.TabelaNaslov, h.Kolona) = (Jez.T("POSLEDNJIH 7 DANA", "LAST 7 DAYS"), Jez.T("DAN", "DAY"));
        }
        else if (rezim == "ned")
        {
            for (int i = 0; i < 7; i++)
            {
                var a = pon.AddDays(-7 * i);
                var e = a.AddDays(6);
                var (x, y) = baza.Zbir(a, e);
                h.Redovi.Add(($"{a:dd.MM}-{e:dd.MM}", x, y));
            }
            h.Zbir = Zbir(baza.Zbir(pon, pon.AddDays(6)));
            h.Kuciste = Jez.T("ISTORIJA · NEDELJE", "HISTORY · WEEKS");
            h.Naslov = $"{Jez.T("NEDELJA", "WEEK")} {ned.pocetak:dd.MM} - {ned.kraj:dd.MM}";
            (h.DesnoNaslov, h.DesnoVrednost, h.DesnoMesta, h.DesnoJedinica) = (Jez.T("PROŠLA NED", "LAST WEEK"), Format.Gb(Zbir(prosla)), 4, "GB");
            (h.TabelaNaslov, h.Kolona) = (Jez.T("POSLEDNJIH 7 NEDELJA", "LAST 7 WEEKS"), Jez.T("NEDELJA", "WEEK"));
        }
        else if (rezim == "mesdani")
        {
            // every day of the current billing period, the newest first; the table scrolls
            var (s, e) = period;
            var poDanima = baza.PoDanima(s, dan);
            for (var d = dan; d >= s; d = d.AddDays(-1))
            {
                var (x, y) = poDanima.GetValueOrDefault(d);
                h.Redovi.Add(($"{Format.Dan(d)} {d:dd.MM}", x, y));
            }
            var (sd, su) = baza.Zbir(s, e);
            int proteklo = Math.Max(1, dan.DayNumber - s.DayNumber + 1);
            int ukupno = Math.Max(proteklo, e.DayNumber - s.DayNumber + 1);
            (h.Zbir, h.Down, h.Up, h.DaniPerioda) = (sd + su, sd, su, true);
            h.Kuciste = Jez.T("ISTORIJA · DANI PERIODA", "HISTORY · PERIOD DAYS");
            h.Naslov = $"PERIOD {s:dd.MM} - {e:dd.MM}";
            (h.DesnoNaslov, h.DesnoVrednost, h.DesnoMesta, h.DesnoJedinica) =
                (Jez.T("PROJEKCIJA", "PROJECTED"), Format.Gb(h.Zbir * ukupno / proteklo), 4, "GB");
            (h.TabelaNaslov, h.Kolona) = ($"{Jez.T("DANI U PERIODU", "DAYS IN PERIOD")} · {proteklo}/{ukupno}", Jez.T("DAN", "DAY"));
        }
        else
        {
            var (s, e) = period;
            var (x, y) = baza.Zbir(s, e);
            h.Redovi.Add((mes[s.Month - 1], x, y));
            h.Zbir = x + y;
            foreach (var (a, z) in prethodni)
            {
                var (pd, pu) = baza.Zbir(a, z);
                h.Redovi.Add((mes[a.Month - 1], pd, pu));
            }
            int n = ResetZa;
            h.Kuciste = $"{Jez.T("ISTORIJA", "HISTORY")} · {mes[s.Month - 1]}";
            h.Naslov = $"PERIOD {s:dd.MM} - {e:dd.MM}";
            (h.DesnoNaslov, h.DesnoVrednost, h.DesnoMesta, h.DesnoJedinica) = (Jez.T("RESET ZA", "RESETS IN"), n.ToString(), 2, Format.Dana(n));
            (h.TabelaNaslov, h.Kolona) = (Jez.T("OBRAČUNSKI PERIODI", "BILLING PERIODS"), "PERIOD");
        }
        return h;
    }

    // ---- buttons, windows, menu ----

    void Dugme(string k)
    {
        if (k == "ss")
        {
            Pauza = !Pauza;
            grafikon.Clear();
        }
        else if (k == "mes")
        {
            // M: billing periods, then the days of the current period, then the live screen
            glavni.Rezim = glavni.Rezim switch { "mes" => "mesdani", "mesdani" => "uzivo", _ => "mes" };
            if (glavni.Rezim == "mesdani") glavni.VratiIstorijuNaPocetak();
        }
        else glavni.Rezim = glavni.Rezim == k ? "uzivo" : k;
        glavni.Osvezi();
        if (mini.Visible) mini.Osvezi();
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    public void PrikaziGlavni()
    {
        if (!glavni.Visible)
        {
            glavni.Rezim = "uzivo";
            pracenje.Osvezi(Pauza);
        }
        if (glavni.WindowState == FormWindowState.Minimized) glavni.WindowState = FormWindowState.Normal;
        glavni.Show();
        glavni.Activate();
        SetForegroundWindow(glavni.Handle);
    }

    void NapraviMeni()
    {
        stavkaOtvori = new ToolStripMenuItem(null, null, (_, _) => PrikaziGlavni());
        akcijaMini = new ToolStripMenuItem { Checked = p.MiniPrikaz, CheckOnClick = true };
        akcijaMini.CheckedChanged += (_, _) => PostaviMiniPrikaz(akcijaMini.Checked);
        akcijaPauza = new ToolStripMenuItem { CheckOnClick = true };
        akcijaPauza.CheckedChanged += (_, _) => { if (akcijaPauza.Checked != Pauza) Dugme("ss"); };
        stavkaPodesavanja = new ToolStripMenuItem(null, null, (_, _) => OtvoriPodesavanja());
        stavkaIzvoz = new ToolStripMenuItem(null, null, (_, _) => IzveziPeriod());
        stavkaPomoc = new ToolStripMenuItem(null, null, (_, _) => OtvoriPomoc());
        stavkaIzlaz = new ToolStripMenuItem(null, null, (_, _) => Izlaz());
        meni.Items.Add(stavkaOtvori);
        meni.Items.Add(akcijaMini);
        meni.Items.Add(akcijaPauza);
        meni.Items.Add(stavkaPodesavanja);
        meni.Items.Add(stavkaIzvoz);
        meni.Items.Add(stavkaPomoc);
        meni.Items.Add(new ToolStripSeparator());
        meni.Items.Add(stavkaIzlaz);
        PrevediMeni();
        meni.Opening += (_, _) => akcijaPauza.Checked = Pauza;
    }

    void PostaviMiniPrikaz(bool prikazi)
    {
        p.MiniPrikaz = prikazi;
        if (akcijaMini.Checked != prikazi) akcijaMini.Checked = prikazi;
        mini.Visible = prikazi;
        if (prikazi) mini.Osvezi();
        p.Sacuvaj();
    }

    void PostaviMini()
    {
        var poz = p.MiniPozicija;
        if (poz is { Length: 2 })
        {
            var centar = new Point(poz[0] + mini.Width / 2, poz[1] + mini.Height / 2);
            if (Screen.AllScreens.Any(s => s.Bounds.Contains(centar)))   // it may stand on the taskbar
            {
                mini.Location = new Point(poz[0], poz[1]);
                return;
            }
        }
        // above the clock: bottom right corner of the work area
        var r = Screen.PrimaryScreen.WorkingArea;
        mini.Location = new Point(r.Right - mini.Width - 10, r.Bottom - mini.Height - 10);
    }

    void PostaviGeometriju()
    {
        if (p.Prozor is { Length: 4 } g)
        {
            var r = new Rectangle(g[0], g[1], g[2], g[3]);
            var centar = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            // the screen the window was on, or the nearest one when that screen is gone
            var ekran = Screen.AllScreens.FirstOrDefault(x => x.WorkingArea.Contains(centar)) ?? NajbliziEkran(centar);
            if (ekran != null)
            {
                // the saved size can be too small, too big or from a screen that is gone: keep it usable and reachable
                r.Width = Math.Clamp(r.Width, GlavniProzor.MinSirina, GlavniProzor.MaxSirina(glavni.DeviceDpi, ekran));
                r.Height = GlavniProzor.VisinaZa(r.Width);
                var w = ekran.WorkingArea;
                // a window that fits goes back fully on screen; a bigger one keeps at least part of the header visible
                int minX = r.Width <= w.Width ? w.Left : w.Left - r.Width + 160;
                int maxX = r.Width <= w.Width ? w.Right - r.Width : w.Right - 160;
                int minY = r.Height <= w.Height ? w.Top : w.Top - r.Height + 80;
                int maxY = r.Height <= w.Height ? w.Bottom - r.Height : w.Bottom - 80;
                r.X = Math.Clamp(r.X, minX, maxX);
                r.Y = Math.Clamp(r.Y, minY, maxY);
                glavni.Bounds = r;
                return;
            }
        }
        var e = Screen.PrimaryScreen.WorkingArea;
        int w2 = Math.Min(980 * glavni.DeviceDpi / 96, (int)(e.Width * 0.8));
        int h = GlavniProzor.VisinaZa(w2);
        glavni.Bounds = new Rectangle(e.Left + (e.Width - w2) / 2, e.Top + (e.Height - h) / 2, w2, h);
    }

    /// <summary>The screen nearest to a point (a saved place can belong to a monitor that is no longer plugged in).</summary>
    static Screen NajbliziEkran(Point tacka)
    {
        Screen najbolji = null;
        long najblize = long.MaxValue;
        foreach (var s in Screen.AllScreens)
        {
            var w = s.WorkingArea;
            int dx = tacka.X < w.Left ? w.Left - tacka.X : tacka.X > w.Right ? tacka.X - w.Right : 0;
            int dy = tacka.Y < w.Top ? w.Top - tacka.Y : tacka.Y > w.Bottom ? tacka.Y - w.Bottom : 0;
            long d = (long)dx * dx + (long)dy * dy;
            if (d < najblize) { najblize = d; najbolji = s; }
        }
        return najbolji;
    }

    // a minimized window cannot own a dialog: the dialog would stay invisible
    bool GlavniVidljiv => glavni.Visible && glavni.WindowState != FormWindowState.Minimized;

    /// <summary>False when a dialog is already open (it comes to the front instead).</summary>
    bool MozeDijalog()
    {
        if (otvorenProzor == null) return true;
        otvorenProzor.Activate();
        return false;
    }

    DialogResult Prikazi(Form dlg)
    {
        otvorenProzor = dlg;
        try { return dlg.ShowDialog(GlavniVidljiv ? glavni : null); }
        finally { otvorenProzor = null; }
    }

    void OtvoriOProgramu()
    {
        if (!MozeDijalog()) return;
        using var dlg = new OProgramuDijalog(ikona, GlavniVidljiv ? glavni.Width : 0);
        Prikazi(dlg);
    }

    void OtvoriPomoc()
    {
        if (!MozeDijalog()) return;
        using var dlg = new PomocDijalog(ikona, GlavniVidljiv ? glavni.Width : 0);
        Prikazi(dlg);
    }

    /// <summary>The connections of one program: listening ports and open remote addresses (read when the dialog opens).</summary>
    void OtvoriVeze(string ime)
    {
        if (!MozeDijalog()) return;
        var stavke = Merenje.Pojedinosti(ime);
        var potrosnja = "";
        if (MeracRadi)
        {
            var (d, u) = BrzinaPrograma(ime);
            var (dd, du) = DanasPrograma(ime);
            var bd = Format.BrzinaPrikaz(d, JedinicaBrzine);
            var bu = Format.BrzinaPrikaz(u, JedinicaBrzine);
            potrosnja = $"▼ {bd.vrednost} {bd.jedinica.ToUpperInvariant()} · ▲ {bu.vrednost} {bu.jedinica.ToUpperInvariant()}"
                + $" · {Jez.T("DANAS", "TODAY")} {Format.Gb(dd + du)} GB";
        }
        using var dlg = new VezeDijalog(ikona, ime, stavke, potrosnja, GlavniVidljiv ? glavni.Width : 0);
        Prikazi(dlg);
    }

    /// <summary>Starts or stops the helper that counts exact traffic per program (the scheduled task makes starts silent).</summary>
    void UskladiMerac()
    {
        const string zadatak = "BronxSoft Netmetar Merac";
        try
        {
            if (!p.TacnaPotrosnja)
            {
                Pokreni("schtasks.exe", $"/end /tn \"{zadatak}\"");
                foreach (var pr in System.Diagnostics.Process.GetProcessesByName("BronxSoft Netmetar Merac"))
                {
                    try { pr.Kill(); } catch { /* already gone */ }
                    pr.Dispose();
                }
                return;
            }
            var exe = Path.Combine(AppContext.BaseDirectory, "BronxSoft Netmetar Merac.exe");
            if (!File.Exists(exe))
            {
                Podesavanja.Log("Merač nije nađen: " + exe);
                return;
            }
            if (Pokreni("schtasks.exe", $"/query /tn \"{zadatak}\"") == 0)
            {
                Pokreni("schtasks.exe", $"/run /tn \"{zadatak}\"");
                return;
            }
            // no task yet: the helper creates it once (one UAC), then it is started silently
            using (var pr = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--install")
                   { UseShellExecute = true, Verb = "runas" }))
                pr?.WaitForExit(30000);
            Pokreni("schtasks.exe", $"/run /tn \"{zadatak}\"");
        }
        catch (Exception e) { Podesavanja.Log("Merač nije pokrenut", e); }
    }

    static int Pokreni(string fajl, string argumenti)
    {
        using var pr = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fajl, argumenti)
            { UseShellExecute = false, CreateNoWindow = true });
        pr.WaitForExit();
        return pr.ExitCode;
    }

    void OtvoriPodesavanja()
    {
        if (!MozeDijalog()) return;
        using var dlg = new PodesavanjaDijalog(p, merac.Imena, ikona);
        if (Prikazi(dlg) != DialogResult.OK) return;
        int stariDan = p.DanReseta;
        dlg.Upisi(p);
        Jez.En = p.Jezik == "en";
        pracenje.Slozi();
        istorija.Clear();
        PrevediMeni();
        merac.Vpn = p.Vpn;
        p.Sacuvaj();
        if (dlg.MeracPromenjen) UskladiMerac();
        PostaviMiniPrikaz(p.MiniPrikaz);
        p.UskladiPokretanje();
        if (stariDan != p.DanReseta)
        {
            Upisi();
            OsveziZbirove();
        }
        glavni.Osvezi();
    }

    void PrevediMeni()
    {
        stavkaOtvori.Text = Jez.T("Otvori Netmetar", "Open Netmetar");
        akcijaMini.Text = Jez.T("Mini prikaz", "Mini display");
        akcijaPauza.Text = Jez.T("Pauziraj merenje", "Pause metering");
        stavkaPodesavanja.Text = Jez.T("Podešavanja…", "Settings…");
        stavkaIzvoz.Text = Jez.T("Izvezi period u CSV…", "Export the period to CSV…");
        stavkaPomoc.Text = Jez.T("Pomoć", "Help");
        stavkaIzlaz.Text = Jez.T("Izlaz", "Exit");
    }

    /// <summary>Writes the current billing period, day by day, to a CSV file (semicolon separated, comma decimals: opens in Excel).</summary>
    void IzveziPeriod()
    {
        if (!MozeDijalog()) return;
        var (s, e) = period;
        using var dlg = new SaveFileDialog
        {
            Title = Jez.T("Izvezi period u CSV", "Export the period to CSV"),
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"Netmetar {s:yyyy-MM-dd} - {e:yyyy-MM-dd}.csv",
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog(GlavniVidljiv ? glavni : null) != DialogResult.OK) return;
        if (Izvezi(dlg.FileName)) return;
        MessageBox.Show(Jez.T("Izvoz nije uspeo. Detalji su u fajlu greske.log.", "The export failed. Details are in greske.log."),
            Podesavanja.Ime, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Lines of the CSV: every day of the period, then the total, the average and the projection.</summary>
    List<string> CsvRedovi()
    {
        var (s, e) = period;
        var (cd, cu) = Cekanje();
        var poDanima = baza.PoDanima(s, dan);
        var redovi = new List<string>
        {
            string.Join(';', Jez.T("Datum", "Date"), "DOWN (GB)", "UP (GB)", Jez.T("UKUPNO (GB)", "TOTAL (GB)")),
        };
        long sd = 0, su = 0;
        for (var d = s; d <= dan; d = d.AddDays(1))
        {
            var (x, y) = poDanima.GetValueOrDefault(d);
            if (d == dan) { x += cd; y += cu; }   // the minute still in memory
            sd += x;
            su += y;
            redovi.Add($"{d:dd.MM.yyyy};{Csv(x)};{Csv(y)};{Csv(x + y)}");
        }
        int proteklo = Math.Max(1, dan.DayNumber - s.DayNumber + 1);
        int ukupno = Math.Max(proteklo, e.DayNumber - s.DayNumber + 1);
        redovi.Add($"{Jez.T("UKUPNO", "TOTAL")};{Csv(sd)};{Csv(su)};{Csv(sd + su)}");
        redovi.Add($"{Jez.T("PROSEK PO DANU", "AVERAGE PER DAY")};{Csv(sd / proteklo)};{Csv(su / proteklo)};{Csv((sd + su) / proteklo)}");
        redovi.Add($"{Jez.T("PROJEKCIJA", "PROJECTED")} {e:dd.MM.yyyy};{Csv(sd * ukupno / proteklo)};{Csv(su * ukupno / proteklo)};{Csv((sd + su) * ukupno / proteklo)}");
        return redovi;
    }

    /// <summary>Writes the CSV (BOM so Excel reads č, ć, ž); the error goes to greske.log and false is returned.</summary>
    public bool Izvezi(string fajl)
    {
        try
        {
            File.WriteAllLines(fajl, CsvRedovi(), new System.Text.UTF8Encoding(true));
            return true;
        }
        catch (Exception ex)
        {
            Podesavanja.Log("Izvoz u CSV nije uspeo", ex);
            return false;
        }
    }

    static string Csv(long bajtova) => (bajtova / 1e9).ToString("F2").Replace('.', ',');

    void ZapamtiProzor()
    {
        if (glavni.Visible && glavni.WindowState == FormWindowState.Normal)
        {
            p.Prozor = [glavni.Left, glavni.Top, glavni.Width, glavni.Height];
            p.Sacuvaj();
        }
    }

    void Izlaz()
    {
        Kraj();   // now: when ApplicationExit comes the window is already gone, and Windows may not wait for it
        ExitThread();
    }

    void Kraj()
    {
        if (zavrseno) return;
        zavrseno = true;
        try
        {
            tajmer.Stop();
            Upisi();
            ZapamtiProzor();
            p.Sacuvaj();
            tray.Visible = false;
            tray.Dispose();
            baza.Dispose();
        }
        catch (Exception e) { Podesavanja.Log("Greška pri izlasku", e); }
    }

    /// <summary>Pictures of every screen (for checking the look).</summary>
    public void Snimi(string folder)
    {
        Directory.CreateDirectory(folder);
        Thread.Sleep(1500);
        Tik();
        pracenje.Osvezi(Pauza);
        int w = 980, h = GlavniProzor.VisinaZa(w);
        foreach (var r in new[] { "uzivo", "dan", "ned", "mes", "mesdani", "programi" })
        {
            glavni.Rezim = r;
            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp)) glavni.Nacrtaj(g, w, h);
            bmp.Save(Path.Combine(folder, $"glavni_{r}.png"), ImageFormat.Png);
        }
        using (var m = mini.Slika()) m.Save(Path.Combine(folder, "mini.png"), ImageFormat.Png);
        using (var d = new OProgramuDijalog(ikona, w))
        using (var slika = d.Slika()) slika.Save(Path.Combine(folder, "o_programu.png"), ImageFormat.Png);
        // the connections dialog, for the program with the most connections (with its traffic today, if known)
        if (Merenje.ProgramiNaInternetu().FirstOrDefault() is { } prvi)
        {
            string potrosnja = "";
            try
            {
                var b = baza.ProgramiDanaBajtovi(dan).GetValueOrDefault(prvi.Ime);
                if (b.d + b.u > 0) potrosnja = $"{Jez.T("DANAS", "TODAY")} {Format.Gb(b.d + b.u)} GB";
            }
            catch (Exception e) { Podesavanja.Log("Bajtovi programa nisu pročitani", e); }
            using (var d = new VezeDijalog(ikona, prvi.Ime, Merenje.Pojedinosti(prvi.Ime), potrosnja, w))
            using (var slika = d.Slika())
                slika.Save(Path.Combine(folder, "veze.png"), ImageFormat.Png);
        }
        using (var d = new PomocDijalog(ikona, w))
        {
            int strana = 1;
            foreach (var slika in d.SveStrane())
                using (slika) slika.Save(Path.Combine(folder, $"pomoc_{strana++}.png"), ImageFormat.Png);
        }

        // how long each part of one second takes (milliseconds)
        static double Ms(Action a, int n = 30)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) a();
            return Math.Round(sw.Elapsed.TotalMilliseconds / n, 2);
        }
        var rnd = new Random(1);
        glavni.Rezim = "uzivo";
        using var platno = new Bitmap(w, h);
        using var gp = Graphics.FromImage(platno);
        var mere = new[]
        {
            $"veliki prozor: {Ms(() => { BrzinaDown = rnd.Next(0, 30_000_000); glavni.Nacrtaj(gp, w, h); })}",
            $"mini: {Ms(() => { BrzinaDown = rnd.Next(0, 30_000_000); mini.Slika().Dispose(); })}",
            $"brojaci: {Ms(() => merac.Ocitaj())}",
            $"programi: {Ms(() => Merenje.ProgramiNaInternetu())}",
        };
        File.WriteAllLines(Path.Combine(folder, "mera.txt"), mere);
        tray.Visible = false;
        baza.Dispose();
    }
}
