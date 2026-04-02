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
        bgImg.color = new Color(0.5f, 0.08f, 0.08f, 1f); // 와인색 (Cloth emissionColor과 유사)

        // 타이틀: "Catch Five Stones" — 상단 40% 영역
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(parent, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(900f, 100f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Catch Five Stones";
        titleTmp.fontSize = 72f;
        titleTmp.color = new Color(1f, 0.95f, 0.8f, 1f); // 크림색
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        if (koreanFont != null) titleTmp.font = koreanFont;

        // 장식용 3D 돌 5개 (타이틀 아래 흩어짐)
        CreateDecoStones(parent);

        // "기록 모드" 버튼
        CreateMenuButton("기록 모드", parent, new Vector2(0f, -40f), 34, () => OnModeSelected(false));

        // "연습 모드" 버튼
        CreateMenuButton("연습 모드", parent, new Vector2(0f, -100f), 30, () => OnModeSelected(true));

        // "설정" 버튼
        CreateMenuButton("설정", parent, new Vector2(0f, -160f), 26, () => {
            PauseMenuUI.Instance?.Toggle();
        });

        // "나가기" — 우측 상단
        var exitGo = new GameObject("ExitText");
        exitGo.transform.SetParent(parent, false);
        var exitRect = exitGo.AddComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        exitRect.sizeDelta = new Vector2(120f, 50f);
        exitRect.anchoredPosition = new Vector2(-30f, -20f);

        var exitImg = exitGo.AddComponent<Image>();
        exitImg.color = new Color(0f, 0f, 0f, 0f); // 투명 클릭 영역

        var exitBtn = exitGo.AddComponent<Button>();
        exitBtn.targetGraphic = exitImg;
        var exitHover = exitGo.AddComponent<HandCursorHoverTrigger>();
        exitHover.HoverPose = HandPose.PointIndex;
        exitBtn.onClick.AddListener(() => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var exitLabel = new GameObject("Label");
        exitLabel.transform.SetParent(exitGo.transform, false);
        var exitLabelRect = exitLabel.AddComponent<RectTransform>();
        exitLabelRect.anchorMin = Vector2.zero;
        exitLabelRect.anchorMax = Vector2.one;
        exitLabelRect.offsetMin = Vector2.zero;
        exitLabelRect.offsetMax = Vector2.zero;

        var exitTmp = exitLabel.AddComponent<TextMeshProUGUI>();
        exitTmp.text = "나가기";
        exitTmp.fontSize = 24f;
        exitTmp.color = new Color(1f, 1f, 1f, 0.6f);
        exitTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) exitTmp.font = koreanFont;

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
        toastText.text = "놀지 말고 공기놀이를 시작하시는게 어떨까요?";
        toastText.fontSize = 22f;
        toastText.color = Color.white;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.raycastTarget = false;
        if (koreanFont != null) toastText.font = koreanFont;
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
        var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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

    private void CreateMenuButton(string text, Transform parent, Vector2 pos, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{text}");
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(240f, 50f);
        btnRect.anchoredPosition = pos;

        // 반투명 어두운 배경 (버튼 느낌)
        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.3f, 0.05f, 0.05f, 0.6f); // 어두운 와인색

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.3f, 0.05f, 0.05f, 0.6f);
        colors.highlightedColor = new Color(0.5f, 0.1f, 0.1f, 0.8f);
        colors.pressedColor = new Color(0.2f, 0.02f, 0.02f, 0.9f);
        btn.colors = colors;
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
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
    }

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

    private void OnStartClicked()
    {
        Hide(() => GameManager.Instance?.StartGameFromTitle());
    }

    private void OnModeSelected(bool isTestPlay)
    {
        Hide(() =>
        {
            var session = GameSession.Instance;
            if (session != null)
                session.IsTestPlay = isTestPlay;
            GameManager.Instance?.StartGameFromTitle();
        });
    }
}
