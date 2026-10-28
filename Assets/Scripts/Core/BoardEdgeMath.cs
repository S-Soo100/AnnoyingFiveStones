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
