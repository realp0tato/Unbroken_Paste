using System.Runtime.InteropServices;
using System.Text.Json;
using ClipBridge.Windows;

// 저장한 Windows 샘플을 meta.json의 포맷 순서 그대로 클립보드에 되돌려 넣는다 (#24).
// 원본 앱에서 다시 복사하지 않고 같은 입력으로 시험을 반복할 때 쓴다 (macOS restore-mac-clipboard.swift와 같은 용도)
internal static class Restore
{
    private const uint GMEM_MOVEABLE = 0x0002;

    // sample: tests/fixtures 기준 경로(예: excel/win-basic-table) 또는 샘플 폴더 경로
    public static int Run(string sample, string? fixturesRoot)
    {
        var directory = Directory.Exists(sample) ? sample
            : fixturesRoot is not null ? Path.Combine(fixturesRoot, sample) : sample;
        var metaPath = Path.Combine(directory, "meta.json");
        if (!File.Exists(metaPath))
            return Fail($"샘플을 찾을 수 없습니다: {directory}");

        using var meta = JsonDocument.Parse(File.ReadAllText(metaPath));
        var root = meta.RootElement;
        if (!root.TryGetProperty("os", out var os) || os.GetString() != "windows")
            return Fail("Windows에서 저장한 샘플(win-*)만 되돌릴 수 있습니다. macOS 샘플은 포맷 이름이 달라 Windows 앱이 읽지 못합니다.");

        var formats = new List<(string Name, byte[] Data)>();
        foreach (var format in root.GetProperty("formats").EnumerateArray())
        {
            if (format.TryGetProperty("file", out var file))
                formats.Add((format.GetProperty("name").GetString()!, File.ReadAllBytes(Path.Combine(directory, file.GetString()!))));
        }
        if (formats.Count == 0)
            return Fail("저장된 포맷이 없습니다.");

        if (!Open())
            return Fail("클립보드를 열 수 없습니다. 잠시 후 다시 실행해 주세요.");
        var written = new List<string>();
        try
        {
            NativeMethods.EmptyClipboard();
            foreach (var (name, data) in formats)
            {
                if (Set(name, data))
                    written.Add(name);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }

        // 다시 읽어 바이트가 같은지 확인한다
        var check = ClipboardReader.Read();
        Console.WriteLine($"클립보드에 넣음: {directory}");
        foreach (var (name, data) in formats)
        {
            var back = check?.Formats.FirstOrDefault(f => f.Name == name)?.Data;
            var result = !written.Contains(name) ? "넣지 못함"
                : back is not null && back.Length >= data.Length && back.AsSpan(0, data.Length).SequenceEqual(data) ? "OK" : "불일치";
            Console.WriteLine($"  - {name,-40} {data.Length,10:N0} bytes  {result}");
        }
        Console.WriteLine("이제 대상 앱에서 붙여 넣으세요. 원본 앱의 OLE·링크 포맷은 저장하지 않았으므로 Excel → Word(통과) 시험은 Excel에서 직접 복사합니다.");
        return written.Count == formats.Count ? 0 : 1;
    }

    private static bool Open()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(0))
                return true;
            Thread.Sleep(30);
        }
        return false;
    }

    private static bool Set(string name, byte[] data)
    {
        var id = ClipboardReader.FormatId(name);
        if (id == 0)
            return false;

        var memory = NativeMethods.GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
        if (memory == 0)
            return false;
        var pointer = NativeMethods.GlobalLock(memory);
        if (pointer == 0)
        {
            NativeMethods.GlobalFree(memory);
            return false;
        }
        Marshal.Copy(data, 0, pointer, data.Length);
        NativeMethods.GlobalUnlock(memory);

        // 성공하면 메모리는 클립보드가 가진다
        if (NativeMethods.SetClipboardData(id, memory) != 0)
            return true;
        NativeMethods.GlobalFree(memory);
        return false;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(nint newOwner);

        [DllImport("user32.dll")]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetClipboardData(uint format, nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalAlloc(uint flags, nuint bytes);

        [DllImport("kernel32.dll")]
        public static extern nint GlobalFree(nint memory);

        [DllImport("kernel32.dll")]
        public static extern nint GlobalLock(nint memory);

        [DllImport("kernel32.dll")]
        public static extern bool GlobalUnlock(nint memory);
    }
}
