using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v18 — UI 시안(Figma "UI최종시안 0817")의 공용 스킨. 9-slice 스프라이트를 런타임에 만든다.
///
/// 값의 출처: talk-to-figma MCP로 읽은 실측치다. 눈대중이 아니다.
/// (이전 커밋은 스크린샷만 보고 Win9x 회색 #C0C0C0으로 만들었다 — 구조는 맞고 색이 틀렸다.)
///
/// 왜 런타임 생성인가:
/// 베벨은 규칙이 단순해서(면 그라디언트 + 두 겹 테두리 + 둥근 모서리) 코드로 그리는 편이
/// 정확하고, 폭이 달라도(180 / 284 / 380) 테두리 두께가 일정하다.
/// 이 프로젝트는 그림자·돗자리·하늘도 같은 방식이다. 정식 아트가 나오면 sprite만 갈아끼우면 된다.
///
/// 9-slice를 **좌우로만** 나눈다:
/// 면 그라디언트가 세로 방향이라 세로로 잘라 늘리면 그라디언트가 계단처럼 끊긴다.
/// 그래서 세로는 자르지 않고 텍스처를 **시안 높이 그대로** 만들어 통째로 늘린다.
/// 1920×1080 화면에서는 이 스프라이트가 정확히 1:1로 찍힌다 — 테두리 두께(EdgeOuterW/
/// EdgeInnerW)가 곧 화면 px다. (Overlay: 1280 ref × 1.5 = 1920 / World: 1400 units = 1080px)
/// </summary>
public static class UISkin
{
    // ── 좌표 환산 ────────────────────────────────────────────────────────────
    // 시안은 1920×1080. 캔버스가 두 종류라 환산도 두 개다. 섞어 쓰면 조용히 어긋난다.

    /// <summary>시안 px → Screen Space Overlay 캔버스(referenceResolution 1280×720). 정확히 1.5배.</summary>
    public static float Px(float designPx) => designPx / 1.5f;

    /// <summary>
    /// 시안 px → GameUI의 World Space 캔버스 단위.
    /// 그 캔버스는 1400 units가 카메라 세로 14 world(= 화면 전체)에 대응한다 → 1400/1080.
    /// 가로도 정확히 맞는다: 960 × 1.2963 = 1244.4 = 화면 반폭(24.889/2 ÷ 0.01).
    /// **캔버스 rect가 2500 넓은 것과는 무관하다** — 중앙 기준으로 배치하기 때문.
    /// </summary>
    public static float GamePx(float designPx) => designPx * (1400f / 1080f);

    /// <summary>
    /// 시안 좌표(1920×1080, **좌상단 원점**)의 한 점 → 카메라가 그 점을 비추는 월드 좌표.
    /// World Space 캔버스를 화면 특정 위치에 놓을 때 쓴다. 카메라에서 직접 읽으므로
    /// ortho size나 위치가 바뀌어도 따라간다.
    /// </summary>
    public static Vector3 DesignToWorld(float designX, float designY, float z)
    {
        var cam = Camera.main;
        if (cam == null) return new Vector3(0f, 0f, z);
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        var c = cam.transform.position;
        return new Vector3(c.x + (designX / 1920f - 0.5f) * viewW,
                           c.y + (0.5f - designY / 1080f) * viewH,
                           z);
    }

    // ── 시안 팔레트 ──────────────────────────────────────────────────────────
    // ── 테두리 두께 (시안 px) ────────────────────────────────────────────────
    // ⚠️ 여기 두 숫자가 게임의 모든 테두리를 정한다 — 버튼·창·상태박스·단표시·게이지.
    //    스프라이트는 시안 크기 그대로 구워지므로 1920×1080에서 이 값이 곧 화면 px다.
    //
    // 1px로 시작했다가 "테두리가 너무 작다"는 지적을 두 번 받고 올렸다.
    // 1px에서는 **어두운 바깥선이 밝은 안쪽선에 먹혀서** 윤곽이 사실상 사라진다.
    // 그래서 바깥을 안쪽보다 두껍게 둔다 — 윤곽이 먼저 읽히고 광택이 그 안에 남는다.
    // (MCP get_node_info가 strokeWeight를 안 넘겨줘서 시안 실측값이 아니다.
    //  Figma가 붙으면 버튼을 4배로 내보내 픽셀을 세고 이 두 숫자만 맞추면 된다.)
    public const float EdgeOuterW = 3f;
    public const float EdgeInnerW = 2f;

