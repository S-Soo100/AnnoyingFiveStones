using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI 시안(Figma Settings 572:588 / Dialog 572:441)의 창 부품 공용 빌더.
///
/// 왜 따로 뺐나:
/// 이 코드가 PauseMenuUI 안에만 있었더니, 홈에서 여는 설정창(SettingsPopupUI)은
/// 옛 검정 박스 그대로 남아 있었다. 같은 모양의 창이 두 군데서 각자 그려지면
/// 한쪽만 고치는 일이 반드시 또 생긴다. 창을 그리는 곳은 여기 하나뿐이어야 한다.
///
/// 창 = 어둠막(#000 70%) + 둥근 몸통(#CCD9DD, r12, 테두리 #5A717F)
///      + 머리띠 83(그라디언트, 제목 좌측 44, ✕ 우측)
/// 좌표는 전부 시안 px(1920×1080)이고 UISkin.Px()가 캔버스 단위로 옮긴다.
/// **Screen Space Overlay + referenceResolution 1280×720 캔버스 전용**이다.
/// </summary>
public static class UIWindow
{
    public const float WinW = 680f, HeaderH = 83f;
    public const float TitlePt = 40f, BodyPt = 40f;
    private const float TitleX = 44f, TitleY = 16f;
    private const float CloseX = 610f, CloseY = 20f, CloseSize = 44f;

    // 슬라이더 — 시안 Slide 380×99 (제목 51 + 홈 36)
    public const float SlideW = 380f;
    private const float TrackH = 36f, TrackY = 63f;
    private const float FillInset = 3f, HandleW = 20f;

    /// <summary>창 한 장. 반환값은 화면 전체를 덮는 패널, out window는 창 본체(자식 배치용).</summary>
    public static GameObject Create(Transform parent, string name, string title, float designH,
                                    UnityAction onClose, TMP_FontAsset font,
                                    out RectTransform window, out TextMeshProUGUI titleLabel)
    {
        var panelGo = new GameObject(name);
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelGo.AddComponent<Image>().color = UISkin.WindowDim;

        // 창 몸통
        var winGo = new GameObject("Window");
        winGo.transform.SetParent(panelGo.transform, false);
        window = winGo.AddComponent<RectTransform>();
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(UISkin.Px(WinW), UISkin.Px(designH));
        window.anchoredPosition = Vector2.zero;
        var bodyImg = winGo.AddComponent<Image>();
        bodyImg.sprite = UISkin.WindowBody;
        bodyImg.type = Image.Type.Sliced;

        // 머리띠 — 위 모서리만 둥글다. 아래 테두리선이 몸통과의 구분선이 된다.
        var headGo = new GameObject("Header");
        headGo.transform.SetParent(winGo.transform, false);
        var headRt = headGo.AddComponent<RectTransform>();
        headRt.anchorMin = new Vector2(0f, 1f);
        headRt.anchorMax = new Vector2(1f, 1f);
        headRt.pivot = new Vector2(0.5f, 1f);
        headRt.offsetMin = new Vector2(0f, -UISkin.Px(HeaderH));
        headRt.offsetMax = Vector2.zero;
        var headImg = headGo.AddComponent<Image>();
        headImg.sprite = UISkin.WindowHeader;
        headImg.type = Image.Type.Sliced;

        // 제목 — 시안은 왼쪽 정렬이다(가운데가 아니다)
        titleLabel = Label(headGo.transform, "Title", title, TitlePt, TextAlignmentOptions.Left, font);
        Place(titleLabel.rectTransform, TitleX, TitleY, WinW - TitleX * 2f, 51f);

        // ✕ — 시안에는 회색 자리표시만 있어 아이콘은 코드로 그린다
        var closeGo = new GameObject("Close");
        closeGo.transform.SetParent(headGo.transform, false);
        var closeRt = closeGo.AddComponent<RectTransform>();
        Place(closeRt, CloseX, CloseY, CloseSize, CloseSize);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.sprite = UISkin.CloseIcon;
        closeImg.color = UISkin.Ink;
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        if (onClose != null) closeBtn.onClick.AddListener(onClose);
        closeGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;

        return panelGo;
    }

    /// <summary>부모 좌상단을 원점으로 시안 px 배치.</summary>
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(UISkin.Px(w), UISkin.Px(h));
        rt.anchoredPosition = new Vector2(UISkin.Px(x), -UISkin.Px(y));
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text,
                                        float designPt, TextAlignmentOptions align, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UISkin.Px(designPt);
        tmp.color = UISkin.Ink;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>시안 Slide 380×99 — 제목(가운데 정렬) + 흰 홈 + 파란 채움 + 각진 손잡이.</summary>
    public static Slider MakeSlider(Transform parent, string name, float x, float y,
                                    float initial, UnityAction<float> onChanged,
                                    TMP_FontAsset font, out TextMeshProUGUI label)
    {
        var rowGo = new GameObject(name + "Row", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        Place(rowGo.GetComponent<RectTransform>(), x, y, SlideW, 99f);

        // 시안의 슬라이더 제목만 가운데 정렬이다(머리띠 제목·경고 문구는 왼쪽).
        label = Label(rowGo.transform, "Title", "", BodyPt, TextAlignmentOptions.Center, font);
        Place(label.rectTransform, 0f, 0f, SlideW, 51f);

        var sliderGo = new GameObject(name + "Slider", typeof(RectTransform));
        sliderGo.transform.SetParent(rowGo.transform, false);
        Place(sliderGo.GetComponent<RectTransform>(), 0f, TrackY, SlideW, TrackH);

        var trackImg = sliderGo.AddComponent<Image>();
        trackImg.sprite = UISkin.InsetBoxOf((int)TrackH);
        trackImg.type = Image.Type.Sliced;

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(UISkin.Px(FillInset), UISkin.Px(FillInset));
        fillAreaRt.offsetMax = new Vector2(-UISkin.Px(FillInset), -UISkin.Px(FillInset));

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillGo.AddComponent<Image>().color = UISkin.SliderFill;

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(UISkin.Px(HandleW * 0.5f), 0f);
        handleAreaRt.offsetMax = new Vector2(-UISkin.Px(HandleW * 0.5f), 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(UISkin.Px(HandleW), UISkin.Px(TrackH));
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.sprite = UISkin.SliderHandle;
        handleImg.type = Image.Type.Simple;

        var slider = sliderGo.AddComponent<Slider>();
        slider.targetGraphic = handleImg;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initial;
        if (onChanged != null) slider.onValueChanged.AddListener(onChanged);

        sliderGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;
        return slider;
    }

    /// <summary>시안 Button_outline — 광택 베벨 + 먹색 라벨.</summary>
    public static GameObject MakeButton(string text, Transform parent, UnityAction onClick,
                                        float x, float y, float w, float h,
                                        TMP_FontAsset font, out TextMeshProUGUI label)
    {
        var btnGo = new GameObject("Btn_" + text);
        btnGo.transform.SetParent(parent, false);
        Place(btnGo.AddComponent<RectTransform>(), x, y, w, h);

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
        if (onClick != null) btn.onClick.AddListener(onClick);

        btnGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;

        label = Label(btnGo.transform, "Label", text, BodyPt, TextAlignmentOptions.Center, font);
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        return btnGo;
    }
}
