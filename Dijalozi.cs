// Own dialogs in the look of the device: About and Help.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Netmetar;

/// <summary>Layered window (smooth rounded corners), dragged by anything but its buttons, with a scrollable list.</summary>
abstract class LcdDijalog : Form
{
    const int Korak = 42;

    protected readonly int DW, DH;   // size in design pixels
    protected int Pomak;             // scroll position of the list (design pixels)
    readonly RectangleF zatvoriR, iksR;

    protected abstract int ListaVisina { get; }
    protected abstract int Ukupno { get; }
    protected abstract void Crtaj(Graphics g);

    /// <param name="udeo">Width of the dialog as a part of the main window (smaller when the main window is small).</param>
    protected LcdDijalog(Icon ikona, string naslov, int dw, int dh, int roditeljSirina, float udeo)
    {
        DW = dw;
        DH = dh;
        zatvoriR = new(dw / 2f - 80, dh - 58, 160, 46);
        iksR = new(dw - 70, 8, 52, 40);
        float f = roditeljSirina > 0 ? Math.Clamp(roditeljSirina * udeo / dw, 0.75f, 1.3f) : DeviceDpi / 96f;
        f = Math.Min(f, Screen.PrimaryScreen.WorkingArea.Height * 0.92f / dh);
        AutoScaleMode = AutoScaleMode.None;
        Text = naslov;
        Icon = ikona;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size((int)Math.Round(dw * f), (int)Math.Round(dh * f));
        ShowInTaskbar = false;
        KeyPreview = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000;  // WS_EX_LAYERED
            return cp;
        }
    }

    float Faktor => Width / (float)DW;

    bool NaDugmetu(Point p)
    {
        var t = new PointF(p.X / Faktor, p.Y / Faktor);
        return zatvoriR.Contains(t) || iksR.Contains(t);
    }

    const int WM_NCHITTEST = 0x84;

    // everything except the two buttons drags the dialog
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_NCHITTEST || (int)m.Result != 1) return;  // 1 = HTCLIENT
        long l = m.LParam.ToInt64();
        var p = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
        if (!NaDugmetu(p)) m.Result = (IntPtr)2;  // HTCAPTION
    }

    void Pomeri(int pikseli)
    {
        int novo = Math.Clamp(Pomak + pikseli, 0, Math.Max(0, Ukupno - ListaVisina));
        if (novo == Pomak) return;
        Pomak = novo;
        Osvezi();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Pomeri(-Math.Sign(e.Delta) * Korak);
        base.OnMouseWheel(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int? pomak = e.KeyCode switch
        {
            Keys.Down => Korak,
            Keys.Up => -Korak,
            Keys.PageDown => ListaVisina - Korak,
            Keys.PageUp => Korak - ListaVisina,
            Keys.End => Ukupno,
            Keys.Home => -Ukupno,
            _ => null,
        };
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) { Close(); e.Handled = true; }
        else if (pomak != null) { Pomeri(pomak.Value); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && NaDugmetu(e.Location)) Close();
        base.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Cursor = NaDugmetu(e.Location) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }  // drawn by Osvezi (layered window)

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Osvezi();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Osvezi();
    }

    // ---- parts every dialog has ----

    protected Crtac Okvir(Graphics g, string naslov, RectangleF ekran, float ekranRadijus = 20)
    {
        var c = new Crtac(g, Faktor, 0, 0);
        c.Kuciste(new RectangleF(2, 2, DW - 4, DH - 4), 26);
        c.Tekst(30, 36, naslov, Crtac.Obican(14, 120), Crtac.NatpisKucista);
        c.Iks(DW - 44, 28, 7, Crtac.NatpisKucista, 3);
        c.Ekran(ekran, ekranRadijus);
        return c;
    }

    protected void DugmeZatvori(Crtac c) => c.Dugme(zatvoriR, Jez.T("ZATVORI", "CLOSE"), false, false);

    /// <summary>Scroll handle of the list, when the list does not fit.</summary>
    protected void Klizac(Crtac c, float x, float gore)
    {
        int ukupno = Ukupno, vidi = ListaVisina;
        if (ukupno <= vidi) return;
        float ruc = Math.Max(24, vidi * (float)vidi / ukupno);
        c.Pravougaonik(new RectangleF(x, gore + (vidi - ruc) * Pomak / (ukupno - vidi), 4, ruc), Crtac.Priguseno);
    }

    /// <summary>The dialog as a picture (also used for checking the look).</summary>
    public Bitmap Slika()
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        Crtaj(g);
        return bmp;
    }

    void Osvezi()
    {
        if (!IsHandleCreated) return;
        try
        {
            using var bmp = Slika();
            Sloj.Prikazi(Handle, bmp);
        }
        catch (Exception e) { Podesavanja.Log("Prozor nije osvežen", e); }
    }
}

