# ClipBridge macOS 앱

Swift · SwiftUI `MenuBarExtra` · AppKit (macOS 13 이상). Xcode 프로젝트는 이 폴더에 만든다.

- 메뉴 항목과 기본값: [PRD 4.2절](../../prd.md#42-메뉴-앱-windows-트레이--macos-메뉴-막대). 그 외 메뉴 기능·옵션은 추후 결정한다.
- macOS 동작(클립보드 감시, Cmd+V 감지, 지연 제공, 커서 아이콘, 손쉬운 사용 권한): [PRD 4.5절](../../prd.md#45-플랫폼별-동작-windows--macos)
- 변환은 `src/ClipBridge.Helper`(C#)를 앱 번들에 포함해 표준 입출력으로 호출한다.
