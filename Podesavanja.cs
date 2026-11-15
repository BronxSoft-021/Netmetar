// Paths, settings (podesavanja.json, snake_case keys), error log, start with Windows.
using System.Text.Json;

namespace Netmetar;

sealed class Podesavanja
{
    public int DanReseta { get; set; } = 1;
    public bool PokretanjeSaWindowsom { get; set; } = true;
    public bool MiniPrikaz { get; set; } = true;
    public int[] MiniPozicija { get; set; }   // [x, y]
    public int MiniProvidnost { get; set; } = 40;   // percent: 0, 20, 40 or 60
    public string[] CuvaniProgrami { get; set; } = [];   // the Watch: display names of the programs that must stay online
    public int[] Prozor { get; set; }         // [x, y, w, h]
    public bool Vpn { get; set; }             // count VPN adapters too (traffic would be counted twice)
    public bool TacnaPotrosnja { get; set; }  // exact traffic per program (the helper with administrator rights)
    public string JedinicaBrzine { get; set; } = "Mbit/s"; // KB/s, MB/s or Mbit/s
    public string Jezik { get; set; } = "sr";   // "sr" or "en"
    public string PokretanjeCilj { get; set; } = ""; // what the startup shortcut points to

    public static readonly string Verzija = Izmene.Spisak[0].Broj;   // worked out from Izmene.cs; keep Netmetar.csproj in step
    public const string Faza = "beta";       // beta until 1.0.0
    public static readonly string VerzijaZaPrikaz = Verzija + " " + Faza;
    public const string Ime = "BronxSoft Netmetar";
    const string StaroIme = "UnderGRAND Netmetar";   // name before 0.30.24: data folder and shortcuts are moved over

    // NETMETAR_FOLDER lets tests use a scratch folder instead of the real data
    public static readonly string TestFolder = Environment.GetEnvironmentVariable("NETMETAR_FOLDER");
    public static readonly string Folder = OdrediFolder();

    /// <summary>%APPDATA%\BronxSoft Netmetar; the data of the old name is moved there (kept where it is if the move fails).</summary>
    static string OdrediFolder()
    {
        if (!string.IsNullOrEmpty(TestFolder)) return TestFolder;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var novi = Path.Combine(appData, Ime);
        var stari = Path.Combine(appData, StaroIme);
        try
        {
            if (!Directory.Exists(novi) && Directory.Exists(stari)) Directory.Move(stari, novi);
        }
        catch
        {
            // e.g. the old copy still runs and holds the database: keep using the old folder, nothing is lost
            if (Directory.Exists(stari) && !Directory.Exists(novi)) return stari;
        }
        return novi;
    }
    public static string Baza => Path.Combine(Folder, "netmetar.db");
    static string Fajl => Path.Combine(Folder, "podesavanja.json");
    static string Greske => Path.Combine(Folder, "greske.log");

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static Podesavanja Ucitaj()
    {
        var p = new Podesavanja();
        try
        {
            if (File.Exists(Fajl))
                p = JsonSerializer.Deserialize<Podesavanja>(File.ReadAllText(Fajl), Json) ?? p;
        }
        catch (Exception e) { Log("Podešavanja nisu pročitana, koristim podrazumevana", e); }
        p.DanReseta = Math.Clamp(p.DanReseta, 1, 31);
        p.MiniProvidnost = Math.Clamp(p.MiniProvidnost, 0, 60);
        p.CuvaniProgrami ??= [];
        Jez.En = p.Jezik == "en";
        return p;
    }

    public void Sacuvaj()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = Fajl + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, Fajl, true);
        }
        catch (Exception e) { Log("Podešavanja nisu sačuvana", e); }
    }

    static readonly object logBrava = new();

    public static void Log(string poruka, Exception e = null)
    {
        try
        {
            lock (logBrava)
            {
                Directory.CreateDirectory(Folder);
                var f = new FileInfo(Greske);
                if (f.Exists && f.Length > 500_000)
                    File.Move(Greske, Greske + ".1", true);
                File.AppendAllText(Greske, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {poruka}{(e != null ? "\n" + e : "")}\n");
            }
        }
        catch { /* nothing else to do */ }
    }

    // ---- start with Windows: shortcut in shell:startup ----

    static string StartupLnk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Ime + ".lnk");

    /// <summary>Make the startup shortcut match the setting; rewritten only when missing or pointing elsewhere.</summary>
    public void UskladiPokretanje()
    {
        if (!string.IsNullOrEmpty(TestFolder)) return; // test runs leave Windows startup alone
        try
        {
            var staro = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StaroIme + ".lnk");
            if (File.Exists(staro)) File.Delete(staro);  // shortcut of the old name
            var exe = Environment.ProcessPath;
            var oznaka = $"{exe} --skriveno";
            if (PokretanjeSaWindowsom)
            {
                if (File.Exists(StartupLnk) && PokretanjeCilj == oznaka) return;
                NapraviPrecicu(StartupLnk, exe, "--skriveno");
                PokretanjeCilj = oznaka;
                Sacuvaj();
            }
            else if (File.Exists(StartupLnk))
            {
                File.Delete(StartupLnk);
                PokretanjeCilj = "";
                Sacuvaj();
            }
        }
        catch (Exception e) { Log("Prečica za pokretanje sa Windowsom nije podešena", e); }
    }

    public static void NapraviPrecicu(string lnk, string cilj, string argumenti)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnk));
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic s = shell.CreateShortcut(lnk);
        s.TargetPath = cilj;
        s.Arguments = argumenti;
        s.WorkingDirectory = Path.GetDirectoryName(cilj);
        s.IconLocation = cilj + ",0";
        s.Description = Ime;
        s.Save();
    }
}
