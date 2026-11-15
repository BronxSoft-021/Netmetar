// What is known about the programs online: time online today, their icons and the Watch (a program that must stay online).
// Runs every 3 seconds, also while the windows are hidden: the Watch is useful exactly then.
namespace Netmetar;

enum Cuvanje { Ne, Radi, Prekid, Ugasen }

/// <summary>One row of the program list; a watched program that is offline has Veza = 0.</summary>
sealed record RedPrograma(string Ime, int Veza, string Putanja, long Sekundi, Cuvanje Cuvar, string Napomena, bool Slusa);

sealed class Pracenje
{
    const int Milost = 20_000;          // a short break is not an outage: programs reconnect, and they need time after they start
    const int ProveraProcesa = 30_000;  // the list of all processes costs: it is read rarely, and only when a watched program is offline
    const int Budjenje = 15_000;        // a longer gap between two readings = the computer was asleep

    sealed class Cuvani
    {
        public long Videno;             // last time online (or the start of the waiting), TickCount64
        public DateTime VidenoKada;
        public bool Ugasen;
        public long SledecaProvera;     // for a program whose state is already known; a fresh loss is checked at once
        public long PrekidId;
        public DateTime? PrekidOd;
        public DateTime? PoslednjiOd, PoslednjiDo;
    }

    readonly Podesavanja p;
    readonly Baza baza;
    readonly Dictionary<string, Cuvani> cuvani = [];
    readonly Dictionary<string, string> putanje = [];
    Dictionary<string, long> sekundi = [];
    Dictionary<(string dan, string ime), long> naCekanju = [];
    List<VezaPrograma> veze = [];
    DateOnly dan = DateOnly.FromDateTime(DateTime.Today);
    long poslednje = Environment.TickCount64;

    public List<RedPrograma> Redovi { get; private set; } = [];
    /// <summary>The first watched program in an outage, or null.</summary>
    public string UPrekidu { get; private set; }

    public Pracenje(Podesavanja podesavanja, Baza b)
    {
        p = podesavanja;
        baza = b;
        try { sekundi = baza.ProgramiDana(dan); }
        catch (Exception e) { Podesavanja.Log("Vreme programa nije pročitano", e); }
        foreach (var ime in p.CuvaniProgrami) Dodaj(ime);
    }

    Cuvani Dodaj(string ime)
    {
        var c = new Cuvani { Videno = Environment.TickCount64, VidenoKada = DateTime.Now };
        try { (c.PoslednjiOd, c.PoslednjiDo) = baza.PoslednjiPrekid(ime); }
        catch (Exception e) { Podesavanja.Log("Poslednji prekid nije pročitan", e); }
        return cuvani[ime] = c;
    }

    /// <summary>Turns the Watch on or off for a program.</summary>
    public void Prebaci(string ime)
    {
        if (cuvani.TryGetValue(ime, out var c))
        {
            Zatvori(c);
            cuvani.Remove(ime);
        }
        else Dodaj(ime);
        p.CuvaniProgrami = [.. cuvani.Keys];
        p.Sacuvaj();
        Slozi();
    }

    public void Osvezi(bool pauza)
    {
        long sada = Environment.TickCount64, proteklo = sada - poslednje;
        bool budjenje = proteklo > Budjenje;
        var danas = DateOnly.FromDateTime(DateTime.Today);
        if (danas != dan)
        {
            dan = danas;
            sekundi = [];
        }
        veze = Merenje.ProgramiNaInternetu();
        foreach (var v in veze)
            if (v.Putanja != null) putanje[v.Ime] = v.Putanja;
        if (budjenje) poslednje = sada;
        else if (proteklo >= 1000)
        {
            long sek = proteklo / 1000;
            poslednje += sek * 1000;
            if (!pauza)
                foreach (var v in veze)
                {
                    sekundi[v.Ime] = sekundi.GetValueOrDefault(v.Ime) + sek;
                    var kljuc = (dan.ToString("yyyy-MM-dd"), v.Ime);
                    naCekanju[kljuc] = naCekanju.GetValueOrDefault(kljuc) + sek;
                }
        }
        Prati(sada, budjenje);
        Slozi();
    }

    void Prati(long sada, bool budjenje)
    {
        if (cuvani.Count == 0) return;
        var naMrezi = veze.Select(v => v.Ime).ToHashSet();
        HashSet<string> pokrenuti = null;
        foreach (var (ime, c) in cuvani)
        {
            if (naMrezi.Contains(ime))
            {
                Vidjen(c, sada);
                c.Ugasen = false;
                c.SledecaProvera = 0;
                Zatvori(c);
                continue;
            }
            if (budjenje) Vidjen(c, sada);   // the connections need a moment after waking up
            if (sada - c.Videno < Milost || sada < c.SledecaProvera) continue;
            pokrenuti ??= Merenje.PokrenutiProgrami();
            c.SledecaProvera = sada + ProveraProcesa;
            if (!pokrenuti.Contains(ime))
            {
                // closed on purpose is not an outage
                Zatvori(c);
                c.Ugasen = true;
                Vidjen(c, sada);
            }
            else if (c.Ugasen)
            {
                c.Ugasen = false;            // it has just started: it gets time to connect
                Vidjen(c, sada);
            }
            else if (c.PrekidOd == null) Otvori(ime, c);
        }
    }

