---
name: unity-csharp-scripting
description: >
  Unity 6.3 C# 스크립트 작성·수정 시 MonoBehaviour 생명주기, 직렬화, .meta/GUID 관리, 커맨드라인 검증.
  트리거 — 새 .cs 파일 추가·이름 변경·이동·삭제, "Missing script", 인스펙터에서 필드가 사라짐,
  직렬화 필드 이름 변경, 코루틴/async(Awaitable) 정리, `?.`/`??`를 UnityEngine.Object에 쓰는 코드.
  Writes and fixes Unity 6.3 LTS C# gameplay scripts: lifecycle, serialization, .meta/GUID
  discipline and command-line tests. Not for physics (`unity-physics`) or input (`unity-input-system`).
  CAOF Standard/Critical 코드 작업. Trivial(상수·로그)·비코드 작업엔 쓰지 않는다.
---

## ⚙️ CAOF 통합 (AnnoyingFiveStones 프로젝트)
> **출처:** gamedev-skills/awesome-gamedev-agent-skills (Apache-2.0) — `skills/unity/unity-csharp-scripting` @ `c6ca6c4` (2026-11-16 이식). 라이선스·NOTICE: `.claude/skills/LICENSE-awesome-gamedev-agent-skills.txt`, `NOTICE-awesome-gamedev-agent-skills.txt`. 본문 강제 표현은 원문(영어) 보존, 의존성만 치환.
> **언제 발동:** Standard·Critical 트랙의 C# 수정. Trivial 스킵.
> **우선순위:** 프로젝트 `CLAUDE.md` "게임 코드 수정 규칙"(Read로 먼저 파악 → 진단 → 최소 변경 → 자체 검수)과 `.claude/rules/donts/game.md`가 이 스킬보다 우선한다. 이 스킬은 그 위에 얹는 **체크리스트와 함정 목록**이다.
> **이 프로젝트에서 알아둘 것:**
> - 게임 스크립트는 대부분 `Assembly-CSharp`(asmdef 없음). 순수 로직만 `Assets/Scripts/Core/FiveStones.Core.asmdef`. 테스트 `Assets/Tests/EditMode/FiveStones.Tests.asmdef`는 **Core만** 참조한다 → Assembly-CSharp 스크립트는 EditMode 테스트로 직접 못 부른다.
> - 플레이 동작 검증은 기존 관례인 **Editor 메뉴 검증 스크립트**(`Assets/Editor/V1115VerifyMenu.cs`의 `Tools/검증 1115/전체 실행` 등)를 우선 쓴다.
> - 컴파일 확인은 MCP Unity(포트 8091) `recompile_scripts` → `get_console_logs`가 1순위. ⚠️ 새 .cs는 recompile만으로 임포트되지 않을 수 있다 → `execute_menu_item("Assets/Refresh")`로 .meta 생성 확인. `get_console_logs`가 `[]`여도 거짓 깨끗함일 수 있다(메모리 `feedback_new_cs_meta_refresh`).
> **원문과 다른 점:** 미이식 스킬(`unity-scriptableobjects`·`unity-animation`·`unity-build-pipeline`) 참조 제거. Verify 경로에 MCP Unity와 프로젝트 검증 메뉴 추가. 검사기 스크립트 실행 경로를 이 프로젝트 위치로 고정.

# Unity C# Scripting (MonoBehaviour)

Write correct gameplay scripts for Unity 6.3 LTS (6000.3), C# 9 on .NET Standard 2.1, keep the
project's `.meta` files intact, and prove the result compiles and behaves without opening the
Editor by hand.

## When to use

- Authoring or fixing a `MonoBehaviour`: lifecycle callbacks, component references,
  serialized fields, coroutines, `async` with `Awaitable`.
- Creating, moving, renaming or deleting scripts and assets from the file system.
- Compiling or testing a Unity project from a terminal, for any Unity skill.