    /// <summary>바깥 테두리 (Button_outline stroke).</summary>
    public static readonly Color32 EdgeOuter = new Color32(0x5A, 0x71, 0x7F, 0xFF);
    /// <summary>안쪽 테두리 (Button stroke) — 이게 있어야 면이 볼록해 보인다.</summary>
    public static readonly Color32 EdgeInner = new Color32(0xF4, 0xFC, 0xFC, 0xFF);
    /// <summary>면 그라디언트 위/아래.</summary>
    public static readonly Color32 FaceTop = new Color32(0xFA, 0xFF, 0xFF, 0xFF);
    public static readonly Color32 FaceBottom = new Color32(0x87, 0xBB, 0xC9, 0xFF);

    /// <summary>본문/라벨 글자색. 시안의 모든 라벨이 이 색이다.</summary>
    public static readonly Color32 Ink = new Color32(0x29, 0x31, 0x3B, 0xFF);

    /// <summary>로고 면 / 로고 외곽선.</summary>
    public static readonly Color32 LogoFill = new Color32(0xEE, 0xFA, 0x44, 0xFF);
    public static readonly Color32 LogoOutline = new Color32(0x17, 0x18, 0x1A, 0xFF);

    /// <summary>
    /// 안내문 띠 — 시안 색조(#5A717F) 그대로, 불투명도만 50% → 78%.
    /// 시안은 이 띠를 배경 중 어두운 자리에 얹어 흰 글자가 떴지만, 실제 게임에서는
    /// 같은 자리가 밝은 창문·문이라 50%로는 판도 글자도 묻힌다. 게다가 이 띠는
    /// 3초 뒤 CanvasGroup 알파 0.85로 한 번 더 곱해진다(GameUI.DoGuideText).
    /// 색조를 바꾸지 않고 대비만 확보하는 가장 작은 조정이다.
    /// </summary>
    public static readonly Color GuideBackdrop = new Color32(0x5A, 0x71, 0x7F, 0xC8);

    // 단 표시(클리어) — 초록 3단 그라디언트
    private static readonly Color32 ClearLow = new Color32(0x18, 0xD8, 0x0D, 0xFF);
    private static readonly Color32 ClearMid = new Color32(0x6B, 0xD5, 0x66, 0xFF);
    /// <summary>단 숫자 그라디언트(위→아래). 기본 / 클리어.</summary>
    public static readonly Color32 DotNumTop = new Color32(0xA4, 0xD5, 0xE3, 0xFF);
    public static readonly Color32 DotNumBottom = new Color32(0x08, 0x8D, 0xA6, 0xFF);
    public static readonly Color32 DotNumClearTop = new Color32(0x1F, 0xC8, 0x43, 0xFF);
    public static readonly Color32 DotNumClearBottom = new Color32(0x02, 0x72, 0x0A, 0xFF);

    // 상태박스 안쪽 흰 칸 / 게이지 / 창
    private static readonly Color32 InsetBorder = new Color32(0x29, 0x31, 0x3B, 0xFF);
    private static readonly Color32 WindowFace = new Color32(0xCC, 0xD9, 0xDD, 0xFF);
    private static readonly Color32 HeaderBottom = new Color32(0xBE, 0xCF, 0xD4, 0xFF);
    private static readonly Color32 HandleFace = new Color32(0xF4, 0xFC, 0xFC, 0xFF);
    /// <summary>슬라이더 채움.</summary>
    public static readonly Color32 SliderFill = new Color32(0x9E, 0xC8, 0xD4, 0xFF);
    /// <summary>창 뒤 어둠막 — 시안 #000000 70%.</summary>
    public static readonly Color WindowDim = new Color(0f, 0f, 0f, 0.7f);
    private static readonly Color32 GaugeTrackFace = new Color32(0x6C, 0x84, 0x87, 0xFF);
    private static readonly Color32 GaugeTrackEdge = new Color32(0x33, 0x4B, 0x52, 0xFF);
    private static readonly Color32 GaugeFillEdge = new Color32(0x40, 0xB1, 0x17, 0xFF);
    private static readonly Color32 GaugeFillCore = new Color32(0x57, 0xF8, 0x6A, 0xFF);

