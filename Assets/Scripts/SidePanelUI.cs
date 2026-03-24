using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 왼쪽 여백(viewport 밖)에 게임 상태 표시.
/// Screen Space Overlay Canvas (GaugeBarUI 패턴).
/// sortingOrder=90 (GaugeBarUI=99보다 낮게).
/// </summary>
public class SidePanelUI : MonoBehaviour
{
    public static SidePanelUI Instance { get; private set; }

    // UI 참조
    private Canvas canvas;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI loopValueLabel;
    private TextMeshProUGUI stageValueLabel;
    private Image[] stageDots;
    private TextMeshProUGUI ageValueLabel;
    private Image ageProgressFill;
    private TextMeshProUGUI timeLabel;

    private TMP_FontAsset koreanTmpFont;

    // 시간 갱신 최적화
    private int lastDisplaySeconds = -1;

    // 깜빡임 코루틴
    private Coroutine currentDotBlinkCoroutine;

    // 도트 색상 상수
    private static readonly Color ColorCleared = new Color(1f, 0.84f, 0f, 0.9f);    // 금색
    private static readonly Color ColorCurrent = Color.white;
    private static readonly Color ColorPending = new Color(0.5f, 0.5f, 0.5f, 0.4f); // 회색

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

    private void Update()
    {
        if (GameSession.Instance == null) return;

        // 시간 갱신 최적화: 표시된 초가 바뀔 때만 TMP 갱신
        int displaySeconds = Mathf.FloorToInt(GameSession.Instance.ElapsedTime);
        if (displaySeconds != lastDisplaySeconds)
        {
            lastDisplaySeconds = displaySeconds;
            int min = displaySeconds / 60;
            int sec = displaySeconds % 60;
            if (timeLabel != null)
                timeLabel.text = $"{min:D2}:{sec:D2}";
        }
    }

    /// <summary>GameSession 데이터를 읽어 모든 UI 갱신.</summary>
    public void Refresh()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        if (nameLabel != null)
            nameLabel.text = session.PlayerName;

        if (loopValueLabel != null)
            loopValueLabel.text = $"{session.CurrentLoop} / 10";

        if (stageValueLabel != null)
            stageValueLabel.text = $"{session.CurrentStageInLoop}단";

        if (ageValueLabel != null)
            ageValueLabel.text = $"{session.CurrentAge}살";

        if (ageProgressFill != null)
            ageProgressFill.fillAmount = session.CurrentAge / 50f;

