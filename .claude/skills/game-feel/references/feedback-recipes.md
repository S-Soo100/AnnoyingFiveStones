# Feedback recipes (Unity 6.3)

> 출처: awesome-gamedev-agent-skills (Apache-2.0) `disciplines/game-feel/references/feedback-recipes.md` @ `c6ca6c4`.
> 이 프로젝트용 변경: Godot·Phaser·Cinemachine 내용 제거, 이징 표를 Unity 기준으로 교체, 미이식 스킬 참조 제거.

Contents:
1. The trauma model and tuning ranges
2. Easing cheat sheet, plus an eased pop for Unity without a tween package
3. The rest of the feedback menu (flash, knockback, number pop, particles, freeze)
4. Accessibility toggles

## 1. The trauma model (why shake feels good)

Track a single `trauma` value in `[0, 1]`. Events **add** trauma; it **decays** linearly each
frame. The actual shake amount is `trauma^2` (or `trauma^3`) so:

- small/frequent events barely nudge the screen (low trauma, squared → tiny),
- big events punch hard (high trauma, squared → near full),
- shake **always ends** on its own because trauma decays to 0.

Offset and rotation are `max_* * shake * noise(t)`. Sample a noise function
(`Mathf.PerlinNoise`) or summed sines across time — never a fresh `Random` per frame, which
produces a harsh buzz instead of a shake.

```csharp
// Layered sines at incommensurate rates: smooth, never a per-frame random buzz.
static float ShakeAxis(float seed, float t) =>
    0.6f * Mathf.Sin(t * 11f + seed) + 0.4f * Mathf.Sin(t * 23f + seed * 2f);
```

Tunable starting points: `decay = 1.0..1.5` trauma/sec, per-hit trauma `0.15` (light) → `0.8`
(heavy). Max offset in this project is in world units: orthographic size 7 means the screen is
14 units tall, and the current fail shake peaks at 0.22.

## 2. Easing cheat sheet — which curve for which job

| Goal | Ease | Unity (no tween package) | Notes |
|------|------|--------------------------|-------|
| UI/element "pop" in | overshoot | `EaseOutBack` below | shoots past target, settles back |
| Bouncy, lively land | bounce/elastic | hand-rolled `EaseOutBounce` | use sparingly; reads as cartoonish |
| Settle / decelerate | ease-out | `1 - (1-k)^3` | the default for "comes to rest" |
| Anticipation / wind-up | ease-in | `k^3` | slow start before a fast action |
| Smooth A→B both ends | ease-in-out | `Mathf.SmoothStep(0,1,k)` | camera moves, menu slides |

Unity 6.3 LTS has no built-in tween library; options in order of preference: `Vector3.SmoothDamp`
for spring-like follow, `Mathf.SmoothStep`/hand-rolled ease in a coroutine, Animator curves,
or a third-party tween package if the project already uses one (this project does not). Keep
the *curve choice* the same regardless of tool.

```csharp
// Unity 6.3 LTS: an eased scale "pop" in a coroutine (no external deps).
static float EaseOutBack(float k) {                     // 0 -> 1, peaks ~10% past 1
    const float c1 = 1.70158f, c3 = c1 + 1f;
    float m = k - 1f;
    return 1f + c3 * m * m * m + c1 * m * m;
}

IEnumerator Pop(Transform t, float dur = 0.18f) {
    var squash = new Vector3(1.3f, 0.7f, 1f);            // squash on the event
    for (float e = 0f; e < dur; e += Time.deltaTime) {
        // Unclamped, so the overshoot past Vector3.one survives the lerp.
        t.localScale = Vector3.LerpUnclamped(squash, Vector3.one, EaseOutBack(e / dur));
        yield return null;
    }
    t.localScale = Vector3.one;
}
```

Use `Time.unscaledDeltaTime` instead of `Time.deltaTime` for anything that must keep moving
during hit-stop or pause (UI, as `UIJuice` does). Restore the object's *own* rest scale, not
`Vector3.one`, when it is not 1.

## 3. The rest of the feedback menu

- **Flash:** tint the sprite/material white for 1–3 frames on hit (material color), then ease
  back. Cheap, hugely legible.
- **Knockback:** apply an impulse away from the hit normal, clamped and short. Pair with brief
  control lockout, not a long one. (Not for scripted tosses here: donts game#20.)
- **Number/text pop:** spawn a number that rises, fades, and eases out; randomize the
  horizontal drift so stacked pops fan out.
- **Particles:** a short burst at the contact point (dust, sparkle). Pool them — do not
  instantiate-and-destroy per hit.
- **Freeze frame:** the hit-stop in the body; scale duration to importance (0.04 s light →
  0.15 s heavy). Optionally freeze only the actors involved, not the whole world (this
  project's scatter hit-stop freezes only the hand and stones).
- **Anticipation & follow-through:** a tiny wind-up before a big action and a settle after read
  as weight.
- **Chromatic/vignette/zoom punch:** post-process nudges for big moments; keep brief. (This
  project turned off the default volume's tonemapping and vignette on 2026-10-30; check before
  adding post-process.)

## 4. Accessibility (ship these toggles)

- **Reduce screen shake** (scale trauma output by a 0–100% setting, default ~60–80%).
- **Reduce/disable flashing** (photosensitivity) — replace white flashes with a static tint.
  Even with flashing on, keep any large-area flash at or under three flashes in any one-second
  window ([WCAG 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html);
  [Xbox Accessibility Guideline 118](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/118)).
- **Reduce camera motion** — cut shake roll and zoom punches.

These belong in `SettingsPopupUI`; adding them is a feature change, so agree it with the user
first.
