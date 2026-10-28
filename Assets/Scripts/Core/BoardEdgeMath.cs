using UnityEngine;

namespace FiveStones.Core
{
    /// <summary>
    /// M1-S5 — 변 프로필을 쓰는 순수 판정 수학 (계획서 §5-1). MonoBehaviour 아님 → EditMode 테스트로 잠근다.
    /// 치수는 하드코딩하지 않는다 — 호출부가 BoardSpace.LogicalWidth/Depth의 절반을 넘긴다.
    /// 보드 좌표: 원점 = 중심, +x = 오른쪽, +y = 앞(플레이어 쪽).
    /// </summary>
    public static class BoardEdgeMath
    {
        /// <summary>① 변마다 마진이 다른 직사각형 안인가 (뿌리기 안전 판정). 경계 위는 안.</summary>
        public static bool ContainsEdgeAware(Vector2 p, float halfW, float halfD, EdgeMargins m)
            => p.x >= -halfW - m.Left && p.x <= halfW + m.Right
            && p.y >= -halfD - m.Back && p.y <= halfD + m.Front;

        /// <summary>② 선분 from→to가 (변별 마진) 경계선을 처음 넘는 지점.
        /// 각 변은 **무한 직선(반평면)** 으로 본다 — 다른 변 바깥을 지나다가 이 선을 넘어도 교차로 친다
        /// (예: 25살 우 Flat 밖으로 나간 뒤 앞 Ledge 선을 넘는 경우). from이 이미 그 선 밖이면 그 변은 건너뛴다.
        /// filter가 있으면 그 종류의 변만 본다.</summary>
        public static bool TryFirstCrossing(Vector2 from, Vector2 to, float halfW, float halfD, EdgeMargins m,
                                            BoardEdges kinds, EdgeKind? filter, out float t01, out BoardEdge edge)
        {
            float bestT = float.MaxValue;
            BoardEdge bestE = BoardEdge.Front;
            bool found = false;
            Check(BoardEdge.Front, from.y, to.y, halfD + m.Front, +1f);
            Check(BoardEdge.Back, from.y, to.y, -halfD - m.Back, -1f);
            Check(BoardEdge.Left, from.x, to.x, -halfW - m.Left, -1f);
            Check(BoardEdge.Right, from.x, to.x, halfW + m.Right, +1f);
            t01 = found ? bestT : 0f;
            edge = bestE;
            return found;

            // outward: +1이면 값이 line보다 커질 때 밖, -1이면 작아질 때 밖
            void Check(BoardEdge e, float a, float b, float line, float outward)
            {
                if (filter.HasValue && kinds.Get(e) != filter.Value) return;
                float sa = (a - line) * outward, sb = (b - line) * outward;   // >0 = 밖
                if (sa > 0f || sb <= 0f) return;                              // 이미 밖이거나 안 넘음
                float t = sa / (sa - sb);                                     // sa ≤ 0 < sb
                if (t < bestT) { bestT = t; bestE = e; found = true; }
            }
        }

        /// <summary>③ 밖에 있는 점의 종류 — **모서리 통일 규칙**:
        /// Ledge 선(마진 0)을 하나라도 넘었으면 Ledge, 아니면 Flat.
        /// 대표 변: (Ledge면 Ledge 변 중에서) Front 우선 → 초과량이 큰 변 → 동률이면 좌우 우선.
        /// 아무 선도 넘지 않은 점이면 Flat과 초과량이 가장 큰(가장 덜 음수인) 변을 돌려준다.</summary>
        public static EdgeKind ClassifyOutside(Vector2 p, float halfW, float halfD, BoardEdges kinds,
                                               out BoardEdge representative)
        {
            float oF = p.y - halfD, oB = -halfD - p.y, oL = -halfW - p.x, oR = p.x - halfW;

            bool ledgeCrossed =
                (oF > 0f && kinds.Front == EdgeKind.Ledge) || (oB > 0f && kinds.Back == EdgeKind.Ledge) ||
                (oL > 0f && kinds.Left == EdgeKind.Ledge) || (oR > 0f && kinds.Right == EdgeKind.Ledge);

            if (ledgeCrossed)
            {
                if (oF > 0f && kinds.Front == EdgeKind.Ledge) { representative = BoardEdge.Front; return EdgeKind.Ledge; }
                representative = PickLargest(oF, oB, oL, oR, kinds, EdgeKind.Ledge, onlyCrossed: true);
                return EdgeKind.Ledge;
            }
            representative = PickLargest(oF, oB, oL, oR, kinds, null, onlyCrossed: false);
            return EdgeKind.Flat;
        }

