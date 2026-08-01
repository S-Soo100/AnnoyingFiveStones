# 손 모델 (v17)

- 출처: BlendSwap — "Low Poly Hand (Rigged)" by erik90mx / https://blendswap.com/blend/2429
- 라이선스: **CC0 (Public Domain)** — 상업 이용 가능, 출처표기 의무 없음
  (원문: LICENSE_CC0_erik90mx.txt)

## 변환 (export_hand.py — Blender CLI로 재실행 가능)

```
/Applications/Blender.app/Contents/MacOS/Blender -b "<원본>.blend" -P export_hand.py
```

1. **리그 조작 위젯 제거** — 원본엔 손 외에 폴리곤 0짜리 컨트롤러가 30여 개 있다
   (Controlador*, Hueso Muneca*, "dedo1,1" …). 그대로 내보내면 빈 오브젝트가 잔뜩 생긴다.
   → `Mano`(메시) + `Huesos Mano`(뼈대)만 남긴다.
2. **팔뚝 절단** — `CUT_X = 3.10` 미만 제거 후 절단면을 holes_fill로 막는다.
   그냥 지우면 구멍이 뚫린 채 보인다. 494 verts 남음.
3. **원점 정렬** — 원본은 손이 X≈4.2에 있어 오브젝트 원점에서 멀다.
   그대로 두면 Unity에서 위치를 잡아도 메시가 옆으로 밀려 화면 밖에 그려진다.
   ⚠️ 메시가 아마추어의 **자식**이라 부모(아마추어)만 옮겨야 한다. 둘 다 옮기면 이동이 두 번 먹는다.

## 삽질 기록 — 다시 하지 말 것

축 방향을 세 번 틀렸다. 실루엣만 보면 팔뚝과 손가락이 둘 다 "여러 갈래"로 보여 구분이 안 되고,
정점 밀도(손가락 377 vs 팔뚝 182)도 결정적이지 않았다.
**결국 잘라서 렌더해 보는 게 유일하게 확실한 방법이었다.**
- CUT 4.50 → 손목 절반만 제거
- CUT 4.10 → **손가락이 잘림**
- CUT 3.86 → **손바닥까지 잘려 손가락만 남음**
- CUT 3.10 → ✅ 손바닥+손가락+엄지 온전, 팔뚝만 제거

## Unity 배치
- 정면 회전 **Euler(0, 90, -90)** — 손바닥이 카메라, 손가락이 위.
  (계산으로 후보를 좁힌 뒤 렌더로 확정. 수식만으로는 결론이 안 났다)
- 프리뷰: `Tools > New Hand > Preview (정면)` / `Remove`
