# UP macOS 메뉴 막대 앱

Swift (macOS 13 이상). 메뉴 막대에서 UP 슬라임이 통통 튀고, 아이콘을 누르면 메뉴가 열려요. (#23)

공통 메뉴와 기본값은 [PRD 4.2절](../../prd.md#42-메뉴-앱-windows-트레이--macos-메뉴-막대), 추가 메뉴(상태 변경·클립보드·캐릭터)는 PRD 4.2.2를 따른다. 화면에 보이는 이름은 UP이고, 폴더·모듈·번들 ID 같은 코드 이름은 README 규칙대로 당분간 `ClipBridge`를 유지한다.

## 실행

Xcode 또는 Command Line Tools(`xcode-select --install`)가 필요해요.

```bash
cd macos/ClipBridgeMenuBar
swift run
```

`.app`으로 만들기 (알림까지 동작, 더블클릭 실행):

```bash
./scripts/build-app.sh
open build/ClipBridge.app
```

Xcode에서 열려면 `open Package.swift`.

테스트 (Core 계약 JSON 형식, 복사 감지 동작 확인, Xcode 필요):

```bash
swift test
```

## 메뉴

```text
UP · 대기 중
──────────
Markdown 처리 ▸            ✓ 기호 제거 (기본) / 서식 적용
AI 검증 (외부 API 사용) ▸   ✓ 켜기 (기본) / 끄기 / 외부 API 전송 안내
──────────
상태 변경 ▸   ✓ 대기 / 분석 중 / 맞춤 완료 효과 보기
클립보드 ▸    최근 복사 10개 (누르면 다시 복사) / 기록 지우기 / 복사하면 슬라임 반응
캐릭터 ▸      ✓ 기본 슬라임 / 새 캐릭터 (준비 중) / 시즌 한정 캐릭터 (준비 중)
──────────
UP 종료  ⌘Q
```

- **옵션 저장:** `UserDefaults` (`~/Library/Preferences/app.clipbridge.menubar.plist`, `swift run`일 때는 `ClipBridgeMenuBar.plist`). 옵션 값만 저장하고 복사 본문은 저장하지 않아요. (PRD 4.2)
- **중복 실행 방지:** 이미 실행 중이면 두 번째 실행은 바로 종료돼요. 사용자 임시 폴더의 잠금 파일(`app.clipbridge.menubar.lock`)로 확인하고, 앱이 끝나면 잠금은 자동으로 풀려요.
- **Markdown 처리·AI 검증:** Core 계약과 같은 값(`Strip`/`Format`, AI 검증 기본 켬)으로 저장하고 `Settings.conversionOptions`로 꺼내요. 변환기 연결은 Helper 연동(#36)에서 진행.
- **복사 감지 (#33):** 0.3초마다 `changeCount`를 확인해요. 복사하면 모든 포맷을 `ClipboardSnapshot`으로 메모리에 1건만 보관하고, 그때 앞에 있던 앱의 번들 ID와 `changeCount`를 함께 기록해요. 앱이 직접 쓴 변경(`recordOwnWrite`)은 새 복사로 치지 않아요.
- **붙여넣기 감지 (#34):** 키보드 이벤트 탭으로 Cmd+V를 감지하고, 앞쪽 앱으로 대상(한글·Notion·Word·기타)을 판별해요. 키는 그대로 통과시키고 버리거나 다시 보내지 않아요. 시스템이 탭을 꺼도 바로 다시 켜요. 판별 결과는 시스템 로그로 확인해요.

  ```bash
  log stream --level info --predicate 'subsystem == "app.clipbridge.menubar"'
  ```
- **손쉬운 사용 권한:** 처음 실행할 때 한 번 요청해요. 허용하기 전에는 메뉴 맨 위에 `⚠︎ 손쉬운 사용 권한 필요 · 눌러서 설정 열기`가 보이고, 붙여넣기는 변환 없이 평소대로 돼요. 설정에서 허용하면 2초 안에 자동으로 켜지고 항목이 사라져요.
  - `./scripts/build-app.sh`로 다시 빌드하면 서명이 바뀌어서 권한이 풀릴 수 있어요. 그러면 시스템 설정 → 개인정보 보호 및 보안 → 손쉬운 사용에서 UP를 지우고(−) 다시 허용하세요.
  - `swift run`으로 실행하면 권한은 UP가 아니라 실행한 터미널 앱에 붙어요.
- **포인터 옆 동그라미 아이콘 (#38):** 복사하면 UP 캐릭터 배지(`Resources/Characters/<id>/badge.png`)가 0.8초, 붙여넣으면 대상 앱 아이콘이 1초 동안 포인터 오른쪽 아래(지름 30pt)에 보이고 0.15초에 걸쳐 사라져요. 포인터를 따라다니고, 화면 가장자리에서는 반대쪽으로 뒤집혀요.
  - Notion 웹은 브라우저 대신 Notion 아이콘, 변환 대상(한글·Notion·Word)은 강조색 테두리, 그 외 앱(통과)은 얇은 회색 테두리예요. 아이콘이 없으면 앱 이름 첫 글자, 앱을 모르면 `?`를 보여줘요.
  - 포커스를 가져가지 않고 클릭이 통과하는 패널이라 입력·IME를 방해하지 않아요. 모든 Space와 전체 화면 앱 위에도 떠요.
  - 대상 앱이 데이터를 가져가는 순간 바로 사라지게 하는 건 클립보드 기록(#35)에서 `CursorOverlay.dataTaken()`으로 연결해요.

### 대상 앱 매핑 (PRD 4.5)

| 대상 | 판별 기준 |
|---|---|
| Word | 번들 ID `com.microsoft.Word` (맥에서 확인) |
| Notion 앱 | 번들 ID `notion.id` (맥에서 확인) |
| 한글 (HOP) | 번들 ID `net.golbin.hop` (맥에서 확인). [HOP](https://github.com/golbin/hop)는 HWP 파일을 여는 오픈소스 편집기 |
| 한글 (한컴오피스) | 번들 ID가 `com.hancom.`으로 시작하고 `hwp`가 들어 있거나, 앱 이름에 `HWP`·`한글`이 있음 (버전마다 ID가 달라 규칙으로 판별, 맥에서 미확인) |
| 한글 (한컴독스 웹) | 브라우저 주소가 `webhwp.*hancomdocs.com`(웹 한글 편집기)이거나, 창 제목에 `한컴독스`·`Hancom Docs`가 있음. 아이콘은 `한` 글자 배지 |
| Notion 웹 | 브라우저 주소가 `notion.so`·`*.notion.site`이거나, 창 제목에 `Notion`이 있음 |
| (브라우저 공통) | Safari·Chrome·Edge·Firefox·Whale·Arc. 주소는 브라우저가 손쉬운 사용으로 알려줄 때만(Safari 등) 쓰고, 아니면 창 제목으로 판단. 둘 다 안 되면 통과 |
| 그 외 | 통과 |

설치된 앱의 번들 ID 확인: `osascript -e 'id of app "Microsoft Word"'` (앱 이름만 바꿔서 실행)
- **클립보드:** 텍스트 복사 최근 10개를 메모리에만 보관해요. 비밀번호 관리자가 숨김 표시를 한 복사는 기록하지 않고, 이전 스냅샷도 버려서 그 뒤 붙여넣기에 예전 변환 결과가 붙지 않게 해요.
- **CPU 연동:** 코드에는 있지만 메뉴에서 숨겨 두었어요 (`StatusController.swift`의 `cpu.isHidden`).

## 구조

| 파일 | 역할 |
|---|---|
| `Sources/ClipBridgeContracts/Contracts.swift` | `src/ClipBridge.Core/Contracts`(#29)의 Swift 버전. Helper와 주고받는 타입과 JSON 형식(camelCase, enum 문자열, 바이트 Base64). C# 계약이 바뀌면 같이 바꾼다 |
| `Tests/ClipBridgeContractsTests/` | C# `ContractsJsonTests`와 같은 JSON으로 검증 |
| `Sources/ClipBridgeClipboard/ClipboardMonitor.swift` | 복사 감지, 원본 스냅샷, 자기 기록 무시, 최근 복사 기록 (#33) |
| `Sources/ClipBridgeClipboard/PasteboardSource.swift` | `NSPasteboard` 접근 인터페이스. 테스트에서는 가짜 클립보드로 바꿔 끼움 |
| `Sources/ClipBridgeClipboard/PasteTarget.swift` | 대상 앱 판별(`TargetResolver`)과 Cmd+V 판정(`PasteKey`) (#34) |
| `Sources/ClipBridgeMenuBar/PasteWatcher.swift` | 이벤트 탭, 손쉬운 사용 권한 요청·재확인, 브라우저 창 제목 읽기 (#34) |
| `Sources/ClipBridgeMenuBar/CursorOverlay.swift` | 포인터 옆 동그라미 아이콘 패널 (#38) |
| `Sources/ClipBridgeClipboard/OverlayLayout.swift` | 아이콘 위치 계산(가장자리 뒤집기)과 보여줄 배지 결정 (#38) |
| `Tests/ClipBridgeClipboardTests/` | 가짜 클립보드로 복사 감지 동작 검증 (연속 복사, 자기 기록, 숨김 복사 등), 대상 판별·Cmd+V 판정 |
| `App.swift` | 진입점. Dock 아이콘 없이 메뉴 막대 전용으로 실행, 중복 실행 방지 |
| `StatusController.swift` | 메뉴 막대 아이템, 메뉴 구성, 애니메이션 타이머 |
| `SlimeAnimator.swift` | 시간 기준 프레임 재생, CPU 사용률 측정 |
| `Settings.swift` | 옵션 저장 |
| `Characters.swift` | `Resources/characters.json`과 캐릭터 프레임 로드 |
| `tools/make_frames.py` | 캐릭터 이미지에서 통통 튀는 프레임 생성 (개발용 Python 스크립트) |

아이콘 애니메이션은 SwiftUI `MenuBarExtra` 대신 AppKit `NSStatusItem`으로 그린다. 초당 수십 번 이미지를 바꾸는 애니메이션과 수시로 바뀌는 클립보드 메뉴를 안정적으로 처리하기 위해서다.

## 캐릭터 추가

1. `tools/art/<id>/character.png`에 이미지를 넣어요. 지금 추출기는 "남색 원 안의 흰 캐릭터" 형태를 기준으로 해요.
2. `Resources/characters.json`에 `{"id": "<id>", "name": "표시 이름"}`을 추가해요. `"coming_soon": true`면 메뉴에 비활성으로 보여요.
3. `python3 tools/make_frames.py`를 실행하면 `Resources/Characters/<id>/`에 48프레임과 복사 아이콘용 `badge.png`가 생기고, 미리보기 GIF가 `build/previews/`에 만들어져요. (Pillow 필요: `pip install Pillow`)
