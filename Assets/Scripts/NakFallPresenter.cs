using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FiveStones.Core;

/// <summary>
/// M1-S7 — 공용 낙하 연출 (계획서 §5-4). GameManager.Awake가 같은 오브젝트에 붙인다.
///
/// Ledge 변(허공)을 넘은 돌을 넘겨받아 **수평 속도를 유지한 채** 판 아래로 떨어뜨린다(매 프레임 해석식, 물리 없음 —
/// donts #20). 앞변이면 화면 밖까지, 좌·우·뒤 변이면 h&lt;0 순간 판 레이어 뒤(z=35)로 들어가 실제로 가려진다(T4).
/// 판 레이어가 없는 스테이지(35살)는 그 순간 렌더러를 끈다.
///
/// 2.5D 좌표: X=좌우, Y=상하, Z=깊이(카메라 z=-10, 작을수록 앞). 판 레이어 z=30, 배경 z=40, 돌 z=0.
/// 각 연출은 시작 시 GameManager.AddNakBusy, 끝날 때 ReleaseNakBusy + NotifyNakStoneEnded를 부른다.
/// (M2: HoldOnFlat·WatchPhysicsFall은 아직 없다)
/// </summary>
public class NakFallPresenter : MonoBehaviour
{
    public const float BehindBoardZ = 35f;     // 판(30)과 배경(40) 사이
    private const float MaxFallSeconds = 0.80f;
    private const float BackClampV = 0.8f;      // 뒷변 낙하: 보드 v ≥ -0.8 (원근 배율 음수 방지, A2)

    private class Entry
    {
        public Stone Stone;
        public Coroutine Routine;
        public bool Running;
    }

    private readonly List<Entry> running = new List<Entry>();
    private readonly List<Stone> touched = new List<Stone>();   // RestoreAll 대상
    private BackgroundManager background;

    private bool HasBoardLayer
    {
        get
        {
            if (background == null) background = FindFirstObjectByType<BackgroundManager>();
            return background != null && background.HasBoardLayer;
        }
    }

    /// <summary>Ledge 선을 넘은 돌을 넘겨받는다. b0·h0 = 선을 넘는 순간의 보드 좌표·높이, vB·vH = 그 순간 속도.
    /// startTau = 넘겨받는 프레임에 이미 지난 시간(그만큼 앞선 자세부터 그린다).</summary>
    public void FallFromBoard(Stone s, Vector2 b0, Vector2 vB, float h0, float vH, float g, BoardEdge edge, float startTau = 0f)
    {
        if (s == null) return;
        if (!touched.Contains(s)) touched.Add(s);
        var e = new Entry { Stone = s, Running = true };
        running.Add(e);
        GameManager.Instance?.AddNakBusy();
        Debug.Log($"[NakFall] start stone={s.StoneIndex} path=Board edge={edge} kind=Ledge b0={b0:F2} h0={h0:F2}");
        e.Routine = StartCoroutine(FallRoutine(e, b0, vB, h0, vH, g, edge, Mathf.Max(0f, startTau)));
    }

    private IEnumerator FallRoutine(Entry e, Vector2 b0, Vector2 vB, float h0, float vH, float g, BoardEdge edge, float startTau)
    {
        var s = e.Stone;
        s.EnterScriptedFall();
        s.SetShadowGroundCut(true);   // Ledge 선 = 허공 → 0.08초에 걸쳐 그림자 소멸

        var r = s.GetComponent<Renderer>();
        bool hasLayer = HasBoardLayer;
        float tau = startTau;
        string cause = "Cap";
        // 넘겨받은 프레임에는 넘겨받은 자세 그대로(τ=0) 그린다 — 토스 코루틴과 한 프레임도 겹치거나 건너뛰지 않게.
        for (bool first = true; ; first = false)
        {
            if (!first) tau += Time.deltaTime;
            Vector2 b = b0 + vB * tau;
            float h = h0 + vH * tau - 0.5f * g * tau * tau;
            if (edge == BoardEdge.Back)
                b.y = Mathf.Max(b.y, -0.5f * BoardSpace.LogicalDepth - BackClampV * BoardSpace.LogicalDepth);

            bool occlude = edge != BoardEdge.Front && h < 0f;   // T4: 앞변 외 모든 Ledge
            if (occlude && !hasLayer)
            {
                s.SetBoardMotion(b, h, 0f);
                cause = "Hidden";   // 35살: 판 레이어가 없어 가릴 게 없다 → 렌더러를 끈다
                break;
            }
            float z = occlude ? BehindBoardZ : 0f;
            s.SetBoardMotion(b, h, z);

            if (r != null && IsOffscreen(r.bounds)) { cause = "Offscreen"; break; }
            if (z == BehindBoardZ && r != null && r.bounds.max.y < BoardTopY(edge)) { cause = "Hidden"; break; }
            if (tau >= MaxFallSeconds) { cause = "Cap"; break; }
            yield return null;
        }
        Finish(e, cause, tau);
    }

