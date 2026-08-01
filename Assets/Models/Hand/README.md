# 손 모델 (v17)

- 출처: BlendSwap — "Low Poly Hand (Rigged)" by erik90mx
  https://blendswap.com/blend/2429
- 라이선스: **CC0 (Public Domain)** — 상업 이용 가능, 출처표기 의무 없음
  (원문은 LICENSE_CC0_erik90mx.txt 보관)

## 변환 내역
원본 .blend에는 손 메시 외에 **리그 조작용 위젯 30여 개**(Controlador*, Hueso Muneca*, dedo1,1 …)가
폴리곤 0짜리 오브젝트로 들어 있다. 이걸 그대로 내보내면 Unity에 빈 오브젝트가 잔뜩 생긴다.
→ `Mano`(손 메시) + `Huesos Mano`(뼈대) 둘만 남기고 FBX로 내보냈다.

- 메시: 560 vert / 580 poly (로우폴리)
- 뼈대: 30개. 손가락은 `dedo1/2/3` 계열(스페인어 dedo = 손가락)
- 축: -Z forward / Y up (Unity 규약), add_leaf_bones=False

재변환이 필요하면 Blender CLI로:
```
Blender -b "<원본>.blend" -P export_hand.py
```
