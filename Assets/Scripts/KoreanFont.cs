using UnityEngine;

/// <summary>
/// 크로스 플랫폼 한글 폰트 유틸리티.
/// Mac: AppleGothic, Windows: Malgun Gothic, 폴백: Arial
/// </summary>
public static class KoreanFont
{
    private static Font _font;
    private static bool _initialized;

    public static Font Get()
    {
        if (!_initialized)
        {
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
        return _font;
    }

    private static Font TryLoadFont(string fontName)
    {
        var font = Font.CreateDynamicFontFromOSFont(fontName, 16);
        // 실제로 존재하는지 확인: 글리프 테스트
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
