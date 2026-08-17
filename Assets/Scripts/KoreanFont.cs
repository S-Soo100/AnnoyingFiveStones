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

    /// <summary>
    /// 게임 전체가 쓰는 TMP 폰트.
    ///
    /// v18: UI 시안 폰트(Iosevka Charon Mono)를 **주 폰트**로, 나눔고딕을 **폴백**으로 쓴다.
    /// 시안 폰트에는 한글이 없다 — Figma에서 보이던 한글도 시스템 폴백으로 그려진 것이었다.
    /// 그래서 라틴·숫자·기호는 시안 폰트로, 한글은 폴백으로 넘어간다. 시안을 가장 가깝게
    /// 재현하면서 39MB짜리 한글 아틀라스를 새로 만들지 않는 유일한 방법이다.
    /// (폴백 연결은 IosevkaCharonMono SDF 에셋 안에 있다 — Assets/Editor/FontAssetBuilder.cs)
    ///
    /// 이름이 KoreanFont로 남은 건 호출부가 많아서다. 하는 일은 "이 게임의 표준 폰트".
    /// </summary>
    public static TMP_FontAsset GetTMP()
    {
        if (!_tmpInitialized)
        {
            _tmpInitialized = true;
            _tmpFont = Resources.Load<TMP_FontAsset>("Fonts/IosevkaCharonMono SDF");

            if (_tmpFont == null)
            {
                // 시안 폰트가 없으면 한글이라도 살린다 — 예전 동작 그대로.
                _tmpFont = Resources.Load<TMP_FontAsset>("Fonts/NanumGothic SDF");
                Debug.LogWarning("[KoreanFont] 시안 폰트(IosevkaCharonMono SDF) 없음 → 나눔고딕으로 폴백");
            }

            if (_tmpFont != null)
            {
                int fb = _tmpFont.fallbackFontAssetTable != null ? _tmpFont.fallbackFontAssetTable.Count : 0;
                Debug.Log($"[KoreanFont] TMP FontAsset loaded: {_tmpFont.name} (폴백 {fb}개)");
            }
            else
            {
                Debug.LogWarning("[KoreanFont] TMP 폰트를 찾지 못했다. 기본 폰트로 그려진다(한글 깨짐).");
            }
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