    // ⚠️ 캐시 검사에 `??=`를 쓰면 안 된다. `??=`는 C# null만 보는데, UnityEngine.Object는
    //    파괴된 뒤에도 C# 참조가 살아 있다(== 연산자만 오버로드돼 있다). Play를 멈췄다 켜면
    //    텍스처가 파괴된 채 참조만 남아 **파괴된 스프라이트를 그대로 돌려주게** 된다.
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static Sprite Cached(string key, Func<Sprite> make)
    {
        if (cache.TryGetValue(key, out var s) && s != null) return s;
        s = make();
        cache[key] = s;
        return s;
    }

    // ── 공개 스프라이트 ──────────────────────────────────────────────────────

    /// <summary>버튼 평상시 (= 높이 80 패널).</summary>
    public static Sprite Raised => Panel(80);

    /// <summary>버튼 호버 — 면만 밝게. 시안에 호버 상태는 없지만, 마우스 게임에서
    /// 반응이 없으면 "눌리는 건가?"를 알 수 없다. 배색은 유지하고 명도만 올린다.</summary>
    public static Sprite RaisedHover => Cached("hover", () =>
        Build(32, 80, 6f, 12, FaceGrad(Lighten(FaceBottom, .35f), Lighten(FaceTop, .35f)), false, EdgeOuter, EdgeInner));

    /// <summary>버튼 눌림 — 그라디언트를 뒤집고 안쪽 테두리를 어둡게. 방향이 뒤집혀야
    /// "들어갔다"가 읽힌다. 색만 바꾸면 눌린 느낌이 나지 않는다.</summary>
    public static Sprite Sunken => Cached("sunken", () =>
        Build(32, 80, 6f, 12, FaceGrad(FaceTop, Darken(FaceBottom, .12f)), false, EdgeOuter, EdgeOuter));

    /// <summary>같은 배색의 패널. 시안 높이를 그대로 넘긴다(상태박스 172 등).</summary>
    public static Sprite Panel(int designHeight) => Cached($"panel{designHeight}", () =>
        Build(32, designHeight, 6f, 12, FaceGrad(FaceBottom, FaceTop), false, EdgeOuter, EdgeInner));

    /// <summary>상태박스 안쪽 흰 칸 / 슬라이더 홈 — 흰 바탕 + 먹색 테두리, 모서리 각짐.</summary>
    public static Sprite InsetBox => InsetBoxOf(56);

    public static Sprite InsetBoxOf(int designHeight) => Cached($"inset{designHeight}", () =>
        Build(32, designHeight, 0f, 12, _ => (Color)Color.white, false, InsetBorder, null));

    // ── 창(일시정지 / 경고) ──────────────────────────────────────────────────
    // 시안 Settings 572:588 · Dialog 572:441. 둘 다 같은 부품이고 높이만 다르다.

    /// <summary>창 몸통 — 둥근 모서리 12, 면 #CCD9DD, 테두리 #5A717F.
    /// 면이 단색이라 위아래로도 잘라 늘릴 수 있다(그라디언트가 없으니 계단이 안 생긴다).</summary>
    public static Sprite WindowBody => Cached("winbody", () =>
        Build(32, 32, 12f, 12, _ => (Color)WindowFace, false, EdgeOuter, null, sliceY: 12));

    /// <summary>창 머리띠 — **위쪽만** 둥글다(아래는 몸통과 맞닿아 각져야 한다).
    /// 테두리를 넣어두면 창 바깥선이 머리띠 구간에서 끊기지 않고, 아래쪽 선이
    /// 그대로 머리띠↔몸통 구분선이 된다(시안에서 몸통 위쪽 stroke가 하는 역할).</summary>
    public static Sprite WindowHeader => Cached("winhead", () =>
        Build(32, 83, 12f, 12, FaceGrad(HeaderBottom, Color.white), false, EdgeOuter, null,
              topRoundedOnly: true));

