using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경량 로컬라이제이션 (한/영). v9(260703).
/// 런타임 코드 생성 UI에 맞춰 Dictionary 기반 — L("key")로 현재 언어 문자열 조회.
/// 언어 전환 시 OnLanguageChanged 이벤트 → 각 UI가 텍스트 재설정.
/// static 유틸이라 인스턴스 불필요. GameManager 부팅 시 Init() 1회 호출.
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

        // === 설정창 ===
        ["settings.title"]     = ("설정", "Settings"),
        ["settings.bgm"]       = ("배경음", "BGM"),
        ["settings.sfx"]       = ("효과음", "Sound Effect"),
        ["settings.language"]  = ("언어", "Language"),
        ["settings.volume"]    = ("음량 {0}%", "Volume {0}%"),
        ["settings.close"]     = ("닫기", "Close"),

        // === 일시정지 ===
        ["pause.title"]   = ("일시정지", "Pause"),
        ["pause.resume"]  = ("게임 재개", "Resume"),
        ["pause.quit"]    = ("게임 종료", "Quit Game"),
        ["pause.music"]   = ("음악 {0}%", "Music {0}%"),

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

        // === 스테이지 인트로 ===
        ["stage.ready"]    = ("준비하세요.", "Get ready."),
        ["stage.fold"]     = ("꺾기", "Flip"), // 공기놀이 꺾기 (임시 번역, 2026-07-06 승인)

        // === 튜토리얼 (10살) — 영어 확정 (2026-07-06 사용자 승인) ===
        ["tutorial.slide1"] = ("꾹 눌러서 게이지를 조절하세요.\n놓으면 돌이 퍼집니다.",
                               "Press and hold to set the gauge.\nRelease to scatter the stones."),
        ["tutorial.slide2"] = ("돌 위로 커서를 이동하면\n자동으로 줍습니다.",
                               "Move the cursor over a stone\nto pick it up automatically."),
        ["tutorial.slide3"] = ("하늘에서 떨어지는 돌을\n손바닥으로 받으세요!",
                               "Catch the falling stones\nwith your palm!"),

        // === 공통 === (260703 스펙: 클릭 기반 게임 → "클릭하여 계속")
        ["common.click_continue"] = ("클릭하여 계속", "Click to continue"),

        // === 타이틀 토스트 (영어 확정, 2026-07-06 사용자 승인) ===
        ["title.toast"] = ("놀지 말고 공기놀이를 시작하시는게 어떨까요?",
                           "How about starting the game instead of playing around?"),
    };
}
