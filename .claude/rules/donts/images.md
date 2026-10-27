# Don'ts — 이미지 / 에셋 생성

> 이미지 생성 시 반복된 실수. 루트 [`donts.md`](../donts.md)의 제너럴 규칙과 함께 적용.
> 2026-10-23: 자체 이미지 생성 도구(pixel-artist·illustrator·hatchet-art·img-gen·gemini-image 등)를 전부 폐기했다. 모델 이름을 코드에 고정하면 모델 종료 때마다 깨지기 때문이다(gpt-image-1 종료가 계기). 이제 아래 "생성 방법"대로 `codex exec`를 직접 쓴다.

## 🎨 생성 방법 (엄수)

1. **생성은 Codex 내장 `image_gen`으로만** — ChatGPT 구독이라 API 키·과금이 없고, 모델은 OpenAI가 고른다. 우리가 모델 이름을 지정하거나 이미지 API를 직접 호출하는 스크립트를 새로 만들지 않는다. Gemini 이미지는 사용자가 명시할 때만.
2. **역할:** Codex = 이미지 생성, Claude = 리서치/설계/구현/총괄 + 생성물 검수(직접 `Read`로 열어 본다). 기획 문서 검수는 Codex+Claude 이종 2모델.

```bash
# 1장 생성 (2026-10-23 실생성으로 확인: 약 1분, 1254×1254 PNG, 투명 배경 실제 알파)
codex exec --skip-git-repo-check -s workspace-write -C "$(mktemp -d)" [-i 참조.png] \
  "Call your built-in image_gen tool exactly once to generate one image, then stop. \
Do not use the CLI fallback, SVG, or ImageMagick. Use the prompt verbatim. \
Transparent background. Copy the result to <절대경로>.png and print the source path. PROMPT: <프롬프트>" \
  < /dev/null
```
- **`< /dev/null` 필수** — 없으면 stdin을 기다리며 멈춘다.
- **원본 위치:** `~/.codex/generated_images/<세션id>/exec-*.png`. 출력된 source path가 이 아래가 아니면 내장 도구를 안 쓴 것이니 실패로 본다.
- **크기는 지정 안 된다** — 비율만 프롬프트로 말하고, 크기는 생성 후 맞춘다: `magick 원본.png -filter Point -resize 32x32 PNG32:결과.png` (작은 픽셀아트는 nearest-neighbor 필수, #10).
- **여러 장은 1장씩 반복 호출** — 한 호출에 여러 장을 요구하지 않는다.

## 📋 생성 전 필수 절차

3. **이미지 생성 전 사전 승인 필수** — 바로 생성 금지. 제안서(유형/크기/프롬프트/참조/경로/호출 수) 제시 → 사용자 "ㅇㅇ" 받은 뒤에만 실행.
4. **예외:** 사용자가 "바로 만들어", "자동으로 해"라고 명시한 건만 승인 생략 가능.

## 🖼️ 참조 이미지 규칙

5. **캐릭터 설정화 있으면 참조 필수** — 텍스트만으로 생성하면 매번 얼굴이 달라짐. `codex exec -i 설정화.png`로 넘긴다.
6. **캐릭터 ref 1장만 + 도구는 텍스트로** — ref 2장(캐릭터+도구)을 동시에 넘기면 얼굴이 무너졌다(gpt-image-1 edits API, 2026-03-27 검증). 새 모델에서 재검증 전까지 유지.

## 🖌️ 스프라이트 품질

7. **스프라이트는 2×2 그리드 방식으로 생성** — 개별 생성 금지. 4방향/4프레임을 한 번에 뽑아야 일관성 유지(gpt-image-1 시절 검증, 재검증 전까지 유지).
8. **생성물 품질 체크 필수** — 생성 직후 Claude가 원본(축소 전)을 `Read`로 열어 4대 불량을 본다: (a) 픽셀 경계에 어긋난 **직선 아티팩트**(삐뚤삐뚤한 1~2px 계단), (b) **실루엣 뭉개짐**(가장자리 대비 부족, 배경과 섞임), (c) **프레임 간 불일치**(2×2 그리드에서 팔·머리 크기 달라짐), (d) **업스케일 번짐**(blur 리사이즈). 미통과 시 재생성 여부를 사용자에게 묻는다.

## 💾 저장 / 네이밍

9. **덮어쓰기 금지** — 같은 이름으로 재생성 금지. 버전 suffix(`_v2`, `_v3`) 또는 날짜 붙여서 히스토리 보존.

## 🔳 투명 배경 / 파일 포맷

10. **투명 배경은 `-alpha on` + `PNG32:` 필수** — ImageMagick에서 투명 처리 안 되면 이 옵션 빠진 것. 리사이즈는 반드시 nearest-neighbor(`-filter Point`).
11. **favicon은 `.ico` 대신 `.png`** — 투명도·품질 안정적.

## 📐 일러스트 파이프라인

12. **설정화(SOT)를 먼저 확정 → 포즈/변형은 그 설정화를 ref 1장으로** — 순서 바꾸지 말 것. SOT가 먼저 확정되어야 일관성 유지.

---
**출처 메모리:** `feedback_api_roles`, `feedback_image_naming`, `feedback_illustration_pipeline`, `feedback_image_alpha_channel`, `feedback_sprite_grid_method`, `feedback_sprite_quality`
**연관 규칙:** `CLAUDE.md` "시각 에셋 생성 규칙", "이미지 생성 사전 승인 프로세스"
