# ClipBridge.Helper (#31)

macOS 앱이 Core를 쓰기 위한 보조 프로그램입니다. 앱이 실행 중 계속 띄워 두고 **표준 입출력으로 한 줄에 JSON 메시지 하나**씩 주고받습니다 (PRD 4.5, FR-29).

- 메시지 안의 계약 타입(`snapshot`, `options`, 결과)은 [Core 계약](../ClipBridge.Core/Contracts/README.md) JSON과 같습니다. 속성은 camelCase, enum은 문자열, 바이트는 Base64입니다.
- 표준 출력에는 프로토콜 메시지만 나옵니다. 줄바꿈은 `\n`이고, 한글과 `<` 같은 문자는 `\uXXXX`로 이스케이프될 수 있습니다(JSON 표준이라 Swift `JSONDecoder`가 그대로 읽습니다).
- 요청은 받은 순서대로 하나씩 처리하고, **요청마다 응답이 정확히 하나** 갑니다. 응답의 `id`는 요청의 `id`와 같습니다.

## 흐름

```text
앱 시작   → Helper 실행                       ◀ ready
복사      → prepare (스냅샷·옵션)              ◀ results (대상 4개 결과)
옵션 변경 → setOptions                         ◀ results (현재 옵션 기준으로 다시) / 준비된 게 없으면 ok
(AI 결과 B 채택)                               ◀ results (id: null)
붙여넣기  → Helper를 부르지 않고 마지막 results에서 대상 결과를 기록
앱 종료   → shutdown                           ◀ ok, Helper 종료
```

앱은 **가장 최근 복사의 `sequenceNumber`와 같은 `results`만** 씁니다. 다른 번호의 results는 버립니다 (FR-17).

## 앱 → Helper

### prepare

복사 직후 보냅니다. 이전 복사의 결과와 진행 중인 AI 검증은 버립니다. `options`는 생략할 수 있고, 생략하면 마지막 옵션을 씁니다.

```json
{"type":"prepare","id":1,"snapshot":{"formats":{"public.html":"PGh0bWw+...","public.utf8-plain-text":"7J2066aE..."},"sourceApp":"com.microsoft.Excel","sequenceNumber":101},"options":{"markdownMode":"Strip","aiVerification":true}}
```

- `formats`에는 변환에 쓰는 타입만 넣는 것을 권장합니다: `public.html`, `public.utf8-plain-text`, `public.rtf`, `org.chromium.source-url`(Chrome 계열·Claude 데스크톱이 넣는 페이지 주소, 웹·AI 답변 판별에 사용), Excel 고유 타입. Excel은 PDF·이미지까지 18개 타입을 넣기 때문에 전부 보내면 메시지가 수백 KB가 됩니다 ([샘플](../../tests/fixtures/excel/)).
- `sourceApp`은 복사 시점의 앞쪽 앱 번들 ID입니다.
- `sequenceNumber`는 `NSPasteboard.changeCount`입니다.

### setOptions

메뉴에서 Markdown 처리나 AI 검증을 바꿨을 때 보냅니다. 다시 복사하지 않아도 바뀐 옵션의 결과가 옵니다 (PRD 4.2).

```json
{"type":"setOptions","id":2,"options":{"markdownMode":"Format","aiVerification":false}}
```

### shutdown

```json
{"type":"shutdown","id":3}
```

## Helper → 앱

### ready

시작하자마자 한 번 보냅니다. 이 메시지가 오면 요청을 보낼 수 있습니다.

```json
{"type":"ready","protocolVersion":1}
```

### results

대상마다 확정된 결과입니다. 키는 `TargetApp` 이름(`Hangul`, `Notion`, `Word`, `Excel`, `Other`)이고, 값은 Core의 `ConversionResult`입니다.

- `isPassthrough`가 true면 원본 스냅샷을 그대로 다시 기록합니다 (PRD 6.5).
- false면 `html`을 `public.html`에, `plainText`를 `public.utf8-plain-text`에 기록합니다. 둘 중 null인 것은 기록하지 않습니다.

