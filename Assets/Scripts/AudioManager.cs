using UnityEngine;
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

    private AudioSource sfxSource;       // 짧은 효과음용
    private AudioSource jingleSource;    // 징글용 (겹치지 않게)

    private Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    // 게이지 틱 쿨다운 (너무 빠르게 반복 방지)
    private float lastGaugeTickTime;
    private const float GaugeTickInterval = 0.15f;

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

        LoadAllClips();
    }

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

    /// <summary>징글 재생 (기존 징글 중단 후 재생)</summary>
    public void PlayJingle(string clipName, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null) return;

        jingleSource.Stop();
        jingleSource.clip = clip;
        jingleSource.volume = jingleVolume * volumeScale;
        jingleSource.pitch = 1f;
        jingleSource.Play();
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
