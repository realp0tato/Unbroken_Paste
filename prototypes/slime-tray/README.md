# ClipBridge 슬라임 상태 아이콘 (프로토타입)

ClipBridge 캐릭터가 통통 튀는 상태 아이콘이에요. 아이콘을 누르면 상태 변경, 클립보드 기록, 캐릭터 변경 메뉴가 열려요.

- macOS: 화면 오른쪽 위 메뉴 막대 (아이콘 클릭)
- Windows: 화면 오른쪽 아래 알림 영역 (아이콘 오른쪽 클릭)

ClipBridge 본체와는 아직 연결하지 않았어요. 상태 변경은 애니메이션 확인용 시뮬레이션이에요.

## 실행 (macOS)

```bash
cd prototypes/slime-tray
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
.venv/bin/python slime_tray.py
```

## 실행 (Windows)

```powershell
cd prototypes\slime-tray
py -3.12 -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\python slime_tray.py
```

아이콘이 안 보이면 작업 표시줄의 `^`(숨겨진 아이콘)를 열어 슬라임을 작업 표시줄로 끌어오세요.

## 메뉴

```text
ClipBridge · 대기 중
──────────
상태 변경 ▸   ● 대기 / 분석 중 / CPU 연동 / 맞춤 완료 효과 보기
클립보드 ▸    최근 복사 10개 (누르면 다시 복사) / 기록 지우기 / 복사하면 슬라임 반응
캐릭터 ▸      ● 기본 슬라임 / 새 캐릭터 (준비 중) / 시즌 한정 캐릭터 (준비 중)
──────────
ClipBridge 종료
```

- **클립보드:** 텍스트 복사를 감지해 최근 10개를 보여줘요. 기록은 메모리에만 있고 파일로 저장하지 않아요. 비밀번호 관리자가 숨김 표시를 한 복사는 기록하지 않아요. 복사하면 슬라임이 잠깐 빠르게 뛰어요.
- **캐릭터·설정 저장:** 고른 캐릭터와 "복사하면 슬라임 반응" 설정은 `~/.clipbridge-slime.json`에 저장돼요.

## 캐릭터 추가하기

1. `assets/characters/<캐릭터id>/character.png`에 이미지를 넣어요. 지금 추출기는 "남색 원 안의 흰 캐릭터" 형태를 기준으로 해요.
2. `assets/characters.json`에 `{"id": "<캐릭터id>", "name": "표시 이름"}`을 추가해요. `"coming_soon": true`를 넣으면 메뉴에 "준비 중"으로 비활성 표시돼요.
3. `python make_frames.py`를 실행하면 `frames/<캐릭터id>/`에 프레임이 만들어져요.

## 프레임

- 48프레임, 시간 기준 재생. 대기 1.5초, 분석 중 0.6초에 한 번 뛰어요.
- `frames/<id>/mac/`: 36×22pt(@2x) 검정+투명 템플릿 이미지. 다크/라이트 모드에서 색이 자동으로 바뀌어요.
- `frames/<id>/win/`: 32×32px 컬러 아이콘. 밝은/어두운 작업 표시줄 모두에서 보이도록 남색 테두리를 넣었어요.
- `frames/<id>/preview.gif`: 맥 라이트·다크 메뉴 막대와 윈도우 트레이 미리보기.