/// <summary>About: icon, name, version and the list of changes.</summary>
sealed class OProgramuDijalog : LcdDijalog
{
    const int Sirina = 600, Visina = 420;
    const int ListaGore = 176;
    const float Levo = 182, Desno = Sirina - 44;

    readonly Icon izvor;
    Bitmap slikaIkone;

    protected override int ListaVisina => 168;
    protected override int Ukupno => Izmene.Spisak.Sum(VisinaUnosa);

    static int VisinaUnosa(Izmene.Unos u) => 26 + u.Stavke.Length * 21 + 12;  // same number of lines in both languages

    public OProgramuDijalog(Icon ikona, int roditeljSirina = 0)
        : base(ikona, Jez.T("O programu", "About"), Sirina, Visina, roditeljSirina, 0.55f)
    {
        izvor = ikona;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) slikaIkone?.Dispose();
        base.Dispose(disposing);
    }

    protected override void Crtaj(Graphics g)
    {
        var c = Okvir(g, Jez.T("O PROGRAMU", "ABOUT"), new RectangleF(16, 50, DW - 32, 300));

        // original app icon on the left
        try
        {
            if (slikaIkone == null)
            {
                using var velika = new Icon(izvor, 128, 128);
                slikaIkone = velika.ToBitmap();
            }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(slikaIkone, new RectangleF(35, 152, 96, 96));
        }
        catch (Exception ex) { Podesavanja.Log("Greška pri crtanju ikonice", ex); }
        c.Pravougaonik(new RectangleF(160, 80, 2, 240), Crtac.Linija);

        // title in the same font as the main window header (DSEG14 has no real X)
        float x = Levo;
        c.Tekst(x, 94, "BRONXSOFT · NETMETAR", Crtac.Obican(22, 120), Crtac.Cifra);

        c.Oval(new RectangleF(x, 102, 130, 26), "v" + Podesavanja.Verzija + "-" + Podesavanja.Faza, Crtac.Obican(14),
            Color.FromArgb(40, 0, 0, 0), Color.FromArgb(150, 238, 244, 240), Crtac.Cifra);
        c.Crta(x, Desno, 140, Crtac.Linija, 2);

        // Segoe UI (not DSEG14) so that Š, ž, č... are shown
        c.Tekst(x, 166, Jez.T("ŠTA JE NOVO", "WHAT’S NEW"), Crtac.Obican(17, 130), Crtac.Cifra);

        var staro = g.Clip;
        g.SetClip(new RectangleF(x - 6, ListaGore, Desno - x + 12, ListaVisina));
        float vrh = ListaGore + 2 - Pomak;
        foreach (var u in Izmene.Spisak)
        {
            int h = VisinaUnosa(u);
            if (vrh + h > ListaGore && vrh < ListaGore + ListaVisina)
            {
                c.Tekst(x, vrh + 16, $"{u.Broj} · {u.Oznaka}", Crtac.Obican(14, 105), Crtac.Cifra);
                c.Tekst(Desno, vrh + 16, u.Datum, Crtac.Obican(13), Crtac.Priguseno, 'd');
                float yy = vrh + 38;
                foreach (var st in u.Linije)
                {
                    c.Pravougaonik(new RectangleF(x + 2, yy - 9, 5, 5), Crtac.Cifra);
                    c.Tekst(x + 16, yy, st, Crtac.Obican(14), Crtac.Cifra);
                    yy += 21;
                }
                c.Crta(x, Desno, vrh + h - 5, Crtac.LinijaReda, 1);
            }
            vrh += h;
        }
        g.Clip = staro;

        Klizac(c, DW - 40, ListaGore);
        DugmeZatvori(c);
    }
}

