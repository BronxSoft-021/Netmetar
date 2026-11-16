// The big window, the mini display above the clock and the settings dialog.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Netmetar;

sealed class Istorija
{
    public string Kuciste = "ISTORIJA", Naslov = "", DesnoNaslov = "", DesnoVrednost = "", DesnoJedinica = "", TabelaNaslov = "", Kolona = "";
    public long Zbir;
    public int DesnoMesta = 2;
    /// <summary>Average per day; an empty label means there is nothing to show.</summary>
    public string ProsekNatpis = "";
    public long Prosek;
    /// <summary>Days-in-period screen: the big number and ▼/▲ cover the whole period, the right side shows the projection.</summary>
    public bool DaniPerioda;
    public long Down, Up;
    public List<(string natpis, long d, long u)> Redovi = [];
    public List<(string natpis, long bajtova)> Dole = [];
}

/// <summary>What the windows show; implemented by the program.</summary>
interface IStanje
{
    bool Pauza { get; }
    int DanReseta { get; }
    string JedinicaBrzine { get; }
    int MiniProvidnost { get; }
    double BrzinaDown { get; }
    double BrzinaUp { get; }
    double MaxDown { get; }
    double MaxUp { get; }
    DateTime? MaxDownKada { get; }
    DateTime? MaxUpKada { get; }
    ulong VezaBrzina { get; }
    ulong VezaNajveca { get; }
    long VezaGreske { get; }
    bool Prenos { get; }
    long PrenosBajtova { get; }
    long PrenosSekundi { get; }
    IReadOnlyCollection<(double d, double u)> Grafikon { get; }
    List<RedPrograma> Programi { get; }
    string UPrekidu { get; }
    long Danas { get; }
    long Nedelja { get; }
    long Mesec { get; }
    int ResetZa { get; }
    Istorija Istorija(string rezim);
}

sealed class GlavniProzor : Form
{
    public const float W = 1186, H = 858;
    const float Margina = 14;
    static readonly RectangleF EkranR = new(33, 74, 1120, 655);
    static readonly RectangleF PodesavanjaR = new(790, 28, 240, 34);
    // own title-bar parts (the window has no Windows frame)
    static readonly RectangleF VerzijaR = new(640, 31, 128, 28);
    static readonly RectangleF MinR = new(1040, 24, 52, 44);
    // programs online: the "ONLINE NOW" heading opens the full list; in the list the title goes back
    static readonly RectangleF ProgramiR = new(60, 506, 1065, 34);
    static readonly RectangleF ProgramiNaslovR = new(60, 96, 640, 38);
    const int ProgramiRedova = 7;
    const int IstorijaRedova = 7;   // a period can have 31 days: the table scrolls like the list of programs
    const float CuvarX = 832;   // the Watch column of the program list
    int programiPrvi;
    int istorijaPrvi;
    static readonly RectangleF ZatvoriR = new(1096, 24, 52, 44);
    static readonly string[] Kljucevi = ["dan", "ned", "mes", "ss"];
    static string Natpis(string k) => k switch
    {
        "dan" => Jez.T("DAN", "DAY"),
        "ned" => Jez.T("NED", "WEEK"),
        "mes" => Jez.T("MES", "MONTH"),
        _ => "S/S",
    };
    static string NatpisOpcije => Jez.T("OPCIJE", "OPTIONS");
    static string NatpisPomoc => Jez.T("POMOĆ", "HELP");
    static string NatpisOProgramu => Jez.T("O PROGRAMU", "ABOUT");
    RectangleF opcijeR, pomocR, oProgramuR;
    readonly RectangleF[] dugmadR = new RectangleF[4];
    bool? rasporedEn;

    /// <summary>Bottom row, as wide as the content of the screen: the widths follow the labels of the current language.</summary>
    void RasporediDugmad()
    {
        if (rasporedEn == Jez.En) return;
        rasporedEn = Jez.En;
        const float levo = 69, desno = 1117, gore = 751, visina = 70, unutra = 12;
        float[] a = [Crtac.SirinaAkcije(NatpisOpcije), Crtac.SirinaAkcije(NatpisPomoc), Crtac.SirinaAkcije(NatpisOProgramu)];
        float c = Math.Max(104, Kljucevi.Max(k => Crtac.F14(22).Sirina(Natpis(k))) + 26);
        float izmedju = (desno - levo - a.Sum() - 4 * c - 4 * unutra) / 2;
        opcijeR = new(levo, gore, a[0], visina);
        pomocR = new(opcijeR.Right + unutra, gore, a[1], visina);
        for (int i = 0; i < dugmadR.Length; i++)
            dugmadR[i] = new(pomocR.Right + izmedju + i * (c + unutra), gore, c, visina);
        oProgramuR = new(desno - a[2], gore, a[2], visina);
    }

    readonly IStanje s;
    public string Rezim = "uzivo";
    string pritisnuto;
    Bitmap pozadina, okvir;
    object pozadinaKljuc;

    public event Action<string> Pritisak;
    /// <summary>A click on a program in the list: show its connections.</summary>
    public event Action<string> TraziVeze;
    public event Action TraziPodesavanja;
    public event Action TraziOProgramu;
    public event Action TraziPomoc;
    public event Action<string> TraziCuvanje;
    public event Action Sakrivanje;   // right before the window hides: its place is still known
    public event Action<Point> TraziMeni;

    public GlavniProzor(IStanje stanje, Icon ikona)
    {
        s = stanje;
        Text = Podesavanja.Ime;
        Icon = ikona;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimumSize = new Size(MinSirina, VisinaZa(MinSirina));
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
    }

    /// <summary>Smallest and largest width of the device: below it the labels stop being readable, above 2× the
    /// drawing costs too much (measured: 4.7 ms a frame at 980 px, about 27 ms at 2×, once a second).</summary>
    public const int MinSirina = 700;
    static readonly double[] Koraci = [0.5, 0.75, 1, 1.25, 1.5, 2];

    /// <summary>The largest width that still fits the screen: Windows does not let a window be bigger than the screen,
    /// so the width is limited by the height as well (otherwise the proportions would break at the bottom).</summary>
    public static int MaxSirina(int dpi, Screen ekran = null)
    {
        var r = (ekran ?? Screen.PrimaryScreen).WorkingArea;
        int poSirini = Math.Min((int)Math.Round(2 * (double)W * dpi / 96), r.Width);
        return Math.Min(poSirini, SirinaZa(r.Height));
    }

    /// <summary>Width of the device at 100 % on this screen.</summary>
    double Osnova => (double)W * DeviceDpi / 96;

