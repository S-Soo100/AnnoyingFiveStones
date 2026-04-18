using UnityEngine;

public enum GimmickType
{
    None,           // Stage 1 (기본)
    ColorSelect,    // Stage 2 (색깔 선택)
    Flee,           // Stage 3 (도망)
    // Stage 4: None (추후)
    FakeStone,      // Stage 5 (가짜)
    Obstacle,       // Stage 6 (방해물)
    Gravity,        // Stage 7 (중력 변화)
    AgedHand,       // Stage 8 (노화된 손)
    Spotlight,      // Stage 9 (시야 제한)
    Monochrome,     // Stage 10 (모노톤 기억력)
}

/// <summary>
/// 10개 스테이지의 정적 데이터. StageNumber(1~10) 기준으로 접근.
/// </summary>
public class StageConfig
{
    public int StageNumber;      // 1~10
    public int Age;              // 10, 15, 20, 25, 30, 35, 40, 45, 50, 55
    public string StageName;     // "기본 공기", "색깔 선택" 등
    public string Theme;         // "단순한 시작", "호기심의 사춘기" 등
    public string StoryMent;     // 스테이지 시작 멘트
    public GimmickType Gimmick;  // 기믹 타입
    public int TotalStones;      // 기본 5, 일부 스테이지 20

    // 정적 데이터: 10개 스테이지 정의
    private static readonly StageConfig[] stages = new StageConfig[]
    {
        new StageConfig { StageNumber=1,  Age=10, StageName="기본 공기",       Theme="단순한 시작",                     StoryMent="다섯 개의 돌.\n이것이 당신에게 주어진 전부입니다.",          Gimmick=GimmickType.None,        TotalStones=5  },
        new StageConfig { StageNumber=2,  Age=15, StageName="색깔 선택",       Theme="호기심의 사춘기",                  StoryMent="세상에 색이 보이기 시작합니다.\n나에게 맞는 것을 골라야 해요.", Gimmick=GimmickType.ColorSelect, TotalStones=18 },
        new StageConfig { StageNumber=3,  Age=20, StageName="도망가는 공기",   Theme="통제하기 힘든 청춘",               StoryMent="잡으려 할수록 달아나는 것들.\n그래도 쫓아야 합니다.",          Gimmick=GimmickType.Flee,        TotalStones=5  },
        new StageConfig { StageNumber=4,  Age=25, StageName="순서대로 잡기",   Theme="사회 룰에 적응하는 초년생",        StoryMent="반이 지났습니다.\n남은 반은 더 빨리 갑니다.",                   Gimmick=GimmickType.None,        TotalStones=5  },
        new StageConfig { StageNumber=5,  Age=30, StageName="분신 가짜 잡기",  Theme="부딪혀봐야 본색을 드러내는 가짜들", StoryMent="겉보기엔 똑같아 보입니다.\n가까이 가야 진짜를 알 수 있어요.",  Gimmick=GimmickType.FakeStone,   TotalStones=20 },
        new StageConfig { StageNumber=6,  Age=35, StageName="움직이는 방해물", Theme="삶에 끼어드는 방해꾼",             StoryMent="손끝이 예전 같지 않습니다.\n그래도, 놓지 마세요.",              Gimmick=GimmickType.Obstacle,    TotalStones=5  },
        new StageConfig { StageNumber=7,  Age=40, StageName="중력 변화",       Theme="어깨를 짓누르는 삶의 무게",        StoryMent="무거워지는 것은 돌만이 아닙니다.",                              Gimmick=GimmickType.Gravity,     TotalStones=5  },
        new StageConfig { StageNumber=8,  Age=45, StageName="노화된 손",       Theme="몸이 안 따라주는",                StoryMent="마음은 앞서는데\n손이 따라오지 않습니다.",                      Gimmick=GimmickType.AgedHand,    TotalStones=5  },
        new StageConfig { StageNumber=9,  Age=50, StageName="시야 제한",       Theme="서서히 좁아지는 시야",             StoryMent="보이는 것이 줄어듭니다.\n그래도 손은 기억합니다.",              Gimmick=GimmickType.Spotlight,   TotalStones=5  },
        new StageConfig { StageNumber=10, Age=55, StageName="모노톤",          Theme="색이 바랜 노년",                  StoryMent="마지막 한 바퀴.\n여기까지 온 것만으로도 충분합니다.",             Gimmick=GimmickType.Monochrome,  TotalStones=5  },
    };

    /// <summary>stageNumber(1~10)로 설정 반환. 범위 초과 시 클램프.</summary>
    public static StageConfig Get(int stageNumber)
    {
        int index = Mathf.Clamp(stageNumber - 1, 0, stages.Length - 1);
        return stages[index];
    }

    public static int TotalStages => stages.Length; // 10
}