        /// <summary>④ (M2) 플레이 중 화면 사다리꼴 낙 판정 (계획서 §5-2 D9 + D9-b).
        /// quad = [BL, BR, FL, FR] 화면 좌표. 화면 y는 위가 뒤(하늘)다.
        ///  1) 하늘 면제: pos.y &gt; max(뒷변 y + flatMargin, skyFloorY)이면 낙 아님 — skyExemptOnlyFlatBack이면 뒷변이 Flat일 때만(D9-b A)
        ///  2) 사다리꼴 안이면 낙 아님
        ///  3) Ledge 변의 바깥 부호거리 &gt; ledgeEps이면 낙 (D9: Ledge = 보이는 가장자리, eps는 투영 오차 흡수용)
        ///     변을 **무한 직선**으로 보는 건 의도다 — 투영이 깊이에 선형이라 옆변 연장선 = 보드 좌표 x=±hw 선이고,
        ///     연장선 바깥 모서리는 ClassifyOutside의 모서리 통일 규칙(Ledge 선을 하나라도 넘으면 Ledge)과 같아진다.
        ///  4) 나머지는 현행 Flat 규칙: 둘레까지 유클리드 거리 &gt; flatMargin이면 낙
        /// 전 변 Flat이면 BoardBounds.IsOutsideMat(pos, flatMargin)과 같은 결과다(테스트로 잠금).</summary>
        public static bool IsNakScreen(Vector2[] quad, Vector2 pos, BoardEdges kinds, float flatMargin, float ledgeEps,
                                       bool skyExemptOnlyFlatBack, float skyFloorY)
        {
            Vector2 bl = quad[0], br = quad[1], fl = quad[2], fr = quad[3];
            bool exemptAllowed = !skyExemptOnlyFlatBack || kinds.Back == EdgeKind.Flat;
            if (exemptAllowed)
            {
                float sky = Mathf.Max(Mathf.Max(bl.y, br.y) + flatMargin, skyFloorY);
                if (pos.y > sky) return false;
            }

            float c0 = Cross(bl, br, pos), c1 = Cross(br, fr, pos), c2 = Cross(fr, fl, pos), c3 = Cross(fl, bl, pos);
            bool inside = (c0 >= 0f && c1 >= 0f && c2 >= 0f && c3 >= 0f) || (c0 <= 0f && c1 <= 0f && c2 <= 0f && c3 <= 0f);
            if (inside) return false;

            Vector2 centroid = (bl + br + fl + fr) * 0.25f;
            if (kinds.Back == EdgeKind.Ledge && OutwardDistance(bl, br, centroid, pos) > ledgeEps) return true;
            if (kinds.Right == EdgeKind.Ledge && OutwardDistance(br, fr, centroid, pos) > ledgeEps) return true;
            if (kinds.Front == EdgeKind.Ledge && OutwardDistance(fr, fl, centroid, pos) > ledgeEps) return true;
            if (kinds.Left == EdgeKind.Ledge && OutwardDistance(fl, bl, centroid, pos) > ledgeEps) return true;

            if (flatMargin <= 0f) return true;
            float d = SegmentDistance(pos, bl, br);
            d = Mathf.Min(d, SegmentDistance(pos, br, fr));
            d = Mathf.Min(d, SegmentDistance(pos, fr, fl));
            d = Mathf.Min(d, SegmentDistance(pos, fl, bl));
            return d > flatMargin;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        /// <summary>a→b 직선에서 pos까지의 부호 거리. 다각형 중심 반대쪽(바깥)이 양수.</summary>
        private static float OutwardDistance(Vector2 a, Vector2 b, Vector2 centroid, Vector2 pos)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-6f) return 0f;
            Vector2 n = new Vector2(-d.y, d.x) / len;
            if (Vector2.Dot(n, centroid - a) > 0f) n = -n;   // 바깥을 향하게
            return Vector2.Dot(pos - a, n);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        // 순서 = 동률 우선순위: Left, Right, Front, Back (좌우 우선). 엄격히 클 때만 교체.
        private static BoardEdge PickLargest(float oF, float oB, float oL, float oR, BoardEdges kinds,
                                             EdgeKind? kind, bool onlyCrossed)
        {
            BoardEdge best = BoardEdge.Front;
            float bestO = float.NegativeInfinity;
            Consider(BoardEdge.Left, oL);
            Consider(BoardEdge.Right, oR);
            Consider(BoardEdge.Front, oF);
            Consider(BoardEdge.Back, oB);
            return best;

            void Consider(BoardEdge e, float o)
            {
                if (kind.HasValue && kinds.Get(e) != kind.Value) return;
                if (onlyCrossed && o <= 0f) return;
                if (o > bestO) { bestO = o; best = e; }
            }
        }
    }
}
