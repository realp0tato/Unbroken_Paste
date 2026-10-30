using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Helper;

internal static class Program
{
    // 표준 입력으로 요청을 받아 Core를 호출하고 표준 출력으로 결과를 보낸다 (README.md).
    // 표준 출력은 프로토콜 전용이므로 로그는 표준 오류로 쓴다
    private static async Task Main()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n" };

        IClipConverter converter = new UpClipConverter();
        await new HelperSession(converter, output).RunAsync(input);
    }
}
