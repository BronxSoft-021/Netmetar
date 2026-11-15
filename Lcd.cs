// Drawing helpers for the LCD look. Everything is drawn on a fixed design canvas (units = pixels of the
// approved design) that is scaled to the window. Texts are drawn from glyph outlines (no ClearType fringes)
// and kept as small pictures, because filling outlines every second costs too much CPU.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Netmetar;

/// <summary>A font: family, style, size in canvas pixels and letter spacing in percent (like Qt).</summary>
/// <remarks>Keyed by the family name: every FontFamily.Name or hash is a call into GDI+.</remarks>
sealed record Pismo(string Ime, FontStyle Stil, float Px, float Razmak = 100, bool Slova = false, bool Tekst = false)
{
    // A label that contains letters is drawn completely (letters, digits, signs) in a clean font: DSEG14 draws Y, W, X, M...
    // badly and has no Š, Č, Ž. Pure numbers (no letters) stay in the DSEG segments; Tekst = true forces the clean font.
    public static FontFamily PorodicaSlova;
    public static string ImeSlova;
    const float SkalaSlova = 0.91f / 0.643f;   // cap height of Rajdhani -> height of the DSEG digits
    bool UTekstu(string s) => Slova && PorodicaSlova != null && (Tekst || s.Any(char.IsLetter));
    float PxSlova => Px * SkalaSlova;
    float AscentSlova => PorodicaSlova.GetCellAscent(FontStyle.Regular) * PxSlova / PorodicaSlova.GetEmHeight(FontStyle.Regular);

    static readonly Dictionary<string, FontFamily> porodice = [];
    public static void Dodaj(FontFamily f) => porodice[f.Name] = f;
    public FontFamily Porodica => porodice[Ime];
    public bool Dseg => Ime.StartsWith("DSEG");
    float Ascent => Porodica.GetCellAscent(Stil) * Px / Porodica.GetEmHeight(Stil);

    static readonly Dictionary<(string, FontStyle, float, char), float> sirine = [];
    static readonly Graphics merac = Graphics.FromImage(new Bitmap(1, 1));
    static readonly StringFormat Tipografski = MakeFormat();

    static StringFormat MakeFormat()
    {
        var f = (StringFormat)StringFormat.GenericTypographic.Clone();
        f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return f;
    }

    /// <summary>Advance of one character (DSEG: '!' is a blank as wide as a digit, '.' has no width).</summary>
    public float Korak(char c, bool tekst = false)
    {
        float k;
        if (tekst) k = Izmeri(ImeSlova, PorodicaSlova, FontStyle.Regular, PxSlova, c);
        else if (Dseg)
            k = Px * c switch { '.' => 0f, ' ' or ':' => 0.2f, '·' => 0.36f, '/' when Ime.StartsWith("DSEG7") => 0.58f, _ => 0.82f };
        else k = Izmeri(Ime, Porodica, Stil, Px, c);
        return k * Razmak / 100f;
    }

    static float Izmeri(string ime, FontFamily porodica, FontStyle stil, float px, char c)
    {
        var kljuc = (ime, stil, px, c);
        if (!sirine.TryGetValue(kljuc, out var k))
        {
            using var f = new Font(porodica, px, stil, GraphicsUnit.Pixel);
            k = sirine[kljuc] = merac.MeasureString(c.ToString(), f, PointF.Empty, Tipografski).Width;
        }
        return k;
    }

    public float Sirina(string s)
    {
        bool t = UTekstu(s);
        return s.Sum(c => Korak(c, t));
    }

    /// <summary>Outline of s with its baseline at y = 0.</summary>
    public GraphicsPath Put(string s)
    {
        var put = new GraphicsPath(FillMode.Winding);
        float x = 0, a = Ascent;
        bool t = UTekstu(s);
        foreach (var c in s)
        {
            if (c != ' ' && c != '!')
            {
                if (t) put.AddString(c.ToString(), PorodicaSlova, (int)FontStyle.Regular, PxSlova, new PointF(x, -AscentSlova), StringFormat.GenericTypographic);
                else put.AddString(c.ToString(), Porodica, (int)Stil, Px, new PointF(x, -a), StringFormat.GenericTypographic);
            }
            x += Korak(c, t);
        }
        return put;
    }
}

