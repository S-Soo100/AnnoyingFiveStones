# AGENTS.md — AI 에이전트 공용 진입점 (Annoying Five Stones)

> Claude / Codex / 기타 AI 도구가 이 레포에 들어왔을 때 먼저 읽는 파일.
> **규칙의 단일 출처는 [`CLAUDE.md`](CLAUDE.md)다.** 이 파일은 진입 안내만 한다.
> (2026-11-21: CLAUDE.md의 5월판 사본 → 포인터로 전환. 사본은 없는 경로(`.Codex/rules/`)를 가리켰고, 기획 원본을 옛 v2로 적었으며, 폐기된 Gemini 코드 리뷰와 `/검수 code`를 담고 있었다.)

## 한 줄 요약

전통 공기놀이를 3D로 재현한 캐주얼 게임. Unity 6 (URP).
기획 원본(SOT)은 [`docs/기획서-v11-통합본.md`](docs/기획서-v11-통합본.md)다.

## 너가 어떤 AI인지에 따라

### Claude (Claude Code)
→ [`CLAUDE.md`](CLAUDE.md) 자동 로드됨. 이 파일은 보조.

### Codex / ChatGPT (codex CLI)
→ [`CLAUDE.md`](CLAUDE.md)를 읽고 그대로 따른다. 특히:
- 금지 규칙: [`.claude/rules/donts.md`](.claude/rules/donts.md), [`.claude/rules/donts/game.md`](.claude/rules/donts/game.md), [`.claude/rules/donts/images.md`](.claude/rules/donts/images.md)
- 기획 근거는 v11 통합본만 인용한다. ideaBank의 기획서 v1~v10은 이력이다.
- 코드 리뷰는 Claude Code의 내장 `/code-review`가 맡는다. Codex는 기획 문서와 설계 검수에 쓴다.
- Codex 전용 설정(에이전트 .toml, config)은 `.codex/`에 있다. **규칙 본문은 거기 없음** — `.claude/`가 유일한 규칙 위치.

### Cursor / 기타
→ [`CLAUDE.md`](CLAUDE.md) + 작업 영역의 규칙 파일.

## 변경 시 규칙

규칙을 고칠 일이 있으면 **CLAUDE.md(및 `.claude/rules/`)만 고친다.**
이 파일에 규칙을 복사하지 마라 — 복사하는 순간 드리프트가 시작된다.
