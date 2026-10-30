# UP macOS 메뉴 막대 앱

Swift (macOS 13 이상). 메뉴 막대에서 UP 슬라임이 통통 튀고, 아이콘을 누르면 메뉴가 열려요. (#23)

공통 메뉴와 기본값은 [PRD 4.2절](../../prd.md#42-메뉴-앱-windows-트레이--macos-메뉴-막대), 추가 메뉴(클립보드·캐릭터·정보)는 PRD 4.2.2를 따른다. 프로그램 이름은 **Unbroken Paste**, 그 밖의 화면 표시는 **UP**이다. 이 폴더의 코드 이름(폴더·패키지·모듈·번들 ID)은 `UnbrokenPaste`이고, 맥 앱이 쓰는 C# 프로젝트(`src/ClipBridge.*`)는 팀이 정한 시점에 따로 바꾼다.

## 실행

Xcode 또는 Command Line Tools(`xcode-select --install`)가 필요해요. 변환(Helper)까지 쓰려면 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)도 필요해요. 없으면 앱은 그대로 동작하고 붙여넣기만 변환 없이 평소대로 돼요.

```bash
cd macos/UnbrokenPaste
swift run
```

`.app`으로 만들기 (더블클릭 실행, 정보 창에 앱 아이콘 표시):

```bash
./scripts/build-app.sh
open "build/Unbroken Paste.app"
```

`build-app.sh`는 .NET SDK가 있으면 `src/ClipBridge.Helper`를 이 Mac의 아키텍처용으로 빌드해 `Unbroken Paste.app/Contents/MacOS/UnbrokenPaste.Helper-arm64`(또는 `-x64`)로 넣어요. `swift run`일 때는 `build/helper-arm64/ClipBridge.Helper`를 쓰니, 빌드 스크립트를 한 번 실행해 두세요. 다른 위치의 Helper를 쓰려면 환경 변수 `UP_HELPER`에 경로를 넣어요.

배포용 DMG 만들기 (#40):

```bash
./scripts/build-app.sh
./scripts/build-dmg.sh
```

`build/UnbrokenPaste-0.1.0.dmg`가 생겨요. 열면 앱과 `Applications` 바로가기가 있어서 앱을 끌어다 놓으면 설치돼요. 앱, DMG 볼륨, DMG 파일 모두 슬라임 아이콘(`Resources/AppIcon.icns`)으로 보여요. Helper는 .NET 런타임을 함께 묶은 단일 파일이라 받는 Mac에 .NET이 없어도 돼요.

- DMG 파일 자체의 아이콘은 만든 Mac의 Finder에서만 보여요. 웹에 올렸다 받으면 파일 아이콘은 기본 모양으로 돌아가고, 열었을 때의 볼륨 아이콘과 앱 아이콘은 그대로예요.
- 서명·공증을 하지 않은 앱이라 받은 Mac에서 처음 열면 열 수 없다는 경고가 떠요. 경고를 닫고 시스템 설정 → 개인정보 보호 및 보안 맨 아래의 "그래도 열기"를 눌러요(macOS 14 이하는 앱을 Control-클릭 → 열기도 돼요). 그래도 안 열리면 터미널에서 `xattr -dr com.apple.quarantine "/Applications/Unbroken Paste.app"`.

Xcode에서 열려면 `open Package.swift`.

테스트 (Core 계약 JSON 형식, 복사 감지 동작 확인, Xcode 필요):

```bash
swift test
```

## 메뉴

```text
UP · 대기 중
──────────
Markdown 처리 ▸            ✓ 서식 적용 (기본) / 기호 제거
AI 검증 ▸                  ✓ 켜기 (기본) / 끄기 / 외부 API 전송 안내
──────────
클립보드 ▸    최근 복사 10개 (누르면 다시 복사) / 기록 지우기 / 복사하면 슬라임 반응
캐릭터 ▸      ✓ 기본 슬라임 / 고양이 슬라임 / 녹는 슬라임 / 구름 슬라임 / 토끼 슬라임
──────────
Unbroken Paste 정보   ← 앱 이름·버전·만든 사람·저작권
UP 종료  ⌘Q
```

- **옵션 저장:** `UserDefaults` (`~/Library/Preferences/app.unbrokenpaste.menubar.plist`, `swift run`일 때는 `UnbrokenPaste.plist`). 옵션 값만 저장하고 복사 본문은 저장하지 않아요. (PRD 4.2)
- **중복 실행 방지:** 이미 실행 중이면 두 번째 실행은 바로 종료돼요. 사용자 임시 폴더의 잠금 파일(`app.unbrokenpaste.menubar.lock`)로 확인하고, 앱이 끝나면 잠금은 자동으로 풀려요.
- **Markdown 처리·AI 검증:** Core 계약과 같은 값(`Format`/`Strip`, 기본 서식 적용·AI 검증 켬)으로 저장하고 `Settings.conversionOptions`로 꺼내요. 변환기 연결은 Helper 연동(#36)에서 진행.
- **복사 감지 (#33):** 0.3초마다 `changeCount`를 확인해요. 복사하면 모든 포맷을 `ClipboardSnapshot`으로 메모리에 1건만 보관하고, 그때 앞에 있던 앱의 번들 ID와 `changeCount`를 함께 기록해요. 앱이 직접 쓴 변경(`recordOwnWrite`)은 새 복사로 치지 않아요.
- **붙여넣기 감지 (#34):** 키보드 이벤트 탭으로 Cmd+V를 감지하고, 앞쪽 앱으로 대상(한글·Notion·Word·기타)을 판별해요. 키는 그대로 통과시키고 버리거나 다시 보내지 않아요. 시스템이 탭을 꺼도 바로 다시 켜요. 판별 결과는 시스템 로그로 확인해요.

  ```bash
  log stream --level info --predicate 'subsystem == "app.unbrokenpaste.menubar"'
  ```
- **손쉬운 사용 권한:** 처음 실행할 때 한 번 요청해요. 허용하기 전에는 메뉴 맨 위에 `⚠︎ 손쉬운 사용 권한 필요 · 눌러서 설정 열기`가 보이고, 붙여넣기는 변환 없이 평소대로 돼요. 설정에서 허용하면 2초 안에 자동으로 켜지고 항목이 사라져요.
  - `./scripts/build-app.sh`로 다시 빌드하면 서명이 바뀌어서 권한이 풀릴 수 있어요. 그러면 시스템 설정 → 개인정보 보호 및 보안 → 손쉬운 사용에서 UP를 지우고(−) 다시 허용하세요.
  - `swift run`으로 실행하면 권한은 UP가 아니라 실행한 터미널 앱에 붙어요.
  - 앱 이름이 `ClipBridge.app`에서 `Unbroken Paste.app`으로, 번들 ID가 `app.clipbridge.menubar`에서 `app.unbrokenpaste.menubar`로 바뀌었어요(#47). 예전에 허용한 `ClipBridge` 항목은 지우고 `Unbroken Paste`를 새로 허용하세요. 실행 중인 앱을 터미널에서 끄려면 `pkill -x UP; pkill -f UnbrokenPaste.Helper`(실행 파일 이름은 `UP`).
  - 예전 번들 ID로 저장된 메뉴 옵션은 처음 실행할 때 한 번 옮겨 와요(`Settings.migrateLegacyDefaults`).
  - 이름 규칙: 프로그램 이름(앱 파일·Finder·앱 정보)은 풀네임 **Unbroken Paste**, 메뉴·툴팁·알림 등 그 밖의 화면 표시는 **UP**이에요.
- **Helper 연동 (#36):** 앱이 켜질 때 Helper를 실행해 계속 띄워 두고 표준 입출력으로 주고받아요([프로토콜](../../src/ClipBridge.Helper/README.md)). 복사하면 `prepare`로 대상별 결과를 미리 받고, 메뉴에서 Markdown 처리·AI 검증을 바꾸면 `setOptions`를 보내요. Helper에는 Core가 읽는 포맷(`public.html`, `public.utf8-plain-text`, `public.rtf`, Chrome 계열 페이지 주소 `org.chromium.source-url`)만 보내고, 원본 스냅샷 전체는 앱에 남겨 둬요.
  - 가장 최근 복사와 순서 번호가 같은 결과만 써요. 결과가 없거나 오래됐거나 오류면 통과예요.
  - Helper가 꺼지면 1초 뒤 다시 실행하고(실패가 이어지면 최대 30초까지 늘림), 10초 동안 답이 없으면 강제로 끄고 다시 실행해요. 다시 실행되면 현재 복사를 다시 보내요.
  - `UP 종료` 시 `shutdown`을 보내고 Helper도 함께 끝나요. 앱이 비정상 종료돼도 Helper는 입력이 끊겨 스스로 끝나요.
  - 받은 결과는 시스템 로그(`helper` 분류)에 본문 없이 남아요.
- **붙여넣기 때 결과 기록 (#35):** Cmd+V가 대상 앱에 닿기 전에 그 대상의 결과를 클립보드에 넣어요. 바이트는 대상 앱이 달라고 할 때 건네는 지연 제공(`NSPasteboardItemDataProvider`)이라 기록이 빨라요. HTML은 `public.html`에 헤더 없이, 글은 `public.utf8-plain-text`에 넣어요.
  - 결과가 없거나 통과면 건드리지 않아요. 앞서 변환 결과를 넣었는데 이번 대상이 통과면 원본 스냅샷을 다시 넣어요(PRD 6.5). 같은 대상에 다시 붙이면 다시 쓰지 않아요.
  - 스냅샷 이후 다른 앱이 클립보드를 바꿨으면(아직 감지 전인 새 복사) 절대 덮어쓰지 않아요.
  - UP가 쓴 변경은 새 복사로 치지 않고 최근 복사에도 넣지 않아요. `UP 종료` 때 클립보드가 UP의 기록이면 원본을 실제 데이터로 되돌려 놓아요(지연 제공 데이터는 앱이 꺼지면 사라지기 때문).
  - 로그: `paste write·restore·leave converter=… target=… <ms>`
  - Helper가 연결되지 않으면 메뉴 첫 줄 아래에 `변환 엔진 연결 중 · 붙여넣기는 평소대로` 또는 (Helper 없이 빌드했을 때) `변환 엔진 없음 · .NET 설치 후 다시 빌드`가 보여요. 연결되면 사라져요.
- **포인터 옆 동그라미 아이콘 (#38):** 복사하면 UP 캐릭터 배지(`Resources/Characters/<id>/badge.png`)가 0.8초, 붙여넣으면 대상 앱 아이콘이 1초 동안 포인터 오른쪽 아래(지름 30pt)에 보이고 0.15초에 걸쳐 사라져요. 포인터를 따라다니고, 화면 가장자리에서는 반대쪽으로 뒤집혀요.
  - Notion 웹은 브라우저 대신 Notion 아이콘, 변환 대상(한글·Notion·Word)은 강조색 테두리, 그 외 앱(통과)은 얇은 회색 테두리예요. 아이콘이 없으면 앱 이름 첫 글자, 앱을 모르면 `?`를 보여줘요.
  - 포커스를 가져가지 않고 클릭이 통과하는 패널이라 입력·IME를 방해하지 않아요. 모든 Space와 전체 화면 앱 위에도 떠요.
  - 대상 앱이 데이터를 가져가는 순간(지연 제공 요청) 바로 사라지고, 그 신호가 없으면 1초 뒤 사라져요. 테두리는 실제로 변환 결과를 넣었을 때만 강조색이에요.

### 대상 앱 매핑 (PRD 4.5)

| 대상 | 판별 기준 |
|---|---|
| Word | 번들 ID `com.microsoft.Word` (맥에서 확인) |
| Notion 앱 | 번들 ID `notion.id` (맥에서 확인) |
| 한글 (HOP) | 번들 ID `net.golbin.hop` (맥에서 확인). [HOP](https://github.com/golbin/hop)는 HWP 파일을 여는 오픈소스 편집기 |
| 한글 (한컴오피스) | 번들 ID가 `com.hancom.`으로 시작하고 `hwp`가 들어 있거나, 앱 이름에 `HWP`·`한글`이 있음 (버전마다 ID가 달라 규칙으로 판별, 맥에서 미확인) |
| 한글 (한컴독스 웹) | 브라우저 주소가 `webhwp.*hancomdocs.com`(웹 한글 편집기)이거나, 창 제목에 `한컴독스`·`Hancom Docs`가 있음. 아이콘은 `한` 글자 배지 |
| Notion 웹 | 브라우저 주소가 `notion.so`·`*.notion.site`이거나, 창 제목에 `Notion`이 있음 |
| (브라우저 공통) | Safari·Chrome·Edge·Firefox·Whale·Arc. 주소는 창의 `AXDocument`, 없으면 페이지 영역(`AXWebArea`)의 `AXURL`에서 읽고(30ms 안에서만 찾음), 못 읽으면 창 제목으로 판단. 둘 다 안 되면 통과. 로그에 `browser url=<도메인> via=document·webArea·none title=yes·no`가 남아요(전체 주소·제목은 남기지 않음) |
| 그 외 | 통과 |

설치된 앱의 번들 ID 확인: `osascript -e 'id of app "Microsoft Word"'` (앱 이름만 바꿔서 실행)
- **클립보드:** 텍스트 복사 최근 10개를 메모리에만 보관해요. 비밀번호 관리자가 숨김 표시를 한 복사는 기록하지 않고, 이전 스냅샷도 버려서 그 뒤 붙여넣기에 예전 변환 결과가 붙지 않게 해요.
- **CPU 연동:** 코드에는 있지만 메뉴에서 숨겨 두었어요 (`StatusController.swift`의 `cpu.isHidden`).

## 구조

| 파일 | 역할 |
|---|---|
| `Sources/UnbrokenPasteContracts/Contracts.swift` | `src/ClipBridge.Core/Contracts`(#29)의 Swift 버전. Helper와 주고받는 타입과 JSON 형식(camelCase, enum 문자열, 바이트 Base64). C# 계약이 바뀌면 같이 바꾼다 |
| `Tests/UnbrokenPasteContractsTests/` | C# `ContractsJsonTests`와 같은 JSON으로 검증 |
| `Sources/UnbrokenPasteClipboard/ClipboardMonitor.swift` | 복사 감지, 원본 스냅샷, 자기 기록 무시, 최근 복사 기록 (#33) |
| `Sources/UnbrokenPasteClipboard/PasteboardSource.swift` | `NSPasteboard` 접근 인터페이스. 테스트에서는 가짜 클립보드로 바꿔 끼움 |
| `Sources/UnbrokenPasteClipboard/PasteTarget.swift` | 대상 앱 판별(`TargetResolver`)과 Cmd+V 판정(`PasteKey`) (#34) |
| `Sources/UnbrokenPaste/PasteWatcher.swift` | 이벤트 탭, 손쉬운 사용 권한 요청·재확인, 브라우저 창 제목 읽기 (#34) |
| `Sources/UnbrokenPaste/CursorOverlay.swift` | 포인터 옆 동그라미 아이콘 패널 (#38) |
| `Sources/UnbrokenPaste/HelperClient.swift` | Helper 실행·재시작·요청/응답, 최근 복사의 결과 보관 (#36) |
| `Sources/UnbrokenPasteContracts/HelperProtocol.swift` | Helper 메시지 타입, 보낼 포맷 고르기, 줄 단위 읽기 (#36) |
| `Sources/UnbrokenPasteClipboard/PastePlan.swift` | 붙여넣기 순간 클립보드에 무엇을 할지 결정 (쓰기·원본 복원·그대로) (#35) |
| `Sources/UnbrokenPasteClipboard/PasteWriter.swift` | 지연 제공으로 결과·원본 기록, 데이터 요청 알림, 종료 시 원본 복원 (#35) |
| `Sources/UnbrokenPasteClipboard/OverlayLayout.swift` | 아이콘 위치 계산(가장자리 뒤집기)과 보여줄 배지 결정 (#38) |
| `Tests/UnbrokenPasteClipboardTests/` | 가짜 클립보드로 복사 감지 동작 검증 (연속 복사, 자기 기록, 숨김 복사 등), 대상 판별·Cmd+V 판정 |
| `App.swift` | 진입점. Dock 아이콘 없이 메뉴 막대 전용으로 실행, 중복 실행 방지 |
| `StatusController.swift` | 메뉴 막대 아이템, 메뉴 구성, 애니메이션 타이머 |
| `SlimeAnimator.swift` | 시간 기준 프레임 재생, CPU 사용률 측정 |
| `About.swift` | `Unbroken Paste 정보` 창: 만든 사람(UP 팀 4명과 역할)·저작권·저장소 주소 |
| `Settings.swift` | 옵션 저장 |
| `Characters.swift` | `Resources/characters.json`과 캐릭터 프레임 로드 |
| `tools/make_frames.py` | 캐릭터 이미지에서 메뉴 막대 애니메이션 프레임 생성 (통통 튀기·떠서 물방울 떨어뜨리기, 개발용 Python 스크립트) |
| `tools/make_icon.py` | 캐릭터 이미지로 앱 아이콘 `Resources/AppIcon.icns` 생성 (개발용 Python 스크립트) |
| `scripts/build-app.sh` / `scripts/build-dmg.sh` | `.app` 빌드 / 배포용 DMG 만들기 |

아이콘 애니메이션은 SwiftUI `MenuBarExtra` 대신 AppKit `NSStatusItem`으로 그린다. 초당 수십 번 이미지를 바꾸는 애니메이션과 수시로 바뀌는 클립보드 메뉴를 안정적으로 처리하기 위해서다.

## 캐릭터 추가

1. `tools/art/<id>/character.png`에 이미지를 넣어요. 지금 추출기는 "남색 원 안의 흰 캐릭터" 형태를 기준으로 해요.
2. `Resources/characters.json`에 `{"id": "<id>", "name": "표시 이름"}`을 추가해요. `"coming_soon": true`면 메뉴에 비활성으로 보여요. 필요하면 아래 값을 더해요.
   - `"height"`: 캐릭터 높이(@2x 픽셀, 기본 26). 귀처럼 위로 솟은 부분이 있으면 키워서 몸통 크기를 다른 캐릭터와 맞춰요(고양이 28, 토끼 30, 구름 24)
   - `"lift"`: 튀어 오르는 높이(기본 13). 키가 큰 캐릭터는 줄여서 위가 잘리지 않게 해요(토끼 11)
   - `"motion": "drip"`: 통통 튀는 대신 공중에 떠서 아래로 늘어진 부분 끝에서 물방울이 떨어져요(녹는 슬라임). 물방울 위치는 그림에서 아래로 늘어진 부분을 찾아 정해요
3. `python3 tools/make_frames.py`를 실행하면 `Resources/Characters/<id>/`에 48프레임과 복사 아이콘용 `badge.png`가 생기고, 미리보기 GIF가 `build/previews/`에 만들어져요. 프레임 너비는 캐릭터 몸통 너비에 맞춰 정해져요(기본 슬라임 16pt). (Pillow 필요: `pip install Pillow`)
