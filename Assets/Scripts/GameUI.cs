using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas+TMP 기반 통합 게임 UI.
/// 가이드 텍스트, 진행도 도트, 전환 연출 (인트로/클리어/실패/올클리어).
/// GameManager에서 직접 호출 (풀링 아님).
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;

    [Header("Guide Text")]
    private TextMeshProUGUI guideText;
    private Image guideBackground;
    private CanvasGroup guideGroup;

    [Header("Progress Dots")]
    private Image[] progressDots = new Image[5];
    private TextMeshProUGUI[] dotNumbers = new TextMeshProUGUI[5];

    [Header("Overlay")]
    private Image overlayBg;
    private TextMeshProUGUI overlayMainText;
    private TextMeshProUGUI overlaySubText;
    private CanvasGroup overlayGroup;

    private Coroutine guideCoroutine;
    private Coroutine overlayCoroutine;
    private TMP_FontAsset koreanTmpFont;
    private static Sprite circleSprite; // 진행 도트용 원형 스프라이트 (공유)

    private TextMeshProUGUI pauseHudLabel; // v10 다국어: 우상단 "중지" HUD 라벨

    private GameObject compositionHeader; // v12: Stage 4 "공기 구성" 헤더 (순서대로 잡기)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Init();
    }

    private void OnEnable()
    {
        // v10 다국어: 언어 전환 시 정적 HUD 라벨 갱신
        LocalizationManager.OnLanguageChanged += RefreshLocalized;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalized;
        StopOverlay();
        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }
    }

    /// <summary>런타임 오버레이 멘트는 코루틴 실행 시점에 L()로 조회되므로,
    /// 여기서는 빌드 시 1회 설정되는 정적 HUD 라벨(중지)만 갱신한다.</summary>
    private void RefreshLocalized()
    {
        if (pauseHudLabel != null)
            pauseHudLabel.text = LocalizationManager.L("hud.pause");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 런타임 생성 텍스처 해제
        if (circleSprite != null && circleSprite.texture != null)
        {
            Destroy(circleSprite.texture);
            circleSprite = null;
        }
    }

    private void Init()
    {
        koreanTmpFont = KoreanFont.GetTMP();

        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
                canvas = CreateCanvas();
        }

        CreateProgressDots(); // v9(260703): 단계 프로그레스 도트 부활
        CreateGuideText();
        CreateOverlay();
        CreatePauseButton();
        CreateCompositionHeader(); // v12: Stage 4 "공기 구성" 헤더 (기본 숨김)
    }

    // ==========================================================
    // Canvas 구조 코드 생성
    // ==========================================================

    private Canvas CreateCanvas()
    {
        var canvasGo = new GameObject("GameUICanvas");
        canvasGo.transform.SetParent(transform);

        var c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 100;

        // World Space Canvas: 카메라(z=-10)와 게임 오브젝트(z=0) 사이에 배치
        // 카메라: position=(0,-1.5,-10), ortho size=7, viewport width≈0.3*aspect
        // 가시 영역: Y = -1.5 ± 7 → 높이=14 units, X ≈ ±3.73 → 너비≈7.5 units
        // localScale=0.01로 설정하여 sizeDelta를 픽셀 기준(750×1400)으로 유지
        // → TMP fontSize(pt 단위)가 Screen Space와 동일하게 렌더됨
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(0f, -1.5f, -1f);
        rt.sizeDelta = new Vector2(2500f, 1400f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f); // 2500px × 0.01 = 25 world units

        canvasGo.AddComponent<GraphicRaycaster>();
        c.worldCamera = Camera.main;
        return c;
    }

    // v18: UI 시안(Figma Stage_example 572:501) 실측 배치.
    // 작은 원 60 네 개(1~4단) + **큰 원 80 하나(5단=꺾기)**. 5단만 크게 그려서
    // "마지막이 다르다"를 표시 자체로 알린다 — 이전의 별표 하나보다 눈에 먼저 들어온다.
    private const float DotsW = 384f, DotsH = 80f, DotsCx = 960f, DotsCy = 90f;
    private const float DotSmall = 60f, DotLarge = 80f, DotStep = 76f;

    /// <summary>시안 원 60/80은 **면 기준**이다 — 어두운 테두리가 그 바깥에 그려진다.
    /// 스프라이트와 요소를 함께 그만큼 키워야 면적이 시안과 같아진다.
    /// 중심은 그대로라 배치 계산에는 원래 크기를 쓴다.</summary>
    private static int DotPx(float designSize) => Mathf.RoundToInt(designSize + UISkin.Outset * 2f);

    private void CreateProgressDots()
    {
        var container = CreateUIObject("ProgressDots", canvas.transform);
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(UISkin.GamePx(DotsW), UISkin.GamePx(DotsH));
        rt.anchoredPosition = new Vector2(UISkin.GamePx(DotsCx - 960f), UISkin.GamePx(540f - DotsCy));

        for (int i = 0; i < 5; i++)
        {
            bool last = (i == 4);
            float size = last ? DotLarge : DotSmall;

            var dot = CreateUIObject($"Dot_{i}", container.transform);
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0f, 1f);
            // pivot을 중앙으로 — 현재 단을 키울 때 모서리 기준이면 원이 오른아래로 밀린다.
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(UISkin.GamePx(DotPx(size)), UISkin.GamePx(DotPx(size)));
            // 크기가 달라도 세로 중심은 같다(작은 원 10+30, 큰 원 0+40 → 둘 다 40)
            dotRt.anchoredPosition = new Vector2(UISkin.GamePx(i * DotStep + size * 0.5f),
                                                 -UISkin.GamePx(DotsH * 0.5f));

            var img = dot.AddComponent<Image>();
            img.sprite = UISkin.Dot(DotPx(size), false);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            progressDots[i] = img;

            var numGo = CreateUIObject("Num", dot.transform);
            var numRt = numGo.GetComponent<RectTransform>();
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.offsetMin = numRt.offsetMax = Vector2.zero;

            var num = numGo.AddComponent<TextMeshProUGUI>();
            num.text = (i + 1).ToString();
            num.fontSize = UISkin.GamePx(last ? 30f : 24f);
            num.alignment = TextAlignmentOptions.Center;
            num.raycastTarget = false;
            if (koreanTmpFont != null) num.font = koreanTmpFont;
            dotNumbers[i] = num;
            // 기본 배색을 여기서 미리 입힌다 — UpdateProgressDots가 처음 불릴 때까지
            // TMP 기본색(흰색)이면 하늘색 원 위에서 숫자가 보이지 않는다.
            ApplyNumberGradient(num, UISkin.DotNumTop, UISkin.DotNumBottom);
        }
    }

    /// <summary>단 숫자의 세로 그라디언트. 시안은 원 색에 따라 숫자 색도 함께 바뀐다.</summary>
    private static void ApplyNumberGradient(TextMeshProUGUI tmp, Color32 top, Color32 bottom)
    {
        if (tmp == null) return;
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(top, top, bottom, bottom);
    }

    private void CreateGuideText()
    {
        var container = CreateUIObject("GuideContainer", canvas.transform);
        guideGroup = container.AddComponent<CanvasGroup>();
        var rt = container.GetComponent<RectTransform>();
        // v18: 시안 실측 — 759×58, 화면 정중앙(중심 y=540). 하단에 두면 새 가로 게이지와 겹치고,
        // 중앙은 돗자리 뒷변(design y≈679)보다 위라 **놀이판을 가리지 않는다**.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(UISkin.GamePx(759f), UISkin.GamePx(58f));

        // 배경
        var bgGo = CreateUIObject("GuideBg", container.transform);
        guideBackground = bgGo.AddComponent<Image>();
        guideBackground.color = UISkin.GuideBackdrop;   // 시안 #5A717F 50%
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 텍스트
        var textGo = CreateUIObject("GuideText", container.transform);
        guideText = textGo.AddComponent<TextMeshProUGUI>();
        guideText.fontSize = UISkin.GamePx(30f);   // 시안 30px
        guideText.color = Color.white;
        guideText.alignment = TextAlignmentOptions.Center;
        guideText.textWrappingMode = TextWrappingModes.NoWrap;
        guideText.overflowMode = TextOverflowModes.Truncate;
        // 문구 길이가 언어마다 다르다 — 띠 밖으로 넘치느니 줄어드는 편이 낫다.
        guideText.enableAutoSizing = true;
        guideText.fontSizeMin = UISkin.GamePx(20f);
        guideText.fontSizeMax = UISkin.GamePx(30f);
        if (koreanTmpFont != null) guideText.font = koreanTmpFont;
        // 외곽선은 쓰지 않는다(시안에도 없다). 이 크기의 나눔고딕은 획이 얇아서
        // 외곽선을 넣으면 흰 심이 깎여 오히려 회색으로 보인다 — 대비는 판 쪽에서 만든다.
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        guideGroup.alpha = 0f;
    }

    private void CreateOverlay()
    {
        var container = CreateUIObject("Overlay", canvas.transform);
        overlayGroup = container.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // World Space Canvas: viewport 충돌 없으므로 앵커를 전체(0~1)로 복원
        var bgGo = CreateUIObject("OverlayBg", container.transform);
        overlayBg = bgGo.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 메인 텍스트 — Canvas 중앙 기준으로 복원
        var mainGo = CreateUIObject("OverlayMain", container.transform);
        overlayMainText = mainGo.AddComponent<TextMeshProUGUI>();
        overlayMainText.enableAutoSizing = true;
        overlayMainText.fontSizeMin = 60;
        overlayMainText.fontSizeMax = 200;
        overlayMainText.fontStyle = FontStyles.Bold;
        overlayMainText.alignment = TextAlignmentOptions.Center;
        overlayMainText.textWrappingMode = TextWrappingModes.NoWrap;
        overlayMainText.overflowMode = TextOverflowModes.Truncate;
        if (koreanTmpFont != null) overlayMainText.font = koreanTmpFont;
        var mainRt = mainGo.GetComponent<RectTransform>();
        mainRt.anchorMin = new Vector2(0f, 0.35f);
        mainRt.anchorMax = new Vector2(1f, 0.65f);
        mainRt.sizeDelta = Vector2.zero;

        // 서브 텍스트
        var subGo = CreateUIObject("OverlaySub", container.transform);
        overlaySubText = subGo.AddComponent<TextMeshProUGUI>();
        overlaySubText.fontSize = 56;
        overlaySubText.alignment = TextAlignmentOptions.Center;
        overlaySubText.textWrappingMode = TextWrappingModes.NoWrap;
        overlaySubText.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) overlaySubText.font = koreanTmpFont;
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0.2f);
        subRt.anchorMax = new Vector2(1f, 0.35f);
        subRt.sizeDelta = Vector2.zero;
    }

    private void CreatePauseButton()
    {
        var btnGo = new GameObject("PauseButton");
        btnGo.transform.SetParent(canvas.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        // v18: 시안 실측 — 180×80 @(1690,50). 이전에는 우상단 500×250 **투명** 영역이라
        // 버튼이 있는지 알 수 없었고, 그 넓이 때문에 지나가다 잘못 눌리기도 했다.
        btnRect.anchorMin = btnRect.anchorMax = btnRect.pivot = new Vector2(0.5f, 0.5f);
        // 시안 180×80은 면 기준 — 테두리가 바깥에 그려진다. 사방으로 넓혀 면적을 맞춘다.
        btnRect.sizeDelta = new Vector2(UISkin.GamePx(180f + UISkin.Outset * 2f),
                                        UISkin.GamePx(80f + UISkin.Outset * 2f));
        btnRect.anchoredPosition = new Vector2(UISkin.GamePx(1690f + 90f - 960f),
                                               UISkin.GamePx(540f - (50f + 40f)));

        var img = btnGo.AddComponent<Image>();
        img.sprite = UISkin.Raised;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.spriteState = new SpriteState
        {
            highlightedSprite = UISkin.RaisedHover,
            pressedSprite     = UISkin.Sunken,
            selectedSprite    = UISkin.RaisedHover,
            disabledSprite    = UISkin.Raised,
        };
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => PauseMenuUI.Instance?.Toggle());

        // 호버 시 중지 가리킴 포즈 🖕 (열받게!)
        var hover = btnGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointMiddle;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = LocalizationManager.L("hud.pause");
        tmp.fontSize = UISkin.GamePx(40f);   // 시안 라벨 40px
        tmp.color = UISkin.Ink;              // 밝은 면 위 → 흰 글자는 안 읽힌다
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanTmpFont != null) tmp.font = koreanTmpFont;
        pauseHudLabel = tmp;
    }

    // ==========================================================
    // v12: Stage 4 "공기 구성" 헤더 (순서대로 잡기 — Figma 260710)
    // 좌상단에 번호별 색 돌 4개(Figma 배열 4·1·3·2) + 던지는 공(검정) 표시.
    // 색상은 SequencePalette(SequenceGimmick.cs) 공유 SOT. Sequence 스테이지에서만 노출.
    // ==========================================================

    private void CreateCompositionHeader()
    {
        var container = CreateUIObject("CompositionHeader", canvas.transform);
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -24f);
        rt.sizeDelta = new Vector2(560f, 150f);

        // 반투명 배경 패널 (오피스 씬 위 가독성 확보)
        var bgGo = CreateUIObject("Bg", container.transform);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.30f);
        bg.raycastTarget = false;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // "공기 구성" 타이틀
        var titleGo = CreateUIObject("Title", container.transform);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "공기 구성";
        title.fontSize = 26;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Left;
        title.raycastTarget = false;
        if (koreanTmpFont != null) title.font = koreanTmpFont;
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(0f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(18f, -10f);
        titleRt.sizeDelta = new Vector2(300f, 34f);

        // 번호 돌 칩 (Figma 배열 순서: 4, 1, 3, 2)
        int[] displayOrder = { 4, 1, 3, 2 };
        const float chipY = -52f;
        for (int i = 0; i < displayOrder.Length; i++)
            CreateStoneChip(container.transform, displayOrder[i], 18f + i * 88f, chipY);

        // 던지는 공 칩 (검정)
        CreateThrowChip(container.transform, 18f + 4 * 88f + 8f, chipY);

        compositionHeader = container;
        compositionHeader.SetActive(false); // 기본 숨김 — Sequence 스테이지에서만 표시
    }

    private void CreateStoneChip(Transform parent, int number, float x, float y)
    {
        const float d = 68f;
        var chipGo = CreateUIObject($"Chip_{number}", parent);
        var img = chipGo.AddComponent<Image>();
        img.color = SequencePalette.NumberColors[number];
        img.raycastTarget = false;
        MakeCircle(img);
        var rt = chipGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(d, d);
        rt.anchoredPosition = new Vector2(x, y);

        var numGo = CreateUIObject("Num", chipGo.transform);
        var t = numGo.AddComponent<TextMeshProUGUI>();
        t.text = number.ToString();
        t.fontSize = 34;
        t.fontStyle = FontStyles.Bold;
        // 노랑(3)은 흰 글씨 대비가 낮아 어두운 글씨로 처리
        t.color = number == 3 ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (koreanTmpFont != null) t.font = koreanTmpFont;
        var nrt = numGo.GetComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero;
        nrt.anchorMax = Vector2.one;
        nrt.sizeDelta = Vector2.zero;
    }

    private void CreateThrowChip(Transform parent, float x, float y)
    {
        var chipGo = CreateUIObject("ThrowChip", parent);
        var img = chipGo.AddComponent<Image>();
        img.color = SequencePalette.ThrowBall;
        img.raycastTarget = false;
        var rt = chipGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(132f, 64f);
        rt.anchoredPosition = new Vector2(x, y - 2f);

        var lblGo = CreateUIObject("Label", chipGo.transform);
        var t = lblGo.AddComponent<TextMeshProUGUI>();
        t.text = "던지는 공";
        t.fontSize = 24;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (koreanTmpFont != null) t.font = koreanTmpFont;
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
    }

    /// <summary>Stage 4 "공기 구성" 헤더 표시 (SequenceGimmick.OnStageStart)</summary>
    public void ShowComposition()
    {
        if (compositionHeader != null) compositionHeader.SetActive(true);
    }

    /// <summary>Stage 4 "공기 구성" 헤더 숨김 (SequenceGimmick.OnStageEnd)</summary>
    public void HideComposition()
    {
        if (compositionHeader != null) compositionHeader.SetActive(false);
    }

    // ==========================================================
    // 공개 API — GameManager에서 호출
    // ==========================================================

    /// <summary>하단 안내 텍스트 갱신 (펄스 + 페이드)</summary>
    public void UpdateGuideText(string text)
    {
        if (guideCoroutine != null)
            StopCoroutine(guideCoroutine);
        guideCoroutine = StartCoroutine(DoGuideText(text));
    }

    /// <summary>안내 텍스트 즉시 숨김</summary>
    public void HideGuideText()
    {
        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }
        guideGroup.alpha = 0f;
    }

    /// <summary>상단 진행 도트 갱신</summary>
    public void UpdateProgressDots(int currentStage)
    {
        // v18: 시안대로 **스프라이트 교체**로 상태를 표시한다(클리어=초록, 미클리어=하늘색).
        // 현재 단만 살짝 키운다 — 시안에는 "현재" 표시가 없지만, 그것만으로는
        // 방금 깬 단과 지금 하는 단을 구분할 수 없다. 크기 차이는 1.08로 얕게 둔다
        // (5단 원은 이미 80이라 크게 키우면 이웃과 겹친다).
        for (int i = 0; i < 5; i++)
        {
            if (progressDots[i] == null) continue;
            int stage = i + 1;
            bool cleared = stage < currentStage;
            int size = DotPx((i == 4) ? DotLarge : DotSmall);

            progressDots[i].sprite = UISkin.Dot(size, cleared);
            progressDots[i].color = stage > currentStage ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            progressDots[i].transform.localScale = (stage == currentStage) ? Vector3.one * 1.08f : Vector3.one;

            ApplyNumberGradient(dotNumbers[i],
                cleared ? UISkin.DotNumClearTop : UISkin.DotNumTop,
                cleared ? UISkin.DotNumClearBottom : UISkin.DotNumBottom);
        }
    }

    /// <summary>"N단" / "꺾기" 중앙 인트로 연출</summary>
    public void ShowStageIntro(int stage)
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoStageIntro(stage));
    }

    /// <summary>"CLEAR!" 연출</summary>
    public void ShowClear()
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoClear());
    }

    /// <summary>"FAIL" + 실패 사유 연출</summary>
    public void ShowFail(string reason)
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoFail(reason));
    }

    /// <summary>"ALL CLEAR!" + "탭하여 다시 시작"</summary>
    public void ShowAllClear()
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoAllClear());
    }

    /// <summary>v9(260703): Credit 롤 (역할×4). 완료 후 onComplete 호출.</summary>
    public void ShowCredit(System.Action onComplete)
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoCredit(onComplete));
    }

    /// <summary>회귀 연출: Fade Out → "인생을 다시 시작합니다" → Fade In</summary>
    public void ShowRegressionTransition()
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoRegressionTransition());
    }

    private IEnumerator DoRegressionTransition()
    {
        // 1. Fade Out (0.5초) — 검은 배경 알파 0→1
        overlayMainText.text = "";
        overlaySubText.text = "";
        overlayGroup.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / 0.5f);
            overlayBg.color = new Color(0, 0, 0, a);
            yield return null;
        }
        overlayBg.color = new Color(0, 0, 0, 1f);

        // 2. 중앙 텍스트 표시 (1.5초 유지)
        overlayMainText.text = LocalizationManager.L("result.restart_life");
        overlayMainText.color = new Color(1f, 1f, 1f, 0.9f);
        overlayMainText.fontSize = 70;
        yield return new WaitForSeconds(1.5f);

        // 3. 텍스트 숨기기
        overlayMainText.text = "";

        // 4. Fade In (0.5초) — 검은 배경 알파 1→0
        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(elapsed / 0.5f);
            overlayBg.color = new Color(0, 0, 0, a);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayBg.color = new Color(0, 0, 0, 0);
        overlayCoroutine = null;
    }

    /// <summary>오버레이 즉시 숨김 (ALL CLEAR 탭 재시작 시)</summary>
    public void HideOverlay()
    {
        StopOverlay();
        overlayGroup.alpha = 0f;
    }

    // ==========================================================
    // 코루틴 연출
    // ==========================================================

    private IEnumerator DoGuideText(string text)
    {
        guideText.text = text;
        guideGroup.alpha = 1f;

        // 펄스: 0.3초간 1.5배 → 1배
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(1.5f, 1f, elapsed / 0.3f);
            guideText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        guideText.transform.localScale = Vector3.one;

        // 3초 유지
        yield return new WaitForSeconds(3f);

        // 알파 감소 — v18: 0.5 → 0.85.
        // 0.5는 예전 **검은** 판에 맞춘 값이었다(25%만 남아도 배경이 어두워져 글자가 떴다).
        // 시안의 회청색 판은 같은 0.5를 곱하면 밝은 창문 위에서 판도 글자도 사라진다.
        // 시안 색·알파를 그대로 두는 대신 "물러나는 정도"만 낮춘다 — 힌트는 계속 읽혀야 한다.
        guideGroup.alpha = 0.85f;
        guideCoroutine = null;
    }

    private IEnumerator DoStageIntro(int stage)
    {
        // v13: "준비하세요" 완전 제거 (v11 결정 완결). 일반 스테이지는 아무것도 표시하지 않음.
        if (stage != 5)
        {
            overlayGroup.alpha = 0f;
            overlayCoroutine = null;
            yield break;
        }

        // Stage 5: '꺾기' 단 안내만 유지.
        Color mainColor = new Color(1f, 0.84f, 0f, 1f);
        overlayMainText.text = LocalizationManager.L("stage.fold");
        overlayMainText.color = mainColor;
        overlayMainText.fontSize = 80;
        overlaySubText.text = "";
        overlaySubText.color = new Color(1f, 1f, 1f, 0.8f);
        overlayBg.color = new Color(0, 0, 0, 0.4f);
        overlayGroup.alpha = 1f;

        float holdTime = 0.5f;
        float fadeTime = 1.5f;

        // 홀드
        yield return new WaitForSeconds(holdTime);

        // 페이드 아웃
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            overlayMainText.color = new Color(mainColor.r, mainColor.g, mainColor.b, alpha);
            overlaySubText.color = new Color(1f, 1f, 1f, alpha * 0.8f);
            overlayBg.color = new Color(0, 0, 0, 0.4f * alpha);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoClear()
    {
        overlayMainText.text = "CLEAR!";
        overlayMainText.fontSize = 80;
        overlaySubText.text = "";
        overlayBg.color = new Color(0, 0, 0, 0.3f);
        overlayGroup.alpha = 1f;

        // 금색 밝기 펄스
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.8f + 0.2f * Mathf.Sin(elapsed * 4f);
            overlayMainText.color = new Color(1f, 0.84f, 0f, pulse);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoFail(string reason)
    {
        overlayMainText.text = "FAIL";
        overlayMainText.fontSize = 80;
        overlayMainText.color = new Color(1f, 0.27f, 0.27f, 0f);
        overlaySubText.text = "";
        overlaySubText.color = Color.clear;
        overlayBg.color = new Color(0, 0, 0, 0);
        overlayGroup.alpha = 1f;

        // 빨간 flash (0~0.3초)
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float flashAlpha = elapsed < 0.15f
                ? Mathf.Lerp(0f, 0.4f, elapsed / 0.15f)
                : Mathf.Lerp(0.4f, 0f, (elapsed - 0.15f) / 0.15f);
            overlayBg.color = new Color(1f, 0f, 0f, flashAlpha);

            float textAlpha = Mathf.Clamp01(elapsed / 0.2f);
            overlayMainText.color = new Color(1f, 0.27f, 0.27f, textAlpha);
            yield return null;
        }

        overlayBg.color = new Color(0, 0, 0, 0);
        overlayMainText.color = new Color(1f, 0.27f, 0.27f, 1f);

        // 실패 사유 (0.3초 후 페이드인)
        if (!string.IsNullOrEmpty(reason))
        {
            overlaySubText.text = reason;
            elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / 0.2f);
                overlaySubText.color = new Color(1f, 1f, 1f, a * 0.9f);
                yield return null;
            }
        }

        // 나머지 시간 유지 (~1.0초)
        yield return new WaitForSeconds(1.0f);

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoAllClear()
    {
        overlayBg.color = new Color(0, 0, 0, 0.7f);
        overlayMainText.text = LocalizationManager.L("ending.mainment");
        overlayMainText.fontSize = 80;

        // [Online] 클리어 시간 표시
        float clearTime = GameSession.Instance != null ? GameSession.Instance.ElapsedTime : 0f;
        // v10: 기획 형식 00:00:00 (HH:MM:SS)
        int ch = (int)(clearTime / 3600);
        int cm = (int)((clearTime % 3600) / 60);
        int cs = (int)(clearTime % 60);
        string timeStr = $"{ch:00}:{cm:00}:{cs:00}";

        // v9(260703): 조선시대 놀림 톤 → 진지한 위로. "이번 생은 여기까지 입니다 / 수고하셨습니다"
        overlaySubText.text = $"{LocalizationManager.L("ending.thanks")}\n\n<size=80%>{LocalizationManager.LF("ending.record", timeStr)}</size>";
        overlaySubText.enableAutoSizing = false;
        overlaySubText.fontSize = 36;
        overlaySubText.textWrappingMode = TextWrappingModes.Normal;
        overlayGroup.alpha = 1f;

        // 무한 펄스 (외부에서 HideOverlay로 종료)
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(elapsed * Mathf.PI);
            overlayMainText.color = new Color(1f, 0.84f, 0f, pulse);

            float blink = Mathf.Sin(elapsed * 3f) > 0f ? 0.7f : 0.3f;
            overlaySubText.color = new Color(1f, 1f, 1f, blink);
            yield return null;
        }
    }

    private IEnumerator DoCredit(System.Action onComplete)
    {
        StopOverlay();
        overlayBg.color = new Color(0f, 0f, 0f, 1f); // 암전
        overlayGroup.alpha = 1f;

        overlayMainText.text = LocalizationManager.L("ending.credit");
        overlayMainText.enableAutoSizing = false;
        overlayMainText.fontSize = 64;
        overlayMainText.color = new Color(1f, 1f, 1f, 0f);

        // v9(260703): 역할×4. 실제 이름은 추후 교체(placeholder).
        // v16 정렬 수정: overlaySubText는 Center 정렬이라 <pos=%>가 줄마다 어긋난다.
        // <mspace>로 전 문자 고정폭 → 공백 패딩만으로 이름 열이 정확히 맞음 (가변폭 깨짐 해결).
        overlaySubText.text =
            "<mspace=0.62em>Game Design   ___\n" +
            "Art           ___\n" +
            "Programming   ___\n" +
            "Sound         ___</mspace>";
        overlaySubText.enableAutoSizing = false;
        overlaySubText.fontSize = 30;
        overlaySubText.textWrappingMode = TextWrappingModes.Normal;
        overlaySubText.color = new Color(0.9f, 0.9f, 0.9f, 0f);

        // 페이드인
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / 0.6f);
            overlayMainText.color = new Color(1f, 1f, 1f, a);
            overlaySubText.color = new Color(0.9f, 0.9f, 0.9f, a);
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        // 페이드아웃
        t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / 0.6f);
            overlayMainText.color = new Color(1f, 1f, 1f, a);
            overlaySubText.color = new Color(0.9f, 0.9f, 0.9f, a);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayBg.color = new Color(0f, 0f, 0f, 0f);
        overlayMainText.enableAutoSizing = true; // 원복 (다음 연출 대비)
        overlayCoroutine = null;
        onComplete?.Invoke();
    }

    // ==========================================================
    // 유틸리티
    // ==========================================================

    private void StopOverlay()
    {
        if (overlayCoroutine != null)
        {
            StopCoroutine(overlayCoroutine);
            overlayCoroutine = null;
        }
        // 방어적 리셋 — 코루틴이 중단될 때 색상값이 남아 잔상 발생하므로
        // overlayGroup.alpha 뿐 아니라 개별 색상도 즉시 초기화.
        if (overlayGroup != null)
            overlayGroup.alpha = 0f;
        if (overlayBg != null)
            overlayBg.color = new Color(0, 0, 0, 0);
        if (overlayMainText != null)
            overlayMainText.color = Color.white;
        if (overlaySubText != null)
            overlaySubText.color = Color.white;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void MakeCircle(Image img)
    {
        if (circleSprite == null)
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 1;

            for (int px = 0; px < size; px++)
                for (int py = 0; py < size; py++)
                {
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(center, center));
                    tex.SetPixel(px, py, dist <= radius ? Color.white : Color.clear);
                }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        img.sprite = circleSprite;
    }
}
