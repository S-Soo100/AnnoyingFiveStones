---
name: unity-input-system
description: >
  Unity 6.3 Input System(New Input System) 입력 배선·디버깅. 트리거 — 클릭/탭/포인터 입력 추가·수정,
  "입력이 안 먹어", "두 번 눌려", "메뉴 열려 있는데 게임 입력이 들어가", FindAction이 null,
  InputAction 구독/해제, 레거시 Input.GetKey 포팅. Wires player input with the Input System package:
  InputAction, .inputactions assets, action maps, PlayerInput. Not for moving bodies once you have
  the input (`unity-physics`, `unity-csharp-scripting`). CAOF Standard/Critical 코드 작업. Trivial 스킵.
---

## ⚙️ CAOF 통합 (AnnoyingFiveStones 프로젝트)
> **출처:** gamedev-skills/awesome-gamedev-agent-skills (Apache-2.0) — `skills/unity/unity-input-system` @ `c6ca6c4` (2026-11-16 이식). 라이선스·NOTICE: `.claude/skills/LICENSE-awesome-gamedev-agent-skills.txt`, `NOTICE-awesome-gamedev-agent-skills.txt`. 강제 표현은 원문 보존.
> **언제 발동:** 입력을 추가·수정하는 Standard·Critical 작업, 입력 버그 진단(systematic-debugging과 함께). Trivial 스킵.
> **연결:** 프로젝트 `CLAUDE.md` §5 "New Input System 사용", donts game#6.
> **이 프로젝트 관례 (원문보다 우선 — donts #7 기존 구조 보존):**
> - `activeInputHandler: 1`(Input System only). 레거시 `Input.Get*`는 **예외를 던진다**.
> - `com.unity.inputsystem` **1.19.0** (원문 기준 1.20.x보다 한 단계 낮음).
> - 스크립트마다 **코드로 `new InputAction(...)`을 만들고 직접 `Enable()`/`Disable()`** 한다(`HandController`·`HandCursorUI`·`TitleScreenUI`·`TutorialUI`·`GraveyardUI`·`ScreenManager`). 원문이 기본으로 권하는 project-wide actions(`InputSystem.actions.FindAction`)로 **갈아엎지 않는다**. 새 입력도 기존 관례를 따른다.
> - `Assets/InputSystem_Actions.inputactions`는 존재하지만 게임 입력 대부분이 이를 쓰지 않는다. 쓰려면 먼저 Grep으로 현재 사용처를 확인한다.
> **원문과 다른 점:** Pattern 1을 이 프로젝트 관례(코드 생성 액션)로 교체, project-wide 패턴은 대안으로 내림. 미이식 스킬(`input-systems`·`platformer`) 참조와 rebinding 레퍼런스(이 게임에 리바인딩 없음) 제외. `testables` 추가 등 manifest 변경은 사용자 승인 후로 명시.

# Unity Input System

Read input through the Input System package (`com.unity.inputsystem`), which replaces the
legacy `Input.GetAxis`/`Input.GetKey` Input Manager. Targets Unity 6.3 LTS (6000.3).

## When to use

- Pointer, click and tap input; action maps; reading a `Vector2` from a pointer or stick; one
  set of actions for mouse, keyboard and touch.
- Any input bug: nothing fires, fires twice, fires under a menu.

**When not to use:** moving the hand or stones once you have the input → `unity-physics` /
`unity-csharp-scripting`.

## Workflow

1. **Read Active Input Handling from the file.** `ProjectSettings/ProjectSettings.asset` has
   `activeInputHandler: 0` (old Input Manager only), `1` (Input System only) or `2` (both).
   This project is `1`. Changing the value restarts the Editor: never change it without asking.
2. **Follow the project's pattern.** Grep the nearest existing script (`HandController.cs`,
   `HandCursorUI.cs`) and copy its lifecycle: create the action, bind it, `Enable()` it,
   subscribe; unsubscribe and `Disable()` on the way out.
3. **Edit an `.inputactions` asset as JSON only when the Editor is closed.** An `.inputactions`
   file is JSON: `maps[].actions[]` (`name`, `type`, `expectedControlType`) and
   `maps[].bindings[]` (`path` such as `<Keyboard>/space`, `action`, `groups` for control
   schemes, `isComposite` and `isPartOfComposite`). Give each new action and binding a fresh
   `id` GUID like the existing entries, keep the file valid JSON, and re-read it after writing.
4. **Switch action maps (or enable/disable the actions) for context** (gameplay vs menu)
   instead of guarding every handler.
5. **Verify** with the checks below.

## Patterns

### 1. Code-created actions (this project's convention)

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class ClickReader : MonoBehaviour
{
    private InputAction _pointer, _click;

    private void Awake()
    {
        _pointer = new InputAction("Pointer", InputActionType.Value);
        _pointer.AddBinding("<Pointer>/position");
        _click = new InputAction("Click", InputActionType.Button);
        _click.AddBinding("<Pointer>/press");
    }

    private void OnEnable()
    {
        _pointer.Enable();
        _click.Enable();
        _click.performed += OnClick;
    }

    private void OnDisable()
    {
        _click.performed -= OnClick;   // every += has a matching -=
        _pointer.Disable();
        _click.Disable();
    }

    private void OnDestroy()
    {
        _pointer.Dispose();
        _click.Dispose();
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        Vector2 screen = _pointer.ReadValue<Vector2>();
        /* act once per press */
    }
}
```

Polling alternative inside `Update`: `_click.WasPressedThisFrame()` (edge, true for one frame)
and `_pointer.ReadValue<Vector2>()` (continuous value).

### 2. Project-wide actions and map switching (alternative, only if the project adopts it)

```csharp
private InputActionMap _player, _ui;

