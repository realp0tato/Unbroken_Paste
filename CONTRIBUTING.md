# 작업 규칙

## 기본 원칙

- **`main`에 직접 push하지 않는다.** 모든 변경은 PR로 합친다.
- 작업은 이슈 단위로 한다. 이슈가 없으면 먼저 만든다.
- API 키 등 비밀 정보는 커밋하지 않는다 (`.env`, `*.local.json`은 `.gitignore`에 있음).

## 브랜치

`main`에서 이슈 번호로 시작하는 브랜치를 만든다.

```text
<이슈 번호>-<짧은 설명>
예) 29-core-interface, 7-c1-excel-hangul, 21-windows-tray
```

## PR

1. PR 전에 로컬에서 확인한다.
   ```bash
   dotnet build ClipBridge.slnx
   dotnet test
   ```
2. 제목: `[영역] 내용 (#이슈)` — 예) `[Converter] C1 Excel → 한글 표 변환 (#7)`
3. 본문에 `Closes #이슈번호`를 넣어 머지 시 이슈가 자동으로 닫히게 한다.
4. 아래 리뷰어 표에 따라 **1명 이상 승인**을 받는다. 본인 승인은 인정하지 않는다.
5. **Squash and merge**로 합치고, 머지 후 브랜치를 삭제한다.
6. PR은 작게 유지한다. 이슈 하나에 PR 하나가 기본이다.

## 리뷰어

| 변경 위치 | 리뷰어 |
|---|---|
| `src/ClipBridge.Core/`, `tests/` | 장세찬 ↔ 박재현 (서로 리뷰) |
| `src/ClipBridge.Core/Contracts/` (인터페이스, #29) | 이창민 **과** 장세찬 둘 다 승인 |
| `src/ClipBridge.Windows/` | 장세찬 또는 박재현 |
| `macos/` | 아무나 1명 |
| `prd.md`, `README.md`, 이 문서 | 아무나 1명 |

- 리뷰 요청을 받으면 **1시간 안에** 확인하는 것을 목표로 한다. 급하면 팀 메신저로 알린다.
- PRD 요구사항이 바뀌는 변경은 PR 본문에 바뀐 절 번호를 적는다.
