# 기본 붙여넣기 깨짐 기준선 (#55)

UP 없이 그냥 붙여넣었을 때 대상 앱에서 어떻게 보이는지 기록한다. 입력은 `tests/fixtures`의 샘플을 `restore-mac-clipboard.swift`로 클립보드에 되돌려 넣어 매번 같게 맞춘다.

- **판정:** 정상(손볼 것 없음) / 일부 깨짐(조금 손봐야 함) / 깨짐(다시 정리해야 함)
- **환경:** macOS 26.6.2 · Notion 7.41.1 · Word 16.114.3

## 요약 (Notion, 13개)

| 판정 | 샘플 |
|---|---|
| 정상 | Claude 답변 |
| 정상에 가까움 | Excel 수식 표 (굵게만 빠짐) |
| 일부 깨짐 | 웹 7개(표·기사·글+표 × Chrome·Safari, Chrome 블로그), Excel 3개(기본 표·병합 표·한 열) |
| **깨짐** | **Safari 블로그** — 제목·문단 경계·코드 블록이 모두 무너짐 (시연 후보) |

- **웹 → Notion:** 구조는 대체로 살지만 사이트 찌꺼기가 남는다 — 위키백과 링크, 각주(`[16]`, `[a]`), 표의 `빈칸` 글자, MDN 배지·목차·`jsCopy`. `h2`는 제목 블록이 되지만 `h3`는 본문 크기 굵은 글자가 된다.
- **Excel → Notion:** 값(수식 결과·쉼표·백분율·날짜)은 정확하다. 굵게·가운데 정렬이 빠지고, 칸 안 줄바꿈이 빈 줄·들여쓰기로 틀어진다. 병합은 첫 칸에 값을 두고 풀린다(PRD C2 규칙과 같음). 한 열 데이터는 글이 아니라 1열 표 모양이 된다.
- **AI 답변 → Notion:** 이미 잘 붙는다 → Markdown 기본값을 서식 적용으로 바꾼 근거 (PRD v1.12)
- **아직 안 한 것:** Word, 한글, 깨지기 쉬운 샘플 추가 (#55)

## 스크린샷 파일 규칙

```text
<대상 앱>/<샘플 경로>__<판정>__<번호>.png
예) notion/web-mac-safari-blog__broken__1.png
```

| 부분 | 값 |
|---|---|
| 대상 앱 폴더 | `notion/`, `word/`, `hangul/` |
| 샘플 경로 | `tests/fixtures/` 아래 경로의 `/`를 `-`로 (`web/mac-safari-blog` → `web-mac-safari-blog`) |
| 판정 | `ok` 정상 / `near-ok` 정상에 가까움 / `partial` 일부 깨짐 / `broken` 깨짐 |
| 번호 | 한 결과를 여러 장으로 나눠 찍었을 때 위에서부터 1, 2, 3 |

- 파일 이름은 영문만 쓴다 (GitHub 주소에서 깨지지 않게)
- 판정을 바꾸면 파일 이름도 함께 바꾼다
- 스크린샷은 UP 없이 붙인 직후 그대로 찍는다. 단, 붙인 뒤 손댄 것이 있으면 샘플별 기록에 적는다

## 결과

| 샘플 | Notion | Word | 한글 |
|---|---|---|---|
| `markdown/mac-claude-answer` | **정상** | | |
| `web/mac-safari-table` | **일부 깨짐** | | |
| `web/mac-chrome-table` | **일부 깨짐** | | |
| `web/mac-chrome-blog` | **일부 깨짐** (정리할 것 많음) | | |
| `web/mac-safari-blog` | **깨짐** | | |
| `web/mac-chrome-article` | **일부 깨짐** | | |
| `web/mac-safari-article` | **일부 깨짐** | | |
| `web/mac-chrome-mixed` | **일부 깨짐** | | |
| `web/mac-safari-mixed` | **일부 깨짐** | | |
| `excel/mac-basic-table` | **일부 깨짐** | | |
| `excel/mac-merged-table` | **일부 깨짐** | | |
| `excel/mac-formula-table` | **정상에 가까움** (굵게만 빠짐) | | |
| `excel/mac-single-column` | **일부 깨짐** | | |

## 샘플별 기록

### `markdown/mac-claude-answer` (Claude 앱 복사 버튼)

**Notion — 정상** ([1](notion/markdown-mac-claude-answer__ok__1.png), [2](notion/markdown-mac-claude-answer__ok__2.png))

- 제목(h1·h2), 굵게, 기울임, 중첩 글머리 목록, 중첩 번호 목록(1. → a.), 인라인 코드, 표, 코드 블록(Python 색 강조), 링크가 모두 실제 서식으로 붙음
- 원문의 연속 빈 줄 2–3줄은 사라지고 블록 간격이 일정함
- Notion이 Markdown 원문 대신 함께 들어간 HTML을 쓴 것으로 보임

**판단:** 이 조합은 기본 붙여넣기로도 깨지지 않는다. 당시 UP 기본값(기호 제거)을 적용하면 오히려 서식이 사라져 기준선보다 나빠졌다 → 기본값을 서식 적용으로 바꿈 (PRD v1.12, #57).

### `web/mac-safari-table` (위키백과 표, Safari)

**Notion — 일부 깨짐** ([스크린샷](notion/web-mac-safari-table__partial__1.png))

- 좋은 점: Notion 표 블록으로 들어가고 8열 18행 구조와 숫자가 그대로임. 머리글 행과 `계` 행도 굵게 유지
- 깨진 점
  - **빈 칸에 `빈칸` 글자가 들어감** (서울특별시 시·군, 세종특별자치시 시·군·구, `계` 행 소재지 등 20여 칸). 위키백과가 빈 칸을 `빈칸` 틀로 표시한 글자가 그대로 붙은 것 → 표를 다시 정리해야 함
  - **각주 표시가 남음**: `서구[a]`, `수원시[g]`, 비고 칸 `[f]` 등
  - **위키백과 링크가 따라옴**: 행정 구역 이름, 소재지, 머리글 `시`·`군`·`구`에 밑줄 링크

**판단:** 표 구조는 Notion이 잘 받지만 내용이 오염된다. 웹 정리(#9)·C4(#17)에서 숨김 글자 제거, 각주 표시 제거, 링크 정리가 필요하다.

### `web/mac-chrome-table` (위키백과 표, Chrome)

**Notion — 일부 깨짐** ([스크린샷](notion/web-mac-chrome-table__partial__1.png))

- Safari 표와 결과가 같음: 표 구조·숫자는 정상, `빈칸` 글자·각주 표시·위키백과 링크가 남음
- Safari 샘플의 칸 수 차이(135 vs 136)는 Notion 결과에 드러나지 않음 (마지막 `비고` 칸이 둘 다 빈 칸)

**판단:** 브라우저와 관계없이 같은 문제 → 웹 정리 규칙 하나로 두 브라우저를 함께 처리할 수 있다.

### `web/mac-chrome-blog` (MDN Clipboard API, Chrome)

**Notion — 일부 깨짐, 정리할 것 많음** ([1](notion/web-mac-chrome-blog__partial__1.png), [2](notion/web-mac-chrome-blog__partial__2.png))

- 좋은 점: 제목(h1·h2), 굵게, 인라인 코드, **코드 블록(색 강조 포함)**이 제대로 들어감
- 깨진 점
  - **사이트 장식이 블록으로 남음**: `Baseline`·`Widely available` 배지가 붙어서 `BaselineWidely available*` 토글 블록이 됨
  - **목차("In this article")가 두 번 들어감**: 한 번은 `· 클립보드 접근` 같은 글자 줄로, 한 번은 글머리 목록 안에 `·`가 또 붙은 `● · 클립보드 접근`으로
  - **코드 블록 위 버튼 글자**: 언어 이름과 복사 버튼이 붙어 `jsCopy` 문단이 생김
  - **소제목이 링크로 붙음**: `클립보드 접근` 제목 전체에 밑줄 링크(제목 옆 고정 링크가 감싼 것)
  - 일부 인라인 코드(`Navigator.clipboard`, `read()` 등)가 링크로 붙음
- 참고: 샘플 복사 범위가 한 글자 늦게 시작해 제목이 `lipboard API`로 붙었고, 스크린샷 전에 사용자가 `C`를 직접 붙임 (변환과 무관)

**판단:** 본문 서식은 잘 받지만 사이트 UI 조각(배지·목차·복사 버튼·제목 고정 링크)이 블록으로 남아 지워야 한다 → 웹 정리(#9)에서 UI 조각 제거 규칙 필요.

### `web/mac-safari-blog` (MDN Clipboard API, Safari)

**Notion — 깨짐** ([1](notion/web-mac-safari-blog__broken__1.png), [2](notion/web-mac-safari-blog__broken__2.png))

같은 페이지·같은 범위인데 **Chrome보다 훨씬 나쁨.**

- **제목이 사라짐**: `Clipboard API` 대제목과 `클립보드 접근` 소제목이 제목 블록이 아니라 굵은 글자·밑줄 링크 한 줄로 붙음
- **문단·블록 경계가 사라짐**: 전체가 한 블록 안의 줄바꿈으로 이어지고, 문장 끝에 다음 내용이 붙음 (`Clipboard APIBaselineWIDELY AVAILABLE*`, `디자인되었습니다.In this article`, `사용합니다.JSCopy`)
- **코드 블록이 깨짐**: Notion 코드 블록이 아니라 줄마다 인라인 코드 조각으로 들어감
- 목차는 본문 중간에 `· 클립보드 접근` 글자 줄로, 맨 아래에 `● · 클립보드 접근` 목록으로 또 들어감
- 좋은 점: 인라인 코드·굵게는 유지

**판단:** 브라우저에 따라 결과가 크게 다르다. 원인은 확인 필요: Safari 기사 샘플도 같은 형식 순서(웹 아카이브·RTF가 HTML보다 앞)인데 정상에 가깝게 붙었으므로, 형식 순서보다는 **MDN 페이지 구조를 Safari가 HTML로 옮긴 방식**의 문제일 가능성이 높다. UP이 정리한 HTML만 넣어 주면 Chrome 수준 이상으로 맞출 수 있다 → **시연 후보**.

### `web/mac-chrome-article` (위키백과 클립보드, Chrome)

**Notion — 일부 깨짐** ([1](notion/web-mac-chrome-article__partial__1.png), [2](notion/web-mac-chrome-article__partial__2.png))

- 좋은 점: 문단이 각각 문단 블록으로 들어가고 글자가 깨지지 않음
- 깨진 점
  - **소제목이 제목 블록이 아님**: `아미가OS`·`안드로이드`·`iOS`·`MacOS`가 본문 크기의 굵은 글자로만 붙음 (원본은 HTML 제목 태그)
  - **각주 표시가 남음**: `[16]`~`[21]`이 링크로 문장 끝에 붙음
  - **위키백과 링크가 따라옴**: `아미가`, `모바일 앱`, `UTI`, `macOS` 등 밑줄 링크

**판단:** 읽을 수는 있지만 각주·링크를 지우고 소제목을 다시 제목으로 바꿔야 한다 → 웹 정리(#9)에서 각주 제거, C4(#17)에서 제목 수준 맞추기.

### `web/mac-safari-article` (위키백과 클립보드, Safari)

**Notion — 일부 깨짐** ([1](notion/web-mac-safari-article__partial__1.png), [2](notion/web-mac-safari-article__partial__2.png))

- Chrome 기사와 결과가 같음: 문단은 정상, 소제목(`h3`)이 본문 크기 굵은 글자로만 붙고, 각주 `[16]`~`[21]`·위키백과 링크가 남음
- Safari 블로그와 달리 문단 경계가 무너지지 않음

### `web/mac-chrome-mixed` (위키백과 글+표, Chrome)

**Notion — 일부 깨짐** ([1](notion/web-mac-chrome-mixed__partial__1.png), [2](notion/web-mac-chrome-mixed__partial__2.png), [3](notion/web-mac-chrome-mixed__partial__3.png))

- 좋은 점: 문단, 글머리 목록, 표 블록이 각각 제대로 들어감. 소제목 `행정 구역별 인구 및 면적`(`h2`)은 **Notion 제목 블록**으로 들어감
- 깨진 점
  - **위키백과 링크가 매우 많음**: 첫 문단에만 `대한민국`, `특별시`, `광역시` 등 10개 넘는 밑줄 링크
  - 각주 `[1]`~`[3]`이 남음
  - 표는 표 샘플과 같은 문제(`빈칸` 글자, 각주, 링크)

**판단:** 구조는 잘 받는다. Notion은 `h2` 제목은 제목 블록으로 받지만 `h3`(기사 샘플)는 받지 않는 것으로 보인다 → C4(#17)에서 제목 수준을 Notion이 받는 범위로 맞춘다.

### `web/mac-safari-mixed` (위키백과 글+표, Safari)

**Notion — 일부 깨짐** ([1](notion/web-mac-safari-mixed__partial__1.png), [2](notion/web-mac-safari-mixed__partial__2.png))

- Chrome 글+표와 결과가 같음: 문단·목록·표·`h2` 제목은 정상, 위키백과 링크·각주·표 안 `빈칸` 글자가 남음

### `excel/mac-basic-table` (Excel 기본 표)

**Notion — 일부 깨짐** ([스크린샷](notion/excel-mac-basic-table__partial__1.png)) · Excel 꺼진 상태에서 붙임

- 좋은 점: 3열 3행 표 블록, 쉼표 숫자 `1,234.50`, 수식 결과 `2,469.00`이 그대로
- 깨진 점
  - **머리글의 굵게·가운데 정렬이 사라짐** (`이름`·`부서`·`금액`이 일반 칸)
  - **칸 안 줄바꿈이 어긋남**: `개발` / `2팀` 사이에 빈 줄이 생기고 `2팀`이 들여쓰기됨

**판단:** 내용은 맞지만 Excel 서식 일부가 빠지고 칸 안 줄바꿈 모양이 틀어진다 → C2(#8)에서 머리글 표시와 칸 안 줄바꿈 처리.

### `excel/mac-merged-table` (Excel 병합 셀 표)

**Notion — 일부 깨짐** ([스크린샷](notion/excel-mac-merged-table__partial__1.png)) · Excel 꺼진 상태에서 붙임

- 병합이 풀리고 첫 칸에만 값이 들어감: `상반기 실적`(가로 병합) 오른쪽 칸 비어 있음, `비고`(세로 병합) 아래 칸 비어 있음. Notion 표는 병합을 지원하지 않으므로 PRD 6.3.2(C2) 규칙과 같은 결과
- 깨진 점
  - **`상반기 실적`이 칸 안에서 `상반기` / ` 실적`으로 줄이 나뉘고 들여쓰기됨** (기본 표 샘플의 칸 안 줄바꿈과 같은 모양)
  - 머리글 굵게·가운데 정렬이 사라짐

**판단:** 병합 처리는 Notion 기본 동작이 이미 PRD 규칙과 같다. C2(#8)는 칸 안 줄바꿈·들여쓰기와 머리글 표시만 고치면 된다.

### `excel/mac-formula-table` (Excel 수식 표)

**Notion — 정상에 가까움** ([스크린샷](notion/excel-mac-formula-table__near-ok__1.png)) · Excel 꺼진 상태에서 붙임

- 수식 대신 **결과값**이 들어감(`4,500`, `10,500`), 쉼표 숫자·백분율 `57.1%`·날짜 `2026-10-01`이 화면 표시 그대로
- 빠진 점: 머리글·합계 행의 **굵게**가 사라짐

**판단:** 값은 완벽하다. Excel → Notion에서 공통으로 빠지는 것은 굵게·정렬 같은 서식뿐이다.

### `excel/mac-single-column` (Excel 한 열, 글로 다뤄야 하는 샘플)

**Notion — 일부 깨짐** ([스크린샷](notion/excel-mac-single-column__partial__1.png)) · Excel 꺼진 상태에서 붙임

- 글 줄이 아니라 **가로줄로 칸이 나뉜 1열 표 모양**으로 들어감 (PRD 6.1 기준으로는 '글'로 다뤄야 함)
- 빈 칸(A2)이 **빈 칸 그대로** 남아 사이에 빈 줄이 생김
- 칸 안 줄바꿈(`셋째 줄` / `이어지는 줄`)은 두 줄로 유지, 따옴표(`5" 나사 "표준"`)는 그대로

**판단:** 내용은 맞지만 메모처럼 읽히지 않고 표 모양이 된다 → C2(#8)의 '글' 규칙(셀마다 한 줄, 빈 칸은 줄을 만들지 않음)이 필요하다.

## 확인할 것

- ~~AI 답변 → Notion에서 기본값 '기호 제거'가 기준선보다 나쁨~~ → **해결:** Markdown 기본값을 서식 적용으로 변경, Word·한글은 글자 크기·굵기 (PRD v1.12, #57)