**When not to use:** rigidbodies and collision response → `unity-physics`; reading player
input → `unity-input-system`; juice (shake, hit-stop, pop) → `game-feel`.

## Workflow

1. **Read the project's versions first.** `ProjectSettings/ProjectVersion.txt` gives the
   Editor version (this project: `6000.3.11f1`); `Packages/manifest.json` the packages; any
   `*.asmdef` files tell you which assembly a new script lands in. Keep the project's version
   unless asked to upgrade.
2. **Pick the callback by purpose.** `Awake` caches references to its own components;
   `OnEnable`/`OnDisable` subscribe and unsubscribe; `Start` wires things that depend on other
   objects' `Awake`; `FixedUpdate` is physics; `LateUpdate` follows movement (cameras).
3. **Expose tunables with `[SerializeField] private`** so designers can edit them and other
   code cannot. A plain class needs `[System.Serializable]` to show up. ⚠️ 이 프로젝트 규칙:
   코드 기본값을 바꿔도 **씬 직렬화값이 우선**한다 → MCP `update_component`로 씬 값도 같이 갱신.
4. **Move every file with its `.meta`.** A `.meta` holds the asset's GUID; a new `.meta` means
   a new GUID and every reference to the old one shows "Missing script". A brand-new file needs
   no `.meta`: Unity writes one on the next import. Keep each `MonoBehaviour` or
   `ScriptableObject` class in a file of the same name.
5. **Run the checker:** `python3 -I .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .`
   from the project root (`--json` for machine-readable output). Fix every `ERROR` (duplicate
   or malformed GUIDs, files moved without their `.meta`, missing script references) and re-run
   until it exits 0. It needs Python 3.10+ only, reads text-serialized assets and never writes.
   `--help` lists the checks and exit codes. `missing-meta` warnings for brand-new files are
   normal until the next import.
6. **Compile.** MCP Unity `recompile_scripts` + `get_console_logs` (see the header for the
   new-file caveat). With the Unity CLI and a ready Editor, `unity recompile --project-path
   <project>`; otherwise a batch-mode import and `grep -E "error CS[0-9]{4}"` on its log.
7. **Prove the behavior** with an EditMode test (Core logic), the project's `Tools/검증 …`
   menus, a PlayMode test, or by reading the value back from the running Editor. Read
   `references/agent-checks.md` for the exact commands, the test `.asmdef` and how to read the
   results. Report which checks ran and which could not.

## Patterns

### 1. Serialized fields that survive refactors

```csharp
using UnityEngine;
using UnityEngine.Serialization;

public class Turret : MonoBehaviour
{
    [SerializeField, Range(0.1f, 5f)] private float fireInterval = 0.5f;

    // Renamed from "range": without this attribute every scene and prefab loses the value.
    [FormerlySerializedAs("range")]
    [SerializeField] private float maxRange = 12f;

    [System.Serializable]
    public class Stats { public int hp = 100; public int armor = 0; }

    [SerializeField] private Stats stats = new();
}
```

### 2. A coroutine you can stop exactly

```csharp
private Coroutine _spawnLoop;

private void OnEnable()  => _spawnLoop = StartCoroutine(SpawnLoop());
private void OnDisable() { if (_spawnLoop != null) StopCoroutine(_spawnLoop); }

private System.Collections.IEnumerator SpawnLoop()
{
    var wait = new WaitForSeconds(1f);   // allocate once, reuse
    while (true) { Spawn(); yield return wait; }
}

private void Spawn() { }
```

For `async`/`await` with `Awaitable`, the full execution order and custom yield
instructions, read `references/lifecycle-and-coroutines.md`.

## Gotchas

