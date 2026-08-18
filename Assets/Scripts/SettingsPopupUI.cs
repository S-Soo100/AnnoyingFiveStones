using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈(타이틀)에서 여는 설정 창. 항목: BGM · 효과음 · 언어 · 닫기.
/// Screen Space Overlay Canvas (sortingOrder=260) — 타이틀(250)·일시정지(200) 위에 표시.
///
/// v18: 옛 검정 박스 → UI 시안 창(Figma Settings 572:588)으로 교체.
/// 창 부품은 <see cref="UIWindow"/>가 그린다 — 일시정지 창과 **같은 코드**를 쓴다.
/// 이전에는 여기만 옛 스타일로 남아 있었는데, 원인은 창 만드는 코드가
/// PauseMenuUI 안에만 있어서였다.
///
/// 배치도 시안 그대로다. 슬라이더 2개 + 버튼 2개라 일시정지 창과 칸이 정확히 맞는다:
///   Slide (150,61)·(150,200) / Button (150,339)·(150,459)  ※ 전부 머리띠 83 아래 기준
/// </summary>
public class SettingsPopupUI : MonoBehaviour
{
    public static SettingsPopupUI Instance { get; private set; }

    private const float WinH = 683f;

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private TMP_FontAsset koreanFont;

    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI closeLabel;
    private TextMeshProUGUI languageLabel;

    private TextMeshProUGUI bgmVolumeLabel;
    private Slider bgmVolumeSlider;
    private TextMeshProUGUI sfxVolumeLabel;
    private Slider sfxVolumeSlider;

    private bool isOpen;

    public static SettingsPopupUI EnsureInstance()
    {
        if (Instance == null)
            new GameObject("SettingsPopupUI").AddComponent<SettingsPopupUI>();
        return Instance;
    }

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
    }

    private void Init()
    {
        koreanFont = KoreanFont.GetTMP();

        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

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

        BuildWindow(rootGo.transform);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void BuildWindow(Transform parent)
    {
        UIWindow.Create(parent, "SettingsPanel", LocalizationManager.L("settings.title"),
                        WinH, Close, koreanFont, out var win, out titleLabel);

        bgmVolumeSlider = UIWindow.MakeSlider(win, "BGM", 150f, UIWindow.HeaderH + 61f,
            AudioManager.GetBGMVolume(),
            v => { AudioManager.SetBGMVolume(v); UpdateBGMVolumeLabel(v); },
            koreanFont, out bgmVolumeLabel);

        sfxVolumeSlider = UIWindow.MakeSlider(win, "SFX", 150f, UIWindow.HeaderH + 200f,
            AudioManager.GetSFXVolume(),
            v => { AudioManager.SetSFXVolume(v); UpdateSFXVolumeLabel(v); },
            koreanFont, out sfxVolumeLabel);

        UpdateBGMVolumeLabel(bgmVolumeSlider.value);
        UpdateSFXVolumeLabel(sfxVolumeSlider.value);

        UIWindow.MakeButton("", win, () => LocalizationManager.Toggle(),
                            150f, UIWindow.HeaderH + 339f, 380f, 80f, koreanFont, out languageLabel);
        UIWindow.MakeButton(LocalizationManager.L("settings.close"), win, Close,
                            150f, UIWindow.HeaderH + 459f, 380f, 80f, koreanFont, out closeLabel);
        UpdateLanguageLabel();
    }

    private void UpdateBGMVolumeLabel(float v)
    {
        if (bgmVolumeLabel != null)
            bgmVolumeLabel.text = LocalizationManager.L("pause.music");
    }

    private void UpdateSFXVolumeLabel(float v)
    {
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = LocalizationManager.L("pause.sfx");
    }

    /// <summary>언어 버튼은 "무엇으로 바뀌는지"가 아니라 "지금 무엇인지"를 보여준다.</summary>
    private void UpdateLanguageLabel()
    {
        if (languageLabel != null)
            languageLabel.text = LocalizationManager.L("settings.language") + ": " +
                (LocalizationManager.IsKorean ? "한국어" : "English");
    }

    public void Open()
    {
        isOpen = true;
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;

        // 저장된 BGM/SFX 값을 슬라이더에 재동기화
        if (bgmVolumeSlider != null)
        {
            float bv = AudioManager.GetBGMVolume();
            bgmVolumeSlider.SetValueWithoutNotify(bv);
            UpdateBGMVolumeLabel(bv);
        }
        if (sfxVolumeSlider != null)
        {
            float sv = AudioManager.GetSFXVolume();
            sfxVolumeSlider.SetValueWithoutNotify(sv);
            UpdateSFXVolumeLabel(sv);
        }
        RefreshTexts();
    }

    public void Close()
    {
        isOpen = false;
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private void RefreshTexts()
    {
        if (titleLabel != null) titleLabel.text = LocalizationManager.L("settings.title");
        if (closeLabel != null) closeLabel.text = LocalizationManager.L("settings.close");
        UpdateLanguageLabel();
        if (bgmVolumeSlider != null) UpdateBGMVolumeLabel(bgmVolumeSlider.value);
        if (sfxVolumeSlider != null) UpdateSFXVolumeLabel(sfxVolumeSlider.value);
    }

    private void OnEnable()  { LocalizationManager.OnLanguageChanged += RefreshTexts; }
    private void OnDisable() { LocalizationManager.OnLanguageChanged -= RefreshTexts; }
}
