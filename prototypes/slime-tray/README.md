# ClipBridge 슬라임 상태 아이콘 (프로토타입)

RunCat처럼 ClipBridge 캐릭터가 통통 튀는 상태 아이콘이에요.

- macOS: 화면 오른쪽 위 메뉴 막대
- Windows: 화면 오른쪽 아래 알림 영역(트레이)

ClipBridge 본체와는 아직 연결하지 않았어요. 메뉴에서 상태를 골라 애니메이션을 확인하는 데모예요.

## 실행 (macOS)

```bash
cd prototypes/slime-tray
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
.venv/bin/python slime_tray.py
```

메뉴 막대 오른쪽에 슬라임이 나타나요. 종료는 아이콘 클릭 → `ClipBridge 종료`, 또는 터미널에서 Ctrl+C.

## 실행 (Windows)

```powershell
cd prototypes\slime-tray
py -3.12 -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\python slime_tray.py
```

아이콘이 안 보이면 작업 표시줄의 `^`(숨겨진 아이콘)를 열어 슬라임을 작업 표시줄로 끌어오세요. 메뉴는 오른쪽 클릭으로 열려요.

## 메뉴

| 항목 | 동작 |
|---|---|
| 대기 (천천히) | 느리게 통통 |
| 분석 중 (빠르게) | 빠르게 통통 (복사 후 AI 분석 중) |
| 맞춤 완료 (신나게) | 두 번 빠르게 뛰고 알림 후 대기로 |
| CPU 연동 (RunCat 모드) | CPU 사용률이 높을수록 빠르게 |

## 프레임 다시 만들기

`assets/character.png`를 바꾼 뒤 아래를 실행하면 `frames/`가 새로 만들어져요.

```bash
.venv/bin/python make_frames.py
```

- `frames/mac/`: 36×22pt(@2x) 검정+투명 템플릿 이미지. 다크/라이트 모드에서 색이 자동으로 바뀌어요.
- `frames/win/`: 32×32px 컬러 아이콘. 밝은/어두운 작업 표시줄 모두에서 보이도록 남색 테두리를 넣었어요.
- `frames/preview.gif`: 맥 라이트·다크 메뉴 막대와 윈도우 트레이 미리보기.