    static void Vidjen(Cuvani c, long sada)
    {
        c.Videno = sada;
        c.VidenoKada = DateTime.Now;
    }

    void Otvori(string ime, Cuvani c)
    {
        c.PrekidOd = c.VidenoKada;   // the outage began when the program was last seen online
        try { c.PrekidId = baza.PocniPrekid(ime, c.VidenoKada); }
        catch (Exception e) { Podesavanja.Log("Prekid nije upisan", e); }
    }

    void Zatvori(Cuvani c)
    {
        if (c.PrekidOd == null) return;
        var kraj = DateTime.Now;
        try { if (c.PrekidId != 0) baza.ZavrsiPrekid(c.PrekidId, kraj); }
        catch (Exception e) { Podesavanja.Log("Kraj prekida nije upisan", e); }
        (c.PoslednjiOd, c.PoslednjiDo) = (c.PrekidOd, kraj);
        c.PrekidOd = null;
        c.PrekidId = 0;
    }

    static string Vreme(DateTime t) => t.Date == DateTime.Today ? $"{t:HH:mm}" : $"{t:dd.MM HH:mm}";

    static string Trajanje(DateTime od, DateTime doKad) => Format.Trajanje((long)(doKad - od).TotalSeconds);

    /// <summary>Builds the rows again (also after a change of language): outages first, then the other watched programs, then the rest.</summary>
    public void Slozi()
    {
        var naMrezi = new List<RedPrograma>();
        var bezVeze = new List<RedPrograma>();
        foreach (var v in veze)
        {
            var (stanje, napomena) = (Cuvanje.Ne, "");
            if (cuvani.TryGetValue(v.Ime, out var c))
            {
                stanje = Cuvanje.Radi;
                napomena = c.PoslednjiOd is DateTime od
                    ? $"{Jez.T("PREKID", "DOWN")} {Vreme(od)}" + (c.PoslednjiDo is DateTime kraj ? $" · {Trajanje(od, kraj)}" : "")
                    : Jez.T("RADI", "OK");
            }
            naMrezi.Add(new(v.Ime, v.Veza, v.Putanja, sekundi.GetValueOrDefault(v.Ime), stanje, napomena, v.Slusa));
        }
        foreach (var (ime, c) in cuvani)
        {
            if (veze.Any(v => v.Ime == ime)) continue;
            var (stanje, napomena) = c.PrekidOd is DateTime od
                ? (Cuvanje.Prekid, $"{Jez.T("PREKID", "DOWN")} {Vreme(od)} · {Trajanje(od, DateTime.Now)}")
                : (Cuvanje.Ugasen, c.Ugasen ? Jez.T("UGAŠEN", "NOT RUNNING") : Jez.T("PROVERAVAM", "CHECKING"));
            bezVeze.Add(new(ime, 0, putanje.GetValueOrDefault(ime), sekundi.GetValueOrDefault(ime), stanje, napomena, false));
        }
        Redovi = [.. bezVeze.Where(r => r.Cuvar == Cuvanje.Prekid), .. naMrezi.Where(r => r.Cuvar != Cuvanje.Ne),
            .. bezVeze.Where(r => r.Cuvar != Cuvanje.Prekid), .. naMrezi.Where(r => r.Cuvar == Cuvanje.Ne)];
        UPrekidu = bezVeze.FirstOrDefault(r => r.Cuvar == Cuvanje.Prekid)?.Ime;
    }

    /// <summary>Time online goes to the database once a minute, together with the traffic.</summary>
    public void Upisi()
    {
        if (naCekanju.Count == 0) return;
        try
        {
            baza.UpisiPrograme(naCekanju);
            naCekanju = [];
        }
        catch (Exception e) { Podesavanja.Log("Vreme programa nije upisano, pokušavam ponovo za minut", e); }
    }
}

/// <summary>Icons of the programs, taken from their .exe files; read once per program.</summary>
static class Ikonice
{
    static readonly Dictionary<string, Bitmap> slike = [];

    public static Bitmap Za(string ime, string putanja)
    {
        if (slike.TryGetValue(ime, out var b)) return b;
        if (putanja == null) return null;   // may become known later
        try
        {
            using var ikona = Icon.ExtractAssociatedIcon(putanja);
            b = ikona?.ToBitmap();
        }
        catch { /* the file cannot be read: the row stays without an icon */ }
        return slike[ime] = b;
    }
}
