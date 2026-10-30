# 클립보드 샘플 (fixtures)

Windows PC에서 복사 직후 클립보드에 들어 있는 **모든 포맷**을 파일로 저장해 둔다. 변환기 테스트는 이 샘플로 macOS에서 실행한다. (#28)

## 폴더

| 폴더 | 출처 |
|---|---|
| `excel/` | Excel |
| `web/` | 인터넷(브라우저) |
| `markdown/` | Markdown 글 (AI 답변 등 일반 텍스트) |
| `plain/` | 일반 텍스트 |

## 규칙

- 샘플 하나당 폴더 하나: `<출처>/<샘플 이름>/`
- 포맷마다 파일 하나, 파일 이름은 클립보드 포맷 이름 (예: `HTML Format.bin`, `CF_UNICODETEXT.txt`, `XML Spreadsheet.bin`)
- `meta.json`에 출처 앱·버전·복사한 내용 설명을 기록
- 개인정보가 들어간 내용은 올리지 않는다