    /// <summary>Ctrl + wheel: the next or the previous of the crisp sizes (the drawing stays sharp there).</summary>
    public void Zum(bool uvecaj)
    {
        double sada = Width / Osnova;
        double cilj = uvecaj
            ? Koraci.FirstOrDefault(k => k > sada + 0.01)
            : Koraci.LastOrDefault(k => k < sada - 0.01);
        if (cilj == 0) return;   // already the largest or the smallest step
        PostaviSirinu((int)Math.Round(Osnova * cilj));
    }

    /// <summary>Back to the design size (Ctrl+0).</summary>
    public void StoPosto() => PostaviSirinu((int)Math.Round(Osnova));

    /// <summary>As large as fits the screen, keeping the proportions (a double click on the header).</summary>
    public void StaneNaEkran()
    {
        var ekran = Screen.FromHandle(Handle);
        var r = ekran.WorkingArea;
        int sirina = Math.Min(r.Width, MaxSirina(DeviceDpi, ekran));
        while (sirina > MinSirina && VisinaZa(sirina) > r.Height) sirina -= 16;
        PostaviSirinu(sirina);
        Left = r.Left + (r.Width - Width) / 2;
        Top = r.Top + (r.Height - Height) / 2;
        Osvezi();
    }

    /// <summary>Sets the width, keeps the centre, clamps into the allowed range.</summary>
    public void PostaviSirinu(int sirina)
    {
        sirina = Math.Clamp(sirina, MinSirina, MaxSirina(DeviceDpi, Screen.FromHandle(Handle)));
        var centar = new Point(Left + Width / 2, Top + Height / 2);
        int visina = VisinaZa(sirina);
        Bounds = new Rectangle(centar.X - sirina / 2, centar.Y - visina / 2, sirina, visina);
        Osvezi();
    }