    /// <summary>판 뒤로 숨은 것으로 볼 화면 y. 좌·우 = 투영 앞변 y, 뒤 = 뒷변 y − 0.05.</summary>
    private static float BoardTopY(BoardEdge edge)
        => edge == BoardEdge.Back ? BoardSpace.BackScreenY - 0.05f : BoardSpace.FrontScreenY;

    private static bool IsOffscreen(Bounds b)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return false;
        float hh = cam.orthographicSize, hw = hh * cam.aspect;
        Vector3 c = cam.transform.position;
        return b.max.y < c.y - hh || b.min.y > c.y + hh || b.max.x < c.x - hw || b.min.x > c.x + hw;
    }

    private void Finish(Entry e, string cause, float tau)
    {
        if (!e.Running) return;
        e.Running = false;
        e.Stone.SetRendererVisible(false);   // 리셋 전까지 다시 안 보이게
        Debug.Log($"[NakFall] end stone={e.Stone.StoneIndex} cause={cause} t={tau:F3}");
        var gm = GameManager.Instance;
        gm?.ReleaseNakBusy();
        gm?.NotifyNakStoneEnded();
    }

    /// <summary>실패 확정 순간 — 아직 떨어지는 중인 돌을 숨기고 끝낸다(busy·장외음 처리 포함).</summary>
    public void CompleteAll()
    {
        for (int i = 0; i < running.Count; i++)
        {
            var e = running[i];
            if (!e.Running) continue;
            if (e.Routine != null) StopCoroutine(e.Routine);
            Finish(e, "Complete", 0f);
        }
        running.Clear();
    }

    /// <summary>이 컴포넌트가 바꾼 돌을 원래대로 — 렌더러 켬, z=0, 그림자 차단·비행 그림자 해제, 물리 플래그 복원.
    /// 직후 ResetAllStones가 SetState(OnBoard)로 상태에 맞는 값을 다시 쓴다.</summary>
    public void RestoreAll()
    {
        foreach (var e in running) if (e.Routine != null) StopCoroutine(e.Routine);
        running.Clear();
        foreach (var s in touched)
        {
            if (s == null) continue;
            s.SetRendererVisible(true);
            var p = s.transform.position;
            s.transform.position = new Vector3(p.x, p.y, 0f);
            s.SetShadowGroundCut(false);
            s.SetFlightShadow(false);
            s.ExitScriptedFall();
            var col = s.GetComponent<Collider>();
            Debug.Log($"[NakFall] restore stone={s.StoneIndex} renderer={s.IsRendererVisible} " +
                      $"collider={(col != null && col.enabled)} z={s.transform.position.z:F2}");
        }
        touched.Clear();
    }

    /// <summary>낙 대기 취소(스테이지 전환) — 진행 중 연출을 멈추고 복구한다. busy는 게이트가 0으로 리셋한다.</summary>
    public void CancelAll()
    {
        foreach (var e in running) { e.Running = false; if (e.Routine != null) StopCoroutine(e.Routine); }
        running.Clear();
        RestoreAll();
    }
}
