using UnityEngine;

/// <summary>
/// 뿌리기 게이지 왕복 중 "돌이 흩어질 범위"를 보드 위에 원(ring)으로 실시간 표시.
/// 순수 시각 레이어 — 낙/산개/손 로직은 전혀 건드리지 않는다.
/// ★좌표: ScatterSystem.DoScatter와 "동일한" BoardBounds 4꼭짓점 + TrapPoint(LerpUnclamped)로
///   원을 사다리꼴 보드에 매핑 → 링이 테이블 밖으로 넘치면 낙 경고색으로 전환.
/// ★z: 링은 돌(z=0) 바로 뒤·매트 앞 → RingZ=0.03 (안 보이면 실행 확인 후 조정).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ScatterRangeIndicator : MonoBehaviour
{
    private const int SEG = 48;
    private const float RingZ = 0.03f;

    // 게이지바와 톤 통일한 팔레트
    private static readonly Color mint  = new Color(0.40f, 0.85f, 0.70f);
    private static readonly Color amber = new Color(0.95f, 0.75f, 0.30f);
    private static readonly Color coral = new Color(0.95f, 0.40f, 0.35f);

    private LineRenderer lr;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = SEG;
        lr.widthMultiplier = 0.06f;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 0;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        // LineRenderer는 vertex-color·투명이 Sprites/Default에서 가장 안정적.
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.enabled = false; // 시작은 숨김
    }

    /// <summary>게이지 시작 시 표시. 여러 번 호출돼도 무해.</summary>
    public void Show()
    {
        lr.enabled = true;
    }

    /// <summary>게이지 종료/리셋 시 숨김. 여러 번 호출돼도 무해.</summary>
    public void Hide()
    {
        if (lr != null) lr.enabled = false;
    }

    /// <summary>게이지 값에 대응하는 산개 반경(UV)으로 링을 갱신.</summary>
    public void UpdateRing(float radiusUV)
    {
        // 1) 게임의 실제 보드 4꼭짓점 (ScatterSystem.DoScatter와 동일 방식).
        Vector2 cBL = BoardBounds.QuadPoint(0f, 0f);
        Vector2 cBR = BoardBounds.QuadPoint(1f, 0f);
        Vector2 cFL = BoardBounds.QuadPoint(0f, 1f);
        Vector2 cFR = BoardBounds.QuadPoint(1f, 1f);

        // 2) SEG개 점 배치 + 낙 위험(보드 밖) 카운트.
        int outside = 0;
        for (int i = 0; i < SEG; i++)
        {
            float theta = 2f * Mathf.PI * i / SEG;
            float u = 0.5f + radiusUV * Mathf.Cos(theta);
            float v = 0.5f + radiusUV * Mathf.Sin(theta);
            Vector2 world = TrapPoint(u, v, cBL, cBR, cFL, cFR);
            lr.SetPosition(i, new Vector3(world.x, world.y, RingZ));

            if (BoardBounds.IsOutsideMat(world, 0.2f)) outside++;
        }

        float dangerFrac = (float)outside / SEG;

        // 3) 색: radiusUV로 mint→amber, 밖으로 넘치면(dangerFrac) amber→coral.
        Color c = Color.Lerp(mint, amber, Mathf.InverseLerp(0.30f, 0.55f, radiusUV));
        if (dangerFrac > 0f) c = Color.Lerp(c, coral, Mathf.Clamp01(dangerFrac * 3f));
        c.a = 0.85f;
        lr.startColor = c;
        lr.endColor = c;
    }

    /// <summary>보드 폴리곤 bilinear 매핑 (ScatterSystem.TrapPoint와 동일).
    /// LerpUnclamped라 uv>1이면 외삽 → 링이 보드 밖으로 넘침.</summary>
    private static Vector2 TrapPoint(float u, float v, Vector2 bl, Vector2 br, Vector2 fl, Vector2 fr)
    {
        Vector2 back  = Vector2.LerpUnclamped(bl, br, u);
        Vector2 front = Vector2.LerpUnclamped(fl, fr, u);
        return Vector2.LerpUnclamped(back, front, v);
    }
}
