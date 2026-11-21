---
name: unity-physics
description: >
  Unity 6.3 3D 물리(Rigidbody·Collider·트리거·레이어 충돌) 함정 점검. 트리거 — 돌이 밀려나거나 튐,
  OnTriggerEnter/OnCollisionEnter가 안 불림, 키네마틱 전환, linearVelocity·isKinematic 수정, 콜라이더
  추가·활성화, "물리가 이상해". Unity 6.3 LTS 3D physics: Rigidbody, forces and linearVelocity,
  the layer collision matrix, contacts between kinematic bodies. 프로젝트 CLAUDE.md "Unity 물리
  주의사항"이 우선. CAOF Standard/Critical 코드 작업. Trivial 스킵.
---

## ⚙️ CAOF 통합 (AnnoyingFiveStones 프로젝트)
> **출처:** gamedev-skills/awesome-gamedev-agent-skills (Apache-2.0) — `skills/unity/unity-physics` @ `c6ca6c4` (2026-11-16 이식). 라이선스·NOTICE: `.claude/skills/LICENSE-awesome-gamedev-agent-skills.txt`, `NOTICE-awesome-gamedev-agent-skills.txt`. 강제 표현은 원문 보존.
> **언제 발동:** 물리 코드·콜라이더·Rigidbody 설정을 바꾸는 Standard·Critical 작업, 물리 버그 진단(systematic-debugging과 함께). Trivial 스킵.
> **우선순위 — 이 프로젝트 규칙이 원문보다 먼저다:**
> - `CLAUDE.md` "Unity 물리 주의사항"·"2.5D 좌표 매핑"(카메라 z=-10 정면 ortho, **X=좌우·Y=상하·Z=깊이**).
> - donts game#8(텔레포트는 Kinematic→이동→복원), game#19(보드/낙 판정은 `BoardBounds.QuadPoint`+`IsOutsideMat` 하나), game#20(**연출 동작은 물리 임펄스가 아니라 코루틴**, 안착 돌은 `isKinematic=true`), game#21(좌표 추측 금지, 실행 중 게임 직접 확인).
> - "Kinematic Rigidbody + 활성 Collider는 non-kinematic을 밀어낸다" → Bounds 판정만 필요하면 Collider를 켜지 않는다.
> **이 프로젝트 사실:** 3D `Rigidbody`만 쓴다(`Rigidbody2D` 0개). `Physics.Raycast` 계열 사용 0.
> **원문과 다른 점:** 2D 물리 섹션·`references/physics-2d.md`·`references/raycasting-and-joints.md` 제외(사용 안 함). 원문 Pattern 1(힘 기반 이동)은 game#20과 충돌해 "연출 동작에 쓰지 말 것"으로 표시. 미이식 스킬(`physics-tuning`·`unity-tilemap-2d`·`unity-navmesh`) 참조 제거. Verify에 MCP Unity 경로 추가.

# Unity Physics (3D PhysX)

Make objects move, collide and detect each other in Unity 6.3 LTS (6000.3). Unity has two
separate physics engines: 3D (`Rigidbody`, `Collider`, `Physics`) and 2D (`Rigidbody2D`,
`Collider2D`, `Physics2D`). They never interact. This project uses 3D only.

## When to use

- Changing how a stone or the hand moves, collides or rests; adding or enabling a collider;
  switching `isKinematic`; responding to collisions or triggers; setting up layers.
- Any "stone got pushed / popped out / passed through / never fired" bug.

**When not to use:** scripted toss, catch and scatter arcs (those are coroutines by project
rule, see the header); input → `unity-input-system`.

## Workflow

1. **Find out what is on the object before touching code.** Grep the scene or prefab YAML for
   `Rigidbody:` and `BoxCollider:`/`SphereCollider:`/`MeshCollider:` document headers, or read
   it live with MCP `get_gameobject`. Note `m_IsKinematic`, `m_IsTrigger` and `m_Enabled`.
2. **Read the physics settings as text, not through the Inspector.** 3D settings live in
   `ProjectSettings/DynamicsManager.asset` (`m_QueriesHitTriggers`, `m_ContactPairsMode`,
   `m_LayerCollisionMatrix`). The matrix is a hex bitfield: read and change it with
   `Physics.GetIgnoreLayerCollision`/`IgnoreLayerCollision` rather than editing the hex.
3. **Do physics in `FixedUpdate`.** Read input in `Update`, store intent, apply forces or set
   `linearVelocity` or call `MovePosition` in `FixedUpdate`.
4. **Move dynamic bodies through the physics API, not the Transform.** `AddForce`,
   `linearVelocity`, `MovePosition`. Writing `transform.position` on a dynamic body teleports
   it and skips collision resolution. For a deliberate teleport, follow donts game#8.
5. **Pick collision vs trigger.** A solid collider blocks and calls `OnCollisionEnter`;
   `isTrigger` passes through and calls `OnTriggerEnter`. At least one of the pair needs a
   rigidbody.
6. **Prune layer pairs** that never need to touch. A runtime `IgnoreLayerCollision` call is
   not saved; for a lasting change ask the user to untick the pair in Project Settings >
   Physics > Layer Collision Matrix.
7. **Verify** with one of the checks below, then remove any temporary logging.

## Patterns

### 1. Force-based movement in `FixedUpdate` with a speed clamp

