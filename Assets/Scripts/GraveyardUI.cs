using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 납골당 랭킹보드 싱글톤 (시안 0822 — Ranking 593:693 / 593:719).
/// ALL CLEAR 후, 그리고 홈의 "납골당" 버튼으로 표시. Screen Space Overlay Canvas (sortingOrder=190).
///
/// v19: 흰 배경 + 회색 카드 3열 그리드 → **납골당 벽 사진 합성**으로 교체.
/// 벽은 1920×988 사진 한 장(<see cref="WallTexPath"/>)을 세로로 이어 붙여 만든다 —
/// 시안도 같은 Back 사각형을 2장 겹쳐 1976 높이를 만들었다(593:694/695).
/// 기록 한 건은 그 위에 유골함 카드 284×288 + 이름/시간 텍스트로 얹는다.
/// </summary>
public class GraveyardUI : MonoBehaviour
{
    public static GraveyardUI Instance { get; private set; }

    // ── 시안 좌표 (1920×1080 기준, 좌상단 원점) ──────────────────────────
    // 열 x와 행 y는 시안 실측값(593:696~699)이고, **배경 사진의 실제 니치와 대조해 확인했다**:
    //   wall_tile.png에서 칸막이를 픽셀로 재면 니치 안쪽이 y 17~312 / 348~642 / 677~972,
    //   기둥 사이가 x 480~782 / 806~1109 / 1131~1421 이다.
    //   아래 카드 사각형(284×288)이 세 행·세 열 모두 그 안에 들어간다.
    // ⚠️ 행 피치(326.94)와 타일 높이(988)는 정수배가 아니다(3행=980.82). 그래서 행을
    //    "이어서 계속 더하는" 방식으로 두면 타일마다 7.18px씩 밀려 결국 칸막이를 밟는다.
    //    **타일마다 RowY를 처음부터 다시 쓴다** — 벽 사진이 반복되므로 그게 맞다.
    private const string WallTexPath = "Columbarium/wall_tile";
    private const string UrnTexPath  = "Columbarium/urn_card";
    private const float TileW = 1920f, TileH = 988f;
    private const float CardW = 284f, CardH = 288f;
    private static readonly float[] ColX = { 485.94f, 810f, 1134.06f };
    private static readonly float[] RowY = { 23.06f, 350f, 676.94f };
    private const int SlotsPerTile = 9;          // 3열 × 3행
    private const int MinTiles = 2;              // 시안이 기본으로 두 장을 깐다(스크롤 여지)

    // 카드 안 텍스트 (593:700 계열) — 카드 좌상단 기준 오프셋
    private const float TextDX = 73.5f, TextW = 137f;
    private const float NameDY = 84.94f, NameH = 108f;   // 28px × 3줄 = 15자
    private const float TimeDY = 204.94f, TimeH = 36f;
    private const float TextPt = 28f;

    // 하단 버튼 (593:712) — Go Home은 시안이 291.5지만 300으로 통일(디자이너 확정 2026-08-22)
    private const float BtnX = 1528f, BtnY0 = 798f, BtnGap = 110f, BtnW = 300f, BtnH = 80f;

    private Canvas canvas;
    private ScrollRect scrollRect;
    private RectTransform content;
    private TextMeshProUGUI statusText;
    private GameObject restartHintWrapper;
    private GameObject playAgainBtn, goHomeBtn;
    /// <summary>홈에서 들어온 관람 모드. 아직 판을 한 판도 안 끝냈으므로 "Play Again"이 성립하지 않는다.</summary>
    private bool isViewOnly;
    private TMP_FontAsset koreanFont;

    // v10 다국어: 하단 버튼(Play Again/Go Home) 라벨 → 키. Show() 때 현재 언어로 재설정.
    private readonly List<(TextMeshProUGUI tmp, string key)> endButtonLabels = new();

    private Sprite wallSprite;   // 납골당 벽 타일 (1920×988)
    private Sprite urnSprite;    // 유골함 카드 (284×288) — 아직 없으면 null

    private Coroutine scrollCoroutine;
    private bool isShowing;
    private bool hasReachedEnd;

    private InputAction tapAction;

    public bool IsShowing => isShowing;

