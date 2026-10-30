# 클립보드 샘플 (fixtures)

Windows PC와 Mac에서 복사 직후 클립보드에 들어 있는 **모든 포맷**을 파일로 저장해 둔다. 클립보드 포맷은 OS마다 다르므로 두 OS 샘플을 모두 모은다. 변환기 테스트는 이 샘플로 macOS에서 실행한다.

## 폴더

| 폴더 | 출처 |
|---|---|
| `excel/` | Excel |
| `web/` | 인터넷(브라우저) |
| `markdown/` | Markdown 글 (AI 답변 등 일반 텍스트) |
| `plain/` | 일반 텍스트 |

## 규칙

- 샘플 하나당 폴더 하나: `<출처>/<OS>-<샘플 이름>/` (OS는 `win` 또는 `mac`, 예: `excel/win-basic-table/`, `excel/mac-basic-table/`)
- 포맷마다 파일 하나, 파일 이름은 클립보드 포맷 이름 (예: Windows `HTML Format.bin`, `CF_UNICODETEXT.txt`, `XML Spreadsheet.bin` / macOS `public.html.bin`, `public.utf8-plain-text.txt`)
  - 파일 내용은 클립보드 바이트 **그대로**다. 인코딩을 바꾸지 않는다 (`CF_UNICODETEXT.txt`는 UTF-16LE, 끝에 `\0`)
  - 파일 이름에 쓸 수 없는 문자(`/`, `:` 등)는 `_`로 바꾸고, 원래 이름은 `meta.json`에 남긴다
- `meta.json`에 OS·출처 앱·버전·복사한 내용 설명을 기록 (아래 형식)
- 개인정보가 들어간 내용은 올리지 않는다

## meta.json

```json
{
  "os": "windows",
  "osVersion": "Microsoft Windows 10.0.22631",
  "sourceApp": "EXCEL.EXE",
  "sourceAppVersion": "16.0.17928.20114",
  "description": "3열 5행 표, 머리글 굵게",
  "capturedAt": "2026-10-30T09:00:00+00:00",
  "sequenceNumber": 1234,
  "formats": [
    { "id": 49373, "name": "HTML Format", "file": "HTML Format.bin", "size": 2048 },
    { "id": 13, "name": "CF_UNICODETEXT", "file": "CF_UNICODETEXT.txt", "size": 120 },
    { "id": 2, "name": "CF_BITMAP", "skipped": "GDI 핸들 포맷" }
  ]
}
```

| 필드 | 내용 |
|---|---|
| `os` | `windows` 또는 `macos` |
| `sourceApp` | 복사한 앱. Windows는 프로세스 이름, macOS는 번들 ID (`ClipboardSnapshot.SourceApp`과 같은 값) |
| `sourceAppVersion` | 복사한 앱의 버전 |
| `sequenceNumber` | Windows `GetClipboardSequenceNumber`, macOS `changeCount` |
| `formats` | 클립보드에 있던 순서 그대로. `name`은 `ClipFormats`와 같은 포맷 이름, `file`이 없으면 저장하지 않은 포맷이고 이유는 `skipped`에 적는다 |

Windows 등록 포맷의 `id`는 실행할 때마다 달라지므로 참고용이다. 포맷은 `name`으로 찾는다.

macOS 스크립트(`dump-mac-clipboard.swift`)는 `formats`를 타입 이름 문자열 배열로 적고 `sequenceNumber`를 적지 않는다. 파일 이름은 타입 이름에서 `/`만 `_`로 바꾼 것이다. `Fixture.Load`는 두 형식을 모두 읽는다.

## Windows에서 수집하기 (#28)

`tools/ClipBridge.FixtureDump`가 클립보드를 읽어 위 형식으로 저장한다. 저장소 루트에서 실행한다.

1. 터미널에서 먼저 실행한다. `이제 원본 앱에서 복사하세요`가 나오면 기다리는 상태다.
   ```bash
   dotnet run --project tools/ClipBridge.FixtureDump -- excel basic-table "3열 5행 표, 머리글 굵게"
   ```
2. 원본 앱(Excel, 브라우저 등)에서 내용을 선택하고 복사한다. 복사하는 순간 저장된다.
3. `tests/fixtures/excel/win-basic-table/`에 저장된 파일과 화면에 나온 포맷 목록을 확인한다.

- 명령어를 복사해 붙여 넣는 과정에서 클립보드가 덮이므로, 실행한 **뒤의** 복사를 저장한다. 이미 복사해 둔 내용을 저장하려면 `--now`를 붙인다.
- `excel` 샘플은 복사한 앱이 `EXCEL.EXE`가 아니면 저장하지 않는다.
- Windows PowerShell 5.1에서 `meta.json`을 볼 때는 `Get-Content -Encoding UTF8`로 연다. 지정하지 않으면 한글이 깨져 보인다 (파일은 정상).
- 같은 이름이 이미 있으면 멈춘다. 다시 저장하려면 `--force`를 붙인다.
- 포맷 하나가 1MB를 넘으면 저장하지 않고 `meta.json`에만 적는다 (이미지 포맷 등). 바꾸려면 `--max-kb 4096`처럼 지정한다.
- 비트맵·메타파일처럼 바이트로 읽을 수 없는 포맷은 `skipped`로 기록된다.
- 이미지(`CF_DIB` 등)와 OLE 개체·링크 포맷(`Embed Source`, `Native`, `Biff8`, `Link`, `Hyperlink` 등)은 저장하지 않고 `skipped`에 `제외:`로 기록한다. 변환기가 쓰지 않고 크기가 크며, 통합 문서 파일 경로나 다른 시트 내용이 들어갈 수 있기 때문이다. 꼭 필요하면 `--all`을 붙인다.

## 수집 도구 (macOS)

`tools/`의 스크립트는 저장소 루트에서 실행한다.

| 도구 | 하는 일 |
|---|---|
| `dump-mac-clipboard.swift` | 지금 클립보드의 모든 타입을 위 규칙대로 저장하고 `meta.json`을 만든다 |
| `collect-excel-samples.sh` | Excel에 샘플 표 4종(`make-excel-samples.applescript`)을 만들고, 시트마다 복사해 `excel/mac-*/`에 저장한다 |

```bash
# 원본 앱에서 복사한 직후 실행 (다른 것을 복사하기 전에)
swift tests/fixtures/tools/dump-mac-clipboard.swift <저장 폴더> <원본 앱 번들 ID> "<설명>"
# 예) Safari에서 복사한 웹 글
swift tests/fixtures/tools/dump-mac-clipboard.swift tests/fixtures/web/mac-safari-article com.apple.Safari "뉴스 기사 본문 3문단"

# Excel 샘플 4종 다시 만들기 (처음 실행 시 "터미널이 Microsoft Excel을 제어" 권한 허용)
bash tests/fixtures/tools/collect-excel-samples.sh
```

- 파일 이름은 타입 이름 그대로이고, 텍스트 타입(`public.utf8-plain-text` 등)은 `.txt`, 나머지는 `.bin`이다.
- macOS Excel은 HTML에 CF_HTML 헤더가 없고, 마지막 행 뒤 줄바꿈 없는 TSV를 넣는다. 이미지·PDF 등 18개 타입을 함께 넣는다.

## 테스트에서 쓰기

```csharp
using ClipBridge.Core.Tests.Fixtures;

var snapshot = Fixture.Load("excel/win-basic-table");   // ClipboardSnapshot
```

`dotnet test`를 실행하면 올라온 모든 샘플의 `meta.json`과 파일이 맞는지도 함께 확인한다.
