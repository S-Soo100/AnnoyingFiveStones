using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas 기반 세로 게이지 바 (뿌리기 세기 전용).
/// ScatterSystem / HandController에서 직접 Show/Hide/SetValue 호출.
/// v13-2: 미니멀 세련 리디자인 — 둥근 트랙/채움, 뮤트 팔레트, 소프트 섀도우, 정제 타이포.
/// </summary>
public class GaugeBarUI : MonoBehaviour
{
    public static GaugeBarUI Instance { get; private set; }

    private Canvas canvas;
    private GameObject barRoot;
    private Image barTrack;      // 둥근 배경 트랙
    private Image barFill;       // 둥근 채움 (높이 구동)
    private RectTransform barFillRt;
    private RectTransform barTrackRt;
    private Image barMarker;     // 채움 상단 라인
    private RectTransform barMarkerRt;
    private TextMeshProUGUI labelPower;
    private TextMeshProUGUI labelPercent;
    private TMP_FontAsset koreanTmpFont;

    // 채움 인셋 (트랙 내부 여백, px)
    private const float FillInsetX = 4f;
    private const float FillInsetBottom = 4f;
    private const float FillInsetTop = 4f;

    // 뮤트 팔레트 (미니멀 세련): 민트 → 앰버 → 코랄
    private static readonly Color CLow  = new Color(0.44f, 0.80f, 0.62f); // soft mint
    private static readonly Color CMid  = new Color(0.95f, 0.76f, 0.33f); // warm amber
    private static readonly Color CHigh = new Color(0.91f, 0.47f, 0.33f); // coral

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

        // 색상 보간: 민트 → 앰버 → 코랄 (뮤트)
        Color fillColor = value < 0.5f
            ? Color.Lerp(CLow, CMid, value * 2f)
            : Color.Lerp(CMid, CHigh, (value - 0.5f) * 2f);
        barFill.color = fillColor;

        // 채움 높이 (RectTransform 구동 — 둥근 알약이 차오름)
        float trackH = barTrackRt.rect.height;
        float fillRange = Mathf.Max(0f, trackH - FillInsetTop - FillInsetBottom);
        float fillH = value * fillRange;
        barFillRt.sizeDelta = new Vector2(barFillRt.sizeDelta.x, fillH);

        // 마커: 채움 상단에 위치 (트랙 로컬 기준, 하단 원점)
        float markerY = FillInsetBottom + fillH;
        barMarkerRt.anchoredPosition = new Vector2(0f, markerY);
        barMarker.color = new Color(1f, 1f, 1f, value > 0.02f ? 0.9f : 0f);

