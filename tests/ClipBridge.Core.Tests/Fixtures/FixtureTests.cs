using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Fixtures;

public class FixtureTests
{
    [Fact]
    public void LoadFrom_ReadsSavedFormatsAndSkipsUnsaved()
    {
        var directory = Directory.CreateTempSubdirectory("clipbridge-fixture-");
        try
        {
            var text = Encoding.Unicode.GetBytes("a\tb\r\n\0");
            File.WriteAllBytes(Path.Combine(directory.FullName, "CF_UNICODETEXT.txt"), text);
            File.WriteAllBytes(Path.Combine(directory.FullName, "text_html.bin"), [1, 2, 3]);
            File.WriteAllText(Path.Combine(directory.FullName, "meta.json"), """
                {
                  "os": "windows",
                  "sourceApp": "EXCEL.EXE",
                  "sequenceNumber": 42,
                  "formats": [
                    { "id": 13, "name": "CF_UNICODETEXT", "file": "CF_UNICODETEXT.txt", "size": 12 },
                    { "id": 49500, "name": "text/html", "file": "text_html.bin", "size": 3 },
                    { "id": 2, "name": "CF_BITMAP", "skipped": "GDI 핸들 포맷" }
                  ]
                }
                """);

            var snapshot = Fixture.LoadFrom(directory.FullName);

            Assert.Equal("EXCEL.EXE", snapshot.SourceApp);
            Assert.Equal(42, snapshot.SequenceNumber);
            Assert.Equal(text, snapshot.Formats[ClipFormats.WinUnicodeText]);
            Assert.Equal([1, 2, 3], snapshot.Formats["text/html"]);
            Assert.False(snapshot.Formats.ContainsKey("CF_BITMAP"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LoadFrom_ReadsMacTypeNameList()
    {
        var directory = Directory.CreateTempSubdirectory("clipbridge-fixture-");
        try
        {
            File.WriteAllBytes(Path.Combine(directory.FullName, "public.html.bin"), [1, 2]);
            File.WriteAllBytes(Path.Combine(directory.FullName, "public.utf8-plain-text.txt"), "표"u8.ToArray());
            File.WriteAllText(Path.Combine(directory.FullName, "meta.json"), """
                {
                  "formats" : [ "public.html", "public.utf8-plain-text" ],
                  "os" : "mac",
                  "sourceApp" : "com.microsoft.Excel"
                }
                """);

            var snapshot = Fixture.LoadFrom(directory.FullName);

            Assert.Equal("com.microsoft.Excel", snapshot.SourceApp);
            Assert.Equal(0, snapshot.SequenceNumber);
            Assert.Equal([1, 2], snapshot.Formats[ClipFormats.MacHtml]);
            Assert.Equal("표"u8.ToArray(), snapshot.Formats[ClipFormats.MacUtf8Text]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // 올라온 샘플의 meta.json과 파일이 서로 맞는지 확인한다
    [Fact]
    public void AllCommittedSamples_Load()
    {
        foreach (var sample in Fixture.All())
        {
            var snapshot = Fixture.Load(sample);
            Assert.True(snapshot.Formats.Count > 0, $"저장된 포맷이 없습니다: {sample}");
        }
    }
}
