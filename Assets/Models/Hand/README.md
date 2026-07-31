# 손 모델 — Low Poly Hand (Rigged)

- 출처: BlendSwap https://blendswap.com/blend/2429 (원본 페이지 view/4598)
- 제작: **erik90mx**
- **라이선스: Creative Commons Zero (Public Domain, CC0)**
  → 상업 이용 가능, 출처표기 의무 없음. 원문은 다운로드 폴더의 `BLENDSWAP_LICENSE.txt` 참조.
- 원본 포맷: `.blend` (Blender 2.56, 2011년)

## 왜 이 모델인가

v17 결정(6-16 교체 시점 / 6-17 아트 톤)의 요구조건을 모두 만족한다:

| 요구조건 | 충족 |
|---|---|
| 손가락 개별 제어 | ✅ 손가락마다 본 리깅 — 줍기 시 손가락 접힘 연출 유지 가능 |
| 손등/손바닥 뒤집기 | ✅ 제대로 된 손 형태 — 5단 꺾기(손등 받기 → 손바닥 받기) |
| 정면 실루엣 | ✅ 로우폴리라 윤곽이 명확 → 판정 범위를 읽히게 하기 좋음 |
| 상업적 이용 | ✅ CC0 |
| 로우폴리 스타일 | ✅ 무채색 클레이 배경과 톤이 맞음 |

## 현재 상태

**아직 게임에 적용되지 않았다.** 지금 손은 `HandModelBuilder`가 런타임에
Cube 1개 + Cylinder 5개로 조립하는 임시 형태다.

## 변환 (Blender 설치 후 1회)

Unity는 Blender가 설치돼 있어야 `.blend`를 읽는다(내부적으로 Blender를 호출해 변환).

```bash
tools/convert-hand-fbx.sh
```

변환 후 `LowPolyHand.fbx`가 이 폴더에 생성된다.

## 적용 시 해야 할 일 (교체 작업 체크리스트)

`HandModelBuilder`가 프리미티브를 조립하는 구조라, 모델 교체는 단순 스왑이 아니다:

1. `Fingers` 배열(현재 Transform 5개) → 본 Transform으로 대체
2. `DoFingerFoldCustom(각도 배열)` → 본 회전으로 변환
3. `GetPalmPickupBounds()` → 새 메시 기준으로 재계산
4. `PalmCollider` / `FingerColliderL·R` / `FistCollider` 위치·크기 재조정
5. 정면 직교 카메라에 맞는 초기 회전 찾기 (2.5D — X=좌우, Y=상하, Z=깊이)
6. 5단 손등/손바닥 뒤집기 회전축 확인

⚠️ 4·5는 이 프로젝트에서 반복 실패한 영역이다(donts/game #21).
좌표를 추측하지 말고 실행 중 게임에서 직접 확인할 것.
