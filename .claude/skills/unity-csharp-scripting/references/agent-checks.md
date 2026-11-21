# Checking a Unity change without clicking (Unity 6.3 LTS)

> 출처: awesome-gamedev-agent-skills (Apache-2.0) `unity-csharp-scripting/references/agent-checks.md` @ `c6ca6c4`.
> 이 프로젝트용 변경: Path 0(MCP Unity)·프로젝트 검증 메뉴 추가, 테스트 asmdef를 실제 파일 기준으로 교체, CLI 버전 경고 추가.

Run the cheapest check that proves the change, and report which one ran.

## Order of checks

1. `python3 -I .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .`:
   `.meta`, GUID and missing-script errors. No Unity needed.
2. Compile check: no `error CS` lines.
3. EditMode tests: plain logic in `FiveStones.Core`, asset contents.
4. Project verify menus (`Tools/검증 …`) or PlayMode tests: physics, animation, input,
   anything that needs frames.
5. Read live state from a running Editor (Path 0 or A).
6. Ask the user to look and report a number.

Read `ProjectSettings/ProjectVersion.txt` first (`m_EditorVersion`, this project
`6000.3.11f1`) and use that Editor version. A project opened in a newer Editor gets upgraded,
which the user did not ask for.

## Path 0: MCP Unity (this project's default)

The Editor exposes MCP Unity on port 8091.

- `recompile_scripts` → `get_console_logs` (error → warning → info).
- ⚠️ A brand-new `.cs` may not be imported by a recompile alone. Run
  `execute_menu_item("Assets/Refresh")`, then confirm the `.meta` exists. An empty
  `get_console_logs` result is not proof of a clean compile.
- `run_tests` for EditMode tests; `execute_menu_item("Tools/검증 …")` for the project's
  verify menus, then read their log lines with `get_console_logs`.
- ⚠️ A recompile during Play mode does not apply until Play is stopped and restarted.

## Path A: Unity CLI and a connected Editor

Available when `unity --version` works and the project has the `com.unity.pipeline` package.
This project has CLI `1.0.0-beta.14` and `com.unity.pipeline` `0.9.1-exp.1`, older than the
versions upstream tested (beta.17 / 1.0.0-pre.1). Both are pre-release and change between
versions: find a command with `unity command --query <word>` and its flags with
`<command> --help` instead of guessing, and add `--format json` when you parse output.

```bash
unity status --project-path <project>                  # is this project's Editor ready?
unity recompile --project-path <project>               # compile errors, no build
unity test <project> --mode EditMode --output editmode.xml
unity command script eval --project-path <project> --code 'return Application.unityVersion;'
unity command console get --project-path <project>     # recent Console output
```

- `script eval` runs C# inside the Editor and returns the value of `return x;` as JSON.
  Use it to read state back; keep project edits in files so they show in version control.
- `unity test` exits 8 when the tests ran and some failed, and 6 when the run did not finish:
  a compile error, no licence, an Editor crash or a timeout. Fix the cause of a 6 before you
  read any results.
- Always pass `--project-path` when more than one Editor may be open.
- An Editor that started with compile errors boots into Safe Mode and cannot take commands;
  `unity status` lists the errors. Fix them in the files, then retry.
- Installing or upgrading the CLI or the package changes the user's machine or project: ask
  first.

## Path B: batch mode, no CLI

Batch mode cannot open a project that is already open in an Editor, so ask the user to close
it first or use Path 0/A.

```bash
# Compile check: imports the project, compiles scripts, exits.
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$PWD/compile.log"
grep -E "error CS[0-9]{4}" compile.log

# Tests: no -quit, the test runner exits by itself when it finishes.
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testResults "$PWD/editmode.xml" -logFile "$PWD/tests.log"
```

Give absolute paths for `-testResults` and `-logFile`. The interactive Editor log on macOS is
`~/Library/Logs/Unity/Editor.log`; grep it for `error CS` and for your own `Debug.Log` tags.
A built player logs to `~/Library/Logs/{Company}/{Product}/Player.log` (donts game#15).

## Test assemblies (this project)

- `Assets/Tests/EditMode/FiveStones.Tests.asmdef` references `FiveStones.Core`,
  `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, with `nunit.framework.dll`,
  `includePlatforms: ["Editor"]` and `UNITY_INCLUDE_TESTS`. Add new EditMode tests there.
- An assembly definition cannot reference the predefined `Assembly-CSharp`, where most game
  scripts live. To unit-test their logic, move the pure math into `FiveStones.Core` (as the
  board geometry tests do) or use a verify menu. Adding an `.asmdef` around the game scripts
  is a bigger change: ask first.
- For PlayMode tests use a second folder with `"includePlatforms": []` and drop
  `UnityEditor.TestRunner` from `references`.

## Reading results

- The results file is NUnit 3 XML. The root `test-run` element carries `result`, `total`,
  `passed` and `failed`; each failing `test-case` has a `failure/message`. Report those
  numbers and messages, not "tests pass" from the exit code alone.
- Remove temporary `Debug.Log` probes and throwaway test scenes before you finish, and say in
  the report which checks ran, which could not run, and why.

## When only a person can judge

Feel, timing and visuals need eyes. Ask for something specific and countable: "Play 15살 to
the 5단, fail twice, and tell me whether the screen shook both times" beats "check it looks
right". Ask for the Console text if anything is red.
