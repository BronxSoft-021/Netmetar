// Structures and calls for the ETW session and the consumer (advapi32).
using System.Runtime.InteropServices;

namespace NetmetarMerac;

static partial class Etw
{
    [StructLayout(LayoutKind.Sequential)]
    struct WNODE_HEADER
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public long TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EVENT_TRACE_PROPERTIES
    {
        public WNODE_HEADER Wnode;
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct EVENT_TRACE_LOGFILEW
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string LoggerName;
        [MarshalAs(UnmanagedType.LPWStr)] public string LogFileName;
        public long CurrentTime;
        public uint BuffersRead;
        public uint ProcessTraceMode;
        public IntPtr CurrentEvent;
        public IntPtr LogfileHeader;
        public IntPtr BufferCallback;
        public uint BufferSize;
        public uint Filled;
        public uint EventsLost;
        public IntPtr EventRecordCallback;
        public uint IsKernelTrace;
        public IntPtr Context;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void DogadjajCallback(IntPtr zapis);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int StartTraceW(out ulong sesija, string ime, IntPtr svojstva);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int ControlTraceW(ulong sesija, string ime, IntPtr svojstva, uint kod);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern int EnableTraceEx2(ulong sesija, ref Guid provajder, uint kod, byte nivo,
        ulong kljucneReci, ulong sveKljucneReci, uint timeout, IntPtr parametri);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ulong OpenTraceW(ref EVENT_TRACE_LOGFILEW fajl);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern int ProcessTrace(ulong[] handles, uint broj, IntPtr pocetak, IntPtr kraj);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern int CloseTrace(ulong handle);
}
