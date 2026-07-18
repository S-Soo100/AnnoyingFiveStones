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
    [SerializeField] private float sfxVolume = 0.5f;
    [SerializeField] private float jingleVolume = 0.5f;

    private const string SFXVolumePrefKey = "sfx_volume";
    private const float DefaultSFXVolume = 0.5f; // sfxVolume 인스턴스 초기값과 일치

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
    private const float DefaultBGMVolume = 0.5f;

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

    private AudioClip[] bgmClips = new AudioClip[5]; // 0=age10, 1=age20, 2=age30, 3=age40, 4=age50

    // v12-fix: 콜드 부팅 큐 — LoadBGMClips 완료 전 들어온 PlayGameplayBGM(age) 호출을 보관했다가 로드 후 재생.
    // (TitleScreenUI.Show()가 AudioManager.Start()의 1프레임 yield보다 먼저 실행되는 케이스)
    private int pendingBgmAge = -1; // -1 = 큐잉된 요청 없음

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

        // v13: 오디오 설정 일회성 마이그레이션 — 저장된 BGM/SFX=0 무음 복구 + 마스터 개념 제거 + 새 튜토리얼 1회 재노출
        if (PlayerPrefs.GetInt("SettingsMigrationV13", 0) == 0)
        {
            // 무음(0) 또는 미저장(-1)일 때만 0.5로 복구 — 사용자가 설정한 커스텀 볼륨(예: 0.3)은 보존.
            if (PlayerPrefs.GetFloat("BGMVolume", -1f) <= 0f) PlayerPrefs.SetFloat("BGMVolume", 0.5f);
            if (PlayerPrefs.GetFloat("sfx_volume", -1f) <= 0f) PlayerPrefs.SetFloat("sfx_volume", 0.5f);
            PlayerPrefs.DeleteKey("MasterVolume");
            PlayerPrefs.DeleteKey("TutorialSeen");
            PlayerPrefs.SetInt("SettingsMigrationV13", 1);
            PlayerPrefs.Save();
        }

        sfxSource    = CreateChildSource("SFX_Source",    loop: false);
        jingleSource = CreateChildSource("Jingle_Source", loop: false);
        bgmSourceA   = CreateChildSource("BGM_A_Source",  loop: true);
        bgmSourceB   = CreateChildSource("BGM_B_Source",  loop: true);

        bgmVolume = GetBGMVolume();
        sfxVolume = GetSFXVolume();

        // v13: 마스터 슬라이더 제거 → AudioListener.volume을 1로 고정. 이후 BGM/SFX가 유일한 볼륨 제어.
        AudioListener.volume = 1f;
    }

    private IEnumerator Start()
    {
        if (Instance != this) yield break;  // 싱글톤 가드 — Destroy 예약된 중복 인스턴스 차단
        yield return null;                  // 1프레임 대기 — AudioSource 초기화 + FMOD 출력 채널 안정화
        LoadAllClips();
        LoadBGMClips();
    }

    private AudioSource CreateChildSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;  // 2D 사운드 고정
        return src;
    }

    // ──────────────────────────────────────────────────────────────────
    // AudioListener 볼륨: v13에서 마스터 슬라이더 제거 → Awake에서 AudioListener.volume=1f로 고정.
    // 이후 볼륨 제어는 BGM/SFX API가 전담 (별도 마스터 적용 메서드 없음).
    // ──────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────
    // BGM Volume API (신규)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>PlayerPrefs에서 BGM 볼륨 읽기. 기본 0.50.</summary>
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
    // SFX Volume API (신규 — BGM API 미러링. PlaySFX가 sfxVolume을 재생 시점에 직접 곱하므로 output 갱신 불필요)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>PlayerPrefs에서 SFX 볼륨 읽기. 기본 0.50.</summary>
    public static float GetSFXVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(SFXVolumePrefKey, DefaultSFXVolume));
    }

    /// <summary>슬라이더 드래그 중 즉시 반영. 다음 PlaySFX부터 새 볼륨 적용.</summary>
    public static void ApplySFXVolume(float v)
    {
        v = Mathf.Clamp01(v);
        if (Instance != null) Instance.sfxVolume = v;
    }

    /// <summary>저장 포함 (슬라이더 onValueChanged에서 호출).</summary>
    public static void SetSFXVolume(float v)
    {
        v = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(SFXVolumePrefKey, v);
        PlayerPrefs.Save();
        ApplySFXVolume(v);
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
            // v12-fix: 콜드 부팅 시 LoadBGMClips() 완료 전 호출 — age 큐잉 후 로드 완료 시 재시도.
            // (TitleScreenUI.Show() → PlayLobbyBGM이 AudioManager.Start()의 1프레임 yield보다 먼저 실행되는 케이스)
            pendingBgmAge = age;
            Debug.LogWarning($"[AudioManager] BGM clip for track {targetTrack} not loaded yet. Queuing age={age}.");
            return;
        }
        pendingBgmAge = -1; // 정상 재생 진입 — 큐 클리어

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

    /// <summary>로비(타이틀) BGM. 1단(age=10)과 같은 트랙을 재생. 게임 진입 시 no-op으로 무중단.</summary>
    public void PlayLobbyBGM() => PlayGameplayBGM(10);

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
        if (age < 20) return 0;   // bgm_age10 (10, 15)
        if (age < 30) return 1;   // bgm_age20 (20, 25) — 임시 공유, 추후 20.m4a 분리
        if (age < 40) return 2;   // bgm_age30 (30, 35)
        if (age < 50) return 3;   // bgm_age40 (40, 45)
        return 4;                 // bgm_age50 (50, 55, 60+)
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
                case "bgm_age10": bgmClips[0] = clip; break;
                case "bgm_age20": bgmClips[1] = clip; break;
                case "bgm_age30": bgmClips[2] = clip; break;
                case "bgm_age40": bgmClips[3] = clip; break;
                case "bgm_age50": bgmClips[4] = clip; break;
            }
        }
        Debug.Log($"[AudioManager] Loaded {loaded.Length} BGM clips.");
        for (int i = 0; i < bgmClips.Length; i++)
        {
            if (bgmClips[i] == null)
                Debug.LogWarning($"[AudioManager] BGM clip index {i} is null — check Resources/BGM/ folder.");
        }

        // v12-fix: 콜드 부팅 큐잉된 BGM 요청 재생 (PlayGameplayBGM이 clip null로 스킵됐을 때 보관됐던 age)
        if (pendingBgmAge >= 0)
        {
            int age = pendingBgmAge;
            pendingBgmAge = -1; // 재시도 전에 클리어 (재시도가 또 실패해도 무한 재귀 차단)
            Debug.Log($"[AudioManager] Retrying queued BGM after LoadBGMClips: age={age}");
            PlayGameplayBGM(age);
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
