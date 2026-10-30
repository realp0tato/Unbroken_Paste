using System.Runtime.InteropServices;
using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 클립보드의 모든 포맷을 원본 바이트로 읽는다. 샘플 수집 도구(#28)와 복사 스냅샷(#1)이 같이 쓴다
internal static class ClipboardReader
{
    // Data가 null이면 읽지 않은 포맷이고, 이유는 SkipReason에 있다
    public sealed record Entry(uint Id, string Name, byte[]? Data, string? SkipReason);

    public sealed record Result(long SequenceNumber, string? OwnerPath, IReadOnlyList<Entry> Formats)
    {
        // Core에 넘길 스냅샷. 읽지 못한 포맷은 뺀다
        public ClipboardSnapshot ToSnapshot() => new(
            Formats.Where(f => f.Data is not null).ToDictionary(f => f.Name, f => f.Data!),
            OwnerPath is null ? null : Path.GetFileName(OwnerPath),
            SequenceNumber);
    }

    // 클립보드를 열지 못하면 null. owner는 클립보드를 여는 창 핸들(0이면 현재 작업)
    public static Result? Read(nint owner = 0, long maxBytesPerFormat = long.MaxValue)
    {
        if (!TryOpen(owner))
            return null;

        try
        {
            // 열어 둔 동안에는 다른 앱이 바꿀 수 없으므로 번호와 내용이 같은 복사를 가리킨다
            long sequenceNumber = NativeMethods.GetClipboardSequenceNumber();
            var formats = new List<Entry>();
            for (uint id = NativeMethods.EnumClipboardFormats(0); id != 0; id = NativeMethods.EnumClipboardFormats(id))
                formats.Add(ReadFormat(id, maxBytesPerFormat));
            return new Result(sequenceNumber, GetOwnerPath(), formats);
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    // 클립보드가 바뀔 때마다 OS가 올리는 번호 (FR-17 최신 복사 판단)
    public static long SequenceNumber() => NativeMethods.GetClipboardSequenceNumber();

    // 표준 포맷은 CF_ 상수 이름, 등록 포맷은 등록 이름 (ClipFormats와 같은 규칙)
    public static string FormatName(uint id)
    {
        if (StandardFormats.TryGetValue(id, out var name))
            return name;

        var buffer = new StringBuilder(256);
        return NativeMethods.GetClipboardFormatName(id, buffer, buffer.Capacity) > 0 ? buffer.ToString() : $"#{id}";
    }

    private static bool TryOpen(nint owner)
    {
        // 다른 앱이 잠깐 열고 있을 수 있어 짧게 재시도한다
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(owner))
                return true;
            Thread.Sleep(30);
        }
        return false;
    }

    private static Entry ReadFormat(uint id, long maxBytes)
    {
        var name = FormatName(id);
        if (IsHandleFormat(id))
            return new Entry(id, name, null, "GDI 핸들 포맷");

        var handle = NativeMethods.GetClipboardData(id);
        if (handle == 0)
            return new Entry(id, name, null, "데이터 없음");

        var size = (long)NativeMethods.GlobalSize(handle);
        if (size == 0)
            return new Entry(id, name, null, "데이터 없음");
        if (size > maxBytes)
            return new Entry(id, name, null, $"크기 초과 ({size:N0} bytes)");

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0)
            return new Entry(id, name, null, "읽기 실패");

        try
        {
            var data = new byte[size];
            Marshal.Copy(pointer, data, 0, data.Length);
            return new Entry(id, name, data, null);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }
    }

    // HGLOBAL이 아닌 핸들을 담는 포맷은 바이트로 읽을 수 없다
    private static bool IsHandleFormat(uint id) =>
        id is 2 or 3 or 9 or 14 or 0x80 or 0x82 or 0x83 or 0x8E || id is >= 0x200 and <= 0x3FF;

    private static string? GetOwnerPath()
    {
        var window = NativeMethods.GetClipboardOwner();
        if (window == 0 || NativeMethods.GetWindowThreadProcessId(window, out var processId) == 0)
            return null;

        var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
            return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var length = buffer.Capacity;
            return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref length) ? buffer.ToString() : null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static readonly Dictionary<uint, string> StandardFormats = new()
    {
        [1] = "CF_TEXT",
        [2] = "CF_BITMAP",
        [3] = "CF_METAFILEPICT",
        [4] = "CF_SYLK",
        [5] = "CF_DIF",
        [6] = "CF_TIFF",
        [7] = "CF_OEMTEXT",
        [8] = "CF_DIB",
        [9] = "CF_PALETTE",
        [10] = "CF_PENDATA",
        [11] = "CF_RIFF",
        [12] = "CF_WAVE",
        [13] = "CF_UNICODETEXT",
        [14] = "CF_ENHMETAFILE",
        [15] = "CF_HDROP",
        [16] = "CF_LOCALE",
        [17] = "CF_DIBV5",
        [0x80] = "CF_OWNERDISPLAY",
        [0x81] = "CF_DSPTEXT",
        [0x82] = "CF_DSPBITMAP",
        [0x83] = "CF_DSPMETAFILEPICT",
        [0x8E] = "CF_DSPENHMETAFILE",
    };

    private static class NativeMethods
    {
        public const uint ProcessQueryLimitedInformation = 0x1000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(nint newOwner);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetClipboardFormatNameW")]
        public static extern int GetClipboardFormatName(uint format, StringBuilder name, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint GetClipboardData(uint format);

        [DllImport("user32.dll")]
        public static extern nint GetClipboardOwner();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nuint GlobalSize(nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalLock(nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalUnlock(nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        public static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(nint handle);
    }
}
