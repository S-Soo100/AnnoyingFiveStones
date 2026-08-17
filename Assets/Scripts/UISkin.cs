using UnityEngine;

/// <summary>
/// v18 — UI 시안(Figma "UI최종시안 0817")의 공용 스킨. 9-slice 스프라이트를 런타임에 만든다.
///
/// 값의 출처: Figma talk-to-figma MCP로 읽은 실측치다. 눈대중이 아니다.
///   Button_outline(바깥 테두리) #5A717F / Button(면) #FAFFFF→#87BBC9 세로 그라디언트
///   + 안쪽 테두리 #F4FCFC / cornerRadius 6 / 라벨 #29313B
/// (이전 커밋은 스크린샷만 보고 Win9x 회색 #C0C0C0으로 만들었다 — 구조는 맞고 색이 틀렸다.)
///
/// 왜 런타임 생성인가:
/// 베벨은 규칙이 단순해서(면 그라디언트 + 두 겹 테두리 + 둥근 모서리) 코드로 그리는 편이
/// 정확하고, 버튼 폭이 달라도(180 / 284 / 380) 테두리 두께가 일정하다.
/// 이 프로젝트는 그림자·돗자리·하늘도 같은 방식이다. 정식 아트가 나오면 sprite만 갈아끼우면 된다.
///
/// 9-slice를 **좌우로만** 나눈다(border = 12,0,12,0):
/// 시안의 버튼은 폭만 제각각이고 높이는 전부 80이다. 그리고 면 그라디언트가 세로 방향이라
/// 세로로 잘라 늘리면 그라디언트가 계단처럼 끊긴다. 좌우만 늘리면 어떤 폭에서도 그대로다.
/// </summary>
public static class UISkin
{
    /// <summary>
    /// 시안 px(1920×1080 기준) → UI 좌표. 캔버스 CanvasScaler의 referenceResolution이
    /// 1280×720이라 정확히 1.5배 차이다. 시안 수치를 그대로 적고 이 함수만 통과시키면
    /// "대충 비슷하게" 옮기는 실수가 안 생긴다.
    /// </summary>
    public static float Px(float designPx) => designPx / 1.5f;

    // ── 시안 팔레트 ──────────────────────────────────────────────────────────
    /// <summary>바깥 테두리 (Button_outline stroke).</summary>
    public static readonly Color32 EdgeOuter = new Color32(0x5A, 0x71, 0x7F, 0xFF);
    /// <summary>안쪽 테두리 (Button stroke) — 이게 있어야 면이 볼록해 보인다.</summary>
    public static readonly Color32 EdgeInner = new Color32(0xF4, 0xFC, 0xFC, 0xFF);
    /// <summary>면 그라디언트 위쪽.</summary>
    public static readonly Color32 FaceTop = new Color32(0xFA, 0xFF, 0xFF, 0xFF);
    /// <summary>면 그라디언트 아래쪽.</summary>
    public static readonly Color32 FaceBottom = new Color32(0x87, 0xBB, 0xC9, 0xFF);

    /// <summary>본문/라벨 글자색. 시안의 모든 라벨이 이 색이다.</summary>
    public static readonly Color32 Ink = new Color32(0x29, 0x31, 0x3B, 0xFF);

    /// <summary>로고 면 / 로고 외곽선.</summary>
    public static readonly Color32 LogoFill = new Color32(0xEE, 0xFA, 0x44, 0xFF);
    public static readonly Color32 LogoOutline = new Color32(0x17, 0x18, 0x1A, 0xFF);

    // ⚠️ 캐시 검사에 `??=`를 쓰면 안 된다. `??=`는 C# null만 보는데, UnityEngine.Object는
    //    파괴된 뒤에도 C# 참조가 살아 있다(== 연산자만 오버로드돼 있다). Play를 멈췄다 켜면
    //    텍스처가 파괴된 채 참조만 남아 **파괴된 스프라이트를 그대로 돌려주게** 된다.
    private static Sprite raised, raisedHover, sunken;

    /// <summary>버튼 평상시.</summary>
    public static Sprite Raised
    {
        get { if (raised == null) raised = Build(FaceTop, FaceBottom, EdgeInner); return raised; }
    }

    /// <summary>버튼 호버 — 면만 밝게. 시안에 호버 상태는 없지만, 마우스 게임에서
    /// 반응이 없으면 "눌리는 건가?"를 알 수 없다. 배색은 유지하고 명도만 올린다.</summary>
    public static Sprite RaisedHover
    {
        get
        {
            if (raisedHover == null)
                raisedHover = Build(Lighten(FaceTop, 0.35f), Lighten(FaceBottom, 0.35f), EdgeInner);
            return raisedHover;
        }
    }

    /// <summary>버튼 눌림 — 그라디언트를 뒤집고 안쪽 테두리를 어둡게. 방향이 뒤집혀야
    /// "들어갔다"가 읽힌다. 색만 바꾸면 눌린 느낌이 나지 않는다.</summary>
    public static Sprite Sunken
    {
        get
        {
            if (sunken == null)
                sunken = Build(Darken(FaceBottom, 0.12f), FaceTop, EdgeOuter);
            return sunken;
        }
    }

    // 폭 32 × 높이 80(시안 버튼 높이 그대로) / 좌우 12px만 9-slice로 고정.
    private const int Width = 32;
    private const int Height = 80;
    private const int SliceX = 12;
    private const float Radius = 6f;

    private static Color Lighten(Color32 c, float t) => Color.Lerp(c, Color.white, t);
    private static Color Darken(Color32 c, float t) => Color.Lerp(c, Color.black, t);

    private static Sprite Build(Color topColor, Color bottomColor, Color innerEdge)
    {
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,   // 둥근 모서리는 보간이 있어야 계단이 안 보인다
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color[Width * Height];
        const int SS = 4;   // 모서리 곡선을 4×4 슈퍼샘플링 — 32×80이라 비용은 무시할 수준

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float ar = 0f, ag = 0f, ab = 0f, aa = 0f;

                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float fx = x + (sx + 0.5f) / SS;
                        float fy = y + (sy + 0.5f) / SS;

                        // 둥근 사각형 안쪽으로 얼마나 들어와 있는지(px). 음수면 바깥.
                        float depth = -RoundedRectSD(fx, fy);
                        if (depth <= 0f) continue;   // 바깥 → 투명

                        Color c;
                        if (depth < 1f)      c = EdgeOuter;
                        else if (depth < 2f) c = innerEdge;
                        else                 c = Color.Lerp(bottomColor, topColor, fy / Height);

                        ar += c.r; ag += c.g; ab += c.b; aa += 1f;
                    }
                }

                int n = SS * SS;
                px[y * Width + x] = aa > 0f
                    ? new Color(ar / aa, ag / aa, ab / aa, aa / n)   // 색은 덮인 부분만 평균, 알파는 커버리지
                    : new Color(0f, 0f, 0f, 0f);
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, Width, Height),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(SliceX, 0f, SliceX, 0f));   // 좌우만 9-slice — 위아래는 늘리지 않는다
    }

    /// <summary>둥근 사각형 부호거리. 음수 = 안쪽.</summary>
    private static float RoundedRectSD(float x, float y)
    {
        float hw = Width * 0.5f, hh = Height * 0.5f;
        float px = Mathf.Abs(x - hw) - (hw - Radius);
        float py = Mathf.Abs(y - hh) - (hh - Radius);
        float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                                   Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
        return outside + Mathf.Min(Mathf.Max(px, py), 0f) - Radius;
    }
}
