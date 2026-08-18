using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class TitleScreenUI : MonoBehaviour
{
    public static TitleScreenUI Instance { get; private set; }

    // v10: 언어 전환 시 갱신할 (TMP, key) 목록
    private readonly System.Collections.Generic.List<(TextMeshProUGUI tmp, string key)> localizedTexts = new();
    public bool IsShowing { get; private set; }

    private CanvasGroup rootGroup;
    private TMP_FontAsset koreanFont;

    // 장식 돌 애니메이션
    private RectTransform[] decoStoneRects;
    private Vector2[] decoStoneHomePositions; // 원래 위치
    private Vector2[] decoStoneVelocity;      // 현재 속도 (회피 후 감속용)
    private float[] decoStoneNoiseOffsets;     // Perlin Noise 시드 (돌마다 다른 궤적)
    private InputAction decoPointerAction;
    private Canvas titleCanvas;

    // 3D 장식 돌 관련
    private Camera decoStoneCamera;
    private RenderTexture decoStoneRT;
    private Transform[] decoStone3D;
    private Vector3[] decoStoneRotSpeed;
    private GameObject decoStoneCameraGo;

    private const float FloatSpeed = 15f;        // 부유 속도 (px/s)
    private const float FloatRadius = 40f;       // 부유 반경 (원래 위치에서)
    private const float FleeDistance = 150f;     // 회피 시작 거리 (px)
    private const float FleeImpulse = 800f;      // 회피 순간 임펄스 (px/s, dt 미곱)
    private const float FleeDamping = 4f;        // 회피 후 감속 계수
    private const float HomeReturnForce = 0.8f;  // 원래 위치 복귀 힘 (약하게 — 도망 우선)

    // 놀지 말고 토스트
    private float chasingTimer;          // 돌 근처에 머문 누적 시간
    private bool toastShown;             // 이미 표시했으면 재표시 안 함
    private TextMeshProUGUI toastText;
    private CanvasGroup toastGroup;
    private Coroutine toastCoroutine;
    private const float ChasingThreshold = 10f; // 10초

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        decoPointerAction?.Disable();
        decoPointerAction?.Dispose();
        CleanupDecoStone3D();
    }

    private void CleanupDecoStone3D()
    {
        if (decoStoneRT != null)
        {
            decoStoneRT.Release();
            Destroy(decoStoneRT);
            decoStoneRT = null;
        }
        if (decoStoneCameraGo != null)
        {
            Destroy(decoStoneCameraGo);
            decoStoneCameraGo = null;
        }
    }

    private void Init()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay (전체 화면 커버)
        var canvasGo = new GameObject("TitleCanvas");
        canvasGo.transform.SetParent(transform);

        titleCanvas = canvasGo.AddComponent<Canvas>();
        titleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        titleCanvas.sortingOrder = 250;
        var canvas = titleCanvas;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Root with CanvasGroup
        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        var rootRect = rootGo.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        rootGroup = rootGo.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        BuildUI(rootGo.transform);

        // 마우스 위치 입력 (장식 돌 회피용)
        decoPointerAction = new InputAction("DecoPointer", InputActionType.Value);
        decoPointerAction.AddBinding("<Mouse>/position");
        decoPointerAction.AddBinding("<Touchscreen>/primaryTouch/position");
        decoPointerAction.Enable();
    }

    private void EnsureEventSystem()
    {
        var existingES = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void BuildUI(Transform parent)
    {
        // 전체 배경: 와인색 (보드와 동일한 색상)
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.5f, 0.08f, 0.08f, 1f); // 폴백: 와인색 (이미지 로드 실패 시)

        // v18: 시안처럼 타이틀에도 첫 스테이지 배경(교실)을 깐다.
        // 단색 와인색은 "게임이 시작되기 전"이라는 느낌만 줄 뿐, 이 게임이 어떤 장면인지 말해주지 않는다.
        // 경로를 StageConfig에서 읽으므로 1회차 배경 아트가 바뀌면 타이틀도 따라간다.
        var firstStage = StageConfig.Get(1);
        if (firstStage != null && !string.IsNullOrEmpty(firstStage.BackgroundImage))
        {
            var bgTex = Resources.Load<Texture2D>(firstStage.BackgroundImage);
            if (bgTex != null)
            {
                bgImg.sprite = Sprite.Create(bgTex,
                    new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Simple;
            }
            else
            {
                Debug.LogWarning($"[TitleScreenUI] 타이틀 배경 로드 실패: {firstStage.BackgroundImage} — 와인색 폴백");
            }
        }

        // 타이틀: "Catch Five Stones" — 상단 40% 영역
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(parent, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        // 시안 실측: 1268×191 @(350,264) — 중심 y는 화면 상단에서 359.5px 지점
        titleRect.sizeDelta = new Vector2(UISkin.Px(1268f), UISkin.Px(191f));
        titleRect.anchoredPosition = new Vector2(0f, UISkin.Px(1080f * 0.5f - 359.5f));

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Catch Five Stones";
        // 시안 실측 149.13px. 다만 시안 폰트(Iosevka Mono)보다 나눔고딕이 넓어서 그대로 두면
        // 두 줄로 접힌다 — 시안은 한 줄이다. 줄바꿈을 막고, 시안의 로고 폭(1268px) 안에
        // 들어오도록 자동 축소시킨다. 폰트를 바꾸면 자연히 시안 크기에 붙는다.
        titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMax = UISkin.Px(149.13f);
        titleTmp.fontSizeMin = UISkin.Px(80f);
        // v18: 시안의 로고 배색 — 연두 면 + 어두운 외곽선. 밝은 교실 배경 위에서 뭉개지지 않는다.
        // ⚠️ 폰트는 아직 그대로다. 시안은 "Iosevka Charon Mono"를 쓰는데 **한글 글립이 없다**
        //    (Figma에서 한글은 폴백으로 그려진 것이다). 이 게임의 기본 언어가 한국어라
        //    그대로 가져오면 한글이 무너진다. 후보가 정해지면 여기와 koreanFont 로딩만 바꾸면 된다.
        titleTmp.color = UISkin.LogoFill;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        if (koreanFont != null) titleTmp.font = koreanFont;

        // fontMaterial에 접근하면 이 오브젝트 전용 인스턴스가 만들어진다 —
        // 공유 머티리얼을 건드리면 게임 안 모든 텍스트에 외곽선이 붙는다.
        // 배경이 교실(밝은 벽·창문)이라 외곽선이 얇으면 글자가 묻힌다. 시안은 로고가
        // 칠판 위에 걸쳐 있어 얇아도 살았지만, 여기선 밝은 면 위를 지나므로 더 굵어야 한다.
        var titleMat = titleTmp.fontMaterial;
        titleMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
        titleMat.SetColor(ShaderUtilities.ID_OutlineColor, UISkin.LogoOutline);

        // 장식용 3D 돌 5개 (타이틀 아래 흩어짐)
        CreateDecoStones(parent);

        // 말풍선 장식
        CreateSpeechBubbles(parent);

        // v11: 홈은 항상 게임 시작 단일 버튼으로 통일. 연습 모드(IsTestPlay)는 디버그 HUD 테스트 패널에서 진입 (에디터/연습빌드 전용).
        // 시안 실측: 버튼 380×80이 y=639부터 110px 간격으로 3개. 라벨은 전부 40px.
        // 세 버튼의 크기·글자를 다르게 두면 "Play가 더 중요하다"가 아니라 그냥 정렬이 안 맞아 보인다.
        const float LabelPx = 40f;
        float playY     = UISkin.Px(540f - 679f);
        float settingsY = UISkin.Px(540f - 789f);
        float exitY     = UISkin.Px(540f - 899f);

        RegisterLocalized(CreateMenuButton(LocalizationManager.L("home.play"), parent, new Vector2(0f, playY), UISkin.Px(LabelPx), () => OnModeSelected(false)), "home.play");

        // "설정" 버튼
        RegisterLocalized(CreateMenuButton(LocalizationManager.L("home.settings"), parent, new Vector2(0f, settingsY), UISkin.Px(LabelPx), () => {
            SettingsPopupUI.EnsureInstance().Open();
        }), "home.settings");

        // v18: 홈의 묘지(관람 모드) 버튼 제거 — UI 시안이 Play/Settings/Exit 3개로 확정.
        // ⚠️ 묘지 화면 자체는 남는다. GraveyardUI는 올클리어 **엔딩 화면**이기도 하다
        //    (GameManager.DoAllClearTransition → GraveyardUI.Show). 지운 것은 홈에서 다시
        //    들어가는 입구뿐이다. 되돌리려면 PlayerPrefs "EndingSeen"==1 조건으로 버튼을
        //    다시 만들면 된다(해금 플래그는 그대로 기록되고 있다).

        // "나가기" — 우측 상단
        // v18: 나가기를 우상단 투명 영역에서 **세로 메뉴 3번째**로 옮긴다.
        // UI 시안이 Play / Settings / Exit를 한 줄기로 쌓아둔 형태고,
        // 실제로도 화면 구석의 흐린 글자보다 같은 메뉴에 있는 편이 찾기 쉽다.
        RegisterLocalized(CreateMenuButton(LocalizationManager.L("home.exit"), parent, new Vector2(0f, exitY), UISkin.Px(LabelPx), () => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }), "home.exit");

        // 토스트 메시지 (하단, 처음엔 숨김)
        CreateToast(parent);
    }

    private void CreateToast(Transform parent)
    {
        var toastGo = new GameObject("Toast");
        toastGo.transform.SetParent(parent, false);
        var toastRect = toastGo.AddComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.sizeDelta = new Vector2(700f, 50f);
        toastRect.anchoredPosition = new Vector2(0f, 30f);

        // 반투명 배경
        var bgImg = toastGo.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false;

        toastGroup = toastGo.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(toastGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        toastText = textGo.AddComponent<TextMeshProUGUI>();
        toastText.text = LocalizationManager.L("title.toast");
        RegisterLocalized(toastText, "title.toast");
        toastText.fontSize = 22f;
        toastText.color = Color.white;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.raycastTarget = false;
        if (koreanFont != null) toastText.font = koreanFont;
    }

    private void CreateSpeechBubbles(Transform parent)
    {
        // === 1번 말풍선: 왼쪽 아래, 뾰족한 폭발형 (흰색 바탕 + 빨간 글씨 "마참내") ===
        {
            var bubbleGo = new GameObject("Bubble_Left");
            bubbleGo.transform.SetParent(parent, false);
            var rt = bubbleGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(180f, 140f);
            rt.anchoredPosition = new Vector2(40f, 40f);

            // 폭발형 모양: 런타임 텍스처
            var img = bubbleGo.AddComponent<Image>();
            img.sprite = CreateStarburstSprite(12, 64);
            img.color = Color.white;
            img.raycastTarget = false;

            // 텍스트
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bubbleGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(15f, 15f);
            textRt.offsetMax = new Vector2(-15f, -15f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = LocalizationManager.L("title.bubble_left");
            RegisterLocalized(tmp, "title.bubble_left");
            // v16: 한/영 글자 폭 차이("마참내" 3자 vs "FINALLY!" 8자)를 말풍선 안에 흡수.
            // NoWrap + 오토사이즈 → 언어 전환 시 줄바꿈 대신 축소.
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = 36f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.red;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (koreanFont != null) tmp.font = koreanFont;

            // 약간 기울이기 (만화 느낌)
            rt.localRotation = Quaternion.Euler(0f, 0f, 8f);
        }

        // === 2번 말풍선: 오른쪽 아래, 타원형 (노란색 바탕 + 흰색 글씨 "즐겁다") ===
        {
            var bubbleGo = new GameObject("Bubble_Right");
            bubbleGo.transform.SetParent(parent, false);
            var rt = bubbleGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(160f, 100f);
            rt.anchoredPosition = new Vector2(-40f, 50f);

            // 타원형: 런타임 텍스처
            var img = bubbleGo.AddComponent<Image>();
            img.sprite = CreateEllipseSprite(64);
            img.color = new Color(1f, 0.85f, 0.1f, 1f); // 노란색
            img.raycastTarget = false;

            // 텍스트
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bubbleGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 10f);
            textRt.offsetMax = new Vector2(-10f, -10f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = LocalizationManager.L("title.bubble_right");
            RegisterLocalized(tmp, "title.bubble_right");
            tmp.textWrappingMode = TextWrappingModes.NoWrap; // v16: 위 말풍선과 동일 사유
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 16f;
            tmp.fontSizeMax = 30f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (koreanFont != null) tmp.font = koreanFont;

            // 약간 반대로 기울이기
            rt.localRotation = Quaternion.Euler(0f, 0f, -5f);
        }
    }

    /// <summary>폭발/뾰족한 별 모양 스프라이트 런타임 생성</summary>
    private static Sprite CreateStarburstSprite(int points, int texSize)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        float center = texSize / 2f;
        float outerR = texSize / 2f - 1;
        float innerR = outerR * 0.55f;

        for (int px = 0; px < texSize; px++)
        {
            for (int py = 0; py < texSize; py++)
            {
                float dx = px - center;
                float dy = py - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // 별 모양: 각도에 따라 반지름이 inner~outer 사이를 오감
                float t = (Mathf.Sin(angle * points) + 1f) * 0.5f;
                float edgeR = Mathf.Lerp(innerR, outerR, t);

                tex.SetPixel(px, py, dist <= edgeR ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
    }

    /// <summary>타원형 스프라이트 런타임 생성</summary>
    private static Sprite CreateEllipseSprite(int texSize)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        float cx = texSize / 2f;
        float cy = texSize / 2f;
        float rx = texSize / 2f - 1;
        float ry = texSize / 2f - 1;

        for (int px = 0; px < texSize; px++)
        {
            for (int py = 0; py < texSize; py++)
            {
                float dx = (px - cx) / rx;
                float dy = (py - cy) / ry;
                tex.SetPixel(px, py, (dx * dx + dy * dy) <= 1f ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
    }

    private void CreateDecoStones(Transform parent)
    {
        // --- 1. TitleStones 레이어 확인 (없으면 기존 미사용 레이어 사용) ---
        int titleStoneLayer = LayerMask.NameToLayer("TitleStones");
        if (titleStoneLayer == -1)
        {
            // TitleStones 레이어가 없으면 빈 레이어 탐색 (8~31)
            for (int l = 8; l < 32; l++)
            {
                string layerName = LayerMask.LayerToName(l);
                if (string.IsNullOrEmpty(layerName))
                {
                    titleStoneLayer = l;
                    Debug.LogWarning($"[TitleScreenUI] 'TitleStones' 레이어 미설정. 빈 레이어 {l} 사용.");
                    break;
                }
            }
            if (titleStoneLayer == -1) titleStoneLayer = 31; // 최후 수단
        }

        // --- 2. RenderTexture 생성 (640x128, 돌 5개를 x축으로 나열) ---
        decoStoneRT = new RenderTexture(640, 128, 16, RenderTextureFormat.ARGB32);
        decoStoneRT.name = "DecoStoneRT";
        decoStoneRT.Create();

        // --- 3. 전용 카메라 생성 ---
        decoStoneCameraGo = new GameObject("DecoStoneCamera");
        decoStoneCameraGo.transform.SetParent(transform); // TitleScreenUI 하위
        decoStoneCameraGo.transform.position = new Vector3(20f, 100f, -10f); // 화면 밖 먼 곳
        decoStoneCameraGo.layer = titleStoneLayer;

        decoStoneCamera = decoStoneCameraGo.AddComponent<Camera>();
        decoStoneCamera.orthographic = true;
        decoStoneCamera.orthographicSize = 0.8f; // 돌 하나가 128px 높이에 맞도록
        decoStoneCamera.nearClipPlane = 0.1f;
        decoStoneCamera.farClipPlane = 20f;
        decoStoneCamera.cullingMask = 1 << titleStoneLayer;
        decoStoneCamera.clearFlags = CameraClearFlags.SolidColor;
        decoStoneCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명
        decoStoneCamera.targetTexture = decoStoneRT;
        decoStoneCamera.depth = -10; // 메인 카메라보다 낮은 depth

        // URP에서 카메라 추가 데이터 설정
        var camData = decoStoneCamera.GetUniversalAdditionalCameraData();
        if (camData != null)
        {
            camData.renderType = CameraRenderType.Base;
            camData.renderPostProcessing = false;
        }

        // --- 4. 전용 조명 (TitleStones 레이어만 비추는 Directional Light) ---
        var lightGo = new GameObject("DecoStoneLight");
        lightGo.transform.SetParent(decoStoneCameraGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2f, -3f);
        lightGo.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        lightGo.layer = titleStoneLayer;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.color = Color.white;
        light.cullingMask = 1 << titleStoneLayer;

        // --- 5. 돌 머테리얼 생성 (URP Lit + Emission) ---
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) urpShader = Shader.Find("Standard"); // 빌드 fallback
        var stoneMat = new Material(urpShader);
        Color stoneColor = new Color(0.95f, 0.85f, 0.2f, 1f);
        stoneMat.SetColor("_BaseColor", stoneColor);
        stoneMat.SetFloat("_Smoothness", 0.5f);
        stoneMat.SetFloat("_Metallic", 0.1f);
        // Emission 활성화 (조명 의존 제거)
        stoneMat.EnableKeyword("_EMISSION");
        stoneMat.SetColor("_EmissionColor", stoneColor * 0.4f);
        stoneMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        // --- 6. Stone에서 mesh 빌려오기 시도 ---
        Mesh stoneMesh = null;
        var existingStones = FindObjectsByType<StoneShape>(FindObjectsSortMode.None);
        if (existingStones != null && existingStones.Length > 0)
        {
            var mf = existingStones[0].GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                stoneMesh = mf.sharedMesh;
            }
        }

        // --- 7. 3D 돌 5개 생성 ---
        decoStone3D = new Transform[5];
        decoStoneRotSpeed = new Vector3[5];

        // 카메라 orthographicSize=0.8 → 세로 ±0.8 units 표시
        // RT 640x128 → 가로:세로 = 5:1 → 가로 ±4.0 units 표시
        // 돌 5개를 x 간격 1.6 units로 배치 (중앙 정렬: -3.2, -1.6, 0, 1.6, 3.2)
        float[] stoneXOffsets = { -3.2f, -1.6f, 0f, 1.6f, 3.2f };
        float[] stoneSizes = { 0.55f, 0.48f, 0.45f, 0.52f, 0.48f }; // 다양한 크기

        Vector3 camPos = decoStoneCameraGo.transform.position;

        for (int i = 0; i < 5; i++)
        {
            GameObject stoneGo;
            if (stoneMesh != null)
            {
                // Stone mesh 사용
                stoneGo = new GameObject($"DecoStone3D_{i}");
                var mf = stoneGo.AddComponent<MeshFilter>();
                mf.sharedMesh = stoneMesh;
                var mr = stoneGo.AddComponent<MeshRenderer>();
                mr.material = stoneMat;
            }
            else
            {
                // Fallback: Sphere primitive
                stoneGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stoneGo.name = $"DecoStone3D_{i}";
                // Sphere primitive에는 Collider가 붙는데 타이틀용이므로 제거
                var col = stoneGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
                stoneGo.GetComponent<MeshRenderer>().material = stoneMat;
                // StoneShape 추가하여 공기돌 형태로 변형
                stoneGo.AddComponent<StoneShape>();
            }

            stoneGo.transform.SetParent(decoStoneCameraGo.transform, false);
            stoneGo.transform.localPosition = new Vector3(stoneXOffsets[i], 0f, 5f); // 카메라 앞 5 units
            stoneGo.transform.localScale = Vector3.one * stoneSizes[i];
            stoneGo.transform.localRotation = Random.rotation; // 랜덤 초기 회전
            stoneGo.layer = titleStoneLayer;

            // 자식 오브젝트도 같은 레이어로 설정
            foreach (Transform child in stoneGo.transform)
                child.gameObject.layer = titleStoneLayer;

            decoStone3D[i] = stoneGo.transform;

            // 랜덤 회전 속도 (축마다 다르게)
            decoStoneRotSpeed[i] = new Vector3(
                Random.Range(-30f, 30f),
                Random.Range(-40f, 40f),
                Random.Range(-20f, 20f)
            );
        }

        // --- 8. Canvas에 RawImage 5개 배치 (uvRect로 RT 1/5씩 표시) ---
        Vector2[] positions = new Vector2[]
        {
            new Vector2(-420f, -20f),   // 좌측
            new Vector2(-60f, 40f),     // 중앙 위
            new Vector2(-20f, -30f),    // 중앙 아래
            new Vector2(280f, 50f),     // 우측 위
            new Vector2(450f, -140f),   // 우측 하단
        };
        float[] uiSizes = { 80f, 70f, 65f, 75f, 70f }; // UI에서의 크기 (px)

        decoStoneRects = new RectTransform[5];
        decoStoneHomePositions = new Vector2[5];
        decoStoneVelocity = new Vector2[5];
        decoStoneNoiseOffsets = new float[5];

        for (int i = 0; i < 5; i++)
        {
            var stoneGo = new GameObject($"DecoStone_{i}");
            stoneGo.transform.SetParent(parent, false);
            var rect = stoneGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(uiSizes[i], uiSizes[i]);
            rect.anchoredPosition = positions[i];

            var rawImg = stoneGo.AddComponent<RawImage>();
            rawImg.texture = decoStoneRT;
            // UV Rect: 각 돌은 RT의 1/5 구간 (x 방향으로 슬라이스)
            rawImg.uvRect = new Rect(i / 5f, 0f, 1f / 5f, 1f);
            rawImg.raycastTarget = false;

            decoStoneRects[i] = rect;
            decoStoneHomePositions[i] = positions[i];
            decoStoneVelocity[i] = Vector2.zero;
            decoStoneNoiseOffsets[i] = Random.Range(0f, 100f);
        }
    }

    private TextMeshProUGUI CreateMenuButton(string text, Transform parent, Vector2 pos, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{text}");
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        // 시안 380×80은 **면 기준**이다 — 어두운 테두리가 그 바깥에 그려져 실제 188×88로 찍힌다.
        // 우리는 테두리를 안쪽에 그리므로 사방으로 넓혀야 면적이 같아진다(중앙 기준이라 크기만 키우면 된다).
        btnRect.sizeDelta = new Vector2(UISkin.Px(380f + UISkin.Outset * 2f),
                                        UISkin.Px(80f + UISkin.Outset * 2f));
        btnRect.anchoredPosition = pos;

        // v18: 시안(Figma)의 광택 베벨 버튼. 스프라이트를 바꿔 "튀어나옴 → 눌림"을 표현한다.
        // 색 전환(ColorTint)으로는 눌린 느낌이 안 난다 — 그라디언트 방향이 뒤집혀야 읽힌다.
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
        btn.onClick.AddListener(onClick);

        // 호버 시 손가락 가리킴 포즈
        var hover = btnGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = UISkin.Ink;   // v18: 시안 라벨색 #29313B (순검정이 아니라 살짝 푸른 먹색)
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
        return tmp;
    }

    // v10: 로컬라이즈 텍스트 등록/갱신
    private void RegisterLocalized(TextMeshProUGUI tmp, string key)
    {
        if (tmp != null) localizedTexts.Add((tmp, key));
    }

    private void RefreshLocalizedTexts()
    {
        foreach (var (tmp, key) in localizedTexts)
            if (tmp != null) tmp.text = LocalizationManager.L(key);
    }

    private void OnEnable()  { LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts; }
    private void OnDisable() { LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts; }

    // === 장식 돌 애니메이션 ===

    private void Update()
    {
        if (!IsShowing || decoStoneRects == null) return;

        float dt = Time.unscaledDeltaTime; // timeScale 무관
        float time = Time.unscaledTime;

        // 마우스 → Canvas 로컬 좌표 변환
        Vector2 mouseScreen = decoPointerAction.ReadValue<Vector2>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            decoStoneRects[0].parent as RectTransform,
            mouseScreen, null, out Vector2 mouseLocal);

        for (int i = 0; i < 5; i++)
        {
            if (decoStoneRects[i] == null) continue;

            Vector2 pos = decoStoneRects[i].anchoredPosition;
            Vector2 home = decoStoneHomePositions[i];
            float seed = decoStoneNoiseOffsets[i];

            // --- 1. Perlin Noise 부유 ---
            float noiseX = Mathf.PerlinNoise(seed + time * 0.3f, 0f) - 0.5f;
            float noiseY = Mathf.PerlinNoise(0f, seed + time * 0.3f) - 0.5f;
            Vector2 floatTarget = home + new Vector2(noiseX, noiseY) * FloatRadius * 2f;

            // --- 2. 마우스 회피 (임펄스 — dt 안 곱함, 순간 튕김) ---
            Vector2 toMouse = pos - mouseLocal;
            float dist = toMouse.magnitude;
            if (dist < FleeDistance && dist > 0.1f)
            {
                Vector2 fleeDir = toMouse.normalized;
                float fleePower = (1f - dist / FleeDistance);
                fleePower *= fleePower; // 제곱: 가까울수록 폭발적으로 강해짐
                decoStoneVelocity[i] += fleeDir * FleeImpulse * fleePower;
            }

            // --- 3. 원래 위치 복귀 힘 ---
            Vector2 toHome = floatTarget - pos;
            decoStoneVelocity[i] += toHome * HomeReturnForce * dt;

            // --- 4. 감속 ---
            decoStoneVelocity[i] *= (1f - FleeDamping * dt);

            // --- 5. 위치 갱신 ---
            pos += decoStoneVelocity[i] * dt + (floatTarget - pos) * FloatSpeed * dt * 0.1f;

            // --- 6. 바운더리 클램프 (화면 안에 유지) ---
            pos.x = Mathf.Clamp(pos.x, -600f, 600f);
            pos.y = Mathf.Clamp(pos.y, -320f, 320f);

            decoStoneRects[i].anchoredPosition = pos;

            // --- 7. 3D 돌 회전 ---
            if (decoStone3D != null && decoStone3D[i] != null)
            {
                decoStone3D[i].Rotate(decoStoneRotSpeed[i] * dt, Space.World);
            }
        }

        // --- 놀지 말고 토스트 판정 ---
        if (!toastShown)
        {
            // 돌 중 하나라도 회피 거리 안에 있으면 "쫓아다니는 중"
            bool chasingAny = false;
            for (int i = 0; i < 5; i++)
            {
                if (decoStoneRects[i] == null) continue;
                float d = (decoStoneRects[i].anchoredPosition - mouseLocal).magnitude;
                if (d < FleeDistance * 1.5f) { chasingAny = true; break; }
            }

            if (chasingAny)
            {
                chasingTimer += dt;
                if (chasingTimer >= ChasingThreshold)
                {
                    toastShown = true;
                    if (toastCoroutine != null) StopCoroutine(toastCoroutine);
                    toastCoroutine = StartCoroutine(ShowToast());
                }
            }
            else
            {
                // 돌에서 멀어지면 타이머 서서히 감소 (완전 리셋은 아님)
                chasingTimer = Mathf.Max(0f, chasingTimer - dt * 0.5f);
            }
        }
    }

    private IEnumerator ShowToast()
    {
        // 페이드 인 (0.5초)
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(elapsed / 0.5f);
            yield return null;
        }
        toastGroup.alpha = 1f;

        // 3초 유지
        yield return new WaitForSecondsRealtime(3f);

        // 페이드 아웃 (0.5초)
        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.5f);
            yield return null;
        }
        toastGroup.alpha = 0f;
        toastCoroutine = null;
    }

    // === 3D 돌 Show/Hide 헬퍼 ===

    private void SetDecoStone3DActive(bool active)
    {
        if (decoStoneCameraGo != null) decoStoneCameraGo.SetActive(active);
    }

    // === 공개 API ===

    public void Show()
    {
        StopAllCoroutines();
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        IsShowing = true;
        // 토스트 리셋 (타이틀 재진입 시 다시 발동 가능)
        chasingTimer = 0f;
        toastShown = false;
        if (toastGroup != null) toastGroup.alpha = 0f;
        // 3D 돌 + 카메라 활성화
        SetDecoStone3DActive(true);
        Debug.Log("[TitleScreenUI] Show.");
        AudioManager.Instance?.PlayLobbyBGM();
    }

    public void Hide(System.Action onComplete = null)
    {
        StartCoroutine(DoFadeOut(onComplete));
    }

    private IEnumerator DoFadeOut(System.Action onComplete)
    {
        float elapsed = 0f;
        const float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rootGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        IsShowing = false;
        // 3D 돌 + 카메라 비활성화 (성능)
        SetDecoStone3DActive(false);
        Debug.Log("[TitleScreenUI] Hidden.");
        onComplete?.Invoke();
    }

    private void OnStartClicked() => StartCoroutine(DoStartBehindCurtain(null));

    private void OnModeSelected(bool isTestPlay) => StartCoroutine(DoStartBehindCurtain(isTestPlay));

    /// <summary>커튼이 화면을 **완전히 덮은 뒤에** 타이틀을 내리고 게임을 시작한다.
    ///
    /// ⚠️ 예전엔 커튼 올리기(0.15초)와 타이틀 페이드아웃(0.5초)을 동시에 시작했다.
    /// 타이틀이 옅어지는 동안 뒤의 **보드가 비쳐 보였다** — "미묘하게 한 번 깜빡"의 정체다.
    /// 커튼이 덮은 뒤에는 타이틀 페이드가 보이지 않으므로 그냥 즉시 내린다(대기 시간도 줄어든다).
    /// </summary>
    private IEnumerator DoStartBehindCurtain(bool? isTestPlay)
    {
        const float curtainTime = 0.25f;
        BootCurtain.Instance?.Raise(curtainTime);
        yield return new WaitForSeconds(curtainTime);

        HideInstant();

        if (isTestPlay.HasValue)
        {
            var session = GameSession.Instance;
            if (session != null) session.IsTestPlay = isTestPlay.Value;
        }
        GameManager.Instance?.StartGameFromTitle();
    }

#if UNITY_EDITOR
    /// <summary>에디터 검증 전용 — "게임 시작" 버튼과 **완전히 같은 경로**를 탄다.
    /// 검증 코드가 순서를 따로 흉내 내면 정작 실제 연출을 못 본다.</summary>
    public void DebugStartGame() => StartCoroutine(DoStartBehindCurtain(false));
#endif

    /// <summary>페이드 없이 즉시 감춘다 (커튼 뒤라 페이드가 보이지 않을 때).</summary>
    private void HideInstant()
    {
        StopCoroutine(nameof(DoFadeOut));
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        IsShowing = false;
        SetDecoStone3DActive(false);
        Debug.Log("[TitleScreenUI] Hidden (instant).");
    }
}