sealed class Slicica(Bitmap slika, float levo, float gore)
{
    public readonly Bitmap Slika = slika;
    public readonly float Levo = levo, Gore = gore;  // canvas units from the pen position on the baseline
}

/// <summary>Painter on the design canvas.</summary>
sealed class Crtac
{
    public static readonly Color Cifra = Color.FromArgb(0xee, 0xf4, 0xf0);
    public static readonly Color Duh = Color.FromArgb(16, 230, 240, 235);   // switched-off segments, ~7 %
    public static readonly Color Priguseno = Color.FromArgb(115, 238, 244, 240);
    public static readonly Color Linija = Color.FromArgb(38, 238, 244, 240);
    public static readonly Color LinijaReda = Color.FromArgb(26, 238, 244, 240);
    public static readonly Color NatpisKucista = Color.FromArgb(0x5a, 0x58, 0x52);
    public static readonly Color Uzbuna = Color.FromArgb(0xff, 0x6e, 0x5e);   // only for an outage of a watched program

    static readonly PrivateFontCollection fontovi = new();
    static PrivateFontCollection fontoviSlova;   // kept: the font lives as long as its collection
    public static FontFamily D7, D14, Segoe, Consolas;

    public static void UcitajFontove()
    {
        foreach (var ime in new[] { "DSEG7Classic-Bold.ttf", "DSEG14Classic-Bold.ttf", "DSEG14Classic-Regular.ttf" })
            DodajFont(fontovi, ime);
        D7 = fontovi.Families.First(f => f.Name == "DSEG7 Classic");
        D14 = fontovi.Families.First(f => f.Name == "DSEG14 Classic");
        var slova = UcitajSlova();  // without it the labels stay in DSEG14
        if (slova != null) { Pismo.PorodicaSlova = slova; Pismo.ImeSlova = slova.Name; }
        Segoe = Postoji("Segoe UI") ?? FontFamily.GenericSansSerif;
        Consolas = Postoji("Consolas") ?? FontFamily.GenericMonospace;
        foreach (var f in new[] { D7, D14, Segoe, Consolas }) Pismo.Dodaj(f);
        imeD7 = D7.Name; imeD14 = D14.Name; imeSegoe = Segoe.Name; imeConsolas = Consolas.Name;
    }

    static string imeD7, imeD14, imeSegoe, imeConsolas;

