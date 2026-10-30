namespace ClipBridge.Core;

// ClipboardSnapshot.Formats에 쓰는 포맷 이름. Core는 두 OS의 이름을 모두 입력으로 받는다 (PRD 4.5)
public static class ClipFormats
{
    // Windows: 등록 포맷은 등록 이름, 표준 포맷은 CF_ 상수 이름
    public const string WinHtml = "HTML Format";           // CF_HTML, UTF-8 + 헤더
    public const string WinUnicodeText = "CF_UNICODETEXT"; // UTF-16LE, 끝에 \0
    public const string WinRtf = "Rich Text Format";
    public const string WinExcelXml = "XML Spreadsheet";
    public const string WinExcelBiff12 = "Biff12";

    // macOS: UTI 이름. Excel 고유 포맷은 샘플 수집(#32) 후 추가
    public const string MacHtml = "public.html";           // UTF-8, 헤더 없음
    public const string MacUtf8Text = "public.utf8-plain-text";
    public const string MacRtf = "public.rtf";
}
