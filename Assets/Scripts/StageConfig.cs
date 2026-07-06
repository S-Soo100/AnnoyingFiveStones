using UnityEngine;

// BackgroundProp은 BackgroundProp.cs에 정의됨 (동일 어셈블리)

public enum GimmickType
{
    None,           // Stage 1 (기본)
    ColorSelect,    // Stage 2 (색깔 선택)
    Flee,           // Stage 3 (도망)
    Sequence,       // Stage 4 (순서대로 잡기)
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
    public string StoryMent;     // 스테이지 시작 멘트 (한국어)
    public string StoryMentEn;   // v10 다국어: 영어 내레이션 (260703 사용자 제공). 비면 StoryMent 폴백
    public GimmickType Gimmick;  // 기믹 타입
    public int TotalStones;      // 기본 5, 일부 스테이지 20

    /// <summary>현재 언어에 맞는 스토리멘트. 영어 미지정 시 한국어 폴백.</summary>
    public string LocalizedStoryMent =>
        LocalizationManager.IsKorean || string.IsNullOrEmpty(StoryMentEn) ? StoryMent : StoryMentEn;

    // v7-3 난이도 파라미터 (미지정 시 디폴트 = 효과 없음)
    public float ScatterDropHeightAdd = 0f;      // ScatterSystem 시작 Y 가산 (m). 0=기존 동작
    public float ScatterSpreadMultiplier = 1f;   // baseSpreadRadius 배율. 1=기존 동작
    public float BallSpeedMultiplier = 1f;       // ObstacleGimmick.SpawnBalls 공 속도 배율. 1=기존 동작

    // v8-2 배경 무드 필드 (placeholder)
    public Color SkyBottom = new Color(0.53f, 0.81f, 0.98f);
    public Color SkyTop    = new Color(0.08f, 0.25f, 0.65f);
    public Color TableColor = Color.white;
    public Color ClothColor = Color.white;
    public BackgroundProp[] Props = System.Array.Empty<BackgroundProp>();

    // v8-2b 풀스크린 배경 이미지 (Resources/{경로}). null/empty면 placeholder(Sky/Props) 사용.
    public string BackgroundImage = null;
    // v8-2c 이미지 매트 정렬 미세조정. offset=quad localPos.xy 가산, scale=화면 채움 배율(1=정확히 화면).
    public Vector2 BgImageOffset = Vector2.zero;
    public Vector2 BgImageScale  = Vector2.one;

    // v8-2e 매트(책상 상판) 별도 레이어. null/empty면 매트 레이어 미사용(기존 stage = 그대로).
    public string MatImage = null;  // Resources 경로. 예: "StageBackgrounds/Stage02_Desk"
    // v8-2f 매트/보드 분리 (책상 시각 ≠ 플레이 영역). Size=(0,0)이면 미설정 → 기존 동작 폴백.
    public Vector2 MatCenter   = Vector2.zero;  // 책상 quad 월드 중심
    public Vector2 MatSize     = Vector2.zero;  // 책상 quad 월드 크기(PNG 전체). (0,0)=미설정→Cloth.bounds 폴백
    public Vector2 BoardCenter = Vector2.zero;  // 플레이 영역(BoardBounds) 월드 중심
    public Vector2 BoardSize   = Vector2.zero;  // 플레이 영역 월드 크기. (0,0)=미설정→override 없음

    // v9: 사다리꼴 플레이 영역 ([0]=BL, [1]=BR, [2]=FL, [3]=FR). null이면 BoardCenter/Size Rect 폴백.
    public Vector2[] BoardQuad = null;