    static void DodajFont(PrivateFontCollection kolekcija, string ime)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ime);
        var bajtovi = new byte[s.Length];
        s.ReadExactly(bajtovi);
        var mem = Marshal.AllocCoTaskMem(bajtovi.Length);  // stays allocated: GDI+ reads it while drawing
        Marshal.Copy(bajtovi, 0, mem, bajtovi.Length);
        kolekcija.AddMemoryFont(mem, bajtovi.Length);
    }

    /// <summary>The text font, in a collection of its own: added to the DSEG collection, GDI+ accepted it without an
    /// error and left it out of the list of families in about every fifth start. The retry is only a safety net.</summary>
    static FontFamily UcitajSlova()
    {
        for (int pokusaj = 1; pokusaj <= 8; pokusaj++)
        {
            var kolekcija = new PrivateFontCollection();
            DodajFont(kolekcija, "Rajdhani-SemiBold.ttf");
            var slova = kolekcija.Families.FirstOrDefault(f => f.Name.StartsWith("Rajdhani"));
            if (slova != null)
            {
                fontoviSlova = kolekcija;
                if (pokusaj > 1) Podesavanja.Log($"Font slova ucitan iz {pokusaj}. pokusaja");
                return slova;
            }
            kolekcija.Dispose();
        }
        Podesavanja.Log("Font slova nije ucitan");
        return null;
    }

    static FontFamily Postoji(string ime)
    {
        try { return new FontFamily(ime); } catch { return null; }
    }

    public static Pismo F7(float px) => new(imeD7, FontStyle.Bold, px);
    public static Pismo F14(float px, bool tanko = false, float razmak = 106) => new(imeD14, tanko ? FontStyle.Regular : FontStyle.Bold, px, razmak, true);
    /// <summary>Like F14, but always in the clean text font (also for dates and other digits that are text).</summary>
    public static Pismo F14T(float px, bool tanko = false, float razmak = 106) => new(imeD14, tanko ? FontStyle.Regular : FontStyle.Bold, px, razmak, true, true);
    public static Pismo Obican(float px, float razmak = 100) => new(imeSegoe, FontStyle.Bold, px, razmak);
    public static Pismo Mono(float px) => new(imeConsolas, FontStyle.Bold, px, 104);
    /// <summary>For longer text that is read (Help).</summary>
    public static Pismo Citanje(float px) => new(imeSegoe, FontStyle.Regular, px);

    static readonly Dictionary<(Pismo, string, int, bool, float), Slicica> slicice = [];

    public readonly Graphics G;
    readonly float s, ox, oy;
    readonly Matrix platno;

    public Crtac(Graphics g, float skala, float pomakX, float pomakY)
    {
        G = g; s = skala; ox = pomakX; oy = pomakY;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        platno = new Matrix(skala, 0, 0, skala, pomakX, pomakY);
        g.Transform = platno;
    }

    Slicica Napravi(Pismo f, string tekst, Color boja, bool sjaj)
    {
        var kljuc = (f, tekst, boja.ToArgb(), sjaj, s);
        if (slicice.TryGetValue(kljuc, out var v)) return v;
        if (slicice.Count > 800)
        {
            foreach (var x in slicice.Values) x.Slika.Dispose();
            slicice.Clear();
        }
        using var put = f.Put(tekst);
        var sjajevi = new List<(GraphicsPath, Color)>();
        var r = put.GetBounds();
        if (sjaj)
            foreach (var (udeo, alfa) in new[] { (0.09f, 7), (0.045f, 12) })
            {
                var obris = (GraphicsPath)put.Clone();
                using var olovka = new Pen(Color.Black, Math.Max(1.5f, f.Px * udeo)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                obris.Widen(olovka);
                obris.FillMode = FillMode.Winding;
                r = RectangleF.Union(r, obris.GetBounds());
                sjajevi.Add((obris, Color.FromArgb(alfa, 220, 255, 240)));
            }
        r.Inflate(1, 1);
        var bmp = new Bitmap(Math.Max(1, (int)Math.Ceiling(r.Width * s)), Math.Max(1, (int)Math.Ceiling(r.Height * s)), PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(s, s);
            g.TranslateTransform(-r.Left, -r.Top);
            foreach (var (obris, b) in sjajevi)
            {
                using var cetka = new SolidBrush(b);
                g.FillPath(cetka, obris);
                obris.Dispose();
            }
            using var c = new SolidBrush(boja);
            g.FillPath(c, put);
        }
        return slicice[kljuc] = new Slicica(bmp, r.Left, r.Top);
    }

    void Zalepi(float x, float y, Pismo f, string tekst, Color boja, bool sjaj)
    {
        var sl = Napravi(f, tekst, boja, sjaj);
        // 1:1 in device pixels, at a whole pixel, so the picture stays sharp
        int dx = (int)Math.Round(ox + (x + sl.Levo) * s), dy = (int)Math.Round(oy + (y + sl.Gore) * s);
        G.ResetTransform();
        var im = G.InterpolationMode;
        G.InterpolationMode = InterpolationMode.NearestNeighbor;
        G.DrawImage(sl.Slika, dx, dy, sl.Slika.Width, sl.Slika.Height);
        G.InterpolationMode = im;
        G.Transform = platno;
    }

    /// <summary>Text with its baseline at y; x is the left / right / center edge. Returns the width.</summary>
    public float Tekst(float x, float y, string tekst, Pismo f, Color? boja = null, char poravnanje = 'l', bool sjaj = false)
    {
        float sirina = f.Sirina(tekst);
        if (poravnanje == 'd') x -= sirina;
        else if (poravnanje == 'c') x -= sirina / 2;
        if (!string.IsNullOrWhiteSpace(tekst)) Zalepi(x, y, f, tekst, boja ?? Cifra, sjaj);
        return sirina;
    }

    /// <summary>7-segment number with the unlit 8s behind it; y is the bottom of the digits.</summary>
    public float Seg(float x, float y, string vrednost, int mesta, float px, bool desno = false)
    {
        int cifara = vrednost.Count(c => c != '.');
        var v = new string('!', Math.Max(0, mesta - cifara)) + vrednost;
        var duh = new string(v.Select(c => c == '.' ? '.' : '8').ToArray());
        var f = F7(px);
        float sirina = f.Sirina(duh);
        if (desno) x -= sirina;
        Tekst(x, y, duh, f, Duh);
        // digit by digit, so that a few pictures cover every value
        float korak = sirina / Math.Max(mesta, cifara), pero = x;   // a number can be longer than its places
        foreach (var c in v)
        {
            if (c == '!') { pero += korak; continue; }
            Zalepi(pero, y, f, c.ToString(), Cifra, true);
            if (c != '.') pero += korak;
        }
        return sirina;
    }

    /// <summary>A small picture (program icon), smoothly scaled into r.</summary>
    public void Slika(Bitmap slika, RectangleF r)
    {
        var (im, po) = (G.InterpolationMode, G.PixelOffsetMode);
        G.InterpolationMode = InterpolationMode.HighQualityBicubic;
        G.PixelOffsetMode = PixelOffsetMode.Half;
        G.DrawImage(slika, r);
        (G.InterpolationMode, G.PixelOffsetMode) = (im, po);
    }

    /// <summary>The icon of a program; protected programs cannot be read, so they get their first letter in a circle.</summary>
    public void IkonaPrograma(string ime, string putanja, RectangleF r)
    {
        var ikona = Ikonice.Za(ime, putanja);
        if (ikona != null) Slika(ikona, r);
        else Oval(r, ime[..1], F14T(r.Height * 0.5f), Color.FromArgb(20, 238, 244, 240), Linija, Priguseno);
    }

    /// <summary>A lamp: a full circle when it is on, a ring when it is off.</summary>
    public void Lampa(float cx, float cy, float r, Color boja, bool upaljena)
    {
        if (upaljena)
        {
            using var b = new SolidBrush(boja);
            G.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        }
        else
        {
            using var pen = new Pen(boja, 2);
            G.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        }
    }

    public void Strelica(float cx, float cy, float vel, bool dole, Color? boja = null)
    {
        float h = vel * 0.87f;
        PointF[] t = dole
            ? [new(cx - vel / 2, cy - h / 2), new(cx + vel / 2, cy - h / 2), new(cx, cy + h / 2)]
            : [new(cx - vel / 2, cy + h / 2), new(cx + vel / 2, cy + h / 2), new(cx, cy - h / 2)];
        using var b = new SolidBrush(boja ?? Cifra);
        G.FillPolygon(b, t);
    }

    /// <summary>Rounded label ("pill") with centered text.</summary>
    public void Oval(RectangleF r, string tekst, Pismo f, Color punjenje, Color ivica, Color boja)
    {
        using (var p = Zaobljen(r, r.Height / 2))
        using (var b = new SolidBrush(punjenje))
        using (var pen = new Pen(ivica, 2))
        {
            G.FillPath(b, p);
            G.DrawPath(pen, p);
        }
        Tekst(r.Left + r.Width / 2, r.Top + r.Height / 2 + f.Px * 0.36f, tekst, f, boja, 'c');
    }

    /// <summary>Close glyph (X) centered at cx, cy.</summary>
    public void Iks(float cx, float cy, float pola, Color boja, float debljina = 3.5f)
    {
        using var pen = new Pen(boja, debljina) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        G.DrawLine(pen, cx - pola, cy - pola, cx + pola, cy + pola);
        G.DrawLine(pen, cx - pola, cy + pola, cx + pola, cy - pola);
    }

    /// <summary>Minimize glyph (a short horizontal bar) centered at cx, cy.</summary>
    public void Minus(float cx, float cy, float pola, Color boja, float debljina = 3.5f)
    {
        using var pen = new Pen(boja, debljina) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        G.DrawLine(pen, cx - pola, cy, cx + pola, cy);
    }

    public void Pravougaonik(RectangleF r, Color boja)
    {
        using var b = new SolidBrush(boja);
        G.FillRectangle(b, r);
    }

    public void Crta(float x1, float x2, float y, Color? boja = null, float debljina = 2) =>
        Pravougaonik(new RectangleF(x1, y - debljina / 2, x2 - x1, debljina), boja ?? Linija);

    public static GraphicsPath Zaobljen(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static LinearGradientBrush Preliv(PointF a, PointF b, params (float poz, Color boja)[] tacke)
    {
        // GDI+ repeats a gradient past its end: stretch it 3x and hold the last color, so it pads like Qt
        var kraj = new PointF(a.X + (b.X - a.X) * 3, a.Y + (b.Y - a.Y) * 3);
        var br = new LinearGradientBrush(a, kraj, tacke[0].boja, tacke[^1].boja);
        br.InterpolationColors = new ColorBlend
        {
            Positions = tacke.Select(t => t.poz / 3).Append(1f).ToArray(),
            Colors = tacke.Select(t => t.boja).Append(tacke[^1].boja).ToArray(),
        };
        return br;
    }

    // ---- device parts ----

    public void Kuciste(RectangleF r, float rad, float ivica = 3)
    {
        using (var p = Zaobljen(RectangleF.Inflate(r, -ivica / 2, -ivica / 2), rad))
        using (var b = Preliv(r.Location, new PointF(r.Right, r.Bottom + r.Height * 0.4f), (0, Color.FromArgb(0xf4, 0xf3, 0xef)), (1, Color.FromArgb(0xd9, 0xd7, 0xd0))))
        using (var pen = new Pen(Color.FromArgb(0xc9, 0xc7, 0xc0), ivica))
        {
            G.FillPath(b, p);
            G.DrawPath(pen, p);
        }
        if (ivica < 3) return;
        // inner edge: light on top, shadow at the bottom
        using var p2 = Zaobljen(RectangleF.Inflate(r, -4, -4), rad - 3);
        using var b2 = Preliv(new PointF(r.Left, r.Top - 1), new PointF(r.Left, r.Bottom + 1),
            (0, Color.FromArgb(230, 255, 255, 255)), (0.5f, Color.FromArgb(0, 255, 255, 255)),
            (0.97f, Color.FromArgb(0, 169, 166, 158)), (1, Color.FromArgb(255, 169, 166, 158)));
        using var pen2 = new Pen(b2, 2.5f);
        G.DrawPath(pen2, p2);
    }

    /// <summary>Dark green screen with its glass reflection.</summary>
    public void Ekran(RectangleF r, float rad)
    {
        using var put = Zaobljen(r, rad);
        using (var b = Preliv(new PointF(r.Left, r.Top - 1), new PointF(r.Left + r.Width * 0.17f, r.Bottom + 1),
                   (0, Color.FromArgb(0x17, 0x30, 0x29)), (0.55f, Color.FromArgb(0x0e, 0x1c, 0x18)), (1, Color.FromArgb(0x0b, 0x16, 0x13))))
            G.FillPath(b, put);
        var klip = G.Clip;
        G.SetClip(put);
        using (var b = Preliv(new PointF(r.Left, r.Top - 1), new PointF(r.Left, r.Top + 22), (0, Color.FromArgb(150, 0, 0, 0)), (1, Color.FromArgb(0, 0, 0, 0))))
            G.FillRectangle(b, r.Left, r.Top, r.Width, 22);
        G.Clip = klip;
        using (var pen = new Pen(Color.FromArgb(0x0a, 0x13, 0x10), 2))
        using (var p = Zaobljen(RectangleF.Inflate(r, -1, -1), rad))
            G.DrawPath(pen, p);
        using var odsjaj = Preliv(new PointF(r.Left - 1, r.Top - 1), new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.62f),
            (0, Color.FromArgb(18, 255, 255, 255)), (1, Color.FromArgb(0, 255, 255, 255)));
        G.FillPath(odsjaj, put);
    }

    static readonly Color DugmePuno = Color.FromArgb(0x0f, 0x1d, 0x19);

    /// <summary>Action button (OPCIJE / POMOĆ / O PROGRAMU): drawn icon ('z' = gear, '?' = help, 'i' = info) and a normal-font label.</summary>
    public void DugmeAkcija(RectangleF r, string natpis, char ikona, bool pritisnuto)
    {
        if (pritisnuto) r.Offset(0, 2);
        using (var p = Zaobljen(r, 22))
        using (var b = new SolidBrush(DugmePuno))
        using (var pen = new Pen(Color.FromArgb(0x9f, 0xa4, 0x9f), 3))
        {
            G.FillPath(b, p);
            G.DrawPath(pen, p);
        }
        using (var p = Zaobljen(RectangleF.Inflate(r, -2, -2), 20))
        using (var b = Preliv(new PointF(r.Left, r.Top), new PointF(r.Left, r.Top + 10),
            (0, Color.FromArgb(120, 0, 0, 0)), (1, Color.FromArgb(0, 0, 0, 0))))
        {
            var stari = G.Clip;
            G.SetClip(p);
            G.FillRectangle(b, r.Left, r.Top, r.Width, 10);
            G.Clip = stari;
        }
        // icon and label together, in the middle of the button
        float cy = r.Top + r.Height / 2;
        float x = r.Left + (r.Width - IkonaINatpis(natpis)) / 2;
        if (ikona == 'z') Zupcanik(x + IkonaSirina / 2, cy, 15, Cifra);
        else if (ikona == '?') Upitnik(x + IkonaSirina / 2, cy, 14, Cifra);
        else Info(x + IkonaSirina / 2, cy, 14, Cifra);
        Tekst(x + IkonaSirina + IkonaRazmak, cy + 7, natpis, Obican(19, 110), Cifra);
    }

    const float IkonaSirina = 30, IkonaRazmak = 10, AkcijaRub = 16;
    static float IkonaINatpis(string natpis) => IkonaSirina + IkonaRazmak + Obican(19, 110).Sirina(natpis);

    /// <summary>Width an action button needs for this label.</summary>
    public static float SirinaAkcije(string natpis) => IkonaINatpis(natpis) + 2 * AkcijaRub;

    /// <summary>Help icon: ring with a question mark.</summary>
    public void Upitnik(float cx, float cy, float r, Color boja)
    {
        using (var pen = new Pen(boja, 2.4f)) G.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        Tekst(cx, cy + 7, "?", Obican(19), boja, 'c');
    }

    /// <summary>Gear icon: body with 8 teeth and a hole in the color of the button.</summary>
    public void Zupcanik(float cx, float cy, float r, Color boja)
    {
        using var put = new GraphicsPath(FillMode.Winding);
        put.AddEllipse(cx - r * 0.72f, cy - r * 0.72f, r * 1.44f, r * 1.44f);
        for (int i = 0; i < 8; i++)
        {
            using var zub = new GraphicsPath();
            zub.AddRectangle(new RectangleF(cx - r * 0.2f, cy - r, r * 0.4f, r * 0.5f));
            using var m = new Matrix();
            m.RotateAt(i * 45, new PointF(cx, cy));
            zub.Transform(m);
            put.AddPath(zub, false);
        }
        using (var b = new SolidBrush(boja)) G.FillPath(b, put);
        using var rupa = new SolidBrush(DugmePuno);
        G.FillEllipse(rupa, cx - r * 0.3f, cy - r * 0.3f, r * 0.6f, r * 0.6f);
    }

    /// <summary>Info icon: ring with an "i".</summary>
    public void Info(float cx, float cy, float r, Color boja)
    {
        using (var pen = new Pen(boja, 2.4f)) G.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        using var b = new SolidBrush(boja);
        G.FillEllipse(b, cx - 1.9f, cy - r * 0.55f - 1.9f, 3.8f, 3.8f);
        G.FillRectangle(b, cx - 1.5f, cy - r * 0.15f, 3f, r * 0.65f);
    }

    public void Dugme(RectangleF r, string natpis, bool ukljuceno, bool pritisnuto)
    {
        if (pritisnuto) r.Offset(0, 2);
        using (var p = Zaobljen(r, 22))
        using (var b = new SolidBrush(ukljuceno ? Color.FromArgb(0x16, 0x32, 0x2b) : Color.FromArgb(0x0f, 0x1d, 0x19)))
        using (var pen = new Pen(ukljuceno ? Color.FromArgb(0xdf, 0xe6, 0xe0) : Color.FromArgb(0x9f, 0xa4, 0x9f), 3))
        {
            G.FillPath(b, p);
            G.DrawPath(pen, p);
        }
        using (var p = Zaobljen(RectangleF.Inflate(r, -2, -2), 20))
        using (var b = Preliv(new PointF(r.Left, r.Top), new PointF(r.Left, r.Top + 10), (0, Color.FromArgb(120, 0, 0, 0)), (1, Color.FromArgb(0, 0, 0, 0))))
        {
            var stari = G.Clip;
            G.SetClip(p);
            G.FillRectangle(b, r.Left, r.Top, r.Width, 10);
            G.Clip = stari;
        }
        Tekst(r.Left + r.Width / 2, r.Top + r.Height / 2 + 11, natpis, F14(22), null, 'c', ukljuceno);
    }

    /// <summary>Soft shadow around the device; the rest of the window is transparent. Alpha 1 cannot be seen,
    /// but it keeps the mouse: fully transparent pixels of a layered window let clicks through, also on the edges used for resizing.</summary>
    public static void PozadinaProzora(Graphics g, RectangleF uredjaj, float radijus)
    {
        g.Clear(Color.FromArgb(1, 0, 0, 0));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(Color.FromArgb(6, 0, 0, 0));
        for (int i = 12; i >= 1; i--)
        {
            var r = RectangleF.Inflate(uredjaj, i, i);
            r.Offset(0, 2);
            using var p = Zaobljen(r, radijus + i);
            g.FillPath(b, p);
        }
    }
}

