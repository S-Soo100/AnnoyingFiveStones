using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 좌상단 상태 박스 — 나이 / 회귀.
/// World Space Canvas, sortingOrder=95, GraphicRaycaster 없음 (클릭 대상 아님).
///
/// v18: UI 시안(Figma "UI최종시안 0817" / Status_outline 572:531) 실측치로 재구성.
/// 이전에는 배경 위에 흰 글자만 얹혀 있어서, 밝은 창문·칠판을 지날 때 숫자가 묻혔다.
/// 시안은 흰 칸 안에 먹색 글자를 넣어 배경과 무관하게 읽히도록 해뒀다.
/// </summary>
public class SidePanelUI : MonoBehaviour
{
    public static SidePanelUI Instance { get; private set; }

    // 시안 실측(1920×1080 기준, 좌상단 원점)
    private const float PanelX = 50f, PanelY = 50f, PanelW = 320f, PanelH = 172f;
    private const float BoxX = 20f, BoxW = 280f, BoxH = 56f;
    private const float Box1Y = 20f, Box2Y = 96f;
    private const float LabelPadL = 23f, ValuePadR = 20f;
    /// <summary>시안 320×172는 **면 기준** — 어두운 테두리가 그 바깥에 그려진다.
    /// 판을 사방으로 넓히면 자식(흰 칸)도 같은 만큼 안쪽으로 밀어야 제자리에 남는다.</summary>
    private static float O => UISkin.Outset;
    private const float LabelPt = 30f, ValuePt = 35.4f;

    private Canvas canvas;
    private TextMeshProUGUI ageLabel, ageValue;
    private TextMeshProUGUI regressionLabel, regressionValue;

    private TMP_FontAsset koreanTmpFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        koreanTmpFont = KoreanFont.GetTMP();
        CreateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // v10: 언어 전환 시 나이/회귀 갱신
    private void OnEnable()  { LocalizationManager.OnLanguageChanged += Refresh; }
    private void OnDisable() { LocalizationManager.OnLanguageChanged -= Refresh; }

    /// <summary>GameSession 데이터를 읽어 나이/회귀 갱신.</summary>
    public void Refresh()
    {
        // 라벨은 언어에 따라, 숫자는 세션에 따라 바뀐다 — 언어 전환에도 둘 다 다시 쓴다.
        if (ageLabel != null)        ageLabel.text = LocalizationManager.L("hud.age_label");
        if (regressionLabel != null) regressionLabel.text = LocalizationManager.L("hud.regression_label");

        var session = GameSession.Instance;
        if (session == null) return;
        if (ageValue != null)        ageValue.text = session.CurrentAge.ToString();
        if (regressionValue != null) regressionValue.text = session.RegressionCount.ToString();
    }

    private void CreateUI()
    {
        // World Space Canvas — 시안의 패널 위치·크기를 그대로 차지한다.
        var canvasGo = new GameObject("SidePanelCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 95;
        canvas.worldCamera = Camera.main;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = UISkin.DesignToWorld(PanelX + PanelW * 0.5f, PanelY + PanelH * 0.5f, -1f);
        rt.sizeDelta = new Vector2(UISkin.GamePx(PanelW + O * 2f), UISkin.GamePx(PanelH + O * 2f));
        rt.localScale = Vector3.one * 0.01f;

        // 패널 바탕 — 버튼과 같은 광택 베벨(시안도 같은 컴포넌트를 쓴다)
        var bgGo = new GameObject("Panel", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = UISkin.Panel(Mathf.RoundToInt(PanelH + O * 2f));
        bgImg.type = Image.Type.Sliced;
        bgImg.raycastTarget = false;

        CreateInBox(canvasGo.transform, "AgeBox", Box1Y, out ageLabel, out ageValue);
        CreateInBox(canvasGo.transform, "RegressionBox", Box2Y, out regressionLabel, out regressionValue);

        Refresh();   // 플레이스홀더 대신 처음부터 실제 값 — 세션이 없으면 라벨만 채워진다
        Debug.Log("[SidePanelUI] 상태 박스 생성 (시안 실측 배치).");
    }

    /// <summary>흰 칸 하나 + 그 안의 라벨(좌)·숫자(우).</summary>
    private void CreateInBox(Transform parent, string name, float designY,
                             out TextMeshProUGUI label, out TextMeshProUGUI value)
    {
        var boxGo = new GameObject(name, typeof(RectTransform));
        boxGo.transform.SetParent(parent, false);
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0f, 1f);
        boxRt.pivot = new Vector2(0f, 1f);
        boxRt.sizeDelta = new Vector2(UISkin.GamePx(BoxW), UISkin.GamePx(BoxH));
        boxRt.anchoredPosition = new Vector2(UISkin.GamePx(BoxX + O), -UISkin.GamePx(designY + O));

        var img = boxGo.AddComponent<Image>();
        img.sprite = UISkin.InsetBox;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;

        label = CreateText(boxGo.transform, "Label", LabelPt, TextAlignmentOptions.Left,
                           UISkin.GamePx(LabelPadL), 0f);
        value = CreateText(boxGo.transform, "Value", ValuePt, TextAlignmentOptions.Right,
                           0f, UISkin.GamePx(ValuePadR));
    }

    /// <summary>칸 높이 전체를 쓰는 텍스트. 세로 정렬은 TMP에 맡긴다(글자 크기가 달라도 중앙에 선다).</summary>
    private TextMeshProUGUI CreateText(Transform parent, string name, float designPt,
                                       TextAlignmentOptions align, float padLeft, float padRight)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padLeft, 0f);
        rt.offsetMax = new Vector2(-padRight, 0f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = UISkin.GamePx(designPt);
        tmp.color = UISkin.Ink;
        tmp.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.Left
                                                           : TextAlignmentOptions.Right;
        tmp.raycastTarget = false;
        if (koreanTmpFont != null) tmp.font = koreanTmpFont;
        return tmp;
    }
}
