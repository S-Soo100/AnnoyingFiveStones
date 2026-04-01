/// <summary>
/// 손 커서 포즈. HandCursorUI와 HandController 양쪽에서 사용.
/// </summary>
public enum HandPose
{
    Open,        // 손바닥 펼침 (기본)
    PointIndex,  // 검지만 펴서 가리킴 (나머지 접힘)
    PointMiddle  // 중지만 펴서 가리킴 (나머지 접힘) — 🖕
}