- **`?.` and `??` on a destroyed object.** Unity overrides `==` so a destroyed object equals
  `null`, but `?.`, `??` and `is null` skip that override and see a live reference. Use
  `if (target)` or `target == null` for anything derived from `UnityEngine.Object`.
  The trap is a reference to an object that was **destroyed** (a cached field after a scene
  reload, a static instance). (프로젝트 실측 2026-11-16, 6000.3.11f1 Edit·Play 모드 둘 다:
  컴포넌트가 **없을 때** `GetComponent<T>()`는 진짜 C# null을 돌려준다 → `ScreenShake.cs:34`의
  `GetComponent<T>() ?? AddComponent<T>()`는 정상 동작. 이 패턴만 보고 버그로 단정하지 말 것.)
- **Renaming a serialized field wipes its saved values.** Add `[FormerlySerializedAs("old")]`
  (namespace `UnityEngine.Serialization`) when you rename.
- **"Missing script" after a refactor.** The `.meta` stayed behind, was regenerated, or the
  class name no longer matches the file name. The checker reports all three.
- **Physics in `Update`.** Forces or `MovePosition` outside `FixedUpdate` jitter and depend on
  frame rate. Read input in `Update`, apply it in `FixedUpdate`.
- **Order between objects.** All `Awake` calls finish before any `Start`, but the order among
  `Awake`s (and among `Start`s) is undefined unless Script Execution Order is set. Do
  cross-object wiring in `Start`.
- **Coroutines stop when the GameObject is deactivated.** `SetActive(false)` kills them;
  setting only the component's `enabled = false` does not. Restart in `OnEnable` if needed.
- **An `async` method keeps running after its object is destroyed.** Pass
  `destroyCancellationToken` to `Awaitable` waits (they then throw
  `OperationCanceledException`), and await each `Awaitable` instance only once: they are
  pooled.
- **Obsolete warnings on `FindObjectOfType`/`FindObjectsOfType`.** Use `FindAnyObjectByType`
  or `FindObjectsByType(FindObjectsSortMode.None)` on 6.3. From 6.4, `FindFirstObjectByType`
  and the `FindObjectsSortMode` overload are obsolete too: use `FindAnyObjectByType` and
  `FindObjectsByType<T>()`. All of them scan the scene; cache the result.
- **Repeated `GetComponent` in hot paths.** Cache stable references in `Awake`, and profile
  before treating an occasional lookup as the bottleneck.
- **The first `Update` can run in the same frame as `Start`.** Guard fields that other objects
  set later.
- **(프로젝트) 에디터가 백그라운드면 프레임이 안 돈다.** 코루틴 초기화가 멈춘 걸 버그로 오진하지
  않는다(메모리 `feedback_unfocused_editor_no_frames`).

## References

- Read `references/agent-checks.md` before compiling or testing from the command line: MCP
  Unity, Unity CLI, batch mode, this project's test assemblies, log locations and how to read
  NUnit results.
- Read `references/lifecycle-and-coroutines.md` when ordering matters across callbacks, or
  for coroutine yield instructions, `CustomYieldInstruction` and the `Awaitable` equivalents
  of coroutine waits.
- `scripts/check_unity_project.py`: run it (step 5); treat it as a black box and use `--help`
  instead of reading the source.

## Related skills

- `unity-physics`: `Rigidbody`, collisions and `FixedUpdate` motion.
- `unity-input-system`: reading player input into these scripts.
- `game-feel`: shake, hit-stop and eased pops on top of working mechanics.

## Version notes

Last verified upstream: 2026-11-11, against Unity 6000.3.28f1. This project runs 6000.3.11f1.

- `FindObjectsOfType` is obsolete in 6.3; `FindFirstObjectByType` and
  `FindObjectsByType(FindObjectsSortMode)` become obsolete in 6.4. Source: Unity C# reference
  source, `Runtime/Export/Scripting/UnityEngineObject.bindings.cs`
  (https://github.com/Unity-Technologies/UnityCsReference).
- `?.`/`??` caveat: Scripting API `Object` page
  (https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/object).
- Event order: https://docs.unity.com/en-us/engine/6000.3/manual/scripting/managing-update-order/execution-order
- Test runner command line (`-runTests` does not support `-quit`):
  https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/test-framework-introduction/reference-command-line
