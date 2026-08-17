using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경량 로컬라이제이션 (한/영). v9(260703).
/// 런타임 코드 생성 UI에 맞춰 Dictionary 기반 — L("key")로 현재 언어 문자열 조회.
/// 언어 전환 시 OnLanguageChanged 이벤트 → 각 UI가 텍스트 재설정.
/// static 유틸이라 인스턴스 불필요. GameManager 부팅 시 Init() 1회 호출.
///
/// ⚠️ 갱신 전략 불변식 (2026-07-06 교차검수):
///  - **언어 토글 진입점은 SettingsPopupUI 단 하나이며, 이 팝업은 타이틀 화면에서만 열린다.**
///  - 따라서 토글 순간 화면에 보이는 건 Title+Settings뿐 → 이 둘만 OnLanguageChanged 구독으로 라이브 갱신.
///  - 그 외 화면(Pause/Graveyard/NameInput/StoryMent/Tutorial/게임플레이 가이드)은
///    "열릴 때/표시될 때" 재설정하거나 코루틴 실행 시점에 L() 조회 → 재진입 시 항상 최신 언어.
///  - 이 불변식이 깨지면(예: Pause에 언어 토글 추가, 엔딩 위 Settings 오버레이) 재설정형 화면이
///    stale로 남을 수 있으니, 그때는 해당 화면에도 OnLanguageChanged 구독을 얹어야 한다.
/// </summary>
public static class LocalizationManager
{
    public enum Language { Korean, English }

    private const string PrefKey = "Language";
    private static Language current = Language.Korean;
    private static bool initialized;

    public static Language Current => current;

    /// <summary>언어 전환 시 발행. 각 UI가 구독하여 텍스트를 다시 설정한다.</summary>
    public static event Action OnLanguageChanged;

    public static void Init()
    {
        if (initialized) return;
        current = (Language)PlayerPrefs.GetInt(PrefKey, 0); // 0=Korean 기본
        initialized = true;
    }