        UpdateStageDots(session.CurrentStageInLoop);
    }

    private void UpdateStageDots(int currentStage)
    {
        if (stageDots == null) return;

        // 이전 깜빡임 코루틴 중단
        if (currentDotBlinkCoroutine != null)
        {
            StopCoroutine(currentDotBlinkCoroutine);
            currentDotBlinkCoroutine = null;
        }

        for (int i = 0; i < stageDots.Length; i++)
        {
            if (stageDots[i] == null) continue;

            int stageNum = i + 1;
            if (stageNum < currentStage)
            {
                // 클리어된 단계: 금색
                stageDots[i].color = ColorCleared;
            }
            else if (stageNum == currentStage)
            {
                // 현재 단계: 흰색 + 깜빡임
                stageDots[i].color = ColorCurrent;
                currentDotBlinkCoroutine = StartCoroutine(BlinkDot(stageDots[i]));
            }
            else
            {
                // 미완료: 회색
                stageDots[i].color = ColorPending;
            }
        }
    }

    private IEnumerator BlinkDot(Image dot)
    {
        while (true)
        {
            // alpha 1.0 → 0.4
            var c = dot.color;
            c.a = 0.4f;
            dot.color = c;
            yield return new WaitForSeconds(0.5f);

            // alpha 0.4 → 1.0
            c.a = 1.0f;
            dot.color = c;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void CreateUI()
    {
        // 독립 Canvas (GaugeBarUI Canvas와 분리)
        var canvasGo = new GameObject("SidePanelCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 패널 루트: 왼쪽 전체 높이, 너비 200px
        var panelRootGo = new GameObject("PanelRoot", typeof(RectTransform));
        panelRootGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelRootGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.sizeDelta = new Vector2(200f, 0f);
        panelRt.anchoredPosition = new Vector2(20f, 0f);

        // 불투명 배경 — viewport 밖 TMP 잔상 방지
        var bgImage = panelRootGo.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        // VerticalLayoutGroup
        var vlg = panelRootGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(8, 8, 20, 8);

        // --- 이름 레이블 ---
        nameLabel = CreateLabel(panelRootGo.transform, "NameLabel", "Player", 22, FontStyles.Bold, Color.white, 30f);

        // --- 구분선 1 ---
        CreateSeparator(panelRootGo.transform);

        // --- 루프 헤더 ---
        CreateLabel(panelRootGo.transform, "LoopHeader", "루프", 14, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f, 1f), 20f);

        // --- 루프 값 ---
        loopValueLabel = CreateLabel(panelRootGo.transform, "LoopValue", "1 / 10", 28, FontStyles.Bold, Color.white, 38f);

        // --- 단계 헤더 ---
        CreateLabel(panelRootGo.transform, "StageHeader", "단계", 14, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f, 1f), 20f);

        // --- 단계 도트 (가로 배치) ---
        stageDots = CreateStageDots(panelRootGo.transform);

        // --- 단계 값 ---
        stageValueLabel = CreateLabel(panelRootGo.transform, "StageValue", "1단", 16, FontStyles.Normal, Color.white, 24f);

        // --- 구분선 2 ---
        CreateSeparator(panelRootGo.transform);

        // --- 나이 헤더 ---
        CreateLabel(panelRootGo.transform, "AgeHeader", "나이", 14, FontStyles.Normal, new Color(0.7f, 0.7f, 0.7f, 1f), 20f);

        // --- 나이 값 (금색, 크게) ---
        ageValueLabel = CreateLabel(panelRootGo.transform, "AgeValue", "0살", 32, FontStyles.Bold, ColorCleared, 44f);
        // overflowMode=Overflow이면 VerticalLayoutGroup 경계 밖으로 텍스트가 넘쳐
        // 이전 값과 겹쳐 보이는 현상 발생 → Truncate로 제한
        ageValueLabel.overflowMode = TextOverflowModes.Truncate;

        // --- 나이 프로그레스 바 ---
        ageProgressFill = CreateProgressBar(panelRootGo.transform);

        // --- 구분선 3 ---
        CreateSeparator(panelRootGo.transform);

        // --- 경과 시간 (작고 어둡게) ---
        timeLabel = CreateLabel(panelRootGo.transform, "TimeLabel", "00:00", 16, FontStyles.Normal, new Color(0.6f, 0.6f, 0.6f, 1f), 24f);

        Debug.Log("[SidePanelUI] UI created.");
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize,
        FontStyles style, Color color, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth = 1f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) tmp.font = koreanTmpFont;

        return tmp;
    }

    private void CreateSeparator(Transform parent)
    {
        var go = new GameObject("Separator", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f;
        le.flexibleWidth = 1f;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.2f);
    }

    private Image[] CreateStageDots(Transform parent)
    {
        var containerGo = new GameObject("StageDots", typeof(RectTransform));
        containerGo.transform.SetParent(parent, false);

        var le = containerGo.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;
        le.flexibleWidth = 1f;

        var hlg = containerGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var dots = new Image[5];
        var dotSprite = MakeCircleSprite(20);

        for (int i = 0; i < 5; i++)
        {
            var dotGo = new GameObject($"Dot{i + 1}", typeof(RectTransform));
            dotGo.transform.SetParent(containerGo.transform, false);

            var dotLe = dotGo.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 20f;
            dotLe.preferredHeight = 20f;

            var img = dotGo.AddComponent<Image>();
            img.sprite = dotSprite;
            img.color = ColorPending;

            dots[i] = img;
        }

        return dots;
    }

    private Image CreateProgressBar(Transform parent)
    {
        var containerGo = new GameObject("AgeProgressBar", typeof(RectTransform));
        containerGo.transform.SetParent(parent, false);

        var le = containerGo.AddComponent<LayoutElement>();
        le.preferredHeight = 8f;
        le.flexibleWidth = 1f;

        // 배경
        var bgImg = containerGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        var bgRt = containerGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;

        // 채움
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(containerGo.transform, false);

        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = ColorCleared;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0; // 왼쪽에서 오른쪽
        fillImg.fillAmount = 0f;

        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        return fillImg;
    }

    /// <summary>원형 스프라이트 런타임 생성 (GameUI.MakeCircle 패턴).</summary>
    private static Sprite MakeCircleSprite(int radius)
    {
        int size = radius * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx = radius - 0.5f;
        float cy = radius - 0.5f;
        float r2 = (radius - 1f) * (radius - 1f);

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                pixels[y * size + x] = (dx * dx + dy * dy <= r2)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