    // ------------------------------------------------------------------
    // 생명주기
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        tapAction = new InputAction("GraveyardTap", InputActionType.Button);
        tapAction.AddBinding("<Mouse>/leftButton");
        tapAction.AddBinding("<Touchscreen>/primaryTouch/press");
        tapAction.performed += OnTap;
        tapAction.Enable();
    }

    private void OnDisable()
    {
        tapAction.performed -= OnTap;
        tapAction.Disable();
    }

    // ------------------------------------------------------------------
    // 공개 API
    // ------------------------------------------------------------------

    public void Show(float myTime, string myName, int myRegressionCount = 0, bool isTestPlay = false)
    {
        isViewOnly = false;
        canvas.gameObject.SetActive(true);
        isShowing = true;
        hasReachedEnd = false;
        restartHintWrapper.SetActive(false);
        RefreshEndButtons(); // v10 다국어: 현재 언어로 하단 버튼 라벨 갱신
        ApplyEndButtonMode(); // 관람 모드에서 감췄던 Play Again을 되살린다(엔딩 재진입 대비)
        statusText.text = LocalizationManager.L("grave.loading");

        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);
        scrollCoroutine = StartCoroutine(CoLoadAndScroll(myTime, myName, myRegressionCount, isTestPlay));
    }

    /// <summary>
    /// 홈에서 여는 관람 모드. 저장된 기록만 보여주고 **내 기록을 새로 얹지 않는다**.
    /// (isTestPlay 플래그가 "내 비석 생략"을 겸하고 있어 그대로 재사용한다 —
    ///  호출부에서 `isTestPlay: true`라고 쓰면 뜻이 거꾸로 읽혀서 이 이름으로 감쌌다.)
    /// ⚠️ 이 캔버스는 sortingOrder=190, 타이틀은 250이다. 타이틀을 내리지 않고 부르면
    ///    납골당이 타이틀 뒤에 가려 아무것도 안 보인다 — TitleScreenUI.OpenColumbarium 참고.
    /// </summary>
    public void ShowViewOnly()
    {
        Show(0f, string.Empty, 0, isTestPlay: true);
        isViewOnly = true;   // Show()가 false로 되돌리므로 **뒤에** 세운다
        ApplyEndButtonMode();
    }

    /// <summary>
    /// 관람 모드면 "Play Again"을 감추고 "Go Home"만 남긴다.
    /// 홈 → 납골당은 아직 아무 판도 안 끝낸 상태라 "다시 하기"가 말이 안 된다(2026-08-25 지적).
    /// 남은 한 개는 시안의 **첫 번째 자리**로 올린다 — 아래 칸에 혼자 두면 위가 비어 어색하다.
    /// </summary>
    private void ApplyEndButtonMode()
    {
        if (playAgainBtn != null) playAgainBtn.SetActive(!isViewOnly);
        if (goHomeBtn != null)
            UIWindow.Place(goHomeBtn.GetComponent<RectTransform>(),
                           BtnX - UISkin.Outset,
                           (isViewOnly ? BtnY0 : BtnY0 + BtnGap) - UISkin.Outset,
                           BtnW + UISkin.Outset * 2f, BtnH + UISkin.Outset * 2f);
    }

    public void Hide()
    {
        if (scrollCoroutine != null)
        {
            StopCoroutine(scrollCoroutine);
            scrollCoroutine = null;
        }

        // Content 자식 전부 Destroy
        if (content != null)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        canvas.gameObject.SetActive(false);
        isShowing = false;
        hasReachedEnd = false;
    }

    // ------------------------------------------------------------------
    // 탭 입력 처리
    // ------------------------------------------------------------------

    private void OnTap(InputAction.CallbackContext ctx)
    {
        if (!isShowing) return;

        // 그리드 대기(0.6초) 중 탭 시 버튼 즉시 표시. 완료 후에는 화면 버튼(Play Again/Go Home)으로 처리.
        if (!hasReachedEnd)
        {
            hasReachedEnd = true;
            restartHintWrapper.SetActive(true);
        }
    }

    // ------------------------------------------------------------------
    // 로드 + 스크롤 코루틴
    // ------------------------------------------------------------------

    private IEnumerator CoLoadAndScroll(float myTime, string myName, int myRegressionCount, bool isTestPlay = false)
    {
        // 로딩(최대 15초) 동안에도 벽은 서 있어야 한다. v18까지는 전체화면 흰 배경이 그 역할을
        // 했는데, 벽이 content 안으로 들어가면서 **비면 게임 화면이 그대로 비쳤다**. 먼저 깔아 둔다.
        LayoutWall(0);

        List<RecordEntry> records = null;
        bool done = false;

        SupabaseManager.Instance?.GetAllRecords(result =>
        {
            records = result;
            done = true;
        });

        float waited = 0f;
        while (!done && waited < 15f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // 로드 실패(오프라인/타임아웃) 시 튕김 방지 — 빈 리스트로 대체해 빈 흰 화면 + 버튼만 표시
        if (records == null)
        {
            records = new List<RecordEntry>();
        }

        statusText.text = "";

        // Content 기존 자식 정리
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        yield return null; // Destroy 반영 대기

        // 내 기록은 **순위 자리에** 끼운다 — 시안이 첫 줄을 1·2·3등으로 부르는 랭킹 벽이라
        // 맨 뒤에 덧붙이면 빠른 기록이 맨 아래 칸에 앉는다. (관람 모드는 내 기록이 없다.)
        if (!isTestPlay) InsertMyRecord(records, myName, myTime, myRegressionCount);

        LayoutWall(records.Count);
        for (int i = 0; i < records.Count; i++)
            CreateNiche(i, records[i].player_name, records[i].clear_time_seconds);

        scrollRect.verticalNormalizedPosition = 1f; // 맨 위

        // 그리드 완성 후 잠시 뒤 버튼 노출 (가로 자동스크롤 대체)
        yield return new WaitForSecondsRealtime(0.6f);
        hasReachedEnd = true;
        restartHintWrapper.SetActive(true);
        scrollCoroutine = null;
    }

    // ------------------------------------------------------------------
    // 비석 생성
    // ------------------------------------------------------------------

    /// <summary>
    /// 기록 한 건을 벽의 index번째 칸에 얹는다. 채우는 순서는 좌→우, 위→아래.
    /// index는 **랭킹 순위**다(서버가 regression asc, time asc로 내려준다) — 시안의
    /// "일등은여기 / 이등은여기 / 3등은여기"가 첫 줄 세 칸인 이유다.
    /// </summary>
    private void CreateNiche(int index, string playerName, float clearTimeSeconds)
    {
        int tile = index / SlotsPerTile;
        int inTile = index % SlotsPerTile;
        float x = ColX[inTile % 3];
        float y = tile * TileH + RowY[inTile / 3];

        // 유골함 카드 — 시안은 칸마다 같은 사진을 깐다.
        var cardGo = new GameObject($"Niche{index}", typeof(RectTransform));
        cardGo.transform.SetParent(content, false);
        UIWindow.Place(cardGo.GetComponent<RectTransform>(), x, y, CardW, CardH);

        if (urnSprite != null)
        {
            var img = cardGo.AddComponent<Image>();
            img.sprite = urnSprite;
            img.type = Image.Type.Simple;
        }

        // 이름 — 15자가 28px 세 줄로 딱 맞는 칸이다(NameInputUI의 characterLimit와 짝).
        var nameTmp = UIWindow.Label(cardGo.transform, "Name", playerName,
                                     TextPt, TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(nameTmp.rectTransform, TextDX, NameDY, TextW, NameH);
        nameTmp.textWrappingMode = TextWrappingModes.Normal;
        nameTmp.overflowMode = TextOverflowModes.Truncate;

        var timeTmp = UIWindow.Label(cardGo.transform, "Time", FormatTime(clearTimeSeconds),
                                     TextPt, TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(timeTmp.rectTransform, TextDX, TimeDY, TextW, TimeH);
    }

    /// <summary>
    /// 기록 수에 맞춰 벽을 깐다. 타일을 세로로 이어 붙이고 content 높이를 맞춘다.
    /// 카드를 만들기 **전에** 불러야 한다 — content 크기가 정해져야 배치가 맞는다.
    /// </summary>
    private void LayoutWall(int recordCount)
    {
        int tiles = Mathf.Max(MinTiles, Mathf.CeilToInt(recordCount / (float)SlotsPerTile));
        content.sizeDelta = new Vector2(0f, UISkin.Px(tiles * TileH));

        for (int i = 0; i < tiles; i++)
        {
            var tileGo = new GameObject($"WallTile{i}", typeof(RectTransform));
            tileGo.transform.SetParent(content, false);
            UIWindow.Place(tileGo.GetComponent<RectTransform>(), 0f, i * TileH, TileW, TileH);
            var img = tileGo.AddComponent<Image>();
            if (wallSprite != null) { img.sprite = wallSprite; img.type = Image.Type.Simple; }
            else img.color = new Color(0.16f, 0.13f, 0.09f, 1f); // 사진 없을 때도 글자는 읽히게
        }
    }

    /// <summary>
    /// 내 기록을 순위 자리에 끼워 넣는다. 서버 정렬 키(regression asc, time asc)를 그대로 쓴다.
    ///
    /// ⚠️ 목록을 받아오는 GET과 내 기록을 올리는 POST가 **경주한다**(GameManager가 PostRecord를
    ///    콜백으로 띄우고 곧바로 Show를 부른다). POST가 먼저 닿았으면 목록에 이미 내가 있으므로
    ///    같은 값이 두 번 걸리지 않게 걸러낸다.
    /// </summary>
    private static void InsertMyRecord(List<RecordEntry> records, string myName, float myTime, int myRegression)
    {
        foreach (var r in records)
            if (r.player_name == myName && Mathf.Approximately(r.clear_time_seconds, myTime)
                && r.regression_count == myRegression) return;

        int at = records.Count;
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            if (myRegression < r.regression_count ||
                (myRegression == r.regression_count && myTime < r.clear_time_seconds))
            { at = i; break; }
        }
        records.Insert(at, new RecordEntry
        {
            player_name = myName,
            clear_time_seconds = myTime,
            regression_count = myRegression,
        });
    }

    // ------------------------------------------------------------------
    // 디버그 미리보기 (에디터 확인용 — Supabase 불필요)
    // ------------------------------------------------------------------

    public void ShowPreview()  // 더미 그리드 즉시 표시 (Supabase 불필요, 에디터 확인용)
    {
        canvas.gameObject.SetActive(true);
        isShowing = true; hasReachedEnd = true;
        restartHintWrapper.SetActive(false);
        isViewOnly = false;
        RefreshEndButtons();
        ApplyEndButtonMode();
        statusText.text = "";
        if (scrollCoroutine != null) { StopCoroutine(scrollCoroutine); scrollCoroutine = null; }
        scrollCoroutine = StartCoroutine(CoShowPreview());
    }

    private IEnumerator CoShowPreview()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        yield return null; // Destroy 반영
        string[] names = {"일등은여기","이등은여기","3등은여기","일이삼사오육칠팔구십일이삼사오",
                          "최지우","정해인","강동원","한소희","김철수","이영희","박민수","나"};
        LayoutWall(names.Length);
        for (int i = 0; i < names.Length; i++) CreateNiche(i, names[i], 180f + i * 20f);
        scrollRect.verticalNormalizedPosition = 1f;
        restartHintWrapper.SetActive(true);
        scrollCoroutine = null;
    }

    // ------------------------------------------------------------------
    // UI 구조 빌드 (런타임 생성)
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay, sortingOrder=190
        // v19: 200 → 190. BootCurtain이 200이라 **같은 값이면 누가 위인지 정해지지 않는다**.
        // 납골당을 커튼 아래로 확실히 내려야 들고 날 때 검은 막으로 덮어 전환할 수 있다.
        // 위쪽 이웃은 그대로다: LifePanoramaUI 210 · 타이틀 250 · 설정 260 · 이름입력 300.
        var canvasGo = new GameObject("GraveyardCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 배경 사진 — 벽은 **스크롤과 함께 움직여야** 하므로 전체화면 한 장이 아니라
        // content 안에 타일로 깐다(LayoutWall). 여기서는 텍스처만 읽어 스프라이트로 굽는다.
        // 프로젝트 관례대로 Texture2D로 읽고 Sprite.Create 한다(타이틀 배경과 같은 방식).
        wallSprite = LoadSprite(WallTexPath);
        urnSprite  = LoadSprite(UrnTexPath);
        if (wallSprite == null)
            Debug.LogWarning($"[GraveyardUI] 벽 사진 없음: Resources/{WallTexPath} — 어두운 단색으로 대체");
        if (urnSprite == null)
            Debug.LogWarning($"[GraveyardUI] 유골함 카드 없음: Resources/{UrnTexPath} — 빈 칸에 글자만 얹는다");

        // ScrollRect — 수직 3열 그리드, 전체화면 (Figma Image#13)
        var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        // Viewport
        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();

        scrollRect.viewport = viewportRt;

        // Content — 상단 stretch, 세로 3열 그리드
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        // v19: GridLayoutGroup/ContentSizeFitter 제거 — 칸 위치가 **벽 사진의 니치에 박혀 있어서**
        // 자동 배치로는 맞출 수 없다. LayoutWall/CreateNiche가 시안 좌표로 직접 놓는다.
        scrollRect.content = content;

        // StatusText — 중앙
        var statusGo = new GameObject("StatusText", typeof(RectTransform));
        statusGo.transform.SetParent(canvasGo.transform, false);
        var statusRt = statusGo.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.3f, 0.4f);
        statusRt.anchorMax = new Vector2(0.7f, 0.6f);
        statusRt.offsetMin = Vector2.zero;
        statusRt.offsetMax = Vector2.zero;
        statusText = statusGo.AddComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.fontSize = 20f;
        statusText.color = new Color(0.95f, 0.93f, 0.85f, 1f); // v19: 어두운 금색 벽 위 가독
        statusText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) statusText.font = koreanFont;

        // Play Again / Go Home — 시안 593:712는 우측 **세로 2개**다(예전엔 우하단 가로 2개).
        // 벽은 스크롤해도 버튼은 화면에 붙어 있어야 하므로 content가 아니라 캔버스 직속이다.
        restartHintWrapper = new GameObject("EndButtons", typeof(RectTransform));
        restartHintWrapper.transform.SetParent(canvasGo.transform, false);
        var btnsRt = restartHintWrapper.GetComponent<RectTransform>();
        btnsRt.anchorMin = Vector2.zero;   // 화면 전체를 덮는 배치판 — UIWindow.Place가 시안 좌표를 쓴다
        btnsRt.anchorMax = Vector2.one;
        btnsRt.offsetMin = Vector2.zero;
        btnsRt.offsetMax = Vector2.zero;

        playAgainBtn = CreateEndButton("grave.play_again", BtnY0,          () => LeaveTo(false));
        goHomeBtn    = CreateEndButton("grave.go_home",    BtnY0 + BtnGap, () => LeaveTo(true));

        restartHintWrapper.SetActive(false);

        // 초기 비활성화
        canvasGo.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private GameObject CreateEndButton(string locKey, float designY, UnityEngine.Events.UnityAction onClick)
    {
        // v19: 게임 공통 창 부품(UIWindow)으로 통일. 예전에는 이 화면이 시안에 없어서
        // LayoutElement + 자체 조립이었는데, 0822가 좌표까지 정해줬다.
        var go = UIWindow.MakeButton(LocalizationManager.L(locKey), restartHintWrapper.transform, onClick,
                                     BtnX, designY, BtnW, BtnH, koreanFont, out var tmp);
        endButtonLabels.Add((tmp, locKey));
        return go;
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        var tex = Resources.Load<Texture2D>(resourcePath);
        return tex == null ? null
             : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 납골당을 떠난다. **검은 막을 먼저 올린다** — 곧바로 RestartGame을 부르면 배경 이미지·돌이
    /// 아직 안 붙은 날것의 씬(하늘 그라디언트 + 회색 탁자)이 몇 프레임 그대로 노출된다.
    /// 타이틀에서 게임을 시작할 때 BootCurtain을 쓰는 이유와 똑같은데, 이 경로만 빠져 있었다.
    ///
    /// 막을 내리는 쪽이 갈린다:
    ///   게임 시작(toTitle=false) → GameManager.StartStage가 FadeOut(0.6f)을 부른다.
    ///   타이틀 복귀(toTitle=true) → 아무도 안 부르므로 여기서 직접 내린다.
    /// </summary>
    private void LeaveTo(bool toTitle) => StartCoroutine(CoLeaveTo(toTitle));

    private IEnumerator CoLeaveTo(bool toTitle)
    {
        const float CurtainIn = 0.25f;
        restartHintWrapper.SetActive(false);   // 막이 오르는 동안 연타 방지
        BootCurtain.Instance?.Raise(CurtainIn);
        yield return new WaitForSecondsRealtime(CurtainIn);

        GameManager.Instance?.RestartGame(toTitle);
        if (toTitle) BootCurtain.Instance?.FadeOut(0.5f);
    }

    /// <summary>하단 버튼(Play Again/Go Home) 라벨을 현재 언어로 재설정.</summary>
    private void RefreshEndButtons()
    {
        foreach (var (tmp, key) in endButtonLabels)
            if (tmp != null) tmp.text = LocalizationManager.L(key);
    }

    private static string FormatTime(float seconds)
    {
        // v10: 기획 형식 00:00:00 (HH:MM:SS)
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);
        return $"{h:00}:{m:00}:{s:00}";
    }
}
