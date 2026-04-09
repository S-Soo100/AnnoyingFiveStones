using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas 기반 세로 게이지 바 (뿌리기 세기 전용).
/// ScatterSystem에서 직접 Show/Hide/SetValue 호출.
/// </summary>
public class GaugeBarUI : MonoBehaviour
{
    public static GaugeBarUI Instance { get; private set; }

    private Canvas canvas;
    private GameObject barRoot;
    private Image barBackground;
    private Image barFill;
    private Image barMarker;
    private RectTransform barMarkerRt;
    private RectTransform barBackgroundRt;
    private TextMeshProUGUI labelPower;
    private TextMeshProUGUI labelPercent;
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
        barRoot.SetActive(false);
        if (labelPower != null) labelPower.gameObject.SetActive(false);
    }

    /// <summary>게이지 값 갱신 (0~1)</summary>
    public void SetValue(float value)
    {
        value = Mathf.Clamp01(value);
        barFill.fillAmount = value;

        // 색상 보간: 초록 → 노랑 → 빨강
        Color fillColor;
        if (value < 0.5f)
            fillColor = Color.Lerp(Color.green, Color.yellow, value * 2f);
        else
            fillColor = Color.Lerp(Color.yellow, Color.red, (value - 0.5f) * 2f);
        barFill.color = fillColor;

        // 마커 위치 (캐싱된 RectTransform 사용)
        float barHeight = barBackgroundRt.rect.height;
        if (barHeight > 0f)
            barMarkerRt.anchoredPosition = new Vector2(0, value * barHeight - barHeight * 0.5f);

        // 퍼센트 표시
        labelPercent.text = $"{(int)(value * 100)}%";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>게이지 표시</summary>
    public void Show()
    {
        barRoot.SetActive(true);
        if (labelPower != null) labelPower.gameObject.SetActive(true);
        SetValue(0f);
    }

    /// <summary>게이지 숨김</summary>
    public void Hide()
    {
        barRoot.SetActive(false);
        if (labelPower != null) labelPower.gameObject.SetActive(false);
    }

    private void CreateUI()
    {
        // 전용 Canvas (World Space — 보드 좌측 안쪽 배치)
        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("GaugeCanvas");
            canvasGo.transform.SetParent(transform);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 99;
            canvas.worldCamera = Camera.main;

            // World Space 위치: 보드 좌측 안쪽
            // Cloth(보드) pos=(0, -5.8), scale(8, 6.4) → 좌측 X=-4
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.position = new Vector3(-3.2f, -5.8f, -0.1f);
            canvasRt.sizeDelta = new Vector2(60f, 500f);
            canvasRt.localScale = new Vector3(0.01f, 0.01f, 0.01f); // 0.6 x 5.0 world units
        }

        barRoot = new GameObject("BarRoot", typeof(RectTransform));
        barRoot.transform.SetParent(canvas.transform, false);
        var rootRt = barRoot.GetComponent<RectTransform>();
        // 앵커 기반으로 Canvas 전체를 채움
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = new Vector2(5f, 30f);  // 하단 30px 여유 (퍼센트 표시)
        rootRt.offsetMax = new Vector2(-5f, -5f); // 상단/좌우 5px 여백

        // 배경 (어두운 바)
        var bgGo = new GameObject("BarBg", typeof(RectTransform));
        bgGo.transform.SetParent(barRoot.transform, false);
        barBackground = bgGo.AddComponent<Image>();
        barBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        barBackgroundRt = bgGo.GetComponent<RectTransform>();
        var bgRt = barBackgroundRt;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 채움 (아래에서 위로)
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(barRoot.transform, false);
        barFill = fillGo.AddComponent<Image>();
        barFill.color = Color.green;
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Vertical;
        barFill.fillOrigin = 0; // 아래에서 위로
        barFill.fillAmount = 0f;
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0.05f, 0f);
        fillRt.anchorMax = new Vector2(0.95f, 1f);
        fillRt.sizeDelta = Vector2.zero;

        // 마커 (수평선)
        var markerGo = new GameObject("Marker", typeof(RectTransform));
        markerGo.transform.SetParent(barRoot.transform, false);
        barMarker = markerGo.AddComponent<Image>();
        barMarker.color = Color.white;
        barMarkerRt = markerGo.GetComponent<RectTransform>();
        var markerRt = barMarkerRt;
        markerRt.anchorMin = new Vector2(0, 0.5f);
        markerRt.anchorMax = new Vector2(1, 0.5f);
        markerRt.pivot = new Vector2(0.5f, 0.5f);
        markerRt.sizeDelta = new Vector2(10, 4); // 좌우 약간 넓게, 높이 4px

        // "POWER" 레이블 — Canvas 직속 자식, 바 상단에 배치
        var powerGo = new GameObject("LabelPower", typeof(RectTransform));
        powerGo.transform.SetParent(canvas.transform, false);
        labelPower = powerGo.AddComponent<TextMeshProUGUI>();
        labelPower.text = "POWER";
        labelPower.enableAutoSizing = true;
        labelPower.fontSizeMin = 10;
        labelPower.fontSizeMax = 18;
        labelPower.fontStyle = FontStyles.Bold;
        labelPower.color = Color.white;
        labelPower.alignment = TextAlignmentOptions.Center;
        labelPower.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) labelPower.font = koreanTmpFont;
        var powerRt = powerGo.GetComponent<RectTransform>();
        powerRt.anchorMin = new Vector2(0.5f, 1f);
        powerRt.anchorMax = new Vector2(0.5f, 1f);
        powerRt.pivot = new Vector2(0.5f, 0f);
        powerRt.sizeDelta = new Vector2(200f, 30f);
        powerRt.anchoredPosition = new Vector2(0f, 5f); // 캔버스 상단에서 약간 위

        // 퍼센트 레이블 — barRoot 자식 (바 내부 하단, barRoot show/hide에 자동 종속)
        var pctGo = new GameObject("LabelPercent", typeof(RectTransform));
        pctGo.transform.SetParent(barRoot.transform, false);
        labelPercent = pctGo.AddComponent<TextMeshProUGUI>();
        labelPercent.text = "0%";
        labelPercent.enableAutoSizing = true;
        labelPercent.fontSizeMin = 8;
        labelPercent.fontSizeMax = 14;
        labelPercent.fontStyle = FontStyles.Bold;
        labelPercent.color = Color.white;
        labelPercent.alignment = TextAlignmentOptions.Center;
        labelPercent.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) labelPercent.font = koreanTmpFont;
        var pctRt = pctGo.GetComponent<RectTransform>();
        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(1f, 0f);
        pctRt.pivot = new Vector2(0.5f, 0f);
        pctRt.sizeDelta = new Vector2(0f, 20f);
        pctRt.anchoredPosition = new Vector2(0f, 5f); // 바 내부 하단에서 5px 위
    }
}
