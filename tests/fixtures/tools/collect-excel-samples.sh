#!/bin/bash
# macOS Excel 샘플을 만들어 시트마다 복사하고 tests/fixtures/excel/mac-<시트 이름>/ 에 저장한다 (#32)
# 사용법: 저장소 루트에서 bash tests/fixtures/tools/collect-excel-samples.sh
# 처음 실행 시 "터미널이 Microsoft Excel을 제어" 권한을 허용해야 한다
set -euo pipefail

tools="$(cd "$(dirname "$0")" && pwd)"
fixtures="$(dirname "$tools")"

osascript "$tools/make-excel-samples.applescript"

# 시트 이름 → 복사할 범위, 설명
samples=(
  "basic-table|A1:C3|다열 표: 굵게·가운데 정렬 머리글(이름·부서·금액), 셀 안 줄바꿈(개발/2팀), 쉼표 스타일 숫자 1,234.50, 수식 =C2*2"
  "merged-table|A1:C4|병합 셀 표: 가로 병합 A1:B1(상반기 실적), 세로 병합 C1:C2(비고), 굵게·가운데 머리글"
  "single-column|A1:A4|단일 열(글): 빈 셀 A2, 셀 안 줄바꿈 A3, 따옴표가 든 셀 A4"
  "formula-table|A1:E5|수식 표: 곱셈 =B2*C2, 합계 =SUM, 백분율 0.0%, 날짜 yyyy-mm-dd, 굵은 머리글·합계 행"
)

for sample in "${samples[@]}"; do
  IFS='|' read -r sheet range description <<< "$sample"
  osascript -e "tell application \"Microsoft Excel\"
    activate object worksheet \"$sheet\" of active workbook
    select range \"$range\" of active sheet
    copy range range \"$range\" of active sheet
  end tell" > /dev/null
  sleep 1
  rm -rf "$fixtures/excel/mac-$sheet"
  swift "$tools/dump-mac-clipboard.swift" "$fixtures/excel/mac-$sheet" com.microsoft.Excel "$description" | tail -1
done
