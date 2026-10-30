# ClipBridge macOS 메뉴 막대 앱

Swift (macOS 13 이상). 메뉴 막대에서 ClipBridge 슬라임이 통통 튀고, 아이콘을 누르면 메뉴가 열려요. (#23)

메뉴 항목과 기본값은 [PRD 4.2절](../../prd.md#42-메뉴-앱-windows-트레이--macos-메뉴-막대)을 따른다. 그 외 항목(상태 변경·클립보드·캐릭터)은 팀 합의 전 제안 기능이다.

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

## 메뉴

```text
ClipBridge · 대기 중
──────────
Markdown 처리 ▸            ✓ 기호 제거 (기본) / 서식 적용
AI 검증 (외부 API 사용) ▸   ✓ 켜기 (기본) / 끄기
──────────
상태 변경 ▸   ✓ 대기 / 분석 중 / 맞춤 완료 효과 보기
클립보드 ▸    최근 복사 10개 (누르면 다시 복사) / 기록 지우기 / 복사하면 슬라임 반응
캐릭터 ▸      ✓ 기본 슬라임 / 새 캐릭터 (준비 중) / 시즌 한정 캐릭터 (준비 중)
──────────
ClipBridge 종료  ⌘Q
```

- **옵션 저장:** `UserDefaults` (`~/Library/Preferences/app.clipbridge.menubar.plist`, `swift run`일 때는 `ClipBridgeMenuBar.plist`). 옵션 값만 저장하고 복사 본문은 저장하지 않아요. PRD 4.2의 macOS 저장 위치(미정) 제안.
- **Markdown 처리·AI 검증:** 지금은 값만 저장해요. 변환기 연결은 Core 작업 후 진행.
- **클립보드:** 텍스트 복사 최근 10개를 메모리에만 보관해요. 비밀번호 관리자가 숨김 표시를 한 복사는 기록하지 않아요.
- **CPU 연동:** 코드에는 있지만 메뉴에서 숨겨 두었어요 (`StatusController.swift`의 `cpu.isHidden`).

## 구조

| 파일 | 역할 |
|---|---|
| `App.swift` | 진입점. Dock 아이콘 없이 메뉴 막대 전용으로 실행 |
| `StatusController.swift` | 메뉴 막대 아이템, 메뉴 구성, 애니메이션 타이머 |
| `SlimeAnimator.swift` | 시간 기준 프레임 재생, CPU 사용률 측정 |
| `ClipboardHistory.swift` | 클립보드 감시와 최근 복사 기록 |
| `Settings.swift` | 옵션 저장 |
| `Characters.swift` | `Resources/characters.json`과 캐릭터 프레임 로드 |
| `tools/make_frames.py` | 캐릭터 이미지에서 통통 튀는 프레임 생성 (개발용 Python 스크립트) |

아이콘 애니메이션은 SwiftUI `MenuBarExtra` 대신 AppKit `NSStatusItem`으로 그린다. 초당 수십 번 이미지를 바꾸는 애니메이션과 수시로 바뀌는 클립보드 메뉴를 안정적으로 처리하기 위해서다.

## 캐릭터 추가

1. `tools/art/<id>/character.png`에 이미지를 넣어요. 지금 추출기는 "남색 원 안의 흰 캐릭터" 형태를 기준으로 해요.
2. `Resources/characters.json`에 `{"id": "<id>", "name": "표시 이름"}`을 추가해요. `"coming_soon": true`면 메뉴에 비활성으로 보여요.
3. `python3 tools/make_frames.py`를 실행하면 `Resources/Characters/<id>/`에 48프레임이 생기고, 미리보기 GIF가 `build/previews/`에 만들어져요. (Pillow 필요: `pip install Pillow`)
