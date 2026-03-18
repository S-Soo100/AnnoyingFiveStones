using UnityEngine;

/// <summary>
/// 단일 테스트 이벤트. AI 분석용 구조화 데이터.
/// </summary>
[System.Serializable]
public struct TestEvent
{
    public enum Category
    {
        PhaseChange,    // 게임 페이즈 전환
        StageChange,    // 스테이지 변경
        StoneState,     // 돌 상태 변화 (줍기, 던지기, 받기 등)
        StonePosition,  // 돌 위치 스냅샷
        HandPosition,   // 손 위치
        Input,          // 사용자 입력 (클릭, 릴리즈)
        Physics,        // 물리 이벤트 (충돌, 장외)
        Scatter,        // 뿌리기 관련
        Catch,          // 받기 관련
        Failure,        // 실패 원인
        Performance     // 프레임 등 성능
    }

    public float timestamp;       // Time.time 기준
    public int frameCount;        // Time.frameCount
    public Category category;
    public string eventName;
    public string detail;         // 구조화된 key=value 페어 (예: "stone=2 state=InAir pos=(1.2, 3.4, 0)")

    public TestEvent(Category cat, string name, string detail)
    {
        this.timestamp = Time.time;
        this.frameCount = Time.frameCount;
        this.category = cat;
        this.eventName = name;
        this.detail = detail;
    }

    public string ToMarkdownRow()
    {
        return $"| {timestamp:F3} | {frameCount} | {category} | {eventName} | {detail} |";
    }
}