/// <summary>Layered windows: per-pixel transparency, so rounded corners are smooth.</summary>
static class Sloj
{
    [StructLayout(LayoutKind.Sequential)] struct VELICINA { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct TACKA { public int x, y; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alfa, format; }

    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref VELICINA psize, IntPtr hdcSrc, ref TACKA pprSrc, int crKey, ref BLEND pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    /// <summary>Shows the picture as the window; the window stays where it is (and where the user dragged it).</summary>
    public static void Prikazi(IntPtr prozor, Bitmap bmp)
    {
        IntPtr ekran = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(ekran);
        IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0)), staro = SelectObject(mem, hbmp);
        var vel = new VELICINA { cx = bmp.Width, cy = bmp.Height };
        var nula = new TACKA();
        var blend = new BLEND { op = 0, flags = 0, alfa = 255, format = 1 };
        UpdateLayeredWindow(prozor, ekran, IntPtr.Zero, ref vel, mem, ref nula, 0, ref blend, 2);
        SelectObject(mem, staro);
        DeleteObject(hbmp);
        DeleteDC(mem);
        ReleaseDC(IntPtr.Zero, ekran);
    }
}

/// <summary>Number formats (decimal units, like the provider).</summary>
static class Format
{
    static string PrviKojiStane(double v, int mesta, params string[] formati)
    {
        foreach (var f in formati)
        {
            var s = v.ToString(f);
            if (s.Count(c => c != '.') <= mesta) return s;
        }
        return new string('9', mesta);
    }