/// <summary>Help: what each part of the program does and why (text in Pomoc.cs).</summary>
sealed class PomocDijalog : LcdDijalog
{
    const int Sirina = 760, Visina = 640;
    const float Levo = 44, Desno = Sirina - 58;
    const int ListaGore = 66, ListaVidi = Visina - 150;
    const int RedTeksta = 22, RedNaslova = 34, IzaStavke = 7, IzaOdeljka = 16;
    static readonly RectangleF EkranR = new(16, 50, Sirina - 32, Visina - 118);
    static readonly Color BojaTeksta = Color.FromArgb(232, 238, 244, 240);

    sealed record Red(string Tekst, float Y, bool Naslov, bool Tacka);

    readonly List<Red> redovi = [];
    readonly int ukupno;

    protected override int ListaVisina => ListaVidi;
    protected override int Ukupno => ukupno;

    public PomocDijalog(Icon ikona, int roditeljSirina = 0)
        : base(ikona, Jez.T("Pomoć", "Help"), Sirina, Visina, roditeljSirina, 0.75f)
    {
        // the language cannot change while the dialog is open, so the text is laid out once
        var f = Crtac.Citanje(15);
        float y = 0;
        foreach (var o in Pomoc.Spisak)
        {
            redovi.Add(new Red(Jez.Izaberi(o.Naslov), y, true, false));
            y += RedNaslova;
            foreach (var stavka in o.Stavke)
            {
                bool prvi = true;
                foreach (var linija in Prelomi(Jez.Izaberi(stavka), f, Desno - Levo - 16))
                {
                    redovi.Add(new Red(linija, y, false, prvi));
                    prvi = false;
                    y += RedTeksta;
                }
                y += IzaStavke;
            }
            y += IzaOdeljka;
        }
        ukupno = (int)y;
    }

    static List<string> Prelomi(string tekst, Pismo f, float sirina)
    {
        var linije = new List<string>();
        var sada = "";
        foreach (var rec in tekst.Split(' '))
        {
            var proba = sada.Length == 0 ? rec : sada + " " + rec;
            if (sada.Length > 0 && f.Sirina(proba) > sirina)
            {
                linije.Add(sada);
                sada = rec;
            }
            else sada = proba;
        }
        if (sada.Length > 0) linije.Add(sada);
        return linije;
    }

    protected override void Crtaj(Graphics g)
    {
        var c = Okvir(g, Jez.T("POMOĆ", "HELP"), EkranR);
        var naslov = Crtac.Obican(15, 112);
        var tekst = Crtac.Citanje(15);

        var staro = g.Clip;
        g.SetClip(new RectangleF(Levo - 6, ListaGore, Desno - Levo + 12, ListaVisina));
        foreach (var r in redovi)
        {
            float y = ListaGore + r.Y - Pomak;
            if (y + RedNaslova < ListaGore || y > ListaGore + ListaVisina) continue;
            if (r.Naslov)
            {
                c.Tekst(Levo, y + 18, r.Tekst, naslov, Crtac.Cifra);
                c.Crta(Levo, Desno, y + 26, Crtac.Linija, 1.5f);
                continue;
            }
            if (r.Tacka) c.Pravougaonik(new RectangleF(Levo + 2, y + 9, 5, 5), Crtac.Cifra);
            c.Tekst(Levo + 16, y + 16, r.Tekst, tekst, BojaTeksta);
        }
        g.Clip = staro;

        Klizac(c, DW - 40, ListaGore);
        DugmeZatvori(c);
    }

    /// <summary>Pictures of the whole text, screen by screen (for checking the look).</summary>
    public IEnumerable<Bitmap> SveStrane()
    {
        for (Pomak = 0; ; Pomak = Math.Min(Pomak + ListaVidi - RedTeksta, ukupno - ListaVidi))
        {
            yield return Slika();
            if (Pomak >= ukupno - ListaVidi) yield break;
        }
    }
}

