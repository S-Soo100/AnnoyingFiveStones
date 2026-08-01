using UnityEngine;

/// <summary>
/// 디버그 전용 — **줍기 판정 원**을 손바닥 위치에 그린다 (v17).
///
/// 왜 필요한가: "손바닥을 올렸는데 안 잡히고 손가락을 올려야 잡힌다"처럼
/// 판정 기준점이 눈에 안 보이면 감각으로만 추측하게 된다. 이 프로젝트에서
/// 판정↔시각 어긋남은 반복 실패의 단골 원인이라(donts/game#19~21) 눈으로 봐야 한다.
///
/// <see cref="BoardBoundsDebugDrawer"/>와 같은 게이트·같은 토글을 쓴다.
/// ⚠️ LineRenderer로 그린다 — URP에서 GL 즉시모드는 렌더링되지 않는다.
/// </summary>
public class HandPickDebugDrawer : MonoBehaviour
{
    private const int Segments = 32;
    private const float LineZ = -0.6f;   // 손보다 앞
    private const float LineWidth = 0.03f;

    private LineRenderer line;    // 줍기 원 / 받기 손바닥 원
    private LineRenderer line2;   // 받기 손 전체(튕김) 원
    private HandController hand;
    private HandModelBuilder model;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!GateOpen()) return;
        var go = new GameObject("HandPickDebugDrawer");
        go.AddComponent<HandPickDebugDrawer>();
        DontDestroyOnLoad(go);
    }

    private static bool GateOpen()
    {
#if UNITY_EDITOR
        return true;
#else
        return GameSession.Instance != null && GameSession.Instance.IsTestPlay;
#endif
    }

    private void Awake()
    {
        line = MakeLine(gameObject, new Color(0.2f, 1f, 1f, 1f));           // 청록: 확실히 잡히는 범위
        var outerGo = new GameObject("CatchOuter");
        outerGo.transform.SetParent(transform, false);
        line2 = MakeLine(outerGo, new Color(1f, 0.75f, 0.2f, 1f));          // 주황: 튕김 경계
    }

    private static LineRenderer MakeLine(GameObject go, Color color)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.widthMultiplier = LineWidth;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    private void LateUpdate()
    {
        if (hand == null) hand = FindFirstObjectByType<HandController>();
        if (hand != null && model == null) model = hand.GetComponent<HandModelBuilder>();

        bool show = GateOpen() && BoardBoundsDebugDrawer.Enabled && model != null && hand != null;
        // 5단은 판정 반경이 따로라(stage5CatchRadius) 이 원을 그리면 틀린 정보가 된다.
        bool catching = show && hand.IsCatchMode && !hand.IsStage5;
        if (show && hand.IsCatchMode && hand.IsStage5) show = false;
        if (line.enabled != show) line.enabled = show;
        if (line2.enabled != catching) line2.enabled = catching;
        if (!show) return;

        Vector3 palm = model.GetPalmCenter();

        if (catching)
        {
            // 받기: 안쪽=손바닥 안착, 바깥=손 가장자리(튕김). 손끝이 바깥 원에 닿아야 정상이다.
            Draw(line, palm, hand.DebugCatchPalmScreenRadius);
            Draw(line2, palm, hand.DebugCatchHandScreenRadius);
            return;
        }

        // 줍기: 판정은 보드 단위 반경 → 화면 반경은 손이 있는 깊이의 원근 배율을 곱한 값.
        Vector2 board = BoardSpace.ToBoard(new Vector2(palm.x, palm.y));
        board.y = Mathf.Clamp(board.y, -BoardSpace.LogicalDepth * 0.5f, BoardSpace.LogicalDepth * 0.5f);
        Draw(line, palm, model.PalmRadiusBase * BoardSpace.Current.PerspectiveScale(board, 0f));
    }

    private static void Draw(LineRenderer lr, Vector3 center, float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(a) * radius,
                center.y + Mathf.Sin(a) * radius,
                LineZ));
        }
    }
}
