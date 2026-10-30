-- macOS Excel 샘플 통합 문서를 만든다 (#32). 시트 이름 = 샘플 폴더 이름(mac-<시트 이름>)
-- 사용법: tests/fixtures/tools/collect-excel-samples.sh 가 실행한다

on run
	tell application "Microsoft Excel"
		activate
		set wb to make new workbook

		-- 다열 표: 굵게·가운데 머리글, 셀 안 줄바꿈, 쉼표 스타일, 수식
		set ws to active sheet of wb
		set name of ws to "basic-table"
		set value of range "A1:C1" of ws to {{"이름", "부서", "금액"}}
		set bold of font object of range "A1:C1" of ws to true
		set horizontal alignment of range "A1:C1" of ws to horizontal align center
		set value of range "A2:B2" of ws to {{"홍길동", "영업"}}
		set value of range "C2" of ws to 1234.5
		set value of range "A3" of ws to "김철수"
		set value of range "B3" of ws to "개발" & (ASCII character 10) & "2팀"
		set wrap text of range "B3" of ws to true
		set formula of range "C3" of ws to "=C2*2"
		set number format of range "C2:C3" of ws to "#,##0.00"

		-- 병합 셀 표: 가로 병합(A1:B1), 세로 병합(C1:C2)
		set ws to make new worksheet at end of wb
		set name of ws to "merged-table"
		set value of range "A1" of ws to "상반기 실적"
		set value of range "C1" of ws to "비고"
		set value of range "A2:B2" of ws to {{"1분기", "2분기"}}
		set value of range "A3:C3" of ws to {{100, 200, "증가"}}
		set value of range "A4:C4" of ws to {{150, 120, "감소"}}
		merge range "A1:B1" of ws
		merge range "C1:C2" of ws
		set bold of font object of range "A1:C2" of ws to true
		set horizontal alignment of range "A1:C2" of ws to horizontal align center

		-- 단일 열(글): 빈 셀, 셀 안 줄바꿈, 따옴표
		set ws to make new worksheet at end of wb
		set name of ws to "single-column"
		set value of range "A1" of ws to "첫째 메모"
		set value of range "A3" of ws to "셋째 줄" & (ASCII character 10) & "이어지는 줄"
		set wrap text of range "A3" of ws to true
		set value of range "A4" of ws to "5\" 나사 \"표준\""

		-- 수식 표: 곱셈·합계·백분율·날짜
		set ws to make new worksheet at end of wb
		set name of ws to "formula-table"
		set value of range "A1:E1" of ws to {{"품목", "수량", "단가", "금액", "입고일"}}
		set value of range "A2:C2" of ws to {{"사과", 3, 1500}}
		set value of range "A3:C3" of ws to {{"배", 2, 3000}}
		set formula of range "D2" of ws to "=B2*C2"
		set formula of range "D3" of ws to "=B3*C3"
		set formula of range "E2" of ws to "=DATE(2026,10,1)"
		set formula of range "E3" of ws to "=DATE(2026,10,15)"
		set number format of range "E2:E3" of ws to "yyyy-mm-dd"
		set value of range "A4" of ws to "합계"
		set formula of range "B4" of ws to "=SUM(B2:B3)"
		set formula of range "D4" of ws to "=SUM(D2:D3)"
		set value of range "A5" of ws to "배 비율"
		set formula of range "D5" of ws to "=D3/D4"
		set number format of range "D5" of ws to "0.0%"
		set number format of range "C2:D4" of ws to "#,##0"
		set bold of font object of range "A1:E1" of ws to true
		set bold of font object of range "A4:E4" of ws to true
	end tell
end run
