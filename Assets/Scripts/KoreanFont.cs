using UnityEngine;
using TMPro;

/// <summary>
/// 크로스 플랫폼 한글 폰트 유틸리티.
/// OnGUI용 Font + TMP용 TMP_FontAsset 모두 제공.
/// TMP: Resources/Fonts/NanumGothic SDF.asset (Font Asset Creator로 사전 생성)
/// OnGUI: Mac AppleGothic, Windows Malgun Gothic, 폴백 Arial
/// </summary>
public static class KoreanFont
{
    private static Font _font;
    private static TMP_FontAsset _tmpFont;
    private static bool _initialized;
    private static bool _tmpInitialized;

    /// <summary>OnGUI용 (DebugHUD 디버그 전용)</summary>
    public static Font Get()
    {
        EnsureFont();
        return _font;
    }

    /// <summary>TMP용 한글 폰트 에셋 (Resources/Fonts/NanumGothic SDF 로드)</summary>
    public static TMP_FontAsset GetTMP()
    {
        if (!_tmpInitialized)
        {
            _tmpInitialized = true;
            _tmpFont = Resources.Load<TMP_FontAsset>("Fonts/NanumGothic SDF");

            if (_tmpFont != null)
                Debug.Log($"[KoreanFont] TMP FontAsset loaded: {_tmpFont.name}");
            else
                Debug.LogWarning("[KoreanFont] Resources/Fonts/NanumGothic SDF not found. TMP texts will use default font.");
        }
        return _tmpFont;
    }

    private static void EnsureFont()
    {
        if (_initialized) return;
        _initialized = true;

        _font = TryLoadFont("AppleGothic")         // macOS
             ?? TryLoadFont("Malgun Gothic")        // Windows
             ?? TryLoadFont("맑은 고딕")             // Windows (한글명)
             ?? TryLoadFont("NanumGothic")          // 나눔고딕 (설치된 경우)
             ?? TryLoadFont("Arial");               // 최종 폴백

        if (_font != null)
            Debug.Log($"[KoreanFont] Loaded: {_font.name}");
        else
            Debug.LogWarning("[KoreanFont] No suitable font found, using default.");
    }

    private static Font TryLoadFont(string fontName)
    {
        var font = Font.CreateDynamicFontFromOSFont(fontName, 16);
        if (font != null)
        {
            font.RequestCharactersInTexture("가", 16);
            font.GetCharacterInfo('가', out CharacterInfo info, 16);
            if (info.advance > 0)
                return font;
        }
        return null;
    }
}