⚠️ Not for tosses, catches or scatters in this project (donts game#20). Use only for genuinely
physical, unscripted motion.

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Mover : MonoBehaviour
{
    [SerializeField] private float accel = 30f, maxSpeed = 8f;
    private Rigidbody _rb;
    private Vector3 _input;   // set from Update / input system

    private void Awake() => _rb = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        _rb.AddForce(_input * accel, ForceMode.Acceleration);     // mass-independent accel
        // Clamps the X/Z plane and keeps Y (gravity). In this 2.5D project Y is screen-up and
        // Z is depth (CLAUDE.md): pick the plane to clamp for the case at hand.
        Vector3 flat = new(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
            _rb.linearVelocity = new Vector3(flat.x, _rb.linearVelocity.y, flat.z);
        }
    }
}
```

`ForceMode`: `Force` and `Acceleration` are continuous (mass-scaled vs not); `Impulse` and
`VelocityChange` are instant (mass-scaled vs not).

### 2. Kinematic mover that carries resting bodies

```csharp
// isKinematic Rigidbody: MovePosition moves it through the solver, so bodies on top ride along.
private void FixedUpdate() =>
    _rb.MovePosition(_rb.position + Vector3.right * (2f * Time.fixedDeltaTime));
```

## Verify

Pick the first path that is available and say which one you used.

- **MCP Unity (default):** `recompile_scripts` → `get_console_logs`; read a body's state
  with `get_gameobject` (`isKinematic`, `linearVelocity`, collider `enabled`, world bounds)
  before and after a few seconds of Play mode. ⚠️ A recompile during Play mode does not apply
  until Play is restarted.
- **Project verify menus** (`Tools/검증 …`) for scripted, repeatable play checks.
- **Unity CLI with a ready Editor:** `unity recompile --project-path <project>`, then read
  state back with `unity command script eval --code '...'`. Commands change between CLI
  releases: find the current name with `unity command --query <word>`.
- **PlayMode test:** build the case in code (a floor, a falling body,
  `yield return new WaitForSeconds(1f)`, assert the body rests above the floor or the callback
  count is 1). Read the NUnit XML, not just the exit code. Test-assembly setup is in
  `unity-csharp-scripting`.
- **Feel or visual only:** ask the user to enter Play mode and report a number (how many
  stones left the board, how many `OnCollisionEnter` logs per hit).

## Gotchas

- **`OnCollisionEnter` never fires on a `CharacterController`.** `Move` bypasses the
  rigidbody system and reports through `OnControllerColliderHit`.
- **Trigger callbacks never fire.** One of the two colliders needs a rigidbody, and both
  colliders must be enabled. Two static colliders never report.
- **No collision messages between two kinematic bodies, or kinematic vs static.** With the
  default Contact Pairs Mode Unity only reports when one body is dynamic. Make one dynamic,
  use a trigger, or change Contact Pairs Mode. (Relevant here: settled stones are kinematic.)
- **A query stops on a trigger volume.** Queries hit triggers by default. Pass
  `QueryTriggerInteraction.Ignore`, or turn off Queries Hit Triggers.
- **Obsolete warnings on `velocity`, `drag`, `angularDrag`.** Unity 6 renamed them
  `linearVelocity`, `linearDamping`, `angularDamping`. They still compile with warnings;
  `PhysicMaterial` → `PhysicsMaterial` is a hard error.
- **Setting `transform.position` on a dynamic body.** It teleports and skips collision. If
  you do write the Transform, queries see the old position until the next step; call
  `Physics.SyncTransforms()` once before a same-frame query.
- **Setting `linearVelocity` right after switching `isKinematic` off.** Wait one
  `FixedUpdate` or animate with a coroutine (project rule, CLAUDE.md).
- **A resting body hangs in mid-air after its floor moves.** Sleeping bodies wake on
  collisions and forces, but a static collider moved by its Transform may not wake them. Call
  `WakeUp()`. A collider that moves every frame should get a kinematic rigidbody instead of
  staying static.
- **Fast objects pass through walls.** Raise Collision Detection from `Discrete`.
  `Continuous` sweeps only against static colliders; `Continuous Dynamic` also against other
  continuous dynamic bodies; `Continuous Speculative` against everything and cheaper.
- **A concave `MeshCollider` on a moving body.** Concave mesh colliders can only be static or
  kinematic, and two concave colliders never collide. Enable Convex or build a compound
  collider from primitives, and keep scale uniform.
- **Part of a compound body drifts away.** A compound collider has one rigidbody, on the root.
  An extra rigidbody on a child makes it a separate body.
- **Nothing moves, queries still hit.** `Time.timeScale = 0` stops the simulation (this
  project's pause and `ScreenShake` hit-stop both set it); queries still read collider
  geometry.

## Related skills

- `unity-csharp-scripting`: the `FixedUpdate`/`Update` split, test assemblies, the `.meta`
  checker.
- `game-feel`: hit-stop and shake that sit on top of the simulation without changing it.

## Version notes

Last verified upstream: 2026-11-11, against Unity 6000.3.28f1. This project runs 6000.3.11f1.

- Unity 6 renames (`velocity`/`drag`/`angularDrag`) are obsolete warnings with an API Updater
  path in 6.3; `PhysicMaterial` is an error. Source: Unity C# reference source,
  `Modules/Physics/ScriptBindings/*.deprecated.cs`
  (https://github.com/Unity-Technologies/UnityCsReference/tree/6000.3.28f1).
- Scripting API pages for `Rigidbody`, `Physics`.
