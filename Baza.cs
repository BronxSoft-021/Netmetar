// SQLite history through winsqlite3.dll (part of Windows 10/11): bytes per minute, sums per day/week/billing period.
using System.Runtime.InteropServices;

namespace Netmetar;

static partial class Sqlite
{
    const string L = "winsqlite3.dll";
    public const int ROW = 100, DONE = 101, INTEGER = 1;

    [LibraryImport(L, StringMarshalling = StringMarshalling.Utf16)] public static partial int sqlite3_open16(string ime, out IntPtr db);
    [LibraryImport(L)] public static partial int sqlite3_close_v2(IntPtr db);
    [LibraryImport(L, StringMarshalling = StringMarshalling.Utf8)] public static partial int sqlite3_exec(IntPtr db, string sql, IntPtr cb, IntPtr arg, out IntPtr greska);
    [LibraryImport(L, StringMarshalling = StringMarshalling.Utf16)] public static partial int sqlite3_prepare16_v2(IntPtr db, string sql, int bajtova, out IntPtr stmt, IntPtr ostatak);
    [LibraryImport(L)] public static partial int sqlite3_step(IntPtr stmt);
    [LibraryImport(L)] public static partial int sqlite3_finalize(IntPtr stmt);
    [LibraryImport(L)] public static partial int sqlite3_column_count(IntPtr stmt);
    [LibraryImport(L)] public static partial int sqlite3_column_type(IntPtr stmt, int i);
    [LibraryImport(L)] public static partial long sqlite3_column_int64(IntPtr stmt, int i);
    [LibraryImport(L)] public static partial IntPtr sqlite3_column_text16(IntPtr stmt, int i);
    [LibraryImport(L)] public static partial int sqlite3_bind_int64(IntPtr stmt, int i, long v);
    [LibraryImport(L, StringMarshalling = StringMarshalling.Utf16)] public static partial int sqlite3_bind_text16(IntPtr stmt, int i, string v, int bajtova, IntPtr unisti);
    [LibraryImport(L)] public static partial IntPtr sqlite3_errmsg16(IntPtr db);
    [LibraryImport(L)] public static partial void sqlite3_free(IntPtr p);
    [LibraryImport(L)] public static partial int sqlite3_busy_timeout(IntPtr db, int ms);
}

sealed class Baza : IDisposable
{
    const int CuvajMinuteDana = 90;

    const string Shema = """
        CREATE TABLE IF NOT EXISTS minuti (
            vreme TEXT PRIMARY KEY,
            bajtova_down INTEGER NOT NULL DEFAULT 0,
            bajtova_up INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS dani (
            dan TEXT PRIMARY KEY,
            bajtova_down INTEGER NOT NULL DEFAULT 0,
            bajtova_up INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS periodi (
            pocetak TEXT PRIMARY KEY,
            kraj TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS stanje (kljuc TEXT PRIMARY KEY, vrednost TEXT);
        CREATE TABLE IF NOT EXISTS programi_dan (
            dan TEXT NOT NULL,
            program TEXT NOT NULL,
            sekundi INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (dan, program)
        );
        CREATE TABLE IF NOT EXISTS prekidi (
            id INTEGER PRIMARY KEY,
            program TEXT NOT NULL,
            pocetak TEXT NOT NULL,
            kraj TEXT
        );
        """;

    IntPtr db;

