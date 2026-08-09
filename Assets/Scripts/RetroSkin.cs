using UnityEngine;

/// <summary>
/// v18 — Windows 95/98 풍 베벨 UI 스킨. 9-slice 스프라이트를 런타임에 만든다.
///
/// 왜 런타임 생성인가:
/// UI 시안이 아직 "예시" 단계라 확정 에셋이 없다. 그런데 베벨은 규칙이 단순해서
/// (면 + 상/좌 밝은 선 + 하/우 어두운 선) 코드로 그리는 편이 오히려 정확하고,
/// 크기를 바꿔도 테두리 두께가 일정하다. 이 프로젝트는 그림자·돗자리·하늘도 같은 방식이다.
/// 나중에 정식 아트가 나오면 <c>Image.sprite</c>만 갈아끼우면 된다.
///
/// 베벨 구조 (Win9x 원본 규칙):
///   바깥 상/좌 = 흰색, 안쪽 상/좌 = 밝은 회색
///   바깥 하/우 = 검정,  안쪽 하/우 = 중간 회색
///   면 = #C0C0C0
/// 눌린 상태는 이 관계를 뒤집는다 — 그래서 "들어갔다"가 읽힌다.
/// </summary>
public static class RetroSkin
{
    public static readonly Color Face      = new Color32(0xC0, 0xC0, 0xC0, 0xFF);
    public static readonly Color FaceHover = new Color32(0xD8, 0xD8, 0xD8, 0xFF);
    public static readonly Color Highlight = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    public static readonly Color LightEdge = new Color32(0xDF, 0xDF, 0xDF, 0xFF);
    public static readonly Color ShadowEdge= new Color32(0x80, 0x80, 0x80, 0xFF);
    public static readonly Color DarkEdge  = new Color32(0x00, 0x00, 0x00, 0xFF);

    /// <summary>Win9x 본문 글자색. 회색 면 위에서는 검정이라야 읽힌다.</summary>
    public static readonly Color Ink = new Color32(0x00, 0x00, 0x00, 0xFF);

    private static Sprite raised, raisedHover, sunken;

    // ⚠️ 캐시 검사에 `??=`를 쓰면 안 된다. `??=`는 C# null만 보는데, UnityEngine.Object는
    //    파괴된 뒤에도 C# 참조가 살아 있다(== 연산자만 오버로드돼 있다). Play를 멈췄다 켜면
    //    텍스처가 파괴된 채 참조만 남아 **파괴된 스프라이트를 그대로 돌려주게** 된다.

    /// <summary>버튼 평상시 — 튀어나온 모양.</summary>
    public static Sprite Raised
    {
        get { if (raised == null) raised = Build(true, Face); return raised; }
    }

    /// <summary>버튼 호버 — 같은 모양에 면만 밝게. Win9x에 호버 개념은 없지만,
    /// 마우스 게임에서 반응이 없으면 "눌리는 건가?"를 알 수 없다.</summary>
    public static Sprite RaisedHover
    {
        get { if (raisedHover == null) raisedHover = Build(true, FaceHover); return raisedHover; }
    }

    /// <summary>버튼 눌림 / 인셋 패널 — 들어간 모양.</summary>
    public static Sprite Sunken
    {
        get { if (sunken == null) sunken = Build(false, Face); return sunken; }
    }

    // 8×8에 2px 테두리 → 9-slice border(2,2,2,2). 어떤 크기로 늘려도 테두리는 2px 유지.
    private const int Size = 8;
    private const int Border = 2;

    private static Sprite Build(bool isRaised, Color face)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // 베벨은 선명해야 한다 — 보간하면 뭉갠 테두리가 된다
            wrapMode = TextureWrapMode.Clamp
        };

        // 위로 갈수록 y가 큰 좌표계다. 상/좌를 밝게, 하/우를 어둡게 그리면 튀어나와 보인다.
        Color outerTL = isRaised ? Highlight  : DarkEdge;
        Color innerTL = isRaised ? LightEdge  : ShadowEdge;
        Color outerBR = isRaised ? DarkEdge   : Highlight;
        Color innerBR = isRaised ? ShadowEdge : LightEdge;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color c = face;

                // 안쪽 테두리를 먼저, 바깥 테두리를 나중에 찍어 모서리에서 바깥이 이긴다.
                if (x == 1 || y == Size - 2) c = innerTL;
                if (x == Size - 2 || y == 1) c = innerBR;
                if (x == 0 || y == Size - 1) c = outerTL;
                if (x == Size - 1 || y == 0) c = outerBR;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, Size, Size),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(Border, Border, Border, Border));   // 9-slice
    }
}
