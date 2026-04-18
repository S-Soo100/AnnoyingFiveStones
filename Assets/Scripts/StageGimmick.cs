using UnityEngine;

/// <summary>
/// 스테이지 기믹 베이스 클래스. Phase A에서는 껍데기만 정의.
/// 서브클래스는 Phase C~F에서 추가 예정.
/// </summary>
public abstract class StageGimmick
{
    protected GameManager gameManager;

    public virtual void OnStageStart(int stageInLoop) { }
    public virtual void OnScatterComplete(Stone[] activeStones) { }
    public virtual void OnPickPhaseStart() { }
    public virtual void OnStonePicked(Stone stone) { }
    public virtual void OnThrowStart(Stone thrownStone) { }
    public virtual void OnCatchPhaseStart() { }
    public virtual void OnStageEnd() { }
    public virtual void OnUpdate() { }

    /// <summary>줍기 허용 여부. true=허용, false=실패.</summary>
    public virtual bool ValidatePick(Stone stone) => true;

    /// <summary>
    /// 받기 성공 후 클리어 판정. 기본 = remainingOnBoard == 0.
    /// 기믹이 오버라이드하면 기존 판정 대신 기믹 판정을 사용.
    /// </summary>
    public virtual bool IsRoundComplete(int pickedThisRound, int remainingOnBoard)
    {
        return remainingOnBoard == 0;
    }

    public void Init(GameManager gm) { gameManager = gm; }

    /// <summary>GimmickType에 따라 인스턴스 생성. None이면 null 반환.</summary>
    public static StageGimmick Create(GimmickType type, GameManager gm)
    {
        StageGimmick gimmick = type switch
        {
            GimmickType.None        => null,
            GimmickType.ColorSelect => new ColorSelectGimmick(),
            GimmickType.Flee        => new FleeGimmick(),
            GimmickType.FakeStone   => new FakeStoneGimmick(),
            GimmickType.Obstacle    => new ObstacleGimmick(),
            GimmickType.Gravity     => new GravityGimmick(),
            GimmickType.AgedHand    => new AgedHandGimmick(),
            GimmickType.Spotlight   => new SpotlightGimmick(),
            GimmickType.Monochrome  => new MonochromeGimmick(),
            _                       => null,
        };
        gimmick?.Init(gm);
        Debug.Log(gimmick != null
            ? $"[StageGimmick] Created gimmick: {type}"
            : $"[StageGimmick] No gimmick for type: {type}");
        return gimmick;
    }
}
