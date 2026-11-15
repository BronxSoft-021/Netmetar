// BronxSoft Netmetar Merac - the small helper that measures exact traffic per program.
// It needs administrator rights (ETW kernel session), so it is started from a scheduled task
// created once from the main program; the main program stays without administrator rights.
//   "BronxSoft Netmetar Merac.exe"            measure (ETW) and send to the main program
//   "BronxSoft Netmetar Merac.exe" --test      invented numbers, no rights needed (for checking the pipe and the display)
//   "BronxSoft Netmetar Merac.exe" --install   create the scheduled task (one UAC, then silent starts)
//   "BronxSoft Netmetar Merac.exe" --remove    delete the scheduled task
//   "BronxSoft Netmetar Merac.exe" --debug     write the first events (id, task, opcode, bytes) to merac.log
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace NetmetarMerac;

static class Program
{
    /// <summary>The main program listens on this pipe; the name follows its test folder rule.</summary>
    static string Kanal => "BronxSoft-Netmetar-Merac-" + Environment.UserName
        + (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NETMETAR_FOLDER")) ? "" : "-test");

    static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BronxSoft Netmetar");

    static readonly object brava = new();

    public static void Log(string poruka)
    {
        try
        {
            lock (brava)
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "merac.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {poruka}{Environment.NewLine}");
            }
        }
        catch { /* nothing else to do */ }
    }

    const string Zadatak = "BronxSoft Netmetar Merac";

    static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--install")) return ZadatakNapravi();
            if (args.Contains("--remove")) return ZadatakObrisi();
            using var tok = new Tok(Kanal);
            if (args.Contains("--test")) return Test(tok);
            return Etw.Radi(tok, args.Contains("--debug"));
        }
        catch (Exception e)
        {
            Log("Greška: " + e);
            return 1;
        }
    }

    // ---- the scheduled task: one UAC, then the meter starts with Windows without asking ----

    static int ZadatakNapravi()
    {
        var exe = Environment.ProcessPath;
        var r = Pokreni("schtasks.exe", $"/create /tn \"{Zadatak}\" /tr \"\\\"{exe}\\\"\" /sc onlogon /rl highest /f");
        Log($"Zadatak napravljen ({r}): {exe}");
        return r == 0 ? 0 : 1;
    }

    static int ZadatakObrisi()
    {
        var r = Pokreni("schtasks.exe", $"/delete /tn \"{Zadatak}\" /f");
        Log($"Zadatak obrisan ({r})");
        return r == 0 ? 0 : 1;
    }

    static int Pokreni(string fajl, string argumenti)
    {
        var p = Process.Start(new ProcessStartInfo(fajl, argumenti) { UseShellExecute = false, CreateNoWindow = true });
        p.WaitForExit();
        return p.ExitCode;
    }

    // ---- invented numbers: the same pipe, no rights, to check the whole chain ----

    static int Test(Tok tok)
    {
        Log("Probni režim: izmišljeni brojevi");
        var rnd = new Random(1);
        var pidovi = new List<int>();
        foreach (var ime in new[] { "explorer", "msedge", "chrome", "svchost", "viber" })
        {
            try { pidovi.AddRange(Process.GetProcessesByName(ime).Take(1).Select(p => p.Id)); } catch { /* not running */ }
            if (pidovi.Count >= 3) break;
        }
        if (pidovi.Count == 0) pidovi.Add(Environment.ProcessId);
        Log("Probni PID-ovi: " + string.Join(",", pidovi));
        int promasaja = 0;
        while (true)
        {
            var red = new StringBuilder("T");
            foreach (var pid in pidovi)
                red.Append($";{pid}={rnd.Next(0, 6_000_000)},{rnd.Next(0, 400_000)}");
            if (tok.Posalji(red.ToString())) promasaja = 0;
            else if (++promasaja > 5) return 0;   // the main program is not there
            Thread.Sleep(1000);
        }
    }
}

/// <summary>The pipe to the main program: one line per second, "T;pid=down,up;pid=down,up".</summary>
sealed class Tok : IDisposable
{
    readonly string ime;
    NamedPipeClientStream tok;
    StreamWriter pisac;

    public Tok(string ime) { this.ime = ime; }

    public bool Povezan => tok is { IsConnected: true };

    public bool Posalji(string red)
    {
        for (int pokusaj = 0; pokusaj < 2; pokusaj++)
        {
            try
            {
                if (tok == null || !tok.IsConnected)
                {
                    tok?.Dispose();
                    tok = new NamedPipeClientStream(".", ime, PipeDirection.Out);
                    tok.Connect(3000);
                    pisac = new StreamWriter(tok, new UTF8Encoding(false)) { AutoFlush = true };
                }
                pisac.WriteLine(red);
                return true;
            }
            catch (Exception e)
            {
                Program.Log("Kanal nije otvoren: " + e.Message);
                tok?.Dispose();
                tok = null;
                Thread.Sleep(1000);
            }
        }
        return false;
    }

    public void Dispose()
    {
        try { pisac?.Dispose(); } catch { }
        tok?.Dispose();
    }
}