private void Awake()
{
    _player = InputSystem.actions.FindActionMap("Player");
    _ui = InputSystem.actions.FindActionMap("UI");
}

private void Start() => _ui.Disable();   // project-wide actions enable every map at startup

public void OpenMenu()  { _player.Disable(); _ui.Enable(); }
public void CloseMenu() { _ui.Disable(); _player.Enable(); }
```

### 3. `PlayerInput` with Send Messages

`PlayerInput` with Behavior = Send Messages calls `On<ActionName>(InputValue)` on scripts on
the same GameObject. Broadcast Messages also reaches children; Invoke Unity Events and Invoke
C# Events need no naming convention. (Not used in this project today.)

## Verify

- **No Editor needed:** Grep `Assets/Scripts` for `Input.Get` (must be zero while
  `activeInputHandler` is `1`). For every action you added, confirm there is a matching
  `Enable()`, `Disable()`, `+=` and `-=`. If code calls `FindAction`/`FindActionMap`, parse the
  `.inputactions` JSON and confirm every name exists (for `"Player/Jump"`, a map `Player` with
  an action `Jump`), and that composites have all their parts.
- **Tests:** the package's `InputTestFixture` adds virtual devices and presses controls from
  code. It needs `com.unity.inputsystem` under `"testables"` in `Packages/manifest.json` and
  `Unity.InputSystem` and `Unity.InputSystem.TestFramework` in the test `.asmdef`: both change
  project config, so ask first.

  ```csharp
  using NUnit.Framework;
  using UnityEngine.InputSystem;

  public class JumpInputTests : InputTestFixture
  {
      [Test]
      public void SpaceTriggersJump()
      {
          var keyboard = InputSystem.AddDevice<Keyboard>();
          var jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
          int performed = 0;
          jump.performed += _ => performed++;
          jump.Enable();

          Press(keyboard.spaceKey);

          Assert.That(performed, Is.EqualTo(1));
      }
  }
  ```

- **Running game:** the project's verify menus and debug hooks (for example the hand-press
  debug hook added with `Tools/검증 1115`) drive input without a person. With the Unity CLI,
  `unity command --query simulate` lists any key-injection command for your CLI version.
- **Hardware:** ask the user to press the control and report what the Input Debugger
  (Window > Analysis > Input Debugger) shows for the device and action.

## Gotchas

- **No input at all.** The action or map was never enabled. Actions you create in code start
  disabled; project-wide actions and `PlayerInput`'s default map start enabled.
- **Gameplay actions fire while a menu is open.** Project-wide actions enable every map at
  startup, including when `PlayerInput` uses that asset. Disable the maps you do not want in
  `Start`. With code-created actions, disable them (or gate on the game state) when the menu
  opens.
- **`FindAction` returns null.** The name or `Map/Action` path does not exist in that asset,
  or you edited a different asset than the one assigned as project-wide actions (Project
  Settings > Input System Package).
- **`InvalidOperationException` about the `UnityEngine.Input` class.** Old `Input.GetAxis`/
  `GetKey` code runs while `activeInputHandler` is `1`. Port it.
- **A press is missed or handled every frame.** `ReadValue<float>()` on a Button action is the
  held state (non-zero while down, 0 once released), not a press event: polling it acts on every
  frame of the hold, and a short tap between two polls can be missed. For one action per press
  use `performed` or `WasPressedThisFrame()`, and read actions in `Update`:
  `WasPressedThisFrame` in `FixedUpdate` misses presses or sees them twice.
- **Handlers run twice.** `OnEnable` subscribed again without an `OnDisable` unsubscribe.
- **Actions leak across domain reloads or scene loads.** A code-created action that is never
  disabled keeps firing into a destroyed object. Disable in `OnDisable`, `Dispose()` in
  `OnDestroy`.
- **Touch or gamepad not detected.** The device is not in any control scheme the
  `PlayerInput` allows, or a 2D Vector composite is missing a part.
- **(프로젝트) 화면 흔들림 중 `ScreenToWorldPoint`가 흔들린다.** `ScreenShake`가 카메라를 움직이는
  동안 포인터→월드 변환도 같이 흔들린다. 플레이 중 흔들림을 추가하면 손 위치 계산과의 간섭을 먼저
  확인한다(`ScreenShake.cs` 주석).

## Related skills

- `unity-csharp-scripting`: the MonoBehaviour around these handlers and test assemblies.
- `unity-physics`: applying the input to a rigidbody.

## Version notes

Last verified upstream: 2026-11-11 against `com.unity.inputsystem` 1.20.1. This project uses
1.19.0; the APIs above (`InputAction`, `AddBinding`, `WasPressedThisFrame`, `InputTestFixture`,
`activeInputHandler` 0/1/2) exist in both.

- Manual: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/index.html
