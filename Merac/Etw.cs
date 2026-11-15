// ETW: one kernel session (Microsoft-Windows-Kernel-Network) gives every TCP/UDP transfer with its PID.
// Event ids: TCPv4 sent 10 / received 11, TCPv6 26 / 27, UDPv4 42 / 43, UDPv6 58 / 59.
// Payload starts with PID (u32) and size (u32); retransmissions (14, 30) are not new data and are ignored.
using System.Runtime.InteropServices;
using System.Text;

namespace NetmetarMerac;

static partial class Etw
{
    const string Sesija = "BronxSoft-Netmetar";
    static readonly Guid Provajder = new("7DD42A49-5329-4832-8DFD-43D979153A88");

    static readonly Dictionary<int, (long d, long u)> tik = new();
    static Tok tok;
    static bool debug;
    static int prijavljeno;
    static ulong potrosac;

    /// <summary>Starts the session and blocks in ProcessTrace until the session is stopped.</summary>
    public static int Radi(Tok veza, bool saDebug)
    {
        tok = veza;
        debug = saDebug;
        if (!Otvori(out var greska))
        {
            Program.Log("ETW sesija nije otvorena: " + greska);
            return 2;
        }
        Program.Log("ETW sesija otvorena");
        var slanje = new Thread(PosaljiPetlju) { IsBackground = true, Name = "slanje" };
        slanje.Start();
        try { ProcessTrace([potrosac], 1, IntPtr.Zero, IntPtr.Zero); }
        catch (Exception e) { Program.Log("ProcessTrace: " + e); }
        finally { Zatvori(); }
        return 0;
    }

    static bool Otvori(out string greska)
    {
        greska = null;
        Zatvori();  // an old session of the same name would keep the buffers
        int velicina = Marshal.SizeOf<EVENT_TRACE_PROPERTIES>() + (Sesija.Length + 1) * 2;
        var svojstva = Marshal.AllocHGlobal(velicina);
        try
        {
            for (int i = 0; i < velicina; i++) Marshal.WriteByte(svojstva, i, 0);
            var p = new EVENT_TRACE_PROPERTIES
            {
                Wnode = new WNODE_HEADER { BufferSize = (uint)velicina, ClientContext = 1, Flags = 0x00020000 },
                BufferSize = 256,
                MinimumBuffers = 16,
                MaximumBuffers = 64,
                LogFileMode = 0x00000100,   // EVENT_TRACE_REAL_TIME_MODE
                FlushTimer = 1,
                LoggerNameOffset = (uint)Marshal.SizeOf<EVENT_TRACE_PROPERTIES>(),
            };
            Marshal.StructureToPtr(p, svojstva, false);
            var ime = Sesija + "\0";
            Marshal.Copy(ime.ToCharArray(), 0, svojstva + (int)p.LoggerNameOffset, ime.Length);

            int rc = StartTraceW(out var sesija, Sesija, svojstva);
            if (rc != 0)
            {
                greska = rc == 5 ? "nema administratorskih prava (5)" : $"StartTrace {rc}";
                return false;
            }
            var provajder = Provajder;   // a static readonly field cannot be passed by ref
            rc = EnableTraceEx2(sesija, ref provajder, 1, 4, 0, 0, 0, IntPtr.Zero);
            if (rc != 0)
            {
                greska = rc == 5 ? "nema administratorskih prava (5)" : $"EnableTraceEx2 {rc}";
                ControlTraceW(0, Sesija, svojstva, 1);
                return false;
            }

            var fajl = new EVENT_TRACE_LOGFILEW
            {
                LoggerName = Sesija,
                ProcessTraceMode = 0x00000100 | 0x00100000,   // real time + event records
                EventRecordCallback = Marshal.GetFunctionPointerForDelegate(callback),
            };
            potrosac = OpenTraceW(ref fajl);
            if (potrosac == ulong.MaxValue)
            {
                greska = "OpenTrace nije uspeo";
                ControlTraceW(0, Sesija, svojstva, 1);
                return false;
            }
            return true;
        }
        finally { Marshal.FreeHGlobal(svojstva); }
    }

    static void Zatvori()
    {
        try
        {
            if (potrosac != 0 && potrosac != ulong.MaxValue) CloseTrace(potrosac);
            potrosac = 0;
            IntPtr svojstva = IntPtr.Zero;
            try
            {
                int velicina = Marshal.SizeOf<EVENT_TRACE_PROPERTIES>() + (Sesija.Length + 1) * 2;
                svojstva = Marshal.AllocHGlobal(velicina);
                for (int i = 0; i < velicina; i++) Marshal.WriteByte(svojstva, i, 0);
                var p = new EVENT_TRACE_PROPERTIES
                {
                    Wnode = new WNODE_HEADER { BufferSize = (uint)velicina },
                    LoggerNameOffset = (uint)Marshal.SizeOf<EVENT_TRACE_PROPERTIES>(),
                };
                Marshal.StructureToPtr(p, svojstva, false);
                var ime = Sesija + "\0";
                Marshal.Copy(ime.ToCharArray(), 0, svojstva + (int)p.LoggerNameOffset, ime.Length);
                ControlTraceW(0, Sesija, svojstva, 1);   // EVENT_TRACE_CONTROL_STOP
            }
            finally { if (svojstva != IntPtr.Zero) Marshal.FreeHGlobal(svojstva); }
        }
        catch (Exception e) { Program.Log("Sesija nije zatvorena: " + e.Message); }
    }

    /// <summary>Once a second the tally goes to the main program and is cleared.</summary>
    static void PosaljiPetlju()
    {
        int promasaja = 0;
        while (true)
        {
            Thread.Sleep(1000);
            string red;
            lock (tik)
            {
                var sb = new StringBuilder("T");
                foreach (var (pid, v) in tik) sb.Append($";{pid}={v.d},{v.u}");
                red = sb.ToString();
                tik.Clear();
            }
            if (tok.Posalji(red)) promasaja = 0;
            else if (++promasaja > 5) return;   // the main program is gone
        }
    }

    static readonly DogadjajCallback callback = Dogadjaj;

    /// <summary>Must be quick: only the id, PID and size are read; the rest of the event is not touched.</summary>
    static void Dogadjaj(IntPtr zapis)
    {
        try
        {
            int id = Marshal.ReadInt16(zapis, 40);
            bool salje = id is 10 or 26 or 42 or 58;
            bool prima = id is 11 or 27 or 43 or 59;
            if (!salje && !prima)
            {
                if (debug && prijavljeno < 40 && id is >= 10 and <= 60)
                {
                    prijavljeno++;
                    Program.Log($"preskočen id={id}");
                }
                return;
            }
            int duzina = (ushort)Marshal.ReadInt16(zapis, 94);
            if (duzina < 8) return;
            var podaci = Marshal.ReadIntPtr(zapis, 104);
            if (podaci == IntPtr.Zero) return;
            int pid = Marshal.ReadInt32(podaci);
            int velicina = Marshal.ReadInt32(podaci, 4);
            if (pid <= 0 || velicina <= 0) return;
            lock (tik)
            {
                var v = tik.GetValueOrDefault(pid);
                if (prima) v.u += velicina; else v.d += velicina;
                tik[pid] = v;
            }
            if (debug && prijavljeno < 40)
            {
                prijavljeno++;
                Program.Log($"id={id} pid={pid} size={velicina} len={duzina}");
            }
        }
        catch (Exception e) { Program.Log("Događaj nije pročitan: " + e.Message); }
    }
}
