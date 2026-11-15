// The pipe from "BronxSoft Netmetar Merac": exact bytes per program, one line per second:
//   T;1234=down,up;5678=down,up        (down and up are bytes in that second)
// The helper needs administrator rights; this side stays without them.
using System.IO.Pipes;
using System.Text;

namespace Netmetar;

sealed class MeracPrograma
{
    readonly string kanal;

    /// <summary>True while the helper is connected.</summary>
    public bool Radi { get; private set; }

    /// <summary>The last second, per program name (for the connections dialog); replaced whole, never changed in place.</summary>
    public Dictionary<string, (long d, long u)> Sekund => sekund;
    volatile Dictionary<string, (long d, long u)> sekund = [];

    /// <summary>Raised on the thread of the pipe for every line; the program passes it to the UI thread.</summary>
    public event Action<Dictionary<string, (long d, long u)>> Primljeno;

    public MeracPrograma(string kanal) { this.kanal = kanal; }

    public void Slusaj()
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(kanal, PipeDirection.In, 1);
                    s.WaitForConnection();
                    Radi = true;
                    using var citac = new StreamReader(s, Encoding.UTF8);
                    string red;
                    while ((red = citac.ReadLine()) != null) Obradi(red);
                }
                catch (Exception e) { Podesavanja.Log("Kanal merača", e); Thread.Sleep(1000); }
                finally
                {
                    Radi = false;
                    sekund = [];
                }
            }
        }) { IsBackground = true, Name = "merac-programa" };
        t.Start();
    }

    void Obradi(string red)
    {
        var delovi = red.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (delovi.Length == 0 || delovi[0] != "T") return;
        var sek = new Dictionary<string, (long d, long u)>();
        for (int i = 1; i < delovi.Length; i++)
        {
            var deo = delovi[i].Split('=');
            if (deo.Length != 2 || !int.TryParse(deo[0], out var pid)) continue;
            var dve = deo[1].Split(',');
            if (dve.Length != 2 || !long.TryParse(dve[0], out var d) || !long.TryParse(dve[1], out var u)) continue;
            var ime = Merenje.ImeZaPid(pid);
            if (ime == null) continue;
            var v = sek.GetValueOrDefault(ime);
            sek[ime] = (v.d + d, v.u + u);
        }
        sekund = sek;
        Primljeno?.Invoke(sek);
    }
}