    public static (string vrednost, string jedinica) BrzinaPrikaz(double bajtovaUSek, string izabranaJedinica = "MB/s", int mesta = 3)
    {
        double kb = Math.Max(0, bajtovaUSek / 1e3);
        double mb = Math.Max(0, bajtovaUSek / 1e6);

        // Bits: the way providers and speed tests say a speed (1 byte = 8 bits).
        if (izabranaJedinica == "Mbit/s")
        {
            double mbit = Math.Max(0, bajtovaUSek * 8 / 1e6);
            string s = mbit < 10
                ? PrviKojiStane(mbit, mesta, "F2", "F1", "F0")
                : PrviKojiStane(mbit, mesta, "F1", "F0");
            return (s, "Mbit/s");
        }

        // MB/s je fiksan ako je izabran; u KB/s režimu automatski
        // pređi na MB/s kada KB/s vrednost ne može bezbedno da stane.
        if (izabranaJedinica != "KB/s" || kb >= 999.5)
        {
            string s = mb < 10
                ? PrviKojiStane(mb, mesta, "F2", "F1", "F0")
                : PrviKojiStane(mb, mesta, "F1", "F0");
            return (s, "MB/s");
        }

        string kbText = kb < 100
            ? PrviKojiStane(kb, mesta, "F1", "F0")
            : PrviKojiStane(kb, mesta, "F0");
        return (kbText, "KB/s");
    }

