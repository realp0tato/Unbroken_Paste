using System.Diagnostics;
using Microsoft.Win32;

namespace ClipBridge.Windows;

// 브라우저의 Notion 웹에 붙일 때 배지에 보여 줄 Notion 앱의 실행 파일 위치 (#5, #79).
// 설치 방식(사용자 설치·전체 설치·Microsoft Store)마다 위치가 달라 여러 곳을 차례로 본다
internal static class NotionApp
{
    private const string ExeName = "Notion.exe";
    private static string? _found;

    // Notion 앱에 붙여넣을 때 앞 창의 실행 파일 위치를 알려 준다. 가장 확실한 위치라 다음부터 이것을 쓴다
    public static void Remember(string? processPath)
    {
        if (processPath is not null && Path.GetFileName(processPath).Equals(ExeName, StringComparison.OrdinalIgnoreCase))
            _found = processPath;
    }

    // 찾지 못하면 null ("N" 배지). 못 찾은 결과는 기억하지 않아, 나중에 Notion을 설치·실행하면 다시 찾는다
    public static string? Find()
    {
        if (_found is not null && File.Exists(_found))
            return _found;
        _found = Running() ?? KnownFolders() ?? Registered();
        return _found;
    }

    // 실행 중인 Notion 앱
    private static string? Running()
    {
        foreach (var process in Process.GetProcessesByName("Notion"))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } file && File.Exists(file))
                        return file;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // 접근할 수 없는 프로세스는 건너뛴다
                }
            }
        }
        return null;
    }

    // 설치 프로그램의 기본 위치 (사용자 설치, 전체 설치)
    private static string? KnownFolders() =>
        new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        }
        .Where(folder => folder.Length > 0)
        .SelectMany(folder => new[] { Path.Combine(folder, "Programs", "Notion", ExeName), Path.Combine(folder, "Notion", ExeName) })
        .FirstOrDefault(File.Exists);

    // Windows 설정의 "설치된 앱" 목록에 등록된 위치
    private static string? Registered()
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
                continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var app = uninstall.OpenSubKey(name);
                if (app?.GetValue("DisplayName") is not string displayName || !displayName.StartsWith("Notion", StringComparison.OrdinalIgnoreCase))
                    continue;
                // DisplayIcon은 "경로,0" 형태일 수 있다
                var icon = (app.GetValue("DisplayIcon") as string)?.Split(',')[0].Trim('"', ' ');
                var location = app.GetValue("InstallLocation") as string;
                foreach (var candidate in new[] { icon, location is null ? null : Path.Combine(location, ExeName) })
                {
                    if (candidate is not null && candidate.EndsWith(ExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                        return candidate;
                }
            }
        }
        return null;
    }
}