    /// <summary>슬라이더 손잡이 — 20×36 흰 알약(각짐) + 먹색 테두리.</summary>
    public static Sprite SliderHandle => Cached("shandle", () =>
        Build(20, 36, 0f, 0, _ => (Color)HandleFace, false, InsetBorder, null));

    /// <summary>창 닫기 ✕. 시안에는 44×44 회색 자리표시만 있어서 아이콘은 여기서 그린다.</summary>
    public static Sprite CloseIcon => Cached("close", () => BuildCross(44, 4f));

    /// <summary>단 표시 원. 시안은 작은 원 60 / 큰 원(5단) 80 두 가지다.</summary>
    public static Sprite Dot(int designSize, bool cleared) => Cached($"dot{designSize}{cleared}", () =>
        Build(designSize, designSize, designSize * 0.5f, 0,
              cleared ? ClearGrad() : FaceGrad(FaceBottom, FaceTop), false,
              EdgeOuter, cleared ? (Color32)Color.white : EdgeInner));

    /// <summary>파워 게이지 바깥 알약 틀.</summary>
    public static Sprite GaugeFrame => Cached("gframe", () =>
        Build(96, 64, 32f, 32, FaceGrad(new Color32(0x87, 0xB0, 0xC9, 0xFF), FaceTop), false, EdgeOuter, EdgeInner));

    /// <summary>파워 게이지 안쪽 홈(빈 트랙).</summary>
    public static Sprite GaugeTrack => Cached("gtrack", () =>
        Build(80, 40, 20f, 20, _ => (Color)GaugeTrackFace, false, GaugeTrackEdge, null));

    /// <summary>파워 게이지 채움 — 가운데가 밝은 가로 그라디언트(시안 그대로).
    ///
    /// **밝기만 굽고 색은 넣지 않는다.** 게이지 색이 값에 따라 바뀌기 때문이다
    /// (뭉침 coral → 스윗 mint → 경계 amber). 색은 <c>Image.color</c>로 입히고,
    /// 이 스프라이트는 "가운데가 밝은" 심지 모양만 담당한다.
    ///
    /// 9-slice를 하지 않는다: 가로 그라디언트를 좌우로 자르면 심지가 늘어나 뭉갠다.
    /// 대신 <c>Image.Type.Filled</c>로 왼쪽부터 드러내면 심지 위치가 그대로 유지된다.</summary>
    public static Sprite GaugeFill => Cached("gfill", () =>
        Build(640, 40, 20f, 0,
              t => Color.Lerp(new Color(0.70f, 0.70f, 0.70f), Color.white,
                              1f - Mathf.Abs(t * 2f - 1f)), true,
              null, null));

    /// <summary>
    /// 시안의 초록. "좋다/안전하다"를 말하는 자리에 쓴다 —
    /// 게이지 스윗 구간, 보드 위 뿌림 링의 스윗 밴드, 위험 개념이 없는 게이지(5단)의 기본색.
    /// **한 군데서만 정의한다** — 같은 뜻을 두 색으로 말하면 플레이어가 둘을 다른 신호로 읽는다.
    /// </summary>
    public static readonly Color32 SafeGreen = new Color32(0x57, 0xF8, 0x6A, 0xFF);

    // ── 빌더 ────────────────────────────────────────────────────────────────

    private static Color Lighten(Color32 c, float t) => Color.Lerp(c, Color.white, t);
    private static Color Darken(Color32 c, float t) => Color.Lerp(c, Color.black, t);

    /// <summary>t=0 아래 → t=1 위.</summary>
    private static Func<float, Color> FaceGrad(Color bottom, Color top) => t => Color.Lerp(bottom, top, t);

    /// <summary>시안의 클리어 원: 아래 초록 → 0.27에서 연두 → 위 흰색.</summary>
    private static Func<float, Color> ClearGrad() => t =>
        t < 0.27f ? Color.Lerp(ClearLow, ClearMid, Mathf.InverseLerp(0.06f, 0.27f, t))
                  : Color.Lerp(ClearMid, Color.white, Mathf.InverseLerp(0.27f, 1f, t));

