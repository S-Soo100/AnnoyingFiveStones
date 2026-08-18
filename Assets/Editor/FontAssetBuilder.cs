using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// UI 시안 폰트(Iosevka Charon Mono)의 TMP 에셋을 코드로 만든다.
///
/// 왜 이렇게 하나:
/// Font Asset Creator는 에디터 창을 손으로 다뤄야 해서 MCP로는 못 돌린다.
/// 그리고 설정값(샘플링 크기·패딩·아틀라스·문자셋)이 사람 손을 타면 재현이 안 된다.
/// 여기 적힌 값이 곧 기록이다 — 다시 만들 일이 생기면 메뉴만 한 번 누르면 된다.
///
/// 라틴만 굽는 이유:
/// 시안 폰트에는 한글이 없다(Figma에서 보이는 한글은 시스템 폴백으로 그려진 것이다).
/// 그래서 라틴·숫자·기호만 이 폰트로 굽고, 한글은 **폴백으로 기존 나눔고딕**에 넘긴다.
/// 덕분에 39MB짜리 한글 아틀라스를 새로 만들 필요가 없다.
/// </summary>
public static class FontAssetBuilder
{
    // 출처: https://github.com/jul-sh/iosevka-charon/releases (v34.300, SIL OFL — OFL.txt 동봉)
    // Regular만 둔다. 시안이 전부 400 웨이트이고, TMP가 필요하면 굵기를 흉내 낸다.
    // 진짜 Bold가 필요해지면 위 릴리스에서 IosevkaCharonMono-Bold.ttf를 받아 같은 방식으로 구우면 된다.
    private const string SourceTtf = "Assets/Fonts/IosevkaCharon/IosevkaCharonMono-Regular.ttf";
    private const string OutputPath = "Assets/Resources/Fonts/IosevkaCharonMono SDF.asset";
    private const string KoreanFallback = "Assets/Resources/Fonts/NanumGothic SDF.asset";

    // 아틀라스 설정 — 라틴만 담으므로 1024면 넉넉하다(한글용은 4096이 필요했다).
    private const int SamplingPointSize = 90;
    private const int Padding = 9;
    private const int AtlasSize = 1024;

    [MenuItem("Tools/폰트: 시안 폰트(Iosevka) TMP 에셋 생성")]
    public static void BuildIosevka()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SourceTtf);
        if (font == null)
        {
            Debug.LogError($"[FontAssetBuilder] TTF 없음: {SourceTtf}");
            return;
        }

        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallback);
        if (fallback == null)
            Debug.LogWarning($"[FontAssetBuilder] 한글 폴백 없음: {KoreanFallback} — 한글이 깨진다");

        // Dynamic으로 만들어 문자를 채운 뒤 Static으로 굳힌다.
        // 처음부터 Static으로 만들면 채울 방법이 없다.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);

        if (fontAsset == null)
        {
            Debug.LogError("[FontAssetBuilder] CreateFontAsset 실패");
            return;
        }

        fontAsset.name = "IosevkaCharonMono SDF";

        string charset = BuildLatinCharset();
        bool ok = fontAsset.TryAddCharacters(charset, out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[FontAssetBuilder] 이 폰트에 없는 문자: {missing}");

        // 다 구운 뒤 고정 — 런타임에 아틀라스를 다시 만들지 않게 한다.
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        if (fontAsset.material != null)
        {
            // ⚠️ CreateFontAsset은 머티리얼을 **TMP_SDF-Mobile** 셰이더로 만든다.
            //    이 프로젝트에서 외곽선이 실제로 나오던 나눔고딕 에셋은 **TMP_SDF**를 쓴다.
            //    Mobile 쪽에 로고 외곽선을 걸었더니 아무것도 안 그려졌다 — 동작이 확인된
            //    쪽으로 맞춘다.
            var sdf = Shader.Find("TextMeshPro/Distance Field");
            if (sdf != null) fontAsset.material.shader = sdf;
            else Debug.LogWarning("[FontAssetBuilder] TMP_SDF 셰이더를 찾지 못했다 — Mobile 유지");

            // 코드로 만든 머티리얼은 _ScaleRatioA/B/C가 계산되지 않은 채 남는다.
            // 이 값이 없으면 외곽선 두께가 0으로 취급된다. Font Asset Creator는 이걸 대신 해준다.
            ShaderUtilities.UpdateShaderRatios(fontAsset.material);
        }
        if (fallback != null)
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

        // 아틀라스 텍스처와 머티리얼은 **서브에셋으로 붙이지 않으면 저장되지 않는다**.
        AssetDatabase.DeleteAsset(OutputPath);
        AssetDatabase.CreateAsset(fontAsset, OutputPath);

        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex == null) continue;
            tex.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FontAssetBuilder] 생성 완료: {OutputPath} / " +
                  $"글리프 {fontAsset.characterTable.Count}자, 아틀라스 {fontAsset.atlasTextures.Length}장, " +
                  $"폴백 {(fallback != null ? fallback.name : "없음")} / 성공={ok}");
    }

    /// <summary>ASCII 출력 가능 문자 + 화면에 실제로 쓰는 기호 몇 개.</summary>
    private static string BuildLatinCharset()
    {
        var sb = new StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);   // 공백~물결
        sb.Append("←→…·—–‘’“”×"); // 안내문·구분자에서 쓰는 것들
        return sb.ToString();
    }
}