        // 퍼센트 표시 + 값 색상 살짝 반영
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
        // 전용 Canvas (World Space — 보드 좌측 안쪽 배치). 위치/스케일은 기존 유지.
        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("GaugeCanvas");
            canvasGo.transform.SetParent(transform);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 99;
            canvas.worldCamera = Camera.main;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.position = new Vector3(-7.0f, -5.8f, -0.1f);
            canvasRt.sizeDelta = new Vector2(60f, 500f);
            canvasRt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }

        // 둥근 스프라이트 (9-slice) — 트랙/채움 공용
        var roundedSprite = MakeRoundedSprite(48, 18);

        barRoot = new GameObject("BarRoot", typeof(RectTransform));
        barRoot.transform.SetParent(canvas.transform, false);
        var rootRt = barRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = new Vector2(7f, 34f);   // 하단 여유 (퍼센트)
        rootRt.offsetMax = new Vector2(-7f, -6f);

        // 트랙 (둥근 배경) — 어두운 반투명 + 소프트 섀도우 + 얇은 아웃라인
        var trackGo = new GameObject("BarTrack", typeof(RectTransform));
        trackGo.transform.SetParent(barRoot.transform, false);
        barTrack = trackGo.AddComponent<Image>();
        barTrack.sprite = roundedSprite;
        barTrack.type = Image.Type.Sliced;
        barTrack.color = new Color(0.11f, 0.13f, 0.18f, 0.72f);
        barTrackRt = trackGo.GetComponent<RectTransform>();
        barTrackRt.anchorMin = Vector2.zero;
        barTrackRt.anchorMax = Vector2.one;
        barTrackRt.sizeDelta = Vector2.zero;

        var trackShadow = trackGo.AddComponent<Shadow>();
        trackShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        trackShadow.effectDistance = new Vector2(2f, -2f);
        var trackOutline = trackGo.AddComponent<Outline>();
        trackOutline.effectColor = new Color(1f, 1f, 1f, 0.14f);
        trackOutline.effectDistance = new Vector2(1f, 1f);

        // 채움 (둥근 알약, 하단 앵커 · 높이=sizeDelta.y 구동)
        // anchorMin/Max=(0,0)/(1,0): 가로 스트레치, 세로는 하단 고정.
        //   rect.width = parentWidth + sizeDelta.x  → sizeDelta.x = -2*inset 로 좌우 인셋
        //   rect.height = sizeDelta.y               → SetValue에서 구동
        //   anchoredPosition.y = 하단 오프셋 (pivot이 바닥이라 바닥 위치)
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(trackGo.transform, false);
        barFill = fillGo.AddComponent<Image>();
        barFill.sprite = roundedSprite;
        barFill.type = Image.Type.Sliced;
        barFill.color = CLow;
        barFillRt = fillGo.GetComponent<RectTransform>();
        barFillRt.anchorMin = new Vector2(0f, 0f);
        barFillRt.anchorMax = new Vector2(1f, 0f);
        barFillRt.pivot = new Vector2(0.5f, 0f);
        barFillRt.sizeDelta = new Vector2(-2f * FillInsetX, 0f);
        barFillRt.anchoredPosition = new Vector2(0f, FillInsetBottom);

        // 마커 (채움 상단 라인) — 하단 앵커, center pivot, y=SetValue에서 갱신
        var markerGo = new GameObject("Marker", typeof(RectTransform));
        markerGo.transform.SetParent(trackGo.transform, false);
        barMarker = markerGo.AddComponent<Image>();
        barMarker.color = new Color(1f, 1f, 1f, 0f);
        barMarkerRt = markerGo.GetComponent<RectTransform>();
        barMarkerRt.anchorMin = new Vector2(0f, 0f);
        barMarkerRt.anchorMax = new Vector2(1f, 0f);
        barMarkerRt.pivot = new Vector2(0.5f, 0.5f);
        barMarkerRt.sizeDelta = new Vector2(-2f * FillInsetX, 3f);
        barMarkerRt.anchoredPosition = new Vector2(0f, FillInsetBottom);

        // "POWER" 레이블 — Canvas 직속, 바 상단
        var powerGo = new GameObject("LabelPower", typeof(RectTransform));
        powerGo.transform.SetParent(canvas.transform, false);
        labelPower = powerGo.AddComponent<TextMeshProUGUI>();
        labelPower.text = "POWER";
        labelPower.enableAutoSizing = true;
        labelPower.fontSizeMin = 10;
        labelPower.fontSizeMax = 18;
        labelPower.fontStyle = FontStyles.Bold;
        labelPower.characterSpacing = 8f;           // 자간 → 세련
        labelPower.color = new Color(0.96f, 0.97f, 1f, 0.92f);
        labelPower.alignment = TextAlignmentOptions.Center;
        labelPower.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) labelPower.font = koreanTmpFont;
        var powerShadow = powerGo.AddComponent<Shadow>();
        powerShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        powerShadow.effectDistance = new Vector2(1f, -1f);
        var powerRt = powerGo.GetComponent<RectTransform>();
        powerRt.anchorMin = new Vector2(0.5f, 1f);
        powerRt.anchorMax = new Vector2(0.5f, 1f);
        powerRt.pivot = new Vector2(0.5f, 0f);
        powerRt.sizeDelta = new Vector2(220f, 30f);
        powerRt.anchoredPosition = new Vector2(0f, 4f);

        // 퍼센트 레이블 — barRoot 자식 (바 내부 하단, show/hide 종속)
        var pctGo = new GameObject("LabelPercent", typeof(RectTransform));
        pctGo.transform.SetParent(barRoot.transform, false);
        labelPercent = pctGo.AddComponent<TextMeshProUGUI>();
        labelPercent.text = "0%";
        labelPercent.enableAutoSizing = true;
        labelPercent.fontSizeMin = 10;
        labelPercent.fontSizeMax = 18;
        labelPercent.fontStyle = FontStyles.Bold;
        labelPercent.color = new Color(0.96f, 0.97f, 1f, 0.95f);
        labelPercent.alignment = TextAlignmentOptions.Center;
        labelPercent.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) labelPercent.font = koreanTmpFont;
        var pctShadow = pctGo.AddComponent<Shadow>();
        pctShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        pctShadow.effectDistance = new Vector2(1f, -1f);
        var pctRt = pctGo.GetComponent<RectTransform>();
        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(1f, 0f);
        pctRt.pivot = new Vector2(0.5f, 1f);
        pctRt.sizeDelta = new Vector2(0f, 26f);
        pctRt.anchoredPosition = new Vector2(0f, -2f);
    }

    /// <summary>둥근 모서리 9-slice 스프라이트 런타임 생성 (흰색, 색은 Image.color로 틴트).</summary>
    private static Sprite MakeRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 라운드 사각형 signed-distance: 모서리에서만 곡률 적용 + 1px 안티에일리어싱
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius) // 9-slice border
        );
    }
}
