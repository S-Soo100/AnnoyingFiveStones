---
name: game-feel
description: >
  타격감(juice) 추가·조정 — 화면 흔들림, 히트스톱, 이징 팝, 스쿼시&스트레치, 플래시. 트리거 — "밋밋해",
  "손맛이 없어", "타격감", "쫀득하게", "쫄깃하게", "임팩트", 흔들림·히트스톱·팝 수치 조정, 실패/클리어 연출
  강화. Adds engine-neutral juice to a working mechanic: trauma-based shake, hit-stop, eased tweens,
  squash and stretch. Not for the mechanic itself. 기존 ScreenShake·UIJuice를 먼저 확장한다.
  CAOF Standard(기존 연출 수정)/Critical(새 연출 시스템). Trivial 수치 한두 개 조정은 스킵.
---

## ⚙️ CAOF 통합 (AnnoyingFiveStones 프로젝트)
> **출처:** gamedev-skills/awesome-gamedev-agent-skills (Apache-2.0) — `skills/disciplines/game-feel` @ `c6ca6c4` (2026-11-16 이식). 라이선스·NOTICE: `.claude/skills/LICENSE-awesome-gamedev-agent-skills.txt`, `NOTICE-awesome-gamedev-agent-skills.txt`. 강제 표현은 원문 보존.
> **언제 발동:** 연출·타격감을 바꾸는 Standard·Critical 작업. 유저가 겪는 흐름이 바뀌므로 **구현 전 유저 체험 시뮬레이션**(CLAUDE.md GATE 3.5)을 먼저 쓴다. 손맛 적정성은 "사용자만 확인 가능" 항목으로 검증 계획에 적는다.
> **이 프로젝트에 이미 있는 것 (새로 만들지 말고 여기서 확장 — donts #7):**
> - `Assets/Scripts/ScreenShake.cs` — 메인 카메라 히트스톱(`Time.timeScale=0` + `WaitForSecondsRealtime`) + 실시간 흔들림(제곱 감쇠). 일시정지 중엔 timeScale을 건드리지 않는다. `PlayFailImpact()`(0.07s 정지 + 0.28s 흔들림) / `PlayClearImpact()`(0.06s 정지, **흔들림 없음**).
> - **연출 어휘 규칙:** "흔들림은 '부정'의 어휘로 남겨둔다" — 클리어·성공에는 흔들림을 쓰지 않는다(`ScreenShake.cs` 주석). 이 규칙을 깨려면 사용자 합의.
> - `Assets/Scripts/UIJuice.cs` — `WindowOpenPop`, `ButtonPressScale`. **unscaled 시간**으로 돈다(일시정지 창은 timeScale=0에서 열림).
> - `HandController` 뿌리기 히트스톱(0.05s) — **손·돌만 정지, `Time.timeScale` 불변**(원문 "공격자+대상만 멈추기" 옵션과 같은 방식).
> - 기획서 v11 §8: 실패는 즉시 재시작 → 실패 연출은 실패 표시 1.5초 안에서 끝나야 하고 대기 시간을 늘리지 않는다.
> **원문과 다르게 둔 것:** Godot 코드는 Unity C#으로 바꿨고, Cinemachine 경로는 뺐다(패키지 미설치). 미이식 스킬(`platformer`·`input-systems`·`camera-systems`·`audio-design`·`shader-programming`·`physics-tuning`) 참조도 제거했다. 히트스톱 예시는 원문 버그(짧은 정지가 긴 정지를 조기 종료, 일시정지 무시)를 고쳤다. 접근성 토글은 "출시 필수" → "제안 후 합의"로 조건화했다(Workflow 6). 원문은 "프레임마다 랜덤 오프셋은 버즈처럼 거슬린다"고 하지만 현재 `ScreenShake`는 `Random.Range`를 쓴다. 노이즈로 바꾸는 건 **기존 손맛을 바꾸는 일**이라 자동으로 고치지 않는다. 바꾸려면 대안으로 제시한 뒤 사용자 합의를 받는다.

# Game feel (juice)

Feedback sells an action: several small, brief, exaggerated responses fired together on one
event. This skill adds that layer on top of a working mechanic. It never changes the
simulation, so shake moves the camera and hit-stop scales time; neither moves bodies or stalls
gameplay logic.

## When to use

- A catch, toss, scatter, fail, clear or button press works but feels weak.
- Adding or tuning shake, hit-stop, easing, squash and stretch, flash, number pops.
- Deciding how much juice is enough, and adding reduce-shake and reduce-flash options.

**Not this skill:** the toss/catch arc itself (coroutine animation, donts game#20); input
handling (`unity-input-system`).

## Workflow

1. **Find the event hooks.** Juice attaches to discrete events (catch, drop, fail, clear,
   stage start). If the mechanic emits none, add C# events first; never poll for them.
2. **Assign each event a tier** (table below), then fire that tier's bundle from one call so
   the whole game stays proportional. Start from `ScreenShake`'s existing entry points.
3. **Start with 2-3 channels per event** (sound, particles, shake), play, and add channels
   until the event reads. Hit-stop and shake are the strongest and most abused; keep them for
   impacts.
4. **Ease everything that moves.** Overshoot (`back`) for pops, ease-out for settling,
   ease-in only for anticipation.
5. **Decide what runs during hit-stop.** Time scale slows timers, coroutines using
   `WaitForSeconds` and `Update` delta. Shake decay and UI tweens should usually use real time
   (`Time.unscaledDeltaTime`, `WaitForSecondsRealtime`); the frozen pose should not. Check the
   pause menu: it also uses `Time.timeScale = 0`, so a hit-stop must never restore time while
   paused (see `ScreenShake.Routine`).
6. **Ship accessibility toggles**: shake strength 0-100 %, reduce flashing, reduce camera
   motion. (프로젝트: `SettingsPopupUI`에 지금은 없다. 설정 화면 추가는 기능 변경이라 donts #2·#7에 따라
   **먼저 제안하고 합의 후 구현**한다. 원문의 "ship"을 이 프로젝트에서 조건부로 바꾼 유일한 곳.)
7. **Verify with a headless check, then by eye.** Fire the event twice 30 ms apart and assert
   time scale is back to 1.0 after the longer stop, trauma reaches 0 within `1 / decay`
   seconds (for `ScreenShake`: the camera is back at its rest position within
   `hitStopSeconds + duration`), and every scale tween ends at exactly its rest value. Then ask the user to play it and
   report whether the hit reads, whether anything feels nauseating and whether input still
   registers during the freeze.

## Tiers

| Tier | Trauma | Hit-stop | Particles | Extra | Example events (this game) |
|------|:------:|:--------:|:---------:|-------|----------------------------|
| small | 0.10-0.20 | none | 0-4 | tick SFX | (button press: `ButtonPressScale`) |
| medium | 0.30-0.50 | 0.04-0.06 s | 6-12 | flash | scatter release 0.05 s · clear 0.06 s · fail 0.07 s + 0.28 s shake |
| large | 0.70-1.00 | 0.10-0.15 s | 20-40 | flash, zoom, number | (none today) |

(The example column lists what the game does today, 2026-11-16, not a spec. The 기획서
decides which tier an event gets.)

## Patterns

### 1. Screen shake from decaying trauma (Unity)

```csharp
// Shake = trauma^2, so light hits barely move and heavy hits punch.
// Writes a visual-only offset in real time so the shake keeps decaying during hit-stop
// instead of freezing the camera off-centre. Summed sines, not a per-frame Random.
using UnityEngine;

public class TraumaShake : MonoBehaviour
{
    [SerializeField] private float decay = 1.2f;                 // trauma lost per real second
    [SerializeField] private Vector2 maxOffset = new(0.3f, 0.2f); // world units
    [SerializeField, Range(0f, 1f)] private float strength = 1f; // player setting
    private float _trauma, _t;
    private Vector3 _rest;

    private void Awake() => _rest = transform.localPosition;

    public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount); // hits add

    private void LateUpdate()
    {
        if (_trauma <= 0f) return;
        float dt = Time.unscaledDeltaTime;
        _trauma = Mathf.Max(_trauma - decay * dt, 0f);
        float shake = _trauma * _trauma * strength;
        _t += dt * 30f;
        transform.localPosition = _rest + new Vector3(
            maxOffset.x * shake * Mathf.Sin(_t * 1.7f),
            maxOffset.y * shake * Mathf.Sin(_t * 2.3f), 0f);
        if (_trauma == 0f) transform.localPosition = _rest;
    }

    // Turned off mid-shake: never leave the camera at an offset.
    private void OnDisable() { _trauma = 0f; transform.localPosition = _rest; }
}
```

This is the reference shape. In this project, extend `ScreenShake` rather than adding a second
component that writes the same camera transform.

### 2. Hit-stop that survives overlapping hits

```csharp
// WaitForSecondsRealtime ignores Time.timeScale; WaitForSeconds never finishes at 0.
// Restores only when the LATEST END among overlapping stops has passed, so a short stop
// fired during a long one cannot cut the long one off. Never touches time while paused.
float _stopEndsAt;   // realtime
IEnumerator HitStop(float duration = 0.08f, float slow = 0.05f) {
    if (IsPaused()) yield break;                      // pause owns timeScale
    _stopEndsAt = Mathf.Max(_stopEndsAt, Time.realtimeSinceStartup + duration);
    Time.timeScale = slow;
    while (Time.realtimeSinceStartup < _stopEndsAt) yield return null;  // null still ticks at timeScale 0
    if (!IsPaused()) Time.timeScale = 1f;             // paused during the stop: leave it at 0
}
bool IsPaused() => GameManager.Instance != null && GameManager.Instance.IsPaused;
```

Upstream's version ("only the newest call restores") ends a long stop early when a shorter one
is fired inside it. `ScreenShake.Play` today stops the previous routine and starts a new one,
so the newest call's length wins.

### 3. Squash, stretch and overshoot

See `references/feedback-recipes.md` §2 for an eased `Pop` coroutine (no tween package).
Stop or reuse the previous coroutine on the same transform when the event can retrigger, or two
routines fight and the object ends at the wrong scale.

## Gotchas

- **Shaking the body instead of the camera** desyncs collision and aim. Shake the camera offset
  or a visual-only pivot. (Here: the pointer→world mapping also shakes while the camera moves.)
- **Random offset every frame** buzzes like static. Use noise or summed sines under a decaying
  trauma value.
- **Hit-stop never ends**: Unity `WaitForSeconds` does not advance at time scale 0, and barely
  at 0.05. Wait in real time.
- **Overlapping hit-stops** (two hits 30 ms apart) let the first timer restore time during the
  second freeze. Restore only after the latest end time (pattern 2), and never while paused.
- **Shake freezes off-centre during hit-stop** when it decays with scaled delta. Decay with
  `Time.unscaledDeltaTime`.
- **UI and menu tweens crawl during hit-stop.** Drive them with `Time.unscaledDeltaTime`.
- **Time scale does not slow audio**, so a long freeze plays the impact sound over a frozen
  picture. Keep stops short or lower the audio too.
- **Hit-stop on every frame of a held or multi-hit action** locks the game. One per impact, and
  skip it on hits that land during an active stop.
- **Juice that never returns to rest** (scale drift, shake that does not decay) stops reading as
  feedback. Snap to the rest value at the end of every effect, and in `OnDisable`.
- **Over-juicing routine actions** causes nausea and hides real impacts. Pickups get the small
  tier or nothing.
- **Feedback that blocks input.** Long freezes and uncancellable animations feel like lag; keep
  freezes under about 0.15 s and buffer input through them.
- **Flashing past the photosensitivity limit.** Large bright flashes must stay at or under three
  per second (WCAG 2.3.1), and a reduce-flashing option should replace them with a tint.

## References

- `references/feedback-recipes.md`: read when you need the trauma tuning ranges, the easing
  cheat sheet, an eased pop for Unity without a tween package, flash, knockback, number-pop and
  particle recipes, or the accessibility list.

## Related skills

- `unity-csharp-scripting`: coroutines, unscaled time, `OnDisable` cleanup.
- `unity-physics`: knockback forces, `Time.timeScale` and the simulation.

## Version notes

Last verified upstream: 2026-11-11.

- WCAG 2.2 success criterion 2.3.1, three flashes or below threshold
  (https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html).
- Trauma-based shake follows Squirrel Eiserloh's GDC 2016 talk "Math for Game Programmers:
  Juicing Your Cameras With Math".
