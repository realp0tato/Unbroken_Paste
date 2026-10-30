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
- `meta.json`에 OS·출처 앱·버전·복사한 내용 설명을 기록
- 개인정보가 들어간 내용은 올리지 않는다

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