    // layered window: only the device and its shadow are visible, without a rectangular background
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000;  // WS_EX_LAYERED
            return cp;
        }
    }

    /// <summary>Window height that fits the device exactly at this width (and the other way round).</summary>
    public static int VisinaZa(int sirina) => (int)Math.Round((sirina - 2 * Margina) * H / W + 2 * Margina);
    static int SirinaZa(int visina) => (int)Math.Round((visina - 2 * Margina) * W / H + 2 * Margina);

    [StructLayout(LayoutKind.Sequential)] struct PRAVOUGAONIK { public int Levo, Gore, Desno, Dole; }

    // Resizing keeps the proportions of the device, so that no empty transparent strip appears next to it.
    // The width always stays between MinSirina and MaxSirina, whichever edge is dragged.
    void DrziOdnos(ref Message m)
    {
        var r = Marshal.PtrToStructure<PRAVOUGAONIK>(m.LParam);
        int strana = (int)m.WParam;   // 1 left, 2 right, 3 top, 4 top left, 5 top right, 6 bottom, 7 bottom left, 8 bottom right
        bool gore = strana is 3 or 4 or 5;
        bool levo = strana is 1 or 4 or 7;
        int sirina = strana is 3 or 6 ? SirinaZa(r.Dole - r.Gore) : r.Desno - r.Levo;
        sirina = Math.Clamp(sirina, MinSirina, MaxSirina(DeviceDpi, Screen.FromHandle(Handle)));
        int visina = VisinaZa(sirina);
        if (levo) r.Levo = r.Desno - sirina; else r.Desno = r.Levo + sirina;
        if (gore) r.Gore = r.Dole - visina; else r.Dole = r.Gore + visina;
        Marshal.StructureToPtr(r, m.LParam, false);
        m.Result = 1;
    }

    const int WM_CLOSE = 0x10, WM_NCHITTEST = 0x84, WM_NCLBUTTONDBLCLK = 0xA3, WM_NCRBUTTONUP = 0xA5, WM_SIZING = 0x214;

    // Frameless window: the header drags it, the edges resize it.
    protected override void WndProc(ref Message m)
    {
        // Closing a visible window only hides it: the program keeps measuring in the tray. A hidden window can be
        // closed only by Windows (Task Manager), so that really closes it, like shutting Windows down does.
        if (m.Msg == WM_CLOSE && Visible)
        {
            Sakrivanje?.Invoke();
            Hide();
            return;
        }
        if (m.Msg == WM_SIZING)
        {
            DrziOdnos(ref m);
            return;
        }
        if (m.Msg == WM_NCLBUTTONDBLCLK && (int)m.WParam == 2)   // HTCAPTION: a double click on the header
        {
            // 100 % <-> as large as the screen; the sides keep their proportions anyway
            if (Width < Osnova * 1.02) StaneNaEkran();
            else StoPosto();
            return;
        }
        if (m.Msg == WM_NCRBUTTONUP)
        {
            long lp = m.LParam.ToInt64();
            TraziMeni?.Invoke(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            return;
        }
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST || (int)m.Result != 1) return;  // 1 = HTCLIENT
        long l = m.LParam.ToInt64();
        var p = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
        int rub = Math.Max(6, 6 * DeviceDpi / 96);
        bool lijevo = p.X < rub, desno = p.X >= ClientSize.Width - rub, gore = p.Y < rub, dole = p.Y >= ClientSize.Height - rub;
        if (gore && lijevo) m.Result = (IntPtr)13;        // HTTOPLEFT
        else if (gore && desno) m.Result = (IntPtr)14;    // HTTOPRIGHT
        else if (dole && lijevo) m.Result = (IntPtr)16;   // HTBOTTOMLEFT
        else if (dole && desno) m.Result = (IntPtr)17;    // HTBOTTOMRIGHT
        else if (lijevo) m.Result = (IntPtr)10;           // HTLEFT
        else if (desno) m.Result = (IntPtr)11;            // HTRIGHT
        else if (gore) m.Result = (IntPtr)12;             // HTTOP
        else if (dole) m.Result = (IntPtr)15;             // HTBOTTOM
        else
        {
            var (sk, ox, oy) = Mera(ClientSize.Width, ClientSize.Height);
            if ((p.Y - oy) / sk < 72 && DugmeNa(p) == null) m.Result = (IntPtr)2;  // HTCAPTION
        }
    }

    static (float s, float ox, float oy) Mera(int w, int h)
    {
        float s = Math.Max(0.05f, Math.Min((w - 2 * Margina) / W, (h - 2 * Margina) / H));
        return (s, (w - W * s) / 2, (h - H * s) / 2);
    }

    string DugmeNa(Point p)
    {
        var (sk, ox, oy) = Mera(ClientSize.Width, ClientSize.Height);
        var t = new PointF((p.X - ox) / sk, (p.Y - oy) / sk);
        if (MinR.Contains(t)) return "min";
        if (ZatvoriR.Contains(t)) return "zatvori";
        RasporediDugmad();
        if (opcijeR.Contains(t)) return "opcije";
        if (pomocR.Contains(t)) return "pomoc";
        if (oProgramuR.Contains(t)) return "oprogramu";
        for (int i = 0; i < Kljucevi.Length; i++)
            if (dugmadR[i].Contains(t)) return Kljucevi[i];
        if (Rezim == "uzivo" && ProgramiR.Contains(t)) return "programi";
        if (Rezim == "programi" && ProgramiNaslovR.Contains(t)) return "programi";
        if (Rezim == "programi" && t.X >= CuvarX - 12 && t.X < 1125 && t.Y >= 344 && t.Y < 344 + ProgramiRedova * 37)
        {
            int red = programiPrvi + (int)((t.Y - 344) / 37);
            if (red < s.Programi.Count) return "cuvar:" + s.Programi[red].Ime;
        }
        // any other click on a row opens the connections of that program
        if (Rezim == "uzivo" && t.Y >= 545 && t.Y < 545 + 4 * 42)
        {
            int red = (int)((t.Y - 545) / 42);
            if (red < s.Programi.Count) return "veze:" + s.Programi[red].Ime;
        }
        if (Rezim == "programi" && t.Y >= 344 && t.Y < 344 + ProgramiRedova * 37)
        {
            int red = programiPrvi + (int)((t.Y - 344) / 37);
            if (red < s.Programi.Count) return "veze:" + s.Programi[red].Ime;
        }
        return PodesavanjaR.Contains(t) ? "podesavanja" : null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { pritisnuto = DugmeNa(e.Location); Osvezi(); }
        else if (e.Button == MouseButtons.Right) TraziMeni?.Invoke(PointToScreen(e.Location));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var k = pritisnuto;
        pritisnuto = null;
        if (k != null && k == DugmeNa(e.Location))
        {
            if (k == "podesavanja" || k == "opcije") TraziPodesavanja?.Invoke();
            else if (k == "oprogramu") TraziOProgramu?.Invoke();
            else if (k == "pomoc") TraziPomoc?.Invoke();
            else if (k == "programi") { Rezim = Rezim == "programi" ? "uzivo" : "programi"; programiPrvi = 0; }
            else if (k.StartsWith("cuvar:")) TraziCuvanje?.Invoke(k["cuvar:".Length..]);
            else if (k.StartsWith("veze:")) TraziVeze?.Invoke(k["veze:".Length..]);
            else if (k == "min") WindowState = FormWindowState.Minimized;
            else if (k == "zatvori") Close();  // only hides: the program keeps measuring in the tray
            else Pritisak?.Invoke(k);
        }
        Osvezi();
    }

    protected override void OnMouseMove(MouseEventArgs e) =>
        Cursor = DugmeNa(e.Location) != null ? Cursors.Hand : Cursors.Default;

    void PomeriPrograme(int redova)
    {
        int maks = Math.Max(0, s.Programi.Count - ProgramiRedova);
        int novo = Math.Clamp(programiPrvi + redova, 0, maks);
        if (novo == programiPrvi) return;
        programiPrvi = novo;
        Osvezi();
    }

    /// <summary>Scrolling of the days-in-period table: a month has more days than fit on the screen.</summary>
    void PomeriIstoriju(int redova)
    {
        int maks = Math.Max(0, s.Istorija("mesdani").Redovi.Count - IstorijaRedova);
        int novo = Math.Clamp(istorijaPrvi + redova, 0, maks);
        if (novo == istorijaPrvi) return;
        istorijaPrvi = novo;
        Osvezi();
    }

    /// <summary>Back to the newest day when the screen is opened again.</summary>
    public void VratiIstorijuNaPocetak() => istorijaPrvi = 0;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Ctrl + wheel: the window grows or shrinks to the next crisp size
        if ((ModifierKeys & Keys.Control) == Keys.Control)
        {
            Zum(e.Delta > 0);
            return;
        }
        if (Rezim == "programi") PomeriPrograme(-Math.Sign(e.Delta));
        else if (Rezim == "mesdani") PomeriIstoriju(-Math.Sign(e.Delta));
        base.OnMouseWheel(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Ctrl+0: back to the design size
        if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode is Keys.D0 or Keys.NumPad0)
        {
            StoPosto();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        // D, N, M and Space do the same as the buttons under the display; F1 opens the help.
        string k = e.KeyCode switch
        {
            Keys.F1 => "pomoc",
            Keys.D => "dan",
            Keys.N => "ned",
            Keys.M => "mes",
            Keys.Space => "ss",
            _ => "",
        };
        if (k.Length > 0)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (k == "pomoc") TraziPomoc?.Invoke();
            else Pritisak?.Invoke(k);
            return;
        }
        if (Rezim == "programi" && e.KeyCode == Keys.Down) PomeriPrograme(1);
        else if (Rezim == "programi" && e.KeyCode == Keys.Up) PomeriPrograme(-1);
        else if (Rezim == "mesdani" && e.KeyCode == Keys.Down) PomeriIstoriju(1);
        else if (Rezim == "mesdani" && e.KeyCode == Keys.Up) PomeriIstoriju(-1);
        if (e.KeyCode == Keys.Escape && Rezim != "uzivo") { Rezim = "uzivo"; Osvezi(); }
        base.OnKeyDown(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Osvezi();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Osvezi();
    }

    /// <summary>Draws the window again (a layered window gets no WM_PAINT).</summary>
    public void Osvezi()
    {
        if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
        try
        {
            if (okvir == null || okvir.Width != Width || okvir.Height != Height)
            {
                okvir?.Dispose();
                okvir = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            }
            using (var g = Graphics.FromImage(okvir)) Nacrtaj(g, Width, Height);
            Sloj.Prikazi(Handle, okvir);
        }
        catch (Exception ex) { Podesavanja.Log("Greška pri crtanju", ex); }
    }

    public void Nacrtaj(Graphics g, int w, int h)
    {
        if (w < 10 || h < 10) return;
        var (sk, ox, oy) = Mera(w, h);
        // the parts that do not change are painted once into a picture
        var kljuc = (w, h, Rezim, s.Pauza, pritisnuto, Jez.En);
        if (!kljuc.Equals(pozadinaKljuc))
        {
            pozadina?.Dispose();
            pozadina = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using var gp = Graphics.FromImage(pozadina);
            Crtac.PozadinaProzora(gp, new RectangleF(ox, oy, W * sk, H * sk), 46 * sk);
            CrtajPozadinu(new Crtac(gp, sk, ox, oy));
            pozadinaKljuc = kljuc;
        }
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceCopy;   // over the previous second: the transparency must not add up
        g.DrawImage(pozadina, new Rectangle(0, 0, w, h));
        g.CompositingMode = CompositingMode.SourceOver;
        var c = new Crtac(g, sk, ox, oy);
        var desno = Rezim == "uzivo"
            ? (s.Pauza ? Jez.T("‖ PAUZA", "‖ PAUSED") : Jez.T("● MERI", "● METERING"))
              + (s.VezaBrzina > 0 ? $" · {Link(s.VezaBrzina)}" : "") + $" · RESET {s.DanReseta}."
            : Rezim == "programi" ? Jez.T("PROGRAMI · NA INTERNETU", "PROGRAMS · ONLINE") : s.Istorija(Rezim).Kuciste;
        // a link that got slower than it was (a bad cable drops 1 Gbit to 100 Mbit) turns the text red
        bool sporija = Rezim == "uzivo" && s.VezaBrzina > 0 && s.VezaBrzina < s.VezaNajveca;
        c.Tekst(1018, 50, desno, Crtac.Obican(13, 120), sporija ? Crtac.Uzbuna : Crtac.NatpisKucista, 'd');
        if (Rezim == "uzivo") CrtajUzivo(c);
        else if (Rezim == "programi") CrtajPrograme(c);
        else CrtajIstoriju(c);
    }

    void CrtajPozadinu(Crtac c)
    {
        c.Kuciste(new RectangleF(0, 0, W, H), 46);
        c.Tekst(43, 52, "BRONXSOFT · NETMETAR", Crtac.Obican(20, 124), Crtac.NatpisKucista);
        c.Oval(VerzijaR, "v" + Podesavanja.Verzija + "-" + Podesavanja.Faza, Crtac.Obican(14), Color.FromArgb(0xe4, 0xe2, 0xdb), Color.FromArgb(0xbf, 0xbd, 0xb5), Crtac.NatpisKucista);
        c.Pravougaonik(new RectangleF(779, 36, 2, 20), Color.FromArgb(0xc0, 0xbe, 0xb7));
        c.Minus(1066, 46, 9, Crtac.NatpisKucista);
        c.Iks(1122, 46, 8.5f, Crtac.NatpisKucista);
        c.Ekran(EkranR, 26);
        RasporediDugmad();
        c.DugmeAkcija(opcijeR, NatpisOpcije, 'z', pritisnuto == "opcije");
        c.DugmeAkcija(pomocR, NatpisPomoc, '?', pritisnuto == "pomoc");
        for (int i = 0; i < Kljucevi.Length; i++)
        {
            var k = Kljucevi[i];
            c.Dugme(dugmadR[i], Natpis(k), k == Rezim || (k == "mes" && Rezim == "mesdani") || (k == "ss" && s.Pauza), pritisnuto == k);
        }
        c.DugmeAkcija(oProgramuR, NatpisOProgramu, 'i', pritisnuto == "oprogramu");
    }

    void CrtajUzivo(Crtac c)
    {
        // a big transfer in progress: how much has moved and for how long (top left, after the name)
        if (s.Prenos && s.PrenosSekundi >= 2)
        {
            var trajanje = s.PrenosSekundi < 60 ? $"{s.PrenosSekundi} S" : $"{s.PrenosSekundi / 60} MIN";
            var tekst = $"{Jez.T("PRENOS", "TRANSFER")} {Format.Gb(s.PrenosBajtova)} GB · {trajanje}";
            float x = 43 + Crtac.Obican(20, 124).Sirina("BRONXSOFT · NETMETAR") + 26;
            c.Tekst(x, 52, tekst, Crtac.Obican(16, 120), Crtac.NatpisKucista);
        }
        // download
        float sirinaDown = c.Tekst(69, 123, "DOWNLOAD", Crtac.F14(20));
        if (s.VezaBrzina > 0)
        {
            // how much of the link is used right now, and adapter errors since the program started
            long posto = Math.Clamp((long)Math.Round(100.0 * 8 * (s.BrzinaDown + s.BrzinaUp) / s.VezaBrzina), 0, 100);
            var veza = $"{posto}% {Jez.T("VEZE", "LINK")}";
            if (s.VezaGreske > 0) veza += $" · {Jez.T("GREŠKE", "ERRORS")} {s.VezaGreske}";
            c.Tekst(69 + sirinaDown + 16, 123, veza, Crtac.F14(16), Crtac.Priguseno);
        }
        c.Strelica(84, 205, 30, true);
        var down = Format.BrzinaPrikaz(s.BrzinaDown, s.JedinicaBrzine);
        c.Seg(348, 233, s.Pauza ? "" : down.vrednost, 3, 96, desno: true);
        c.Tekst(443, 222, down.jedinica.ToUpperInvariant(), Crtac.Mono(30), null, 'd');
        Grafikon(c, new RectangleF(70, 252, 373, 68), false);
        Maksimum(c, 70, 337, s.MaxDown, s.MaxDownKada, false);
        // upload
        c.Tekst(743, 123, "UPLOAD", Crtac.F14(20));
        c.Strelica(758, 205, 30, false);
        var up = Format.BrzinaPrikaz(s.BrzinaUp, s.JedinicaBrzine);
        c.Seg(1022, 233, s.Pauza ? "" : up.vrednost, 3, 96, desno: true);
        c.Tekst(1117, 222, up.jedinica.ToUpperInvariant(), Crtac.Mono(30), null, 'd');
        Grafikon(c, new RectangleF(744, 252, 373, 68), true);
        Maksimum(c, 1117, 337, s.MaxUp, s.MaxUpKada, true);

        c.Crta(69, 1117, 343);
        int n = s.ResetZa;
        (string, long)[] kolone = [(Jez.T("DANAS", "TODAY"), s.Danas), (Jez.T("NEDELJA", "WEEK"), s.Nedelja), (Jez.T($"MESEC · JOŠ {n} {Format.Dana(n)}", $"MONTH · {n} {Format.Dana(n)} LEFT"), s.Mesec)];
        for (int i = 0; i < kolone.Length; i++)
        {
            float x = 69 + i * 357;
            c.Tekst(x, 386, kolone[i].Item1, Crtac.F14(20), Crtac.Priguseno);
            float sirina = c.Seg(x, 466, Format.Gb(kolone[i].Item2), 4, 64);
            c.Tekst(x + sirina + 16, 464, "GB", Crtac.Mono(22));
        }

        c.Crta(69, 1117, 492);
        var programi = s.Programi;
        var naslov = Jez.T("NA INTERNETU SADA", "ONLINE NOW");
        int naMrezi = programi.Count(x => x.Veza > 0);
        if (naMrezi > 0) naslov += $" · {naMrezi} {(naMrezi == 1 ? "PROGRAM" : Jez.T("PROGRAMA", "PROGRAMS"))}";
        c.Tekst(69, 530, naslov, Crtac.F14(20), Crtac.Priguseno);
        c.Tekst(1110, 530, Jez.T("VEZE", "LINKS"), Crtac.F14(16), Crtac.Priguseno, 'd');
        c.Tekst(1030, 530, Jez.T("DANAS", "TODAY"), Crtac.F14(16), Crtac.Priguseno, 'd');
        float y = 545;
        if (programi.Count == 0)
            c.Tekst(78, y + 28, s.Pauza ? Jez.T("MERENJE JE PAUZIRANO", "METERING PAUSED") : Jez.T("NEMA AKTIVNIH VEZA", "NO ACTIVE CONNECTIONS"), Crtac.F14(19, true), Crtac.Priguseno);
        foreach (var r in programi.Take(4))
        {
            bool prekid = r.Cuvar == Cuvanje.Prekid;
            c.IkonaPrograma(r.Ime, r.Putanja, new RectangleF(78, y + 9, 24, 24));
            var fp = Crtac.F14(19, true);
            float sir = fp.Sirina(r.Ime);
            if (sir > 700) fp = Crtac.F14(19 * 700 / sir, true);   // a very long name is shrunk, never cut
            c.Tekst(112, y + 28, r.Ime, fp, prekid ? Crtac.Uzbuna : r.Veza == 0 ? Crtac.Priguseno : null);
            if (r.Slusa) c.Lampa(112 + Math.Min(sir, 700) + 13, y + 21, 3.5f, Crtac.Priguseno, true);
            if (prekid) c.Tekst(1110, y + 28, r.Napomena, Crtac.F14T(19, true), Crtac.Uzbuna, 'd');
            else
            {
                c.Tekst(1030, y + 28, Format.Trajanje(r.Sekundi), Crtac.F14T(19, true), Crtac.Priguseno, 'd');
                if (r.Veza > 0) c.Tekst(1110, y + 28, r.Veza.ToString(), Crtac.F14(19, true), null, 'd');
            }
            c.Crta(69, 1117, y + 42, Crtac.LinijaReda, 1.5f);
            y += 42;
        }
    }

    /// <summary>Full list of the programs that are online (all of them, scrollable), with the time online and the Watch.</summary>
    void CrtajPrograme(Crtac c)
    {
        var programi = s.Programi;
        int veza = programi.Sum(p => p.Veza), naMrezi = programi.Count(p => p.Veza > 0);
        c.Tekst(69, 121, Jez.T("NA INTERNETU SADA", "ONLINE NOW"), Crtac.F14(20), Crtac.Priguseno);
        float sirina = c.Seg(79, 238, naMrezi.ToString(), 2, 101);
        c.Tekst(79 + sirina + 18, 234, Jez.T(naMrezi == 1 ? "PROGRAM" : "PROGRAMA", naMrezi == 1 ? "PROGRAM" : "PROGRAMS"), Crtac.Mono(30));
        c.Tekst(1117, 121, Jez.T("UKUPNO VEZA", "TOTAL LINKS"), Crtac.F14(20), Crtac.Priguseno, 'd');
        float sj = c.Tekst(1117, 234, Jez.T("VEZA", "LINKS"), Crtac.Mono(30), null, 'd');
        c.Seg(1117 - sj - 22, 238, veza.ToString(), 3, 101, desno: true);

        c.Crta(69, 1117, 262);
        c.Tekst(69, 303, Jez.T("SPISAK PROGRAMA", "PROGRAM LIST"), Crtac.F14(20), Crtac.Priguseno);
        var mali = Crtac.F14(15, true);
        c.Tekst(79, 334, "PROGRAM", mali, Crtac.Priguseno);
        c.Tekst(630, 334, Jez.T("VEZE", "LINKS"), mali, Crtac.Priguseno, 'd');
        c.Tekst(792, 334, Jez.T("DANAS", "TODAY"), mali, Crtac.Priguseno, 'd');
        c.Tekst(CuvarX, 334, Jez.T("ČUVAR", "WATCH"), mali, Crtac.Priguseno);
        c.Crta(69, 1117, 344, Crtac.LinijaReda, 1.5f);

        if (programi.Count == 0)
            c.Tekst(79, 344 + 29, s.Pauza ? Jez.T("MERENJE JE PAUZIRANO", "METERING PAUSED") : Jez.T("NEMA AKTIVNIH VEZA", "NO ACTIVE CONNECTIONS"), Crtac.F14(21, true), Crtac.Priguseno);
        int maks = Math.Max(0, programi.Count - ProgramiRedova);
        programiPrvi = Math.Clamp(programiPrvi, 0, maks);
        float y = 344;
        for (int i = programiPrvi; i < Math.Min(programi.Count, programiPrvi + ProgramiRedova); i++)
        {
            var r = programi[i];
            float osnova = y + 29;
            bool prekid = r.Cuvar == Cuvanje.Prekid;
            c.IkonaPrograma(r.Ime, r.Putanja, new RectangleF(79, y + 6, 26, 26));
            var fp = Crtac.F14(21, true);
            float sir = fp.Sirina(r.Ime);
            if (sir > 440) fp = Crtac.F14(21 * 440 / sir, true);   // a very long name is shrunk, never cut
            c.Tekst(115, osnova, r.Ime, fp, prekid ? Crtac.Uzbuna : r.Veza == 0 ? Crtac.Priguseno : null);
            // a program that listens on a port accepts connections: a small dot, the ports are in the dialog
            if (r.Slusa) c.Lampa(115 + Math.Min(sir, 440) + 14, osnova - 7, 3.5f, Crtac.Priguseno, true);
            if (r.Veza > 0) c.Tekst(630, osnova, r.Veza.ToString(), Crtac.F14(21, true), null, 'd');
            c.Tekst(792, osnova, Format.Trajanje(r.Sekundi), Crtac.F14T(19, true), Crtac.Priguseno, 'd');
            c.Lampa(CuvarX + 8, y + 19, 7, r.Cuvar switch { Cuvanje.Radi => Crtac.Cifra, Cuvanje.Prekid => Crtac.Uzbuna, _ => Crtac.Priguseno }, r.Cuvar is Cuvanje.Radi or Cuvanje.Prekid);
            if (r.Cuvar != Cuvanje.Ne) c.Tekst(CuvarX + 26, osnova - 1, r.Napomena, Crtac.F14T(16, true), prekid ? Crtac.Uzbuna : Crtac.Priguseno);
            y += 37;
            c.Crta(69, 1117, y, Crtac.LinijaReda, 1.5f);
        }
        if (maks > 0)
        {
            float vis = ProgramiRedova * 37, ruc = Math.Max(24, vis * ProgramiRedova / programi.Count);
            c.Pravougaonik(new RectangleF(1128, 344 + (vis - ruc) * programiPrvi / maks, 4, ruc), Crtac.Priguseno);
        }

        c.Crta(69, 1117, 625);
        c.Tekst(69, 676, Jez.T("NASLOV ILI ESC: NAZAD  ·  TOČKIĆ MIŠA: POMERANJE  ·  KRUG: ČUVAR", "TITLE OR ESC: BACK  ·  MOUSE WHEEL: SCROLL  ·  CIRCLE: WATCH"), Crtac.F14(16, true), Crtac.Priguseno);
    }

    /// <summary>"1 GBIT", "2.5 GBIT", "100 MBIT": the speed the adapter negotiated with the switch.</summary>
    static string Link(ulong b) => b >= 1_000_000_000
        ? (b / 1e9).ToString("0.#") + " GBIT"
        : b >= 1_000_000 ? (b / 1e6).ToString("0") + " MBIT"
        : (b / 1e3).ToString("0") + " KBIT";

    /// <summary>The highest speed of the day and when it happened, under the graph. The unit is written too: the same
    /// number means different things in KB/s, MB/s and Mbit/s, and KB/s switches to MB/s above 999.5 by itself.</summary>
    void Maksimum(Crtac c, float x, float y, double vrednost, DateTime? kada, bool desno)
    {
        if (vrednost <= 0) return;
        var (broj, jedinica) = Format.BrzinaPrikaz(vrednost, s.JedinicaBrzine, 4);
        var tekst = $"MAX {broj} {jedinica.ToUpperInvariant()}";
        if (kada is DateTime k) tekst += $" · {k:HH:mm}";
        c.Tekst(x, y, tekst, Crtac.F14T(14), Crtac.Priguseno, desno ? 'd' : 'l');
    }

    void Grafikon(Crtac c, RectangleF r, bool upload)
    {
        var istorija = s.Grafikon.ToList();
        const int n = 30;
        float korak = r.Width / n, sirina = korak * 0.68f;
        double najvise = Math.Max(50_000, istorija.Count > 0
            ? istorija.Max(x => upload ? x.u : x.d) : 0);
        int prazno = n - istorija.Count;
        for (int i = 0; i < n; i++)
        {
            float x = r.Left + i * korak;
            c.Pravougaonik(new RectangleF(x, r.Top, sirina, r.Height), Crtac.Duh);
            if (i < prazno) continue;
            var (d, u) = istorija[i - prazno];
            double vrednost = upload ? u : d;
            float visina = (float)(r.Height * vrednost / najvise);
            if (vrednost > 0) visina = Math.Max(visina, 2);
            c.Pravougaonik(new RectangleF(x, r.Bottom - visina, sirina, visina),
                Color.FromArgb(220, 238, 244, 240));
        }
    }

    void CrtajIstoriju(Crtac c)
    {
        var h = s.Istorija(Rezim);
        c.Tekst(69, 121, h.Naslov, Crtac.F14(20), Crtac.Priguseno);
        float sirina = c.Seg(79, 238, Format.Gb(h.Zbir), 4, 101);
        float gb = c.Tekst(79 + sirina + 18, 234, "GB", Crtac.Mono(30));
        if (h.ProsekNatpis.Length > 0)
            c.Tekst(79 + sirina + 18 + gb + 26, 240, $"{h.ProsekNatpis} {Format.Gb(h.Prosek)} GB", Crtac.F14(16), Crtac.Priguseno);
        c.Tekst(1117, 121, h.DesnoNaslov, Crtac.F14(20), Crtac.Priguseno, 'd');
        float sj = c.Tekst(1117, 234, h.DesnoJedinica, Crtac.Mono(30), null, 'd');
        c.Seg(1117 - sj - 22, 238, h.DesnoVrednost, h.DesnoMesta, 101, desno: true);

        c.Crta(69, 1117, 262);
        c.Tekst(69, 303, h.TabelaNaslov, Crtac.F14(20), Crtac.Priguseno);
        var mali = Crtac.F14(15, true);
        c.Tekst(79, 334, h.Kolona, mali, Crtac.Priguseno);
        c.Tekst(420, 334, "DOWN", mali, Crtac.Priguseno, 'd');
        c.Tekst(541, 334, "UP", mali, Crtac.Priguseno, 'd');
        c.Tekst(747, 334, Jez.T("UKUPNO GB", "TOTAL GB"), mali, Crtac.Priguseno, 'd');
        // days in the period: the split of the whole period stands on the right, next to the table heading
        if (h.DaniPerioda)
            c.Tekst(1117, 303, $"▼ {Format.Gb(h.Down)} GB   ▲ {Format.Gb(h.Up)} GB", Crtac.F14T(16), Crtac.Priguseno, 'd');
        c.Crta(69, 1117, 344, Crtac.LinijaReda, 1.5f);
        var red = Crtac.F14(21, true);
        var redTekst = Crtac.F14T(21, true);   // row labels (dates) are text: clean font
        double najvise = Math.Max(1, h.Redovi.Count > 0 ? h.Redovi.Max(x => x.d + x.u) : 1);
        int maks = Math.Max(0, h.Redovi.Count - IstorijaRedova);
        istorijaPrvi = Math.Clamp(istorijaPrvi, 0, maks);
        float y = 344;
        for (int i = istorijaPrvi; i < Math.Min(h.Redovi.Count, istorijaPrvi + IstorijaRedova); i++)
        {
            var (natpis, d, u) = h.Redovi[i];
            float osnova = y + 29;
            c.Tekst(79, osnova, natpis, redTekst);
            c.Tekst(420, osnova, Format.GbTabela(d), red, null, 'd');
            c.Tekst(541, osnova, Format.GbTabela(u), red, null, 'd');
            c.Tekst(747, osnova, Format.GbTabela(d + u), red, null, 'd');
            var traka = new RectangleF(787, y + 11, 330, 15);
            c.Pravougaonik(traka, Color.FromArgb(20, 238, 244, 240));
            c.Pravougaonik(new RectangleF(traka.Left, traka.Top, (float)(traka.Width * (d + u) / najvise), traka.Height), Color.FromArgb(215, 238, 244, 240));
            y += 37;
            c.Crta(69, 1117, y, Crtac.LinijaReda, 1.5f);
        }
        if (maks > 0)
        {
            float vis = IstorijaRedova * 37, ruc = Math.Max(24, vis * IstorijaRedova / h.Redovi.Count);
            c.Pravougaonik(new RectangleF(1128, 344 + (vis - ruc) * istorijaPrvi / maks, 4, ruc), Crtac.Priguseno);
        }

        c.Crta(69, 1117, 625);
        // Donja statistika: ravnomerne kolone, naslovi i vrednosti centrirani.
        const float sirinaDole = (1117 - 69) / 4f;
        for (int i = 0; i < h.Dole.Count; i++)
        {
            float centar = 69 + sirinaDole * (i + 0.5f);
            c.Tekst(centar, 665, h.Dole[i].natpis, Crtac.F14(18), Crtac.Priguseno, 'c');
            c.Tekst(centar, 704, Format.GbTabela(h.Dole[i].bajtova), Crtac.F14(20, true), null, 'c');
        }
    }
}

