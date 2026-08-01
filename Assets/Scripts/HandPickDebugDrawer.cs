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

    private LineRenderer line;
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
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = LineWidth;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var cyan = new Color(0.2f, 1f, 1f, 1f);
        mat.SetColor("_BaseColor", cyan);
        line.material = mat;
        line.startColor = cyan;
        line.endColor = cyan;
    }

    private void LateUpdate()
    {
        if (hand == null) hand = FindFirstObjectByType<HandController>();
        if (hand != null && model == null) model = hand.GetComponent<HandModelBuilder>();

        // 줍기 판정이 실제로 도는 상황에서만 보여준다 — 받기 모드엔 이 원이 의미가 없다.
        bool show = GateOpen() && BoardBoundsDebugDrawer.Enabled
                    && model != null && hand != null && !hand.IsCatchMode;
        if (line.enabled != show) line.enabled = show;
        if (!show) return;

        Vector3 palm = model.GetPalmCenter();
        // 판정은 보드 단위 반경 → 화면에서의 반경은 손이 있는 깊이의 원근 배율을 곱한 값.
        Vector2 board = BoardSpace.ToBoard(new Vector2(palm.x, palm.y));
        board.y = Mathf.Clamp(board.y, -BoardSpace.LogicalDepth * 0.5f, BoardSpace.LogicalDepth * 0.5f);
        float screenRadius = model.PalmRadiusBase * BoardSpace.Current.PerspectiveScale(board, 0f);

        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(
                palm.x + Mathf.Cos(a) * screenRadius,
                palm.y + Mathf.Sin(a) * screenRadius,
                LineZ));
        }
    }
}