```json
{"type":"results","id":1,"sequenceNumber":101,"source":"Excel","content":"Table","results":{"Hangul":{"converterId":"C1","isPassthrough":false,"html":"<table>...</table>","plainText":"...","signals":[]},"Notion":{...},"Word":{"converterId":"PASS","isPassthrough":true,"html":null,"plainText":null,"signals":[]},"Other":{...}}}
```

- `id`가 null이면 앱이 요청하지 않은 갱신입니다. 백그라운드 AI 검증 결과(B)가 채택됐을 때 옵니다 (PRD 6.6). 받은 results로 통째로 바꾸면 됩니다.
- 복사 후 120초가 지나면 Helper는 준비한 결과를 해제합니다 (NFR-08). 그 뒤 `setOptions`의 응답은 `ok`입니다.

### ok / error

```json
{"type":"ok","id":3}
{"type":"error","id":1,"code":"failed","message":"..."}
```

| code | 뜻 |
|---|---|
| `invalid_request` | JSON이 아니거나, `type`을 모르거나, 필수 값(`snapshot.formats`, `options`)이 없음. JSON을 읽지 못하면 `id`는 null |
| `failed` | 변환 중 오류. 앱은 해당 복사를 통과로 처리 (FR-16) |

오류가 나도 Helper는 종료되지 않고 다음 요청을 처리합니다.

## 터미널에서 확인

macOS 앱 없이 요청을 넣어 볼 수 있습니다.

```bash
dotnet run --project src/ClipBridge.Helper
# 아래 줄을 붙여 넣고 Enter ("a<b"를 Base64로 넣은 일반 텍스트)
{"type":"prepare","id":1,"snapshot":{"formats":{"public.utf8-plain-text":"YTxi"},"sourceApp":null,"sequenceNumber":1}}
{"type":"shutdown","id":2}
```

Helper는 실제 변환기 `UpClipConverter`(#14)를 씁니다. 출처를 판별하고(PRD 6.1) [출처 → 대상] 매트릭스(PRD 6.2)대로 결과를 만들며, 아직 없는 변환기 칸(인터넷 → Word C7, Markdown → Word C8, 일반 텍스트 TX)은 통과입니다.

## 빌드·앱 번들에 넣기

.NET이 설치되지 않은 Mac에서도 실행되는 단일 실행 파일로 만듭니다 (self-contained, 약 80MB).

```bash
dotnet publish src/ClipBridge.Helper -c Release -r osx-arm64 -o out/helper-arm64   # Apple Silicon
dotnet publish src/ClipBridge.Helper -c Release -r osx-x64   -o out/helper-x64     # Intel
```

`out/helper-*/ClipBridge.Helper` 하나만 있으면 됩니다(`.pdb`는 디버그용). 앱 번들에는 다음처럼 넣습니다.

```text
Unbroken Paste.app/Contents/MacOS/UP                      # 메뉴 막대 앱
Unbroken Paste.app/Contents/MacOS/UnbrokenPaste.Helper-arm64
Unbroken Paste.app/Contents/MacOS/UnbrokenPaste.Helper-x64
```

- 실행 위치: 앱에서 `Bundle.main.url(forAuxiliaryExecutable:)`로 찾고, `#if arch(arm64)`로 아키텍처에 맞는 파일을 고릅니다. `lipo`로 합치지 마세요. 단일 파일 실행 파일은 뒤에 데이터를 붙인 구조라 합치면 깨질 수 있습니다.
- 서명: `dotnet publish`가 ad-hoc 서명을 해 줍니다. 앱 전체를 다시 서명할 때는 Helper를 먼저 서명한 뒤 앱을 서명합니다.
- 공증(notarization)을 하려면 hardened runtime에서 .NET JIT가 동작하도록 `com.apple.security.cs.allow-jit` entitlement를 Helper에 줘야 합니다. 해커톤 데모(ad-hoc)에는 필요 없습니다.
- 시작부터 `ready`까지 약 0.02–0.08초입니다(M 시리즈 Mac 측정). 새로 빌드한 파일을 처음 실행할 때만 macOS 검사 때문에 1초 남짓 걸립니다. 앱 시작 때 한 번 띄워 두므로 복사할 때는 영향이 없습니다.