/// <summary>Small borderless display above the clock; a layered window, so its rounded corners are smooth.</summary>
sealed class MiniProzor : Form
{
    public const float W = 480, H = 120, Skala = 0.72f;
    static readonly RectangleF EkranR = new(12, 12, W - 24, H - 24);
    static readonly float[] Kolone = [34, 168, 302];

    readonly IStanje s;
    Bitmap pozadina, okvir;
    float pozadinaSkala, pozadinaAlfa;
    Point start, pozicija;
    bool pritisnut, vucem, preko;

    public event Action Otvori;
    public event Action<int, int> Pomeren;
    public event Action<Point> TraziMeni;

    public MiniProzor(IStanje stanje)
    {
        s = stanje;
        Text = Podesavanja.Ime;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Cursor = Cursors.Hand;
        Promeni();
    }

    float Faktor => Skala * DeviceDpi / 96f;

    /// <summary>Opacity of the case (the screen loses half as much); the numbers stay solid, and all of it is solid under the mouse.</summary>
    float Alfa => preko || pritisnut ? 1f : 1f - s.MiniProvidnost / 100f;

    void Promeni() => Size = new Size((int)Math.Round(W * Faktor), (int)Math.Round(H * Faktor));

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x80 | 0x8000000 | 0x8; // LAYERED | TOOLWINDOW | NOACTIVATE | TOPMOST
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Promeni();
        Osvezi();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        preko = true;
        Osvezi();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        preko = false;
        Osvezi();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { pritisnut = true; vucem = false; start = Cursor.Position; pozicija = Location; }
        else if (e.Button == MouseButtons.Right) TraziMeni?.Invoke(Cursor.Position);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!pritisnut) return;
        var p = Cursor.Position;
        int dx = p.X - start.X, dy = p.Y - start.Y;
        if (vucem || Math.Abs(dx) + Math.Abs(dy) > 4)
        {
            vucem = true;
            Location = new Point(pozicija.X + dx, pozicija.Y + dy);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !pritisnut) return;
        pritisnut = false;
        if (vucem) Pomeren?.Invoke(Left, Top);
        else Otvori?.Invoke();
    }

    public Bitmap Slika()
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        Iscrtaj(bmp, Faktor);
        return bmp;
    }

    /// <summary>The case and the screen; they change only with the size or the transparency.</summary>
    void PripremiPozadinu(float f)
    {
        float alfa = Alfa;
        if (pozadina != null && pozadinaSkala == f && pozadinaAlfa == alfa) return;
        using var puna = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using var ekran = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var gp = Graphics.FromImage(puna))
        {
            var cp = new Crtac(gp, f, 0, 0);
            cp.Kuciste(new RectangleF(0, 0, W, H), 22, 2);
            cp.Ekran(EkranR, 14);
        }
        using (var ge = Graphics.FromImage(ekran)) new Crtac(ge, f, 0, 0).Ekran(EkranR, 14);
        pozadina?.Dispose();
        pozadina = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(pozadina))
        {
            Zalepi(g, puna, alfa);
            // the bright case is what stands out, the dark screen keeps the white numbers readable:
            // the screen loses only half as much, so it gets a second, partial coat
            float alfaEkrana = (1 + alfa) / 2;
            if (alfa < 1) Zalepi(g, ekran, (alfaEkrana - alfa) / (1 - alfa));
        }
        pozadinaSkala = f;
        pozadinaAlfa = alfa;
    }

    void Zalepi(Graphics g, Bitmap slika, float alfa)
    {
        using var atributi = new ImageAttributes();
        atributi.SetColorMatrix(new ColorMatrix { Matrix33 = alfa });
        g.DrawImage(slika, new Rectangle(0, 0, Width, Height), 0, 0, Width, Height, GraphicsUnit.Pixel, atributi);
    }

    void Iscrtaj(Bitmap bmp, float f)
    {
        PripremiPozadinu(f);
        using var g = Graphics.FromImage(bmp);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(pozadina, 0, 0, bmp.Width, bmp.Height);
        g.CompositingMode = CompositingMode.SourceOver;
        var c = new Crtac(g, f, 0, 0);
        var down = Format.BrzinaPrikaz(s.BrzinaDown, s.JedinicaBrzine);
        var up = Format.BrzinaPrikaz(s.BrzinaUp, s.JedinicaBrzine);
        // top row: labels centered in the columns, as on the approved mockup
        c.Strelica(Kolone[0] + 34, 31, 10, true);
        c.Strelica(Kolone[1] + 34, 31, 10, false);
        c.Tekst(Kolone[0] + 67, 37, down.jedinica.ToUpperInvariant(), Crtac.F14(13), null, 'c');
        c.Tekst(Kolone[1] + 67, 37, up.jedinica.ToUpperInvariant(), Crtac.F14(13), null, 'c');
        if (s.UPrekidu is string prekid)
        {
            var natpis = $"{Jez.T("PREKID", "DOWN")}: {prekid}".ToUpperInvariant();
            var fp = Crtac.F14(13);
            float sirina = fp.Sirina(natpis);
            if (sirina > 180) fp = Crtac.F14(13 * 180 / sirina);
            c.Tekst(Kolone[2] + 67, 37, natpis, fp, Crtac.Uzbuna, 'c');
        }
        else c.Tekst(Kolone[2] + 67, 37, Jez.T("DANAS GB", "TODAY GB"), Crtac.F14(13), Crtac.Priguseno, 'c');
        c.Seg(Kolone[0], 94, s.Pauza ? "" : down.vrednost, 3, 44);
        c.Seg(Kolone[1], 94, s.Pauza ? "" : up.vrednost, 3, 44);
        c.Seg(Kolone[2], 94, Format.Gb(s.Danas), 4, 44);
    }

    public void Osvezi()
    {
        if (!IsHandleCreated) return;
        try
        {
            if (okvir == null || okvir.Width != Width || okvir.Height != Height)
            {
                okvir?.Dispose();
                okvir = Slika();
            }
            else Iscrtaj(okvir, Faktor);
            Sloj.Prikazi(Handle, okvir);
        }
        catch (Exception e) { Podesavanja.Log("Mini prikaz nije osvežen", e); }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Osvezi();
    }
}


