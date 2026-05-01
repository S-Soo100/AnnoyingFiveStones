using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 사운드 매니저: Resources/SFX에서 클립을 로드하고 재생.
/// 싱글톤 — GameManager 오브젝트에 AddComponent하거나 씬에 빈 오브젝트로 배치.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume")]
    [SerializeField] private float sfxVolume = 0.7f;
    [SerializeField] private float jingleVolume = 0.5f;

    private const string MasterVolumePrefKey = "MasterVolume";
    private const float DefaultMasterVolume = 0.5f;

    private AudioSource sfxSource;       // 짧은 효과음용
    private AudioSource jingleSource;    // 징글용 (겹치지 않게)

    private Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    // 게이지 틱 쿨다운 (너무 빠르게 반복 방지)
    private float lastGaugeTickTime;
    private const float GaugeTickInterval = 0.15f;

    // ──────────────────────────────────────────────────────────────────
    // BGM 시스템
    // ──────────────────────────────────────────────────────────────────

    private const string BGMVolumePrefKey = "BGMVolume";
    private const float DefaultBGMVolume = 0.30f;

    [Header("BGM Fade Settings")]
    [SerializeField] private float bgmFadeInDuration    = 1.5f;
    [SerializeField] private float bgmCrossfadeDuration = 1.5f;
    [SerializeField] private float bgmFadeOutDuration   = 1.5f;
    [SerializeField] private float bgmDuckDownDuration  = 0.4f;
    [SerializeField] private float bgmDuckUpDuration    = 0.6f;
    [SerializeField] private float bgmDuckMultiplier    = 0.15f;

    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private int         activeBgm   = 0;      // 0 = A, 1 = B
    private int         currentTrack = -1;    // -1 = 재생 없음
    private float       bgmVolume;            // 사용자 슬라이더 값 (0~1)
    private float       currentBaseVolume;    // crossfade 코루틴이 관리 (0~1)
    private float       currentDuckMult      = 1f; // duck 코루틴이 관리 (0~1)
    private bool        isBgmFadingOut       = false; // StopGameplayBGM(fade=true) 진행 중 플래그

    private AudioClip[] bgmClips = new AudioClip[4]; // 0=youth, 1=adult, 2=middle, 3=late

    private Coroutine bgmCoroutine;           // 크로스페이드/페이드인/아웃 전용
    private Coroutine duckCoroutine;          // duck 전용 (bgmCoroutine과 독립)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        jingleSource = gameObject.AddComponent<AudioSource>();
        jingleSource.playOnAwake = false;

        // BGM AudioSource A/B
        bgmSourceA = gameObject.AddComponent<AudioSource>();
        bgmSourceA.playOnAwake = false;
        bgmSourceA.loop = true;

        bgmSourceB = gameObject.AddComponent<AudioSource>();
        bgmSourceB.playOnAwake = false;
        bgmSourceB.loop = true;

        bgmVolume = GetBGMVolume();

        ApplyVolume(GetMasterVolume());
        LoadAllClips();
        LoadBGMClips();
    }

    // ──────────────────────────────────────────────────────────────────
    // Master Volume API (기존 — 변경 없음)
    // ──────────────────────────────────────────────────────────────────

    public static float GetMasterVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, DefaultMasterVolume));
    }

    /// <summary>AudioListener.volume만 즉시 적용 (저장 X). 드래그 중 고빈도 호출용.</summary>
    public static void ApplyVolume(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
    }

    /// <summary>볼륨 적용 + PlayerPrefs 저장. 포인터 뗄 때 / 복귀 완료 시점에서 호출.</summary>
    public static void SetMasterVolume(float v)
    {
        v = Mathf.Clamp01(v);
        ApplyVolume(v);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, v);
        PlayerPrefs.Save();
    }

    // ──────────────────────────────────────────────────────────────────
    // BGM Volume API (신규)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>PlayerPrefs에서 BGM 볼륨 읽기. 기본 0.30.</summary>
    public static float GetBGMVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(BGMVolumePrefKey, DefaultBGMVolume));
    }

    /// <summary>슬라이더 드래그 중 즉시 반영 + 저장. BGM은 매 호출 저장해도 가볍다.</summary>
    public static void ApplyBGMVolume(float v)
    {
        v = Mathf.Clamp01(v);
        if (Instance != null)
        {
            Instance.bgmVolume = v;
            Instance.UpdateBGMVolumeOutput();
        }
    }

    /// <summary>저장 포함 (ApplyBGMVolume과 동일 — 슬라이더 onValueChanged에서 호출).</summary>
    public static void SetBGMVolume(float v)
    {
        v = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(BGMVolumePrefKey, v);
        PlayerPrefs.Save();
        ApplyBGMVolume(v);
    }

    // ──────────────────────────────────────────────────────────────────
    // BGM 게임 흐름 API (신규)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>나이로 트랙 결정 + 재생. 같은 트랙이면 no-op.</summary>
    public void PlayGameplayBGM(int age)
    {
        int targetTrack = AgeToTrackIndex(age);

        if (bgmClips[targetTrack] == null)
        {
            Debug.LogWarning($"[AudioManager] BGM clip for track {targetTrack} not loaded. Skipping.");
            return;
        }

        // 첫 시작 (currentTrack == -1 or 재생 소스 없음)
        if (currentTrack == -1)
        {
            currentTrack = targetTrack;
            currentBaseVolume = 0f;
            currentDuckMult = 1f;
            isBgmFadingOut = false;

            var src = GetActiveSource();
            src.clip = bgmClips[targetTrack];
            src.volume = 0f;
            src.Play();

            if (bgmCoroutine != null) StopCoroutine(bgmCoroutine);
            bgmCoroutine = StartCoroutine(BgmFadeInRoutine(bgmFadeInDuration));
            return;
        }

        // 같은 트랙 — no-op (R2)
        if (targetTrack == currentTrack)
            return;

        // 다른 트랙 — 크로스페이드
        StartCrossfadeTo(targetTrack);
    }

    /// <summary>BGM 정지. fade=true면 bgmFadeOutDuration 동안 페이드아웃, false면 즉시.</summary>
    public void StopGameplayBGM(bool fade = true)
    {
        if (currentTrack == -1) return; // 이미 정지 상태

        if (!fade)
        {
            if (bgmCoroutine != null) { StopCoroutine(bgmCoroutine); bgmCoroutine = null; }
            if (duckCoroutine != null) { StopCoroutine(duckCoroutine); duckCoroutine = null; }
            GetActiveSource().Stop();
            GetInactiveSource().Stop();
            currentTrack = -1;
            currentBaseVolume = 0f;
            currentDuckMult = 1f;
            isBgmFadingOut = false;
            return;
        }

        // 페이드아웃 코루틴 시작
        if (bgmCoroutine != null) StopCoroutine(bgmCoroutine);
        isBgmFadingOut = true;
        bgmCoroutine = StartCoroutine(BgmFadeOutRoutine(bgmFadeOutDuration));
    }

    /// <summary>BGM 일시정지 (활성 소스만 Pause).</summary>
    public void PauseBGM()
    {
        if (currentTrack == -1) return;
        GetActiveSource().Pause();
    }

    /// <summary>BGM 재개.</summary>
    public void ResumeBGM()
    {
        if (currentTrack == -1) return;
        GetActiveSource().UnPause();
        UpdateBGMVolumeOutput();
    }

    /// <summary>징글 길이만큼 BGM을 duck. BGM 정지/페이드아웃 중이면 no-op (D8, R6).</summary>
    public void DuckForJingle(float duration)
    {
        if (currentTrack == -1) return;         // BGM 미재생 — no-op
        if (isBgmFadingOut) return;             // 페이드아웃 중 — no-op (D8)
        if (!GetActiveSource().isPlaying && !GetActiveSource().clip) return; // 재생 없음 — no-op

        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(DuckRoutine(duration));
    }

    // ──────────────────────────────────────────────────────────────────
    // Private 유틸
    // ──────────────────────────────────────────────────────────────────

    private int AgeToTrackIndex(int age)
    {
        if (age < 20) return 0;   // bgm_youth  (10, 15)
        if (age < 35) return 1;   // bgm_adult  (20, 25, 30)
        if (age < 50) return 2;   // bgm_middle (35, 40, 45)
        return 3;                 // bgm_late   (50, 55, 60+)
    }

    private AudioSource GetActiveSource()   => activeBgm == 0 ? bgmSourceA : bgmSourceB;
    private AudioSource GetInactiveSource() => activeBgm == 0 ? bgmSourceB : bgmSourceA;

    private void StartCrossfadeTo(int trackIndex)
    {
        currentTrack = trackIndex; // 즉시 업데이트 — 다음 no-op 판단에 사용 (설계서 §6 시나리오2)

        if (bgmCoroutine != null) StopCoroutine(bgmCoroutine);
        isBgmFadingOut = false;
        bgmCoroutine = StartCoroutine(BgmFadeRoutine(trackIndex, bgmCrossfadeDuration));
    }

    private IEnumerator BgmFadeInRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentBaseVolume = Mathf.Clamp01(elapsed / duration);
            UpdateBGMVolumeOutput();
            yield return null;
        }
        currentBaseVolume = 1f;
        UpdateBGMVolumeOutput();
        bgmCoroutine = null;
    }

    private IEnumerator BgmFadeRoutine(int targetTrack, float duration)
    {
        // 비활성 소스에 새 클립 세팅 후 재생 (volume=0)
        var inactive = GetInactiveSource();
        inactive.clip = bgmClips[targetTrack];
        inactive.volume = 0f;
        inactive.Play();

        float startBase = currentBaseVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 활성 소스: startBase → 0
            currentBaseVolume = Mathf.Lerp(startBase, 0f, t);
            UpdateBGMVolumeOutput();

            // 비활성 소스: 0 → bgmVolume (직접 계산)
            inactive.volume = bgmVolume * Mathf.Lerp(0f, 1f, t) * currentDuckMult;

            yield return null;
        }

        // 전환 완료: 이전 활성 소스 정지, 인덱스 스왑
        GetActiveSource().Stop();
        activeBgm = 1 - activeBgm;
        currentBaseVolume = 1f;
        UpdateBGMVolumeOutput();
        bgmCoroutine = null;
    }

    private IEnumerator BgmFadeOutRoutine(float duration)
    {
        float startBase = currentBaseVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentBaseVolume = Mathf.Lerp(startBase, 0f, Mathf.Clamp01(elapsed / duration));
            UpdateBGMVolumeOutput();
            yield return null;
        }

        GetActiveSource().Stop();
        GetInactiveSource().Stop();
        currentTrack = -1;
        currentBaseVolume = 0f;
        isBgmFadingOut = false;
        bgmCoroutine = null;
    }

    private IEnumerator DuckRoutine(float jingleDuration)
    {
        // Duck down
        float elapsed = 0f;
        float startMult = currentDuckMult;
        while (elapsed < bgmDuckDownDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentDuckMult = Mathf.Lerp(startMult, bgmDuckMultiplier, Mathf.Clamp01(elapsed / bgmDuckDownDuration));
            UpdateBGMVolumeOutput();
            yield return null;
        }
        currentDuckMult = bgmDuckMultiplier;
        UpdateBGMVolumeOutput();

        // 징글 길이 대기 (duck down 시간 제외)
        float holdDuration = Mathf.Max(0f, jingleDuration - bgmDuckDownDuration);
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Duck up
        elapsed = 0f;
        startMult = currentDuckMult;
        while (elapsed < bgmDuckUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentDuckMult = Mathf.Lerp(startMult, 1f, Mathf.Clamp01(elapsed / bgmDuckUpDuration));
            UpdateBGMVolumeOutput();
            yield return null;
        }
        currentDuckMult = 1f;
        UpdateBGMVolumeOutput();
        duckCoroutine = null;
    }

    /// <summary>활성 BGM 소스 볼륨 재계산. 슬라이더 변경·duck·crossfade 매 프레임 호출.</summary>
    private void UpdateBGMVolumeOutput()
    {
        float vol = bgmVolume * currentBaseVolume * currentDuckMult;
        GetActiveSource().volume = Mathf.Clamp01(vol);
    }

    private void LoadBGMClips()
    {
        AudioClip[] loaded = Resources.LoadAll<AudioClip>("BGM");
        foreach (var clip in loaded)
        {
            switch (clip.name)
            {
                case "bgm_youth":  bgmClips[0] = clip; break;
                case "bgm_adult":  bgmClips[1] = clip; break;
                case "bgm_middle": bgmClips[2] = clip; break;
                case "bgm_late":   bgmClips[3] = clip; break;
            }
        }
        Debug.Log($"[AudioManager] Loaded {loaded.Length} BGM clips.");
        for (int i = 0; i < bgmClips.Length; i++)
        {
            if (bgmClips[i] == null)
                Debug.LogWarning($"[AudioManager] BGM clip index {i} is null — check Resources/BGM/ folder.");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // SFX / Jingle (기존 — 변경 없음)
    // ──────────────────────────────────────────────────────────────────

    private void LoadAllClips()
    {
        AudioClip[] all = Resources.LoadAll<AudioClip>("SFX");
        foreach (var clip in all)
        {
            clips[clip.name] = clip;
        }
        Debug.Log($"[AudioManager] Loaded {clips.Count} sound clips.");
    }

    private AudioClip GetClip(string name)
    {
        clips.TryGetValue(name, out var clip);
        if (clip == null)
            Debug.LogWarning($"[AudioManager] Clip not found: {name}");
        return clip;
    }

    /// <summary>효과음 재생 (볼륨/피치 커스텀 가능)</summary>
    public void PlaySFX(string clipName, float volumeScale = 1f, float pitch = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null) return;

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    /// <summary>징글 재생 (기존 징글 중단 후 재생). BGM duck 자동 트리거.</summary>
    public void PlayJingle(string clipName, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null) return;

        jingleSource.Stop();
        jingleSource.clip = clip;
        jingleSource.volume = jingleVolume * volumeScale;
        jingleSource.pitch = 1f;
        jingleSource.Play();

        // D8: BGM duck 자동 트리거 (BGM 정지/페이드아웃 중이면 DuckForJingle 내부에서 no-op)
        DuckForJingle(clip.length);
    }

    // === 게임 이벤트별 편의 메서드 ===

    /// <summary>게이지 왕복 틱 (쿨다운 적용)</summary>
    public void PlayGaugeTick()
    {
        if (Time.time - lastGaugeTickTime < GaugeTickInterval) return;
        lastGaugeTickTime = Time.time;
        PlaySFX("gauge_tick", 0.4f);
    }

    /// <summary>게이지 확정 (손 놓음)</summary>
    public void PlayGaugeConfirm() => PlaySFX("gauge_confirm", 0.8f);

    /// <summary>돌 흩어짐 (인덱스별 다른 파일)</summary>
    public void PlayScatterHit(int stoneIndex)
    {
        int idx = Mathf.Clamp(stoneIndex, 0, 4);
        float pitch = 0.9f + idx * 0.05f; // 약간씩 다른 피치
        PlaySFX($"scatter_hit_{idx}", 0.6f, pitch);
    }

    /// <summary>장외 발생</summary>
    public void PlayOutOfBounds() => PlaySFX("out_of_bounds", 1f);

    /// <summary>던질 돌 자동 줍기</summary>
    public void PlayStonePickThrow() => PlaySFX("stone_pick_throw", 0.7f);

    /// <summary>돌 던지기 (상승)</summary>
    public void PlayThrowUp() => PlaySFX("throw_up", 0.6f);

    /// <summary>돌 최고점</summary>
    public void PlayThrowPeak() => PlaySFX("throw_peak", 0.3f);

    /// <summary>돌 낙하</summary>
    public void PlayThrowDown() => PlaySFX("throw_down", 0.5f);

    /// <summary>바닥 돌 줍기 (누적 카운트로 피치 변화)</summary>
    public void PlayStonePick(int pickCount)
    {
        string name = pickCount % 2 == 0 ? "stone_pick" : "stone_pick_alt";
        float pitch = 1f + pickCount * 0.08f; // 점점 높은 피치
        PlaySFX(name, 0.6f, pitch);
    }

    /// <summary>초과 줍기 실패</summary>
    public void PlayPickExcess() => PlaySFX("pick_excess", 1f);

    /// <summary>받기 성공</summary>
    public void PlayCatchSuccess() => PlaySFX("catch_success", 0.8f);

    /// <summary>받기 실패</summary>
    public void PlayCatchFail() => PlaySFX("catch_fail", 0.8f);

    /// <summary>단계 인트로 (1~4단)</summary>
    public void PlayStageIntro() => PlayJingle("stage_intro");

    /// <summary>5단 인트로</summary>
    public void PlayStage5Intro() => PlayJingle("stage5_intro");

    /// <summary>단계 클리어</summary>
    public void PlayStageClear() => PlayJingle("stage_clear");

    /// <summary>ALL CLEAR</summary>
    public void PlayAllClear() => PlayJingle("all_clear");

    /// <summary>실패</summary>
    public void PlayFail() => PlayJingle("fail");

    /// <summary>5단 동시 던지기</summary>
    public void PlayStage5Toss() => PlaySFX("stage5_toss", 0.8f);

    /// <summary>5단 개별 캐치</summary>
    public void PlayStage5CatchStone(int count)
    {
        float pitch = 0.9f + count * 0.06f;
        PlaySFX("stage5_catch_stone", 0.7f, pitch);
    }
}