    // 정적 데이터: 10개 스테이지 정의
    private static readonly StageConfig[] stages = new StageConfig[]
    {
        // Stage 1 — Age 10, 연노랑 하늘
        new StageConfig
        {
            StageNumber=1, Age=10, StageName="기본 공기", Theme="단순한 시작",
            StoryMent="자 시작합니다.\n한 판 놀아볼까요?",
            StoryMentEn="Let's begin.\nShall we play a game?",
            Gimmick=GimmickType.None, TotalStones=5,
            SkyBottom=new Color(1.0f,0.95f,0.7f), SkyTop=new Color(0.55f,0.85f,1.0f),
            TableColor=new Color(0.95f,0.85f,0.7f), ClothColor=new Color(1.0f,0.5f,0.5f),
            BackgroundImage = "StageBackgrounds/Stage02_Classroom",
            MatImage        = "StageBackgrounds/Stage02_Desk",
            MatCenter       = new Vector2(-0.14f, -2.70f),
            MatSize         = new Vector2(20.6f, 11.6f),
            BoardCenter     = new Vector2(0f, -4.8f),
            BoardSize       = new Vector2(10.6f, 3.8f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=System.Array.Empty<BackgroundProp>(),
        },
        // Stage 2 — Age 15, 교실 배경 + 매트(책상) 레이어
        new StageConfig
        {
            StageNumber=2, Age=15, StageName="색깔 선택", Theme="호기심의 사춘기",
            StoryMent="세상이 온통 색깔로 가득합니다.\n하지만 다 가질 수는 없습니다.",
            StoryMentEn="The world is full of colors.\nBut you cannot have them all.",
            Gimmick=GimmickType.ColorSelect, TotalStones=18,
            SkyBottom=new Color(1.0f,0.8f,0.85f), SkyTop=new Color(0.55f,0.7f,0.95f),
            TableColor=new Color(0.6f,0.5f,0.4f), ClothColor=new Color(0.6f,0.65f,0.75f),
            MatImage        = "StageBackgrounds/Stage02_Desk",
            MatCenter       = new Vector2(-0.14f, -2.70f),
            MatSize         = new Vector2(20.6f, 11.6f),
            BoardCenter     = new Vector2(0f, -4.8f),
            BoardSize       = new Vector2(10.6f, 3.8f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=System.Array.Empty<BackgroundProp>(),
        },
        // Stage 3 — Age 20, 대학 캠퍼스 (사용자 픽셀아트 이미지)
        new StageConfig
        {
            StageNumber=3, Age=20, StageName="도망가는 공기", Theme="통제하기 힘든 청춘",
            StoryMent="잡으려 할수록 달아납니다.\n쉴 새 없이 쫓으세요.",
            StoryMentEn="The more you try to catch it, the more it slips away.\nChase it without rest.",
            Gimmick=GimmickType.Flee, TotalStones=5,
            SkyBottom=new Color(0.7f,0.9f,1.0f), SkyTop=new Color(0.3f,0.65f,0.95f),
            TableColor=new Color(0.4f,0.7f,0.35f), ClothColor=new Color(0.4f,0.75f,0.7f),
            BackgroundImage="StageBackgrounds/Stage03_Campus",
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌 (유지)
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우 (유지)
                new Vector2(-5.20f, -7.10f),  // FL 앞-좌 (SafeZone x±5.3 내부, 0.1 margin)
                new Vector2( 5.20f, -7.10f),  // FR 앞-우 (SafeZone x±5.3 내부, 0.1 margin)
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cylinder, Color=new Color(0.25f,0.6f,0.3f), Position=new Vector3(-3.2f,1.0f,0.5f), Scale=new Vector3(0.4f,1.2f,0.4f) },
                new BackgroundProp { Shape=BackgroundPropShape.Cylinder, Color=new Color(0.25f,0.6f,0.3f), Position=new Vector3(3.2f,1.0f,0.5f),  Scale=new Vector3(0.4f,1.2f,0.4f) },
                new BackgroundProp { Shape=BackgroundPropShape.Cube,     Color=new Color(0.6f,0.55f,0.5f), Position=new Vector3(0f,1.5f,1.0f),    Scale=new Vector3(1.4f,1.0f,0.3f) },
            },
        },
        // Stage 4 — Age 25, 흐린 회청
        new StageConfig
        {
            StageNumber=4, Age=25, StageName="순서대로 잡기", Theme="사회 룰에 적응하는 초년생",
            StoryMent="세상이 정한 순서를\n따라야 할 때입니다.",
            StoryMentEn="It is time to follow\nthe rules set by the world.",
            Gimmick=GimmickType.Sequence, TotalStones=5,
            SkyBottom=new Color(0.7f,0.75f,0.8f), SkyTop=new Color(0.45f,0.5f,0.6f),
            TableColor=new Color(0.5f,0.45f,0.4f), ClothColor=new Color(0.85f,0.78f,0.65f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cube, Color=new Color(0.95f,0.95f,0.95f), Position=new Vector3(-3f,1.0f,0.5f), Scale=new Vector3(0.9f,0.6f,0.1f) },
            },
        },
        // Stage 5 — Age 30, 직장인 책상 (사용자 스케치 이미지)
        // v8-1 swap: 메카닉=움직이는 방해물 (구 6단). 배경은 Age 기준 유지.
        new StageConfig
        {
            StageNumber=5, Age=30, StageName="움직이는 방해물", Theme="삶에 끼어드는 방해꾼",
            StoryMent="사방이 방해꾼입니다.\n부딪히지 않게 피하세요.",
            StoryMentEn="Obstacles are everywhere.\nAvoid them and do not crash.",
            Gimmick=GimmickType.Obstacle, TotalStones=5,
            ScatterDropHeightAdd=1.0f, ScatterSpreadMultiplier=1.5f, BallSpeedMultiplier=1.4f,
            SkyBottom=new Color(0.2f,0.3f,0.55f), SkyTop=new Color(0.1f,0.15f,0.3f),
            TableColor=new Color(0.25f,0.35f,0.5f), ClothColor=new Color(0.6f,0.7f,0.85f),
            BackgroundImage="StageBackgrounds/Stage05_Office",
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cube,     Color=new Color(0.3f,0.3f,0.35f),  Position=new Vector3(-3f,1.0f,0.5f), Scale=new Vector3(1.0f,0.7f,0.15f) },
                new BackgroundProp { Shape=BackgroundPropShape.Cylinder, Color=new Color(0.2f,0.6f,0.3f),   Position=new Vector3(2.8f,0.6f,0.5f), Scale=new Vector3(0.25f,0.5f,0.25f) },
                new BackgroundProp { Shape=BackgroundPropShape.Cylinder, Color=new Color(0.85f,0.25f,0.2f), Position=new Vector3(3.3f,0.5f,0.5f), Scale=new Vector3(0.35f,0.3f,0.35f) },
            },
        },
        // Stage 6 — Age 35, 회보라 황혼 (분신술 = 흐릿한 톤)
        // v8-1 swap: 메카닉=분신 가짜 잡기 (구 5단). Sky만 노을 주황→회보라로 보정.
        new StageConfig
        {
            StageNumber=6, Age=35, StageName="분신 가짜 잡기", Theme="부딪혀봐야 본색을 드러내는 가짜들",
            StoryMent="주변이 온통 가짜 같습니다.\n속지 말고 진짜를 찾으세요.",
            StoryMentEn="Everything around you feels fake.\nDon't be fooled, find what's real.",
            Gimmick=GimmickType.FakeStone, TotalStones=10,
            SkyBottom=new Color(0.55f,0.5f,0.65f), SkyTop=new Color(0.3f,0.25f,0.4f),
            TableColor=new Color(0.35f,0.25f,0.2f), ClothColor=new Color(0.25f,0.4f,0.25f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cube, Color=new Color(0.55f,0.4f,0.25f), Position=new Vector3(3f,0.6f,0.5f), Scale=new Vector3(0.5f,0.1f,0.7f) },
            },
        },
        // Stage 7 — Age 40, 진노을 적갈
        new StageConfig
        {
            StageNumber=7, Age=40, StageName="중력 변화", Theme="어깨를 짓누르는 삶의 무게",
            StoryMent="어깨가 점점 무거워집니다.\n돌마저도 무겁게 느껴지네요.",
            StoryMentEn="My shoulders feel heavier by the day.\nEven the stones feel heavy now.",
            Gimmick=GimmickType.Gravity, TotalStones=5,
            SkyBottom=new Color(1.0f,0.55f,0.3f), SkyTop=new Color(0.7f,0.3f,0.2f),
            TableColor=new Color(0.45f,0.25f,0.15f), ClothColor=new Color(0.55f,0.25f,0.2f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cylinder, Color=new Color(0.45f,0.3f,0.15f), Position=new Vector3(-3f,0.6f,0.5f), Scale=new Vector3(0.4f,0.5f,0.4f) },
            },
        },
        // Stage 8 — Age 45, 그늘 회청
        new StageConfig
        {
            StageNumber=8, Age=45, StageName="노화된 손", Theme="몸이 안 따라주는",
            StoryMent="마음은 앞서는데,\n손이 따라오지 않습니다.",
            StoryMentEn="My heart races ahead,\nbut my hands cannot keep up.",
            Gimmick=GimmickType.AgedHand, TotalStones=5,
            SkyBottom=new Color(0.65f,0.7f,0.75f), SkyTop=new Color(0.45f,0.5f,0.6f),
            TableColor=new Color(0.55f,0.5f,0.45f), ClothColor=new Color(0.75f,0.8f,0.85f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Sphere, Color=new Color(0.7f,0.7f,0.7f), Position=new Vector3(3f,0.8f,0.5f), Scale=new Vector3(0.3f,0.3f,0.3f) },
            },
        },
        // Stage 9 — Age 50, 짙은 자주
        new StageConfig
        {
            StageNumber=9, Age=50, StageName="시야 제한", Theme="서서히 좁아지는 시야",
            StoryMent="서서히 시야가 좁아집니다.\n좁아진 세상 속에서 찾아내야 합니다.",
            StoryMentEn="Slowly, my vision narrows.\nYou must find it within this shrinking world.",
            Gimmick=GimmickType.Spotlight, TotalStones=5,
            SkyBottom=new Color(0.25f,0.1f,0.25f), SkyTop=new Color(0.05f,0.05f,0.1f),
            TableColor=new Color(0.2f,0.15f,0.1f), ClothColor=new Color(0.25f,0.15f,0.3f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Cube, Color=new Color(0.1f,0.1f,0.1f), Position=new Vector3(-3f,0.8f,0.5f), Scale=new Vector3(0.4f,0.4f,0.4f) },
            },
        },
        // Stage 10 — Age 55, 회색
        new StageConfig
        {
            StageNumber=10, Age=55, StageName="모노톤", Theme="색이 바랜 노년",
            StoryMent="온통 흑백뿐인 세상.\n이제 마칠 시간입니다.",
            StoryMentEn="A world washed in monochrome.\nIt is time to finish.",
            Gimmick=GimmickType.Monochrome, TotalStones=5,
            SkyBottom=new Color(0.7f,0.7f,0.7f), SkyTop=new Color(0.4f,0.4f,0.4f),
            TableColor=new Color(0.35f,0.35f,0.35f), ClothColor=new Color(0.65f,0.65f,0.65f),
            MatImage    = "StageBackgrounds/BasicBoard",
            MatCenter   = new Vector2(-0.13f, -3.08f),
            MatSize     = new Vector2(19.83f, 10.05f),
            BoardQuad = new Vector2[] {
                new Vector2(-4.40f, -3.95f),  // BL 뒤-좌
                new Vector2( 4.48f, -3.95f),  // BR 뒤-우
                new Vector2(-8.05f, -7.10f),  // FL 앞-좌
                new Vector2( 8.05f, -7.10f),  // FR 앞-우
            },
            Props=new BackgroundProp[]
            {
                new BackgroundProp { Shape=BackgroundPropShape.Sphere, Color=new Color(0.55f,0.55f,0.55f), Position=new Vector3(-3f,1.0f,0.5f), Scale=new Vector3(0.4f,0.4f,0.4f) },
                new BackgroundProp { Shape=BackgroundPropShape.Cube,   Color=new Color(0.45f,0.45f,0.45f), Position=new Vector3(3f,0.7f,0.5f),  Scale=new Vector3(0.5f,0.5f,0.3f) },
            },
        },
    };

    /// <summary>stageNumber(1~10)로 설정 반환. 범위 초과 시 클램프.</summary>
    public static StageConfig Get(int stageNumber)
    {
        if (stageNumber < 1 || stageNumber > stages.Length)
        {
            Debug.LogError($"[StageConfig] Get({stageNumber}) out of range [1..{stages.Length}]. Clamping.");
        }
        int index = Mathf.Clamp(stageNumber - 1, 0, stages.Length - 1);
        return stages[index];
    }

    public static int TotalStages => stages.Length; // 10
}