    public static string Brzina(double bajtovaUSek, string jedinica = "MB/s", int mesta = 3)
        => BrzinaPrikaz(bajtovaUSek, jedinica, mesta).vrednost;

    public static string Gb(double bajtova, int mesta = 4)
    {
        double g = Math.Max(0, bajtova / 1e9);
        return g < 10 ? PrviKojiStane(g, mesta, "F2", "F1", "F0") : PrviKojiStane(g, mesta, "F1", "F0");
    }

    /// <summary>Time for the program list: "< 1 MIN", "12 MIN", "6 H 12 MIN".</summary>
    public static string Trajanje(long sekundi)
    {
        if (sekundi < 60) return "< 1 MIN";
        long minuta = sekundi / 60;
        return minuta < 60 ? $"{minuta} MIN" : $"{minuta / 60} H {minuta % 60} MIN";
    }

    public static string GbTabela(double bajtova)
    {
        double g = bajtova / 1e9;
        return g < 100 ? g.ToString("F2") : g.ToString("F1");
    }

    static readonly string[] MeseciSr = ["JANUAR", "FEBRUAR", "MART", "APRIL", "MAJ", "JUN", "JUL", "AVGUST",
        "SEPTEMBAR", "OKTOBAR", "NOVEMBAR", "DECEMBAR"];
    static readonly string[] MeseciEn = ["JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST",
        "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"];
    static readonly string[] DaniSr = ["PON", "UTO", "SRE", "ČET", "PET", "SUB", "NED"];
    static readonly string[] DaniEn = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];
    public static string[] Meseci => Jez.En ? MeseciEn : MeseciSr;
    public static string[] Dani => Jez.En ? DaniEn : DaniSr;

    public static string Dan(DateOnly d) => Dani[((int)d.DayOfWeek + 6) % 7];
    public static string Dana(int n) => Jez.En ? (n == 1 ? "DAY" : "DAYS") : (n % 10 == 1 && n % 100 != 11 ? "DAN" : "DANA");
}