    public Baza(string putanja)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(putanja));
        Proveri(Sqlite.sqlite3_open16(putanja, out db));
        Sqlite.sqlite3_busy_timeout(db, 3000);
        Izvrsi("PRAGMA journal_mode=WAL");
        Izvrsi("PRAGMA synchronous=NORMAL");
        Izvrsi(Shema);
        // exact traffic per program arrived later: older databases get the two columns now
        if (!Kolona("programi_dan", "bajtova_down"))
        {
            Izvrsi("ALTER TABLE programi_dan ADD COLUMN bajtova_down INTEGER NOT NULL DEFAULT 0");
            Izvrsi("ALTER TABLE programi_dan ADD COLUMN bajtova_up INTEGER NOT NULL DEFAULT 0");
        }
    }

    bool Kolona(string tabela, string ime)
    {
        foreach (var r in Upit($"PRAGMA table_info({tabela})"))
            if ((string)r[1] == ime) return true;
        return false;
    }

    public void Dispose()
    {
        if (db != IntPtr.Zero) Sqlite.sqlite3_close_v2(db);
        db = IntPtr.Zero;
    }

    void Proveri(int rc)
    {
        if (rc != 0 && rc != Sqlite.ROW && rc != Sqlite.DONE)
            throw new InvalidOperationException($"SQLite {rc}: " +
                (db != IntPtr.Zero ? Marshal.PtrToStringUni(Sqlite.sqlite3_errmsg16(db)) : ""));
    }

    /// <summary>Statements without parameters (may be several).</summary>
    void Izvrsi(string sql)
    {
        int rc = Sqlite.sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out var greska);
        if (greska != IntPtr.Zero)
        {
            var poruka = Marshal.PtrToStringUTF8(greska);
            Sqlite.sqlite3_free(greska);
            throw new InvalidOperationException($"SQLite {rc}: {poruka}");
        }
    }

    List<object[]> Upit(string sql, params object[] arg)
    {
        Proveri(Sqlite.sqlite3_prepare16_v2(db, sql, -1, out var st, IntPtr.Zero));
        var redovi = new List<object[]>();
        try
        {
            for (int i = 0; i < arg.Length; i++)
            {
                if (arg[i] is string s) Proveri(Sqlite.sqlite3_bind_text16(st, i + 1, s, -1, new IntPtr(-1)));
                else Proveri(Sqlite.sqlite3_bind_int64(st, i + 1, Convert.ToInt64(arg[i])));
            }
            int rc;
            while ((rc = Sqlite.sqlite3_step(st)) == Sqlite.ROW)
            {
                int n = Sqlite.sqlite3_column_count(st);
                var red = new object[n];
                for (int c = 0; c < n; c++)
                    red[c] = Sqlite.sqlite3_column_type(st, c) == Sqlite.INTEGER
                        ? Sqlite.sqlite3_column_int64(st, c)
                        : Marshal.PtrToStringUni(Sqlite.sqlite3_column_text16(st, c));
                redovi.Add(red);
            }
            Proveri(rc);
        }
        finally { Sqlite.sqlite3_finalize(st); }
        return redovi;
    }

    static string D(DateOnly d) => d.ToString("yyyy-MM-dd");

    // ---- writing ----

    /// <summary>{"yyyy-MM-dd HH:mm": (down, up)} added to what is already there.</summary>
    public void Upisi(Dictionary<string, long[]> poMinutu)
    {
        Izvrsi("BEGIN");
        try
        {
            foreach (var (k, v) in poMinutu)
                if (v[0] != 0 || v[1] != 0)
                    Upit("INSERT INTO minuti (vreme, bajtova_down, bajtova_up) VALUES (?, ?, ?) " +
                         "ON CONFLICT(vreme) DO UPDATE SET bajtova_down = bajtova_down + excluded.bajtova_down, " +
                         "bajtova_up = bajtova_up + excluded.bajtova_up", k, v[0], v[1]);
            Izvrsi("COMMIT");
        }
        catch { Izvrsi("ROLLBACK"); throw; }
    }

    /// <summary>Seconds online, added to what is already there.</summary>
    public void UpisiPrograme(Dictionary<(string dan, string ime), long> sekundi)
    {
        Izvrsi("BEGIN");
        try
        {
            foreach (var ((dan, ime), s) in sekundi)
                Upit("INSERT INTO programi_dan (dan, program, sekundi) VALUES (?, ?, ?) " +
                     "ON CONFLICT(dan, program) DO UPDATE SET sekundi = sekundi + excluded.sekundi", dan, ime, s);
            Izvrsi("COMMIT");
        }
        catch { Izvrsi("ROLLBACK"); throw; }
    }

    /// <summary>Exact traffic per program, added to what is already there (from the meter).</summary>
    public void UpisiProgrameBajtove(Dictionary<(string dan, string ime), (long d, long u)> bajtovi)
    {
        Izvrsi("BEGIN");
        try
        {
            foreach (var ((dan, ime), (d, u)) in bajtovi)
                Upit("INSERT INTO programi_dan (dan, program, bajtova_down, bajtova_up) VALUES (?, ?, ?, ?) " +
                     "ON CONFLICT(dan, program) DO UPDATE SET bajtova_down = bajtova_down + excluded.bajtova_down, " +
                     "bajtova_up = bajtova_up + excluded.bajtova_up", dan, ime, d, u);
            Izvrsi("COMMIT");
        }
        catch { Izvrsi("ROLLBACK"); throw; }
    }

    const string VremePrekida = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Written when the outage starts, so that it stays recorded even if the computer goes down with it.</summary>
    public long PocniPrekid(string ime, DateTime pocetak)
    {
        Upit("INSERT INTO prekidi (program, pocetak) VALUES (?, ?)", ime, pocetak.ToString(VremePrekida));
        return (long)Upit("SELECT last_insert_rowid()")[0][0];
    }

    public void ZavrsiPrekid(long id, DateTime kraj) =>
        Upit("UPDATE prekidi SET kraj = ? WHERE id = ?", kraj.ToString(VremePrekida), id);

    /// <summary>The last outage of a program; the end is unknown if the program was closed during it.</summary>
    public (DateTime? pocetak, DateTime? kraj) PoslednjiPrekid(string ime)
    {
        var r = Upit("SELECT pocetak, kraj FROM prekidi WHERE program = ? ORDER BY id DESC LIMIT 1", ime);
        if (r.Count == 0) return (null, null);
        return (DateTime.Parse((string)r[0][0]), r[0][1] is string kraj ? DateTime.Parse(kraj) : null);
    }

    /// <summary>Fold minutes older than 90 days into whole days.</summary>
    public void Sazmi(DateOnly danas)
    {
        var granica = D(danas.AddDays(-CuvajMinuteDana));
        Izvrsi("BEGIN");
        try
        {
            Upit("INSERT INTO dani (dan, bajtova_down, bajtova_up) " +
                 "SELECT substr(vreme, 1, 10), SUM(bajtova_down), SUM(bajtova_up) FROM minuti " +
                 "WHERE vreme < ? GROUP BY substr(vreme, 1, 10) " +
                 "ON CONFLICT(dan) DO UPDATE SET bajtova_down = bajtova_down + excluded.bajtova_down, " +
                 "bajtova_up = bajtova_up + excluded.bajtova_up", granica);
            Upit("DELETE FROM minuti WHERE vreme < ?", granica);
            Upit("DELETE FROM programi_dan WHERE dan < ?", granica);
            Izvrsi("COMMIT");
        }
        catch { Izvrsi("ROLLBACK"); throw; }
    }

    // ---- reading ----

    /// <summary>(down, up) bytes from od to doDana, both inclusive.</summary>
    public (long d, long u) Zbir(DateOnly od, DateOnly doDana)
    {
        string a = D(od), b = D(doDana.AddDays(1));
        var r = Upit("SELECT COALESCE(SUM(d), 0), COALESCE(SUM(u), 0) FROM (" +
                     " SELECT bajtova_down d, bajtova_up u FROM minuti WHERE vreme >= ? AND vreme < ?" +
                     " UNION ALL SELECT bajtova_down, bajtova_up FROM dani WHERE dan >= ? AND dan < ?)", a, b, a, b)[0];
        return ((long)r[0], (long)r[1]);
    }

    public Dictionary<DateOnly, (long d, long u)> PoDanima(DateOnly od, DateOnly doDana)
    {
        string a = D(od), b = D(doDana.AddDays(1));
        var izlaz = new Dictionary<DateOnly, (long, long)>();
        foreach (var r in Upit("SELECT dan, SUM(d), SUM(u) FROM (" +
                               " SELECT substr(vreme, 1, 10) dan, bajtova_down d, bajtova_up u FROM minuti WHERE vreme >= ? AND vreme < ?" +
                               " UNION ALL SELECT dan, bajtova_down, bajtova_up FROM dani WHERE dan >= ? AND dan < ?)" +
                               " GROUP BY dan", a, b, a, b))
            izlaz[DateOnly.Parse((string)r[0])] = ((long)r[1], (long)r[2]);
        return izlaz;
    }

    /// <summary>Seconds online per program on that day.</summary>
    public Dictionary<string, long> ProgramiDana(DateOnly dan) =>
        Upit("SELECT program, sekundi FROM programi_dan WHERE dan = ?", D(dan)).ToDictionary(r => (string)r[0], r => (long)r[1]);

    /// <summary>Exact traffic per program on that day (from the meter).</summary>
    public Dictionary<string, (long d, long u)> ProgramiDanaBajtovi(DateOnly dan) =>
        Upit("SELECT program, bajtova_down, bajtova_up FROM programi_dan WHERE dan = ?", D(dan))
            .ToDictionary(r => (string)r[0], r => ((long)r[1], (long)r[2]));

    /// <summary>A small value kept between runs (the stanje table); null when it is not there.</summary>
    public string Stanje(string kljuc)
    {
        var r = Upit("SELECT vrednost FROM stanje WHERE kljuc = ?", kljuc);
        return r.Count > 0 ? (string)r[0][0] : null;
    }

    public void PostaviStanje(string kljuc, string vrednost) =>
        Upit("INSERT OR REPLACE INTO stanje (kljuc, vrednost) VALUES (?, ?)", kljuc, vrednost);

    // ---- billing periods ----

    public static DateOnly ResetUMesecu(int god, int mes, int danReseta) =>
        new(god, mes, Math.Min(danReseta, DateTime.DaysInMonth(god, mes)));

    /// <summary>First and last day of the billing period that contains d.</summary>
    public static (DateOnly pocetak, DateOnly kraj) PeriodZa(DateOnly d, int danReseta)
    {
        var r = ResetUMesecu(d.Year, d.Month, danReseta);
        DateOnly pocetak, sledeci;
        if (d >= r)
        {
            var n = d.AddMonths(1);
            (pocetak, sledeci) = (r, ResetUMesecu(n.Year, n.Month, danReseta));
        }
        else
        {
            var p = d.AddMonths(-1);
            (pocetak, sledeci) = (ResetUMesecu(p.Year, p.Month, danReseta), r);
        }
        return (pocetak, sledeci.AddDays(-1));
    }

    /// <summary>Monday..Sunday of the week that contains d.</summary>
    public static (DateOnly pon, DateOnly ned) NedeljaZa(DateOnly d)
    {
        var pon = d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
        return (pon, pon.AddDays(6));
    }

    /// <summary>Close finished periods (they stay in history) and return the current one.</summary>
    public (DateOnly, DateOnly) UskladiPeriode(int danReseta, DateOnly danas)
    {
        var (pocetak, kraj) = PeriodZa(danas, danReseta);
        var r = Upit("SELECT vrednost FROM stanje WHERE kljuc = 'tekuci_pocetak'");
        Izvrsi("BEGIN");
        try
        {
            if (r.Count > 0 && r[0][0] is string staro)
            {
                // only when time moved on (or a new reset day cut the period short);
                // an earlier new start just absorbs the old period
                var s = DateOnly.Parse(staro);
                while (s < pocetak)
                {
                    var e = PeriodZa(s, danReseta).kraj;
                    if (e > pocetak.AddDays(-1)) e = pocetak.AddDays(-1);
                    if (e < s) break;
                    Upit("INSERT OR IGNORE INTO periodi (pocetak, kraj) VALUES (?, ?)", D(s), D(e));
                    s = e.AddDays(1);
                }
            }
            Upit("INSERT OR REPLACE INTO stanje (kljuc, vrednost) VALUES ('tekuci_pocetak', ?)", D(pocetak));
            Izvrsi("COMMIT");
        }
        catch { Izvrsi("ROLLBACK"); throw; }
        return (pocetak, kraj);
    }

    /// <summary>Up to n periods before the current one, newest first; missing ones filled in with the current reset day.</summary>
    public List<(DateOnly s, DateOnly e)> PrethodniPeriodi(DateOnly tekuciPocetak, int n, int danReseta)
    {
        var periodi = Upit("SELECT pocetak, kraj FROM periodi WHERE pocetak < ? ORDER BY pocetak DESC LIMIT ?", D(tekuciPocetak), n)
            .Select(r => (DateOnly.Parse((string)r[0]), DateOnly.Parse((string)r[1]))).ToList();
        while (periodi.Count < n)
        {
            var najraniji = periodi.Count > 0 ? periodi[^1].Item1 : tekuciPocetak;
            var (s, _) = PeriodZa(najraniji.AddDays(-1), danReseta);
            periodi.Add((s, najraniji.AddDays(-1)));
        }
        return periodi;
    }
}
