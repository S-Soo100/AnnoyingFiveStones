using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 하늘 영역 좌측에 나이/회귀 2줄 표시.
/// World Space Canvas, sortingOrder=95, GraphicRaycaster 없음 (클릭 대상 아님).
/// </summary>
public class SidePanelUI : MonoBehaviour
{
    public static SidePanelUI Instance { get; private set; }

    private Canvas canvas;
    private TextMeshProUGUI ageLabel;
    private TextMeshProUGUI regressionLabel;

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

    /// <summary>GameSession 데이터를 읽어 나이/회귀 갱신.</summary>
    public void Refresh()
    {
        var session = GameSession.Instance;
        if (session == null) return;
        if (ageLabel != null) ageLabel.text = $"나이: {session.CurrentAge}살";
        if (regressionLabel != null) regressionLabel.text = $"회귀: {session.RegressionCount}번";
    }

    private void CreateUI()
    {
        // World Space Canvas
        var canvasGo = new GameObject("SidePanelCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 95;
        canvas.worldCamera = Camera.main;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(-10f, 2.5f, -1f);
        rt.sizeDelta = new Vector2(400f, 200f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f); // 400px × 0.01 = 4 world units

        // 나이 레이블 — 좌상단 앵커, fontSize=28, 흰색
        var ageGo = new GameObject("AgeLabel", typeof(RectTransform));
        ageGo.transform.SetParent(canvasGo.transform, false);
        var ageRt = ageGo.GetComponent<RectTransform>();
        ageRt.anchorMin = new Vector2(0f, 1f);
        ageRt.anchorMax = new Vector2(0f, 1f);
        ageRt.pivot = new Vector2(0f, 1f);
        ageRt.sizeDelta = new Vector2(360f, 40f);
        ageRt.anchoredPosition = new Vector2(20f, -30f);

        ageLabel = ageGo.AddComponent<TextMeshProUGUI>();
        ageLabel.text = "나이: -살";
        ageLabel.fontSize = 28f;
        ageLabel.color = Color.white;
        ageLabel.alignment = TextAlignmentOptions.Left;
        if (koreanTmpFont != null) ageLabel.font = koreanTmpFont;

        // 회귀 레이블 — 좌상단 앵커, fontSize=22, 흰색 70% alpha
        var regGo = new GameObject("RegressionLabel", typeof(RectTransform));
        regGo.transform.SetParent(canvasGo.transform, false);
        var regRt = regGo.GetComponent<RectTransform>();
        regRt.anchorMin = new Vector2(0f, 1f);
        regRt.anchorMax = new Vector2(0f, 1f);
        regRt.pivot = new Vector2(0f, 1f);
        regRt.sizeDelta = new Vector2(360f, 36f);
        regRt.anchoredPosition = new Vector2(20f, -70f);

        regressionLabel = regGo.AddComponent<TextMeshProUGUI>();
        regressionLabel.text = "회귀: -번";
        regressionLabel.fontSize = 22f;
        regressionLabel.color = new Color(1f, 1f, 1f, 0.7f);
        regressionLabel.alignment = TextAlignmentOptions.Left;
        if (koreanTmpFont != null) regressionLabel.font = koreanTmpFont;

        Debug.Log("[SidePanelUI] World Space UI created.");
    }
}
