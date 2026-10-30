# Core ↔ 앱 껍데기 계약 (#29)

Windows 앱(`ClipBridge.Windows`)과 macOS 앱(`ClipBridge.Helper` 경유)이 Core와 주고받는 타입입니다. 바꾸려면 이창민·윤승희·장세찬 모두 승인이 필요합니다.

## 호출 흐름

```text
복사   → 앱 껍데기가 ClipboardSnapshot 생성
       → 이전 PreparedConversions.Dispose()  (FR-17 최신 복사 우선)
       → 백그라운드에서 converter.Prepare(snapshot, options, ct) → PreparedConversions 보관
       → 120초 후 Dispose()  (NFR-08)
붙여넣기 → 대상 판별(TargetApp) → prepared.Get(target, 현재 옵션)
       → IsPassthrough면 원본 스냅샷 기록, 아니면 Html/PlainText 기록
```

## 결정 사항

| 항목 | 결정 |
|---|---|
| 포맷 이름 | OS 이름 그대로 넘긴다 (`HTML Format`, `public.html` 등, `ClipFormats` 참고). 두 OS 포맷을 구분하는 것은 Core 책임 |
| 복사한 앱 | `SourceApp` 하나로 받는다. Windows는 프로세스 이름(`EXCEL.EXE`), macOS는 번들 ID(`com.microsoft.Excel`). 매핑은 Core가 한다 |
| 시퀀스 번호 | `long`. Windows `GetClipboardSequenceNumber`, macOS `changeCount` |
| 문자열 인코딩 | 앱 껍데기는 원본 바이트만 넘기고, 바이트 → 문자열 변환은 Core가 한다 (CF_UNICODETEXT는 UTF-16LE, 나머지는 UTF-8) |
| CF_HTML 헤더 | Core 결과(`Html`)는 순수 HTML 조각. Windows 앱이 기록할 때 `CfHtml.Wrap(html)`으로 헤더를 붙인다. macOS는 그대로 `public.html`에 쓴다 |
| 대상 앱 판별 | 각 앱 껍데기가 하고 Core에는 `TargetApp`만 넘긴다 |
| 스레드 모델 | `Prepare`는 백그라운드 스레드에서 동기 호출(목표 p95 200ms, NFR-02). `Get`은 잠금·계산 없이 미리 만든 결과만 돌려준다(NFR-03). 어느 스레드에서 불러도 된다 |
| Markdown 옵션 | `Prepare`가 기호 제거·서식 적용 두 모드를 모두 미리 준비한다. `Get`에 현재 옵션을 넘기므로 다시 복사하지 않아도 반영된다 (PRD 4.2) |
| AI 검증 결과 | B가 채택되면 `PreparedConversions` 내부에서 교체하고 `Updated` 이벤트(백그라운드 스레드)를 발생시킨다. A는 덮어쓰지 않는다. `Get`은 `AiVerification`이 켜져 있을 때만 B를 준다 |
| 취소 | `Prepare`의 `ct`는 준비 단계만 취소한다. 진행 중인 AI 검증은 `Dispose()`로 취소된다. `Dispose` 후 `Get`은 통과를 돌려준다 |
| Helper 프로토콜 | `ContractsJsonContext`로 JSON 직렬화한다. 바이트는 Base64, enum은 문자열, 속성 이름은 camelCase. 메시지 형식은 #31에서 정한다 |

## 가짜 구현

실제 변환기가 들어오기 전까지 `StubClipConverter`를 쓴다. 일반 텍스트가 있으면 한글·Notion·Word용으로 `[ClipBridge] ` 표시를 붙인 결과를 주고, 그 외 대상·포맷은 통과한다. 변환 결과가 실제로 붙는지 눈으로 확인할 수 있다.

```csharp
IClipConverter converter = new StubClipConverter();
using var prepared = converter.Prepare(snapshot, ConversionOptions.Default, ct);
var result = prepared.Get(TargetApp.Hangul, ConversionOptions.Default);
```