    /// <param name="face">면 색. t는 세로면 아래→위, 가로면 왼→오른쪽.</param>
    /// <param name="outer">가장 바깥 EdgeOuterW px. null이면 면으로 채운다.</param>
    /// <param name="inner">그 안쪽 EdgeInnerW px. null이면 면으로 채운다.</param>
    /// <param name="sliceY">위아래 9-slice 폭. 면이 **단색일 때만** 0보다 크게 둔다 —
    /// 세로 그라디언트를 세로로 잘라 늘리면 가운데가 늘어나 계단이 생긴다.</param>
    /// <param name="topRoundedOnly">위 두 모서리만 둥글게. 창 머리띠용.</param>
    private static Sprite Build(int w, int h, float radius, int sliceX,
                                Func<float, Color> face, bool horizontal,
                                Color32? outer, Color32? inner,
                                int sliceY = 0, bool topRoundedOnly = false)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,   // 둥근 모서리는 보간이 있어야 계단이 안 보인다
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color[w * h];
        const int SS = 4;   // 곡선을 4×4 슈퍼샘플링. 이 크기에서는 비용이 무시할 수준이다

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float ar = 0f, ag = 0f, ab = 0f, covered = 0f;

                for (int sy = 0; sy < SS; sy++)
                {
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float fx = x + (sx + 0.5f) / SS;
                        float fy = y + (sy + 0.5f) / SS;

                        // 둥근 사각형 안쪽으로 얼마나 들어와 있는지(px). 0 이하면 바깥.
                        float depth = -(topRoundedOnly ? TopRoundedRectSD(fx, fy, w, h, radius)
                                                       : RoundedRectSD(fx, fy, w, h, radius));
                        if (depth <= 0f) continue;

                        Color c;
                        if (depth < EdgeOuterW && outer.HasValue)
                            c = outer.Value;
                        else if (depth < EdgeOuterW + EdgeInnerW && inner.HasValue)
                            c = inner.Value;
                        else
                            c = face(horizontal ? fx / w : fy / h);

                        ar += c.r; ag += c.g; ab += c.b; covered += 1f;
                    }
                }

                px[y * w + x] = covered > 0f
                    ? new Color(ar / covered, ag / covered, ab / covered, covered / (SS * SS))
                    : new Color(0f, 0f, 0f, 0f);   // 색은 덮인 부분만 평균, 알파는 커버리지
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(
            tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(sliceX, sliceY, sliceX, sliceY));
    }

    /// <summary>가운데가 뚫린 ✕ 아이콘. 알파만 쓰고 색은 Image.color로 입힌다.</summary>
    private static Sprite BuildCross(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color[size * size];
        float pad = size * 0.28f;              // 획이 모서리에 닿지 않게 안쪽으로
        float half = thickness * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                // 두 대각선까지의 거리. 45°라 |dx∓dy|/√2 로 바로 나온다.
                float d1 = Mathf.Abs(fx - fy) * 0.70710678f;
                float d2 = Mathf.Abs(fx + fy - size) * 0.70710678f;
                bool inBox = fx >= pad && fx <= size - pad && fy >= pad && fy <= size - pad;
                float a = inBox ? Mathf.Clamp01(half - Mathf.Min(d1, d2) + 0.5f) : 0f;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, Vector4.zero);
    }

    /// <summary>위 두 모서리만 둥근 사각형. 음수 = 안쪽.</summary>
    private static float TopRoundedRectSD(float x, float y, int w, int h, float radius)
    {
        float dx = Mathf.Abs(x - w * 0.5f);
        float qx = dx - (w * 0.5f - radius);
        float qy = y - (h - radius);
        if (qx > 0f && qy > 0f) return Mathf.Sqrt(qx * qx + qy * qy) - radius;   // 위 모서리 호
        return Mathf.Max(dx - w * 0.5f, Mathf.Max(-y, y - h));                   // 나머지는 각진 사각형
    }

    /// <summary>둥근 사각형 부호거리. 음수 = 안쪽.</summary>
    private static float RoundedRectSD(float x, float y, int w, int h, float radius)
    {
        float hw = w * 0.5f, hh = h * 0.5f;
        float px = Mathf.Abs(x - hw) - (hw - radius);
        float py = Mathf.Abs(y - hh) - (hh - radius);
        float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                                   Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
        return outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
    }
}