/// <summary>Connections of one program: the ports it listens on and the connections it has open.
/// No DNS names are looked up: the program must not talk to the internet.</summary>
sealed class VezeDijalog : LcdDijalog
{
    const int Sirina = 660, Visina = 540;
    const float Levo = 44, Desno = Sirina - 58;
    const int ListaGore = 132, ListaVidi = Visina - 250;
    const int Red = 24;
    static readonly RectangleF EkranR = new(16, 50, Sirina - 32, Visina - 118);
    static readonly Color BojaTeksta = Color.FromArgb(232, 238, 244, 240);

    readonly string ime;
    readonly string potrosnja;
    readonly List<VezaStavka> stavke;
    readonly DateTime kada = DateTime.Now;

    protected override int ListaVisina => ListaVidi;
    protected override int Ukupno => stavke.Count * Red;

    public VezeDijalog(Icon ikona, string ime, List<VezaStavka> stavke, string potrosnja = "", int roditeljSirina = 0)
        : base(ikona, ime, Sirina, Visina, roditeljSirina, 0.6f)
    {
        this.ime = ime;
        this.stavke = stavke;
        this.potrosnja = potrosnja;
    }

    protected override void Crtaj(Graphics g)
    {
        var c = Okvir(g, Jez.T("VEZE PROGRAMA", "PROGRAM CONNECTIONS"), EkranR);
        var tekst = Crtac.Citanje(15);
        c.Tekst(Levo, 80, ime, Crtac.Obican(17, 120), Crtac.Cifra);
        int slusa = stavke.Count(x => x.Slusa);
        var sazetak = stavke.Count == 0
            ? Jez.T("NEMA VEZA", "NO CONNECTIONS")
            : $"{stavke.Count - slusa} {Jez.T("VEZA", "LINKS")}";
        c.Tekst(Desno, 80, sazetak, Crtac.Obican(15), Crtac.Priguseno, 'd');
        c.Crta(Levo, Desno, 94, Crtac.Linija, 1.5f);
        // exact traffic of this program, when the helper is running
        if (potrosnja.Length > 0) c.Tekst(Levo, 118, potrosnja, Crtac.Citanje(13), Crtac.Cifra);

        var staro = g.Clip;
        g.SetClip(new RectangleF(Levo - 6, ListaGore, Desno - Levo + 12, ListaVidi));
        for (int i = 0; i < stavke.Count; i++)
        {
            float y = ListaGore + i * Red - Pomak;
            if (y + Red < ListaGore || y > ListaGore + ListaVidi) continue;
            var s = stavke[i];
            if (s.Slusa)
            {
                c.Tekst(Levo + 2, y + 17, $"{Jez.T("SLUŠA", "LISTENS")} · {Jez.T("PORT", "PORT")} {s.Port}", tekst, Crtac.Cifra);
                c.Tekst(Desno, y + 17, s.IPv6 ? "TCP6" : "TCP4", Crtac.Citanje(13), Crtac.Priguseno, 'd');
            }
            else
            {
                c.Tekst(Levo + 2, y + 17, $"{s.Adresa}:{s.Port}", tekst, BojaTeksta);
                c.Tekst(Desno, y + 17, s.IPv6 ? "IPv6" : "IPv4", Crtac.Citanje(13), Crtac.Priguseno, 'd');
            }
        }
        g.Clip = staro;

        if (stavke.Count == 0)
            c.Tekst(Levo + 16, ListaGore + 26, Jez.T("PROGRAM TRENUTNO NIJE NA MREŽI", "THE PROGRAM IS NOT ON THE NETWORK RIGHT NOW"), tekst, Crtac.Priguseno);

        c.Tekst(Levo, Visina - 82, $"{Jez.T("SNIMLJENO", "TAKEN")} {kada:HH:mm:ss} · {Jez.T("BEZ DNS IMENA", "NO DNS NAMES")}", Crtac.Citanje(13), Crtac.Priguseno);
        Klizac(c, DW - 40, ListaGore);
        DugmeZatvori(c);
    }
}
