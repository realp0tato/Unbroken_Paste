# UP — UnbrokenPaste

평소처럼 복사하고 붙여넣으면, **어디서 복사했는지(출처)**와 **어디에 붙이는지(대상)**를 인식해 그 조합에 맞는 변환기로 글·표·줄간격을 정리한 결과를 붙여 주는 백그라운드 앱입니다.

> 대학 AX 해커톤 MVP · 현재 **개발 중**입니다. 아래 기능은 [PRD](prd.md)에 정의된 요구사항이며, 구현·검증 상태는 [이슈](https://github.com/realp0tato/donga_hackathon/issues)에서 관리합니다.

> **이름:** 제품 이름은 **UP**(UnbrokenPaste의 약자)입니다. 코드의 폴더·프로젝트·네임스페이스는 아직 `ClipBridge.*`이며, 추후 한꺼번에 `UnbrokenPaste.*`로 바꿉니다.

## 해결하려는 문제

- Excel 표를 한글·Notion에 붙이면 서식이 엉키거나 셀 구분이 깨진다.
- 웹 글을 붙이면 사이트 글꼴·색·배경·여백이 함께 따라온다.
- AI 답변의 Markdown 기호(`#`, `##`, `**`)가 그대로 붙는다.
- 빈 줄이 두 배로 들어가 줄간격을 다시 맞춰야 한다.

## 동작 방식

```text
복사 ─▶ 포인터 옆에 UP 아이콘
      ─▶ 원본을 메모리에 보관, 출처(Excel·인터넷·Markdown 글·일반 텍스트) 판별
      ─▶ 대상별 변환 결과를 미리 준비

붙여넣기 ─▶ 붙이는 앱 인식 ─▶ [출처 → 대상] 변환기 선택
         ─▶ 변환 결과를 클립보드에 기록 ─▶ 그 내용이 붙여넣기됨
         ─▶ 포인터 옆에 대상 앱 아이콘, 붙여넣기 시점에 사라짐
```

- 변환 버튼·모드 선택 없이 **복사·붙여넣기만** 하면 됩니다.
- 변환은 항상 **원본**에서 다시 하므로, 같은 내용을 여러 앱에 붙여도 결과가 섞이지 않습니다.
- 변환할 수 없거나 실패하면 **원본 그대로** 붙습니다.

## 변환기

| 출처 ＼ 대상 | 한글 | Notion | Word | 그 외 |
|---|---|---|---|---|
| **Excel** (표·글) | **C1** | **C2** | 통과 ※ | 통과 |
| **인터넷** (글·표) | **C3** | **C4** | **C7** | 통과 |
| **Markdown 글** | **C5** | **C6** | **C8** | 통과 |
| **일반 텍스트** | 줄간격 정리 | 줄간격 정리 | 줄간격 정리 | 통과 |
| 기타 (이미지·파일 등) | 통과 | 통과 | 통과 | 통과 |

※ Excel → Word는 같은 Office 제품이라 Word의 기본 붙여넣기가 더 안정적이므로 변환하지 않습니다.

| ID | 변환기 | 하는 일 |
|---|---|---|
| C1 | Excel → 한글 | 표: 셀 값·굵게·정렬·테두리·열 너비만 남긴 표 / 글: 셀마다 한 문단 |
| C2 | Excel → Notion | 표: Notion 단순 표(첫 행 머리글) / 글: 셀마다 한 줄 |
| C3 | 인터넷 → 한글 | 사이트 서식 제거, 제목·굵게·목록만 유지, 표는 테두리 있는 단순 표 |
| C4 | 인터넷 → Notion | C3 + 링크·코드 블록 유지 |
| C7 | 인터넷 → Word | C3 + 링크 유지, 제목을 Word 제목 스타일로 |
| C5·C6·C8 | Markdown → 한글·Notion·Word | 기본 **서식 적용**: Notion은 제목·굵게·목록·표 그대로, Word·한글은 `#`→큰 굵은 글자·`**`→굵게(제목 스타일 미사용) / 옵션: **기호 제거** |
| 공통 | 줄간격 보정 | 연속 빈 줄·빈 문단·이중 문단 간격 정리 (모든 변환기에 적용) |

변환 규칙 상세: [PRD 6절](prd.md#6-변환기-및-변환-규칙)

## 메뉴와 옵션

Windows는 작업 표시줄 트레이 아이콘, macOS는 메뉴 막대 아이콘에서 같은 메뉴를 엽니다. 별도 설정 창은 없습니다.

```text
UP
├─ Markdown 처리
│   ├─ ● 서식 적용 (기본)
│   └─ ○ 기호 제거
├─ AI 검증 (외부 API 사용)
│   ├─ ● 켜기 (기본)
│   └─ ○ 끄기
└─ 종료
```

macOS 메뉴 막대에는 이 밖에 상태 표시·변경, 최근 복사 10개(메모리 전용), 캐릭터 선택 메뉴가 있습니다. 자세한 내용은 [PRD 4.2.2](prd.md#422-macos-추가-메뉴와-메뉴-막대-아이콘)를 보세요.

## AI 검증과 개인정보

- **기본값은 켜짐**입니다. 변환 결과 중 **의심스러운 것만** AI로 한 번 더 검증합니다. 메뉴에서 끌 수 있습니다.
- AI는 정해진 구조 수정 지시만 낼 수 있고 글자를 새로 만들 수 없습니다. 코드 검사를 통과한 수정만 쓰이며, 통과하지 못하면 변환기 결과가 그대로 붙습니다.
- 켜져 있을 때는 해당 복사 내용 일부가 **외부 AI API로 전송**됩니다. 꺼져 있거나 오프라인이면 모든 처리가 로컬에서 이루어집니다.
- 복사한 본문과 AI 요청·응답은 파일이나 로그로 저장하지 않습니다. 디스크에는 옵션 값만 저장합니다.

## 지원 환경

| 항목 | 내용 |
|---|---|
| Windows | Windows 10/11 x64, 일반 사용자 권한 (트레이 앱) |
| macOS | macOS 13 이상 (메뉴 막대 앱). 처음 실행할 때 **손쉬운 사용** 권한 허용 필요 (Cmd+V 감지용) |
| 붙여넣기 키 | Windows: Ctrl+V, Shift+Insert / macOS: Cmd+V |
| 지원하지 않음 | 관리자 권한 앱·보안 데스크톱·원격 세션, 이미지·수식·차트 재구성, Linux |

두 OS 모두 같은 기능을 제공하며, 같은 입력이면 같은 변환 결과가 나옵니다.

## 기술 스택

변환 로직은 **C# 하나로** 만들어 두 OS가 공유하고, OS와 맞닿는 앱 껍데기만 OS별 언어로 만듭니다.

| 구성 | 기술 | 비고 |
|---|---|---|
| 변환기 라이브러리 (`ClipBridge.Core`) | C# · .NET 10 (`net10.0`) | 출처 판별·변환기 C1–C8·줄간격 보정·AI 검증. **OS API 사용 금지** → 두 OS에서 공유, macOS에서 개발·테스트 가능 |
| HTML 파싱·정리 | [AngleSharp](https://github.com/AngleSharp/AngleSharp), [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) | 웹·Excel HTML 파싱, 허용 목록 방식 정리 |
| Markdown | [Markdig](https://github.com/xoofx/markdig) | 서식 적용 모드의 파싱 |
| 테스트 | xUnit | `tests/fixtures`의 실제 클립보드 샘플로 테스트 |
| Windows 앱 (`ClipBridge.Windows`) | C# · .NET 10 · WinForms (`net10.0-windows`) | Core를 직접 참조. 트레이(`NotifyIcon`), 커서 아이콘 창, P/Invoke로 Win32 호출: 키보드 감지(`WH_KEYBOARD_LL`), 클립보드 감지(`AddClipboardFormatListener`), 지연 렌더링(`WM_RENDERFORMAT`) |
| macOS 보조 프로그램 (`ClipBridge.Helper`) | C# · .NET 10 콘솔 앱 | Core를 macOS 앱에서 쓰기 위한 다리. macOS 앱이 실행 중 계속 띄워 두고 표준 입출력으로 변환 요청·응답 |
| macOS 앱 | Swift 패키지 · AppKit (`NSStatusItem`) | 메뉴 막대(슬라임 애니메이션 아이콘), 클립보드 감시(`NSPasteboard`), Cmd+V 감지(이벤트 탭, 손쉬운 사용 권한), 지연 제공(`NSPasteboardItemDataProvider`), 커서 아이콘(비활성 패널). 옵션은 `UserDefaults`에 저장. Helper를 앱 번들에 포함 |
| AI 검증 | Claude API (Messages API, `HttpClient`) | Core 안에서 호출. 모델 예: Claude Haiku 5.5. API 키는 환경 변수 `ANTHROPIC_API_KEY`에서 읽음 |
| 배포 | `dotnet publish` self-contained 단일 실행 파일 | .NET 설치 없이 실행. Windows 앱, macOS용 Helper 모두 |

**서버는 없습니다.** 모든 변환은 사용자 PC에서 이루어지고, AI 검증이 켜져 있을 때만 앱이 AI API를 직접 호출합니다.

## 저장소 구조

```text
ClipBridge.slnx            # 솔루션: C# 프로젝트 8개를 묶는 목록
global.json                # 모두 같은 .NET 10 SDK로 빌드하도록 고정
Directory.Build.props      # 모든 C# 프로젝트 공통 설정
.gitignore                 # Git에 올리지 않을 파일 (빌드 결과물, API 키 등)
src/
  ClipBridge.Core/         # ① 변환기 라이브러리 (OS 무관)
  ClipBridge.Windows/      # ② Windows 앱 (Core 직접 참조)
  ClipBridge.Helper/       # ③ macOS용 보조 프로그램 (Core를 감싼 콘솔 앱)
tests/
  ClipBridge.Core.Tests/   # ④ 변환기 단위 테스트
  ClipBridge.Helper.Tests/ # ③ Helper 테스트
  ClipBridge.Windows.Tests/ # ② 중 Windows API를 쓰지 않는 부분 테스트 (macOS에서도 실행)
  fixtures/                # ⑤ Windows·macOS에서 수집한 클립보드 원본 샘플
tools/
  ClipBridge.FixtureDump/  # ⑤를 만드는 Windows 클립보드 샘플 저장 도구
  ClipBridge.TryMode/      # 복사할 때마다 변환 결과를 클립보드에 넣어 보는 개발용 시험 모드 (macOS)
macos/
  ClipBridgeMenuBar/       # ⑥ macOS 앱 (Swift, C# 솔루션에 포함 안 됨)
prd.md
README.md
CONTRIBUTING.md
```

### 기본 개념

- **프로젝트:** 코드 묶음 하나. 폴더 안의 `.csproj` 파일이 그 프로젝트의 종류와 설정을 정한다.
- **솔루션:** 여러 프로젝트를 한 번에 열고 빌드하기 위한 목록. `dotnet build ClipBridge.slnx` 한 번으로 ①②③④와 샘플 저장 도구가 모두 빌드된다.
- **라이브러리:** 혼자 실행되지 않고, 다른 프로그램이 가져다 쓰는 기능 모음.
- **참조:** 한 프로젝트가 다른 프로젝트의 코드를 가져다 쓰는 것. ②③④가 ①을 참조한다.
- **앱 껍데기:** 사용자가 실제로 실행하는 프로그램. 메뉴·클립보드·키 감지·아이콘처럼 OS와 맞닿는 부분을 맡고, 변환은 ①에 맡긴다.

### 각 부분의 역할

| | 위치 | 하는 일 | 담당 |
|---|---|---|---|
| ① | `src/ClipBridge.Core/` | 변환 로직 전부: 출처 판별, 변환기 C1–C8, 줄간격 보정, AI 검증. **OS API를 쓰지 않으므로 두 OS가 공유하고 macOS에서 개발·테스트할 수 있다** | 장세찬, 박재현 |
| ② | `src/ClipBridge.Windows/` | Windows 앱 껍데기: 트레이, 복사·붙여넣기 감지, 클립보드 기록, 커서 아이콘. ①을 직접 호출 | 이창민 |
| ③ | `src/ClipBridge.Helper/` | macOS 앱이 ①을 쓸 수 있게 해 주는 보조 프로그램. 표준 입출력으로 요청을 받아 ①을 호출하고 결과를 돌려준다 | 장세찬 |
| ④ | `tests/ClipBridge.Core.Tests/` | "이 입력이면 이 결과가 나와야 한다"를 코드로 적어 `dotnet test`로 자동 확인 | ①을 만드는 사람 |
| ⑤ | `tests/fixtures/` | Excel·웹에서 복사했을 때 클립보드에 실제로 들어가는 데이터를 파일로 저장. ④의 입력으로 쓴다 ([규칙](tests/fixtures/README.md)) | 이창민(Windows), 윤승희(macOS) |
| ⑥ | `macos/ClipBridgeMenuBar/` | macOS 앱 껍데기: 메뉴 막대, 복사·붙여넣기 감지, 클립보드 기록, 커서 아이콘. ③을 통해 ①을 호출. Swift라서 C# 솔루션과 별도로 빌드한다 | 윤승희 |

### 흐름

```text
[Windows]
Ctrl+V ─▶ ② Windows 앱이 감지 ─▶ ① Core에 "이 원본을 이 앱용으로" 요청
       ◀─ ① 변환 결과 반환 ◀─────┘
       ─▶ ② 클립보드에 기록 ─▶ 대상 앱에 붙여넣기

[macOS]
Cmd+C ─▶ ⑥ macOS 앱이 감지 ─▶ ③ Helper ─▶ ① Core: 대상별 결과를 미리 준비
Cmd+V ─▶ ⑥ 대상 앱 인식 ─▶ 미리 받은 결과를 클립보드에 기록 ─▶ 붙여넣기

[테스트]
④ 테스트가 ⑤ 샘플을 ①에 넣어 결과가 맞는지 확인 (macOS에서 실행)
```

①과 앱 껍데기(②, ③을 거친 ⑥) 사이에서 주고받는 타입과 호출 방식은 `src/ClipBridge.Core/Contracts/`에 둔다 ([#29](https://github.com/realp0tato/donga_hackathon/issues/29)에서 확정). ③의 요청·응답 형식도 같은 타입을 따른다.

## 개발 환경

| 담당 | 필요한 도구 |
|---|---|
| 변환기·Helper (macOS) | .NET 10 SDK, VS Code + C# Dev Kit 또는 JetBrains Rider |
| Windows 앱 (Windows) | .NET 10 SDK, Visual Studio 또는 Rider |
| macOS 앱 (macOS) | Xcode 또는 Command Line Tools, .NET 10 SDK (Helper 빌드용). 실행 방법은 [macOS 앱 README](macos/ClipBridgeMenuBar/README.md) |

- 클립보드에 들어가는 데이터는 OS마다 다릅니다. Windows PC와 Mac에서 각각 원본 포맷을 파일로 저장해 `tests/fixtures`에 올리고, 변환기는 두 OS 샘플로 모두 테스트합니다.
- 한글·Word·Notion 실제 붙여넣기 검증은 해당 OS에서 직접 해야 합니다.

## 빌드와 테스트

```bash
dotnet build ClipBridge.slnx   # macOS에서도 Windows 앱까지 빌드됨 (Windows 앱 실행은 Windows에서만)
dotnet test                    # 변환기 단위 테스트
```

- 외부 패키지(AngleSharp, HtmlSanitizer, Markdig)는 해당 기능 담당자가 작업할 때 `dotnet add src/ClipBridge.Core package <이름>`으로 추가합니다.
- 테스트 프로젝트의 패키지 버전은 `*`로 되어 있습니다. 처음 빌드한 사람이 실제 설치된 버전으로 고정합니다.

## 설치 및 실행

배포 패키지는 준비 중입니다. 개발 중에는 저장소 루트에서 실행합니다.

**Windows 앱**

```bash
dotnet run --project src/ClipBridge.Windows
```

- 창 없이 작업 표시줄 알림 영역(트레이)에 UP 아이콘이 생깁니다. 보이지 않으면 `^`(숨겨진 아이콘 표시) 안을 확인하세요.
- 아이콘을 오른쪽 클릭해 `종료`로 끕니다. 이미 실행 중이면 새로 띄우지 않고 안내 창만 보여 줍니다.

**macOS 앱:** [macOS 앱 README](macos/ClipBridgeMenuBar/README.md)

## 문서

- [PRD (제품 요구사항 문서)](prd.md)
- [작업 규칙 (브랜치·PR·리뷰)](CONTRIBUTING.md)
- [이슈 목록](https://github.com/realp0tato/donga_hackathon/issues)

## 팀

| 이름 | 담당 영역 |
|---|---|
| 박재현 | 변환 로직 (C#, Core): 인터넷·Markdown·Notion 변환기, 출처 판별, 변환기 자동 선택, 줄간격 보정, AI 검증 |
| 장세찬 | 변환 로직 (C#, Core): Excel·Word 변환기, 표 공통 모델, macOS용 Helper |
| 이창민 | Windows 앱 껍데기 (C#): 트레이, 클립보드·붙여넣기 연동, 커서 아이콘, Windows 검증·샘플 수집 |
| 윤승희 | macOS 앱 껍데기 (Swift): 메뉴 막대, 클립보드·붙여넣기 연동, 커서 아이콘, Helper 연동, macOS 검증·샘플 수집 |
