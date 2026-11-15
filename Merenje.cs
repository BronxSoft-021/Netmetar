// Traffic measurement straight from Windows (GetIfTable2) and programs with connections (GetExtendedTcpTable).
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Netmetar;

record VezaPrograma(string Ime, int Veza, string Putanja, bool Slusa);

/// <summary>One line of the connections dialog: a listening port or an open connection.</summary>
record VezaStavka(string Adresa, int Port, bool IPv6, bool Slusa);

static class Merenje
{
    // Never counted, even if Windows reports them as hardware
    static readonly string[] Virtuelni = ["loopback", "virtualbox", "vmware", "hyper-v", "vethernet", "wsl", "bluetooth",
        "kernel debug", "wi-fi direct", "isatap", "teredo", "6to4", "ip-https", "miniport", "npcap", "pseudo-interface"];
    static readonly string[] Vpn = ["vpn", "tap-windows", "tap-win", "wireguard", "wintun", "openvpn", "nordlynx", "proton",
        "fortinet", "anyconnect", "zerotier", "tailscale", "hamachi", "radmin"];

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MIB_IF_ROW2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Alias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Description;
        public uint PhysicalAddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PhysicalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PermanentPhysicalAddress;
        public uint Mtu, Type;
        public int TunnelType, MediaType, PhysicalMediumType, AccessType, DirectionType;
        public byte Flags;   // bit 0 HardwareInterface, bit 1 FilterInterface
        public int OperStatus, AdminStatus, MediaConnectState;
        public Guid NetworkGuid;
        public int ConnectionType;
        public ulong TransmitLinkSpeed, ReceiveLinkSpeed;
        public ulong InOctets, InUcastPkts, InNUcastPkts, InDiscards, InErrors, InUnknownProtos, InUcastOctets,
            InMulticastOctets, InBroadcastOctets, OutOctets, OutUcastPkts, OutNUcastPkts, OutDiscards, OutErrors,
            OutUcastOctets, OutMulticastOctets, OutBroadcastOctets, OutQLen;
    }

    [DllImport("iphlpapi.dll")] static extern int GetIfTable2(out IntPtr tabela);
    [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr tabela);
    [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr tabela, ref int velicina, bool sortirano, int af, int klasa, uint r);

    static readonly int VelicinaReda = Marshal.SizeOf<MIB_IF_ROW2>();
    static readonly int PomakIn = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.InOctets));
    static readonly int PomakOut = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.OutOctets));
    static readonly int PomakBrzina = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.TransmitLinkSpeed));
    static readonly int PomakBrzinaIn = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.ReceiveLinkSpeed));
    static readonly int PomakGreskeIn = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.InErrors));
    static readonly int PomakGreskeOut = (int)Marshal.OffsetOf<MIB_IF_ROW2>(nameof(MIB_IF_ROW2.OutErrors));

    public record Adapter(ulong Luid, string Ime, ulong Primljeno, ulong Poslato, ulong Brzina, ulong Greske);

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint pristup, bool nasledi, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int zastavice, System.Text.StringBuilder ime, ref int velicina);

    // which adapters count, by LUID; names are read only every 30 s (they allocate)
    static Dictionary<ulong, string> izabrani = [];
    static long izabranoKada = -1_000_000;
    static bool izabranoVpn;

    /// <summary>The adapters whose traffic is counted, with their counters.</summary>
    public static List<Adapter> Adapteri(bool vpn)
    {
        var izlaz = new List<Adapter>();
        if (GetIfTable2(out var tabela) != 0) return izlaz;
        try
        {
            int n = Marshal.ReadInt32(tabela);
            long sada = Environment.TickCount64;
            if (sada - izabranoKada > 30_000 || vpn != izabranoVpn)
            {
                // adapters come and go (Wi-Fi, VPN)
                var novi = new Dictionary<ulong, string>();
                for (int i = 0; i < n; i++)
                {
                    var r = Marshal.PtrToStructure<MIB_IF_ROW2>(tabela + 8 + i * VelicinaReda);
                    if ((r.Flags & 2) != 0) continue; // filter layers repeat the same counters
                    var tekst = $"{r.Alias} {r.Description}".ToLowerInvariant();
                    if (Virtuelni.Any(tekst.Contains)) continue;
                    bool hardver = (r.Flags & 1) != 0;
                    if (Vpn.Any(tekst.Contains) ? vpn : hardver) novi[r.InterfaceLuid] = r.Alias;
                }
                (izabrani, izabranoKada, izabranoVpn) = (novi, sada, vpn);
            }
            for (int i = 0; i < n; i++)
            {
                var red = tabela + 8 + i * VelicinaReda;
                ulong luid = (ulong)Marshal.ReadInt64(red);
                if (izabrani.TryGetValue(luid, out var ime))
                    izlaz.Add(new Adapter(luid, ime, (ulong)Marshal.ReadInt64(red + PomakIn), (ulong)Marshal.ReadInt64(red + PomakOut),
                        Math.Max((ulong)Marshal.ReadInt64(red + PomakBrzina), (ulong)Marshal.ReadInt64(red + PomakBrzinaIn)),
                        (ulong)Marshal.ReadInt64(red + PomakGreskeIn) + (ulong)Marshal.ReadInt64(red + PomakGreskeOut)));
            }
        }
        finally { FreeMibTable(tabela); }
        return izlaz;
    }

    // ---- programs that have connections right now ----

    static readonly Dictionary<string, string> LepaImena = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "Microsoft Edge", ["msedgewebview2"] = "Microsoft Edge", ["chrome"] = "Google Chrome", ["firefox"] = "Firefox",
        ["opera"] = "Opera", ["brave"] = "Brave", ["vivaldi"] = "Vivaldi", ["obs64"] = "OBS Studio", ["obs32"] = "OBS Studio",
        ["svchost"] = "Windows", ["system"] = "Windows", ["lsass"] = "Windows", ["steamwebhelper"] = "Steam", ["steam"] = "Steam",
        ["ms-teams"] = "Microsoft Teams", ["teams"] = "Microsoft Teams", ["searchhost"] = "Windows Search", ["onedrive"] = "OneDrive",
        ["ekrn"] = "ESET", ["egui"] = "ESET", ["msmpeng"] = "Windows Defender", ["viber"] = "Viber", ["spotify"] = "Spotify",
        ["discord"] = "Discord", ["qbittorrent"] = "qBittorrent", ["telegram"] = "Telegram", ["whatsapp"] = "WhatsApp",
        ["code"] = "Visual Studio Code", ["slack"] = "Slack", ["zoom"] = "Zoom", ["dropbox"] = "Dropbox",
    };

    // lower/UPPER boundaries: "CrossDeviceService" -> "Cross Device Service", "HTTPServer" -> "HTTP Server"
    static readonly System.Text.RegularExpressions.Regex PodelaReci =
        new(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Program name as it is written: full, with capitals (Microsoft Edge, Cross Device Service); never cut.</summary>
    public static string LepoIme(string ime)
    {
        if (ime.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ime = ime[..^4];
        if (LepaImena.TryGetValue(ime, out var lepo)) return lepo;
        var rec = new string(ime.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        rec = PodelaReci.Replace(rec, " ");
        rec = string.Join(' ', rec.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (rec.Length == 0) return "?";
        if (rec == rec.ToLowerInvariant()) rec = char.ToUpperInvariant(rec[0]) + rec[1..];   // "claude" -> "Claude"
        return rec;
    }

    static readonly Dictionary<int, (string ime, string putanja, long kada)> imenaProcesa = [];

    /// <summary>Display name of a process, or null when it cannot be read (the meter needs it for its PIDs).</summary>
    public static string ImeZaPid(int pid) => ImeProcesa(pid).ime;

    static (string ime, string putanja) ImeProcesa(int pid)
    {
        long sada = Environment.TickCount64;
        // a process that gives no path is asked rarely: Process.ProcessName reads the whole list of processes
        if (imenaProcesa.TryGetValue(pid, out var v) && sada - v.kada < (v.putanja != null ? 60_000 : 600_000)) return (v.ime, v.putanja);
        string putanja = PutanjaProcesa(pid), ime = null;
        if (putanja != null) ime = LepoIme(Path.GetFileNameWithoutExtension(putanja));
        else
        {
            try { using var p = Process.GetProcessById(pid); ime = LepoIme(p.ProcessName); }
            catch { /* access denied or the process just ended */ }
        }
        imenaProcesa[pid] = (ime, putanja, sada);
        if (imenaProcesa.Count > 2000) imenaProcesa.Clear();
        return (ime, putanja);
    }

    /// <summary>The .exe of a process, or null; works without administrator rights for almost everything but protected processes.</summary>
    static string PutanjaProcesa(int pid)
    {
        var h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return null;
        try
        {
            var ime = new System.Text.StringBuilder(1024);
            int velicina = ime.Capacity;
            return QueryFullProcessImageName(h, 0, ime, ref velicina) ? ime.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Display names of everything that runs; read only when a watched program has no connection.</summary>
    public static HashSet<string> PokrenutiProgrami()
    {
        var imena = new HashSet<string>();
        foreach (var p in Process.GetProcesses())
        {
            try { imena.Add(LepoIme(p.ProcessName)); }
            catch { /* it just ended */ }
            finally { p.Dispose(); }
        }
        return imena;
    }

    const int AF_INET = 2, AF_INET6 = 23, TCP_TABLE_OWNER_PID_ALL = 5, ESTABLISHED = 5, LISTEN = 2;

    /// <summary>One row of the TCP table, already decoded. Ports are in host order.</summary>
    public readonly record struct TcpRed(int Pid, bool Slusa, bool Spoljna, int LokalniPort, string Adresa, int Port, bool IPv6);

    static bool Spoljna4(uint ip)
    {
        byte a = (byte)ip, b = (byte)(ip >> 8);  // network byte order in memory
        return !(a == 127 || ip == 0 || (a == 169 && b == 254) || (a >= 224 && a <= 239));
    }

    static bool Spoljna6(ReadOnlySpan<byte> ip)
    {
        bool nula = true;
        for (int i = 0; i < 15; i++) if (ip[i] != 0) nula = false;
        if (nula) return false;                                    // :: and ::1
        if (ip[0] == 0xfe && (ip[1] & 0xc0) == 0x80) return false; // fe80::/10
        if (ip[0] == 0xff) return false;                           // multicast
        bool mapirana = ip[10] == 0xff && ip[11] == 0xff;
        for (int i = 0; i < 10; i++) if (ip[i] != 0) mapirana = false;
        return !mapirana || Spoljna4(BitConverter.ToUInt32(ip[12..]));
    }

    /// <summary>The whole TCP table once (IPv4 and IPv6). Addresses are turned into text only when asked for.</summary>
    static List<TcpRed> TcpTabela(bool saAdresama)
    {
        var izlaz = new List<TcpRed>();
        foreach (var af in new[] { AF_INET, AF_INET6 })
        {
            int velicina = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref velicina, false, af, TCP_TABLE_OWNER_PID_ALL, 0);
            if (velicina <= 0) continue;
            var buf = Marshal.AllocHGlobal(velicina + 4096);
            try
            {
                velicina += 4096;
                if (GetExtendedTcpTable(buf, ref velicina, false, af, TCP_TABLE_OWNER_PID_ALL, 0) != 0) continue;
                int n = Marshal.ReadInt32(buf);
                unsafe
                {
                    byte* p = (byte*)buf + 4;
                    for (int i = 0; i < n; i++)
                    {
                        if (af == AF_INET)
                        {
                            // state, localAddr, localPort, remoteAddr, remotePort, pid
                            uint* r = (uint*)(p + i * 24);
                            int pid = (int)r[5];
                            if (pid <= 0) continue;
                            bool slusa = r[0] == LISTEN, veza = r[0] == ESTABLISHED && Spoljna4(r[3]);
                            if (!slusa && !veza) continue;
                            izlaz.Add(new TcpRed(pid, slusa, veza, Port(r[2]),
                                veza && saAdresama ? new IPAddress(new ReadOnlySpan<byte>(r + 3, 4)).ToString() : null,
                                veza ? Port(r[4]) : 0, false));
                        }
                        else
                        {
                            // local[16], scope, port, remote[16], scope, port, state, pid
                            byte* r = p + i * 56;
                            uint stanje = *(uint*)(r + 48);
                            int pid = *(int*)(r + 52);
                            if (pid <= 0) continue;
                            bool slusa = stanje == LISTEN, veza = stanje == ESTABLISHED && Spoljna6(new ReadOnlySpan<byte>(r + 24, 16));
                            if (!slusa && !veza) continue;
                            izlaz.Add(new TcpRed(pid, slusa, veza, Port(*(uint*)(r + 20)),
                                veza && saAdresama ? new IPAddress(new ReadOnlySpan<byte>(r + 24, 16)).ToString() : null,
                                veza ? Port(*(uint*)(r + 44)) : 0, true));
                        }
                    }
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        return izlaz;
    }

    /// <summary>Ports in the table are in network byte order.</summary>
    static int Port(uint p) => (int)((((p & 0xFF) << 8) | ((p >> 8) & 0xFF)) & 0xFFFF);

    /// <summary>Programs with connections or with a listening port, sorted by the number of connections.</summary>
    public static List<VezaPrograma> ProgramiNaInternetu()
    {
        var poPid = new Dictionary<int, int>();
        var slusa = new HashSet<int>();
        try
        {
            foreach (var r in TcpTabela(false))
            {
                if (r.Slusa) slusa.Add(r.Pid);
                else poPid[r.Pid] = poPid.GetValueOrDefault(r.Pid) + 1;
            }
        }
        catch (Exception e) { Podesavanja.Log("Spisak veza nije pročitan", e); }
        var poImenu = new Dictionary<string, int>();
        var slusaImena = new HashSet<string>();
        var putanje = new Dictionary<string, string>();
        void Dodaj(int pid, int broj, bool prima)
        {
            var (ime, putanja) = ImeProcesa(pid);
            if (ime == null) return;
            if (prima) slusaImena.Add(ime);
            poImenu[ime] = poImenu.GetValueOrDefault(ime) + broj;
            if (putanja != null) putanje.TryAdd(ime, putanja);
        }
        foreach (var (pid, broj) in poPid) Dodaj(pid, broj, false);
        foreach (var pid in slusa) Dodaj(pid, 0, true);
        return poImenu.OrderByDescending(x => x.Value).ThenBy(x => x.Key)
            .Select(x => new VezaPrograma(x.Key, x.Value, putanje.GetValueOrDefault(x.Key), slusaImena.Contains(x.Key))).ToList();
    }

    /// <summary>Listening ports and open connections of one program, for the connections dialog.</summary>
    public static List<VezaStavka> Pojedinosti(string ime)
    {
        var izlaz = new List<VezaStavka>();
        try
        {
            foreach (var r in TcpTabela(true))
            {
                var (proces, _) = ImeProcesa(r.Pid);
                if (proces != ime) continue;
                if (r.Slusa) izlaz.Add(new VezaStavka("", r.LokalniPort, r.IPv6, true));
                else if (r.Spoljna) izlaz.Add(new VezaStavka(r.Adresa, r.Port, r.IPv6, false));
            }
        }
        catch (Exception e) { Podesavanja.Log("Veze programa nisu pročitane", e); }
        // the same port seen on IPv4 and IPv6 is one line; ports first, then addresses, both sorted
        var videno = new HashSet<(bool, int, string)>();
        return izlaz.Where(x => videno.Add((x.Slusa, x.Port, x.Adresa)))
            .OrderBy(x => x.Slusa ? 0 : 1).ThenBy(x => x.Port).ThenBy(x => x.Adresa).ToList();
    }
}

/// <summary>Reads counters once a second and returns bytes since the previous read.</summary>
sealed class Merac
{
    public bool Vpn;
    public List<string> Imena = [];
    /// <summary>Speed of the fastest measured link right now (bits per second); 0 if Windows does not say.</summary>
    public ulong Brzina { get; private set; }
    /// <summary>The fastest link seen since the program started: a lower speed means the link got worse.</summary>
    public ulong NajvecaBrzina { get; private set; }
    /// <summary>Adapter errors (in and out) since the program started.</summary>
    public long Greske { get; private set; }
    Dictionary<ulong, (ulong d, ulong u, ulong g)> prethodno = [];
    readonly Stopwatch sat = Stopwatch.StartNew();
    double poslednje = -1;

    public Merac(bool vpn) { Vpn = vpn; }

    /// <summary>(down, up, seconds) since the last call; never negative.</summary>
    public (long d, long u, double sek) Ocitaj()
    {
        double sada = sat.Elapsed.TotalSeconds;
        var adapteri = Merenje.Adapteri(Vpn);
        if (!adapteri.Select(a => a.Ime).SequenceEqual(Imena)) Imena = adapteri.Select(a => a.Ime).ToList();
        long down = 0, up = 0;
        ulong brzina = 0;
        var novo = new Dictionary<ulong, (ulong, ulong, ulong)>();
        foreach (var a in adapteri)
        {
            novo[a.Luid] = (a.Primljeno, a.Poslato, a.Greske);
            if (a.Brzina > brzina) brzina = a.Brzina;
            if (!prethodno.TryGetValue(a.Luid, out var staro)) continue; // first read is only the baseline
            // counter reset (restart of Windows or the adapter): the new value is the baseline
            if (a.Primljeno > staro.d) down += (long)(a.Primljeno - staro.d);
            if (a.Poslato > staro.u) up += (long)(a.Poslato - staro.u);
            if (a.Greske > staro.g) Greske += (long)(a.Greske - staro.g);
        }
        prethodno = novo;
        Brzina = brzina;
        if (brzina > NajvecaBrzina) NajvecaBrzina = brzina;
        double proteklo = poslednje < 0 ? 0 : sada - poslednje;
        poslednje = sada;
        return (down, up, proteklo);
    }
}