sealed class PodesavanjaDijalog : Form
{
    readonly NumericUpDown dan = new() { Minimum = 1, Maximum = 31, Width = 64 };
    readonly CheckBox start = new() { Text = Jez.T("Pokreni sa Windowsom", "Start with Windows"), AutoSize = true };
    readonly CheckBox mini = new() { Text = Jez.T("Prikaži mini prikaz iznad sata", "Show the mini display above the clock"), AutoSize = true };
    readonly CheckBox vpn = new() { Text = Jez.T("Računaj i VPN adaptere", "Count VPN adapters too"), AutoSize = true };
    readonly CheckBox tacna = new() { Text = Jez.T("Tačna potrošnja po programu", "Exact traffic per program"), AutoSize = true };
    readonly ComboBox jedinica = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly ComboBox jezik = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly ComboBox providnost = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    static readonly int[] Providnosti = [0, 20, 40, 60];
    readonly bool biloTacna;

    /// <summary>True when the switch for exact traffic was changed (the program then starts or stops the helper).</summary>
    public bool MeracPromenjen => tacna.Checked != biloTacna;

    public PodesavanjaDijalog(Podesavanja p, IEnumerable<string> adapteri, Icon ikona)
    {
        Text = $"{Jez.T("Podešavanja", "Settings")} - {Podesavanja.Ime}";
        Icon = ikona;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0xf1, 0xf0, 0xeb);
        ForeColor = Color.FromArgb(0x2b, 0x2a, 0x27);
        Font = new Font("Segoe UI", 9.75f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ShowInTaskbar = false;

        dan.Value = p.DanReseta;
        start.Checked = p.PokretanjeSaWindowsom;
        mini.Checked = p.MiniPrikaz;
        vpn.Checked = p.Vpn;
        tacna.Checked = p.TacnaPotrosnja;
        biloTacna = p.TacnaPotrosnja;
        jedinica.Items.AddRange(["KB/s", "MB/s", "Mbit/s"]);
        jedinica.SelectedItem = p.JedinicaBrzine is "KB/s" or "Mbit/s" ? p.JedinicaBrzine : "MB/s";
        jezik.Items.AddRange(["Srpski", "English"]);
        jezik.SelectedIndex = p.Jezik == "en" ? 1 : 0;
        providnost.Items.AddRange([.. Providnosti.Select(x => (object)$"{x} %")]);
        providnost.SelectedIndex = Math.Max(0, Array.IndexOf(Providnosti, p.MiniProvidnost));

        var lay = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            Padding = new Padding(18, 14, 18, 12), Dock = DockStyle.Fill,
        };
        lay.Controls.Add(new Label { Text = "BRONXSOFT · NETMETAR", AutoSize = true, ForeColor = Color.FromArgb(0x5a, 0x58, 0x52), Font = new Font("Segoe UI", 11.25f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) });
        var red = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        red.Controls.Add(new Label { Text = Jez.T("Dan resetovanja obračunskog meseca:", "Billing month reset day:"), AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        red.Controls.Add(dan);
        lay.Controls.Add(red);
        lay.Controls.Add(Napomena(Jez.T("Period traje od tog dana do dana pre sledećeg resetovanja. Ako mesec nema taj dan (npr. 31. u novembru), reset je poslednjeg dana u mesecu. Stari periodi ostaju u istoriji.", "The period runs from that day until the day before the next reset. If a month has no such day (e.g. the 31st in November), the reset is on the last day of the month. Old periods stay in the history.")));
        var redJedinica = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        redJedinica.Controls.Add(new Label { Text = Jez.T("Jedinica brzine:", "Speed unit:"), AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        redJedinica.Controls.Add(jedinica);
        lay.Controls.Add(redJedinica);
        var redJezik = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        redJezik.Controls.Add(new Label { Text = "Jezik / Language:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        redJezik.Controls.Add(jezik);
        lay.Controls.Add(redJezik);
        var redProvidnost = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        redProvidnost.Controls.Add(new Label { Text = Jez.T("Providnost mini prikaza:", "Mini display transparency:"), AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        redProvidnost.Controls.Add(providnost);
        foreach (var c in new Control[] { start, mini, redProvidnost, vpn, tacna })
        {
            c.Margin = new Padding(0, 4, 0, 0);
            lay.Controls.Add(c);
        }
        lay.Controls.Add(Napomena(Jez.T("Saobraćaj kroz VPN prolazi i kroz mrežnu karticu, pa bi se sa VPN adapterom brojao dva puta.", "Traffic through a VPN also passes through the network card, so with the VPN adapter it would be counted twice.")));
        lay.Controls.Add(Napomena(Jez.T("Tačna potrošnja traži mali pomoćni program sa pravima administratora; Windows pita jednom, posle se pokreće sam. Bez njega se vidi koliko programa ima veza, ali ne i koliko koji troši.", "Exact traffic needs a small helper with administrator rights; Windows asks once, later it starts by itself. Without it you see how many connections a program has, but not how much it uses.")));
        var lista = string.Join(", ", adapteri);
        lay.Controls.Add(Napomena(Jez.T("Mere se: ", "Measured: ") + (lista.Length > 0 ? lista : Jez.T("nijedan adapter nije pronađen", "no adapter found"))));
        var folder = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        var putanja = Napomena($"{Jez.T("Podaci", "Data")}: {Podesavanja.Folder}");
        putanja.MaximumSize = new Size(330, 0);
        folder.Controls.Add(putanja);
        var otvori = new Button { Text = Jez.T("Otvori folder", "Open folder"), AutoSize = true };
        otvori.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Podesavanja.Folder);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{Podesavanja.Folder}\"");
            }
            catch (Exception e) { Podesavanja.Log("Folder nije otvoren", e); }
        };
        folder.Controls.Add(otvori);
        lay.Controls.Add(folder);
        lay.Controls.Add(Napomena($"{Podesavanja.Ime} {Podesavanja.VerzijaZaPrikaz} · " + Jez.T("ništa ne šalje na internet, sve ostaje na ovom računaru.", "sends nothing to the internet, everything stays on this computer.")));
        var dugmad = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0), Width = 460 };
        var otkazi = new Button { Text = Jez.T("Otkaži", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var sacuvaj = new Button { Text = Jez.T("Sačuvaj", "Save"), DialogResult = DialogResult.OK, AutoSize = true };
        dugmad.Controls.Add(otkazi);
        dugmad.Controls.Add(sacuvaj);
        lay.Controls.Add(dugmad);
        AcceptButton = sacuvaj;
        CancelButton = otkazi;
        Controls.Add(lay);
    }

    static Label Napomena(string s) => new()
    {
        Text = s, AutoSize = true, MaximumSize = new Size(460, 0),
        ForeColor = Color.FromArgb(0x6b, 0x69, 0x62), Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 4, 0, 2),
    };

    public void Upisi(Podesavanja p)
    {
        p.DanReseta = (int)dan.Value;
        p.PokretanjeSaWindowsom = start.Checked;
        p.MiniPrikaz = mini.Checked;
        p.MiniProvidnost = Providnosti[Math.Max(0, providnost.SelectedIndex)];
        p.Vpn = vpn.Checked;
        p.TacnaPotrosnja = tacna.Checked;
        p.JedinicaBrzine = jedinica.SelectedItem?.ToString() switch { "KB/s" => "KB/s", "Mbit/s" => "Mbit/s", _ => "MB/s" };
        p.Jezik = jezik.SelectedIndex == 1 ? "en" : "sr";
    }
}