    public static void SetLanguage(Language lang)
    {
        if (current == lang) return;
        current = lang;
        PlayerPrefs.SetInt(PrefKey, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    public static void Toggle()
        => SetLanguage(current == Language.Korean ? Language.English : Language.Korean);

    public static bool IsKorean => current == Language.Korean;

    /// <summary>키로 현재 언어 문자열 반환. 키 미등록 시 키 자체 반환(폴백) + 경고.</summary>
    public static string L(string key)
    {
        if (table.TryGetValue(key, out var pair))
            return current == Language.Korean ? pair.ko : pair.en;
        Debug.LogWarning($"[Localization] 미등록 키: {key}");
        return key;
    }

    /// <summary>포맷 문자열 조회 (예: "회귀 {0}번"). L(key) 결과에 string.Format 적용.</summary>
    public static string LF(string key, params object[] args)
        => string.Format(L(key), args);

    // ──────────────────────────────────────────────────────────────
    // 문자열 테이블 (key → (한국어, English))
    // 화면별로 점진 확장. 260703 확보 영어 우선 반영.
    // ──────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, (string ko, string en)> table = new()
    {
        // === 홈화면 ===
        ["home.play"]      = ("게임 시작", "Play"),
        ["home.settings"]  = ("설정", "Settings"),
        ["home.exit"]      = ("게임 종료", "Exit"), // 260703 스펙 일치 (구: "나가기")
        ["home.cemetery"]  = ("묘지", "Cemetery"),
        ["home.record_mode"]   = ("기록 모드", "Record"), // v11 개발 빌드: 기록/연습 분기
        ["home.practice_mode"] = ("연습 모드", "Practice"),

        // === 설정창 ===
        ["settings.title"]     = ("설정", "Settings"),
        ["settings.bgm"]       = ("배경음", "BGM"),
        ["settings.sfx"]       = ("효과음", "Sound Effect"),
        ["settings.language"]  = ("언어", "Language"),
        ["settings.close"]     = ("닫기", "Close"),

        // === 일시정지 ===
        ["pause.title"]   = ("일시정지", "Pause"),
        ["pause.resume"]  = ("게임 재개", "Resume"),
        ["pause.quit"]    = ("게임 종료", "Quit Game"),
        ["pause.music"]   = ("BGM {0}%", "BGM {0}%"),
        ["pause.sfx"]     = ("효과음 {0}%", "Sound Effect {0}%"), // v11 일시정지 SFX 슬라이더

        // === 게임 종료 확인 모달 (260703) ===
        ["quit.message"]  = ("게임을 종료하시겠습니까?\n현재 기록은 저장되지 않습니다.",
                             "Quit game?\nYour score won't be saved."),
        ["quit.confirm"]  = ("확인", "Quit"),
        ["quit.cancel"]   = ("취소", "Cancel"),

        // === 성공/실패 (260703) ===
        ["result.clear"]  = ("CLEAR!", "CLEAR!"),
        ["result.fail"]   = ("FAIL", "FAIL"),
        ["result.restart_life"] = ("인생을 다시 시작합니다", "Restart your life."),

        // === 엔딩·묘지 (260703) ===
        ["ending.mainment"] = ("이번 생은 여기까지 입니다", "This is the end of this life."),
        ["ending.thanks"]   = ("수고하셨습니다", "Well done."),
        ["ending.record"]   = ("기록: {0}", "Record: {0}"),
        ["ending.credit"]   = ("Credit", "Credit"),
        ["grave.play_again"] = ("Play Again", "Play Again"),
        ["grave.go_home"]    = ("Go Home", "Go Home"),
        ["grave.name_prompt"] = ("묘비에 새길 이름을 지어주세요", "Enter a name for the tombstone."),
        ["grave.save"]        = ("이 이름으로 저장", "Save with this name."),
        ["grave.name_start"]  = ("시작", "Start"),
        ["grave.elapsed"]     = ("소요 시간  {0}", "Time taken  {0}"),
        ["grave.loading"]     = ("불러오는 중...", "Loading..."),
        ["grave.load_fail"]   = ("기록을 불러올 수 없습니다", "Failed to load records."),
        ["grave.regression"]  = ("회귀 {0}번", "Loop {0}"),

        // === 게임플레이 HUD ===
        ["hud.age"]        = ("{0}살", "Age {0}"),
        ["hud.regression"] = ("회귀: {0}번", "Regression: Loop {0}"),
        ["hud.pause"]      = ("중지", "Pause"),
        // v18: 시안의 상태박스는 라벨과 숫자를 좌우로 나눠 흰 칸에 넣는다.
        // 위의 hud.age / hud.regression은 값을 문장에 끼워 넣는 형태라 그대로 못 쓴다(둘 다 남겨둔다).
        ["hud.age_label"]        = ("나이", "Age"),
        ["hud.regression_label"] = ("회귀", "Regression"),

        // === 스테이지 인트로 ===
        ["stage.ready"]    = ("준비하세요.", "Get ready."),
        ["stage.fold"]     = ("꺾기", "Flip"), // 공기놀이 꺾기 (임시 번역, 2026-07-06 승인)

        // === 게임플레이 조작 가이드 자막 (10살 전용, 영어 임시 번역 2026-07-06 승인) ===
        ["guide.scatter"]          = ("[ 꾹 눌러서 게이지 조절, 놓으면 뿌리기 ]", "[ Hold to set the gauge, release to scatter ]"),
        ["guide.pick_throw"]       = ("[ 커서를 돌 위로 이동 ]", "[ Move the cursor over a stone ]"),
        ["guide.throw"]            = ("[ 클릭하여 던지기 ]", "[ Click to throw ]"),
        ["guide.pick_stones"]      = ("[ 돌을 단계에 맞게 주우세요 ]", "[ Pick up the stones in order ]"),
        ["guide.pick_1"]           = ("[ 돌을 한 개씩 집으세요 ]", "[ Pick up the stones one by one ]"),
        ["guide.pick_2"]           = ("[ 돌을 두 개씩 집으세요 ]", "[ Pick up two stones at a time ]"),
        ["guide.pick_3"]           = ("[ 돌을 세 개, 한 개씩 집으세요 ]", "[ Pick up three stones, then one ]"),
        ["guide.pick_4"]           = ("[ 돌을 네 개 집으세요 ]", "[ Pick up all four stones at once ]"),
        ["guide.catch"]            = ("[ 커서를 움직여 돌을 받으세요! ]", "[ Move the cursor to catch the stone! ]"),
        ["guide.s5_throw_palm"]    = ("[ 게이지에 맞춰 클릭! 손바닥 던지기 ]", "[ Click on the gauge! Palm toss ]"),
        ["guide.s5_throw_back"]    = ("[ 게이지에 맞춰 클릭! 손등 던지기 ]", "[ Click on the gauge! Back-hand toss ]"),
        ["guide.s5_throw_default"] = ("[ 클릭하여 던지기! ]", "[ Click to throw! ]"),
        ["guide.s5_catch_back"]    = ("[ 손등으로 5개 모두 받기! ]", "[ Catch all 5 on the back of your hand! ]"),
        ["guide.s5_catch_snatch"]  = ("[ 타이밍에 맞춰 클릭! 낚아채기! ]", "[ Click on time! Snatch! ]"),
        ["guide.s5_catch_default"] = ("[ 돌을 받으세요! ]", "[ Catch the stones! ]"),

        // === 튜토리얼 (10살) — 영어 확정 (2026-07-06 사용자 승인) ===
        ["tutorial.slide1"] = ("꾹 눌러서 힘을 조절하고,\n놓으면 돌이 뿌려집니다.",
                               "Press and hold to set your power,\nrelease to scatter the stones."),
        ["tutorial.slide2"] = ("돌 위로 커서를 옮기면\n돌을 잡습니다.",
                               "Move the cursor over a stone\nto pick it up."),
        ["tutorial.slide3"] = ("잡은 돌을 클릭해 위로 던지고,\n떨어질 때 다시 받으세요!",
                               "Click to toss the stone up,\nthen catch it as it falls!"),

        // === 공통 === (260703 스펙: 클릭 기반 게임 → "클릭하여 계속")
        ["common.click_continue"] = ("클릭하여 계속", "Click to continue"),

        // === 타이틀 토스트 (영어 확정, 2026-07-06 사용자 승인) ===
        ["title.toast"] = ("놀지 말고 공기놀이를 시작하시는게 어떨까요?",
                           "How about starting the game instead of playing around?"),

        // === 타이틀 말풍선 장식 (v16) — 만화풍 감탄사. 좁은 말풍선에 들어가야 하므로 짧게.
        ["title.bubble_left"]  = ("마참내", "FINALLY!"),   // "마침내"의 밈 표기 — 영어도 감탄조로
        ["title.bubble_right"] = ("즐겁다", "SO FUN!"),
    };
}
