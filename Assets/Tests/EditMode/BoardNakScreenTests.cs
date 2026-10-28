using NUnit.Framework;
using UnityEngine;
using FiveStones.Core;

namespace FiveStones.Tests
{
    /// <summary>
    /// M2-S1 — 플레이 중 화면 사다리꼴 낙 판정 IsNakScreen (계획서 §5-2, D9 + D9-b A).
    ///
    /// 잠그는 관계:
    ///  - 전 변 Flat이면 현행 BoardBounds.IsOutsideMat(quad, 0.8)과 **결과가 같다** (아래 Reference가 현행 식 사본)
    ///  - Ledge 변은 보이는 가장자리 + 수치 허용치(0.02)만 넘어도 낙
    ///  - Flat 변은 유클리드 0.8 유예 그대로
    ///  - D9-b A: 하늘 면제는 뒷변이 Flat일 때만
    /// </summary>
    public class BoardNakScreenTests
    {
        // 화면 사다리꼴 [BL, BR, FL, FR] — 실제 스테이지와 비슷한 모양 (관계만 본다)
        private static readonly Vector2[] Quad =
        {
            new Vector2(-5.54f, -2.67f), new Vector2(5.54f, -2.67f),
            new Vector2(-10.24f, -7.15f), new Vector2(10.24f, -7.15f),
        };
        private const float Flat = 0.8f, Eps = 0.02f;
        private const float SkyFloor = -2.67f;   // SetQuadOverride가 뒷변 y로 동기화한 값

        private static readonly BoardEdges AllFlat = new BoardEdges(EdgeKind.Flat, EdgeKind.Flat, EdgeKind.Flat, EdgeKind.Flat);
        private static readonly BoardEdges Age25 = new BoardEdges(EdgeKind.Ledge, EdgeKind.Flat, EdgeKind.Flat, EdgeKind.Flat);

        private static bool Nak(Vector2 p, BoardEdges k)
            => BoardEdgeMath.IsNakScreen(Quad, p, k, Flat, Eps, true, SkyFloor);

        // ── 현행 BoardBounds.IsOutsideMat(quad, margin) 사본 (v30) — 비교 기준 ──
        private static bool Reference(Vector2 pos, float m)
        {
            float backY = Mathf.Max(Quad[0].y, Quad[1].y) + m;
            float sky = Mathf.Max(backY, SkyFloor);
            if (pos.y > sky) return false;
            float c0 = Cross(Quad[0], Quad[1], pos), c1 = Cross(Quad[1], Quad[3], pos);
            float c2 = Cross(Quad[3], Quad[2], pos), c3 = Cross(Quad[2], Quad[0], pos);
            bool inside = (c0 >= 0 && c1 >= 0 && c2 >= 0 && c3 >= 0) || (c0 <= 0 && c1 <= 0 && c2 <= 0 && c3 <= 0);
            if (inside) return false;
            if (m <= 0f) return true;
            float d = Seg(pos, Quad[0], Quad[1]);
            d = Mathf.Min(d, Seg(pos, Quad[1], Quad[3]));
            d = Mathf.Min(d, Seg(pos, Quad[3], Quad[2]));
            d = Mathf.Min(d, Seg(pos, Quad[2], Quad[0]));
            return d > m;
        }
        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        [Test]
        public void 전_변_Flat이면_현행_IsOutsideMat과_격자_전체가_같다()
        {
            int n = 0;
            for (float x = -13f; x <= 13f; x += 0.091f)
                for (float y = -9.5f; y <= 0.5f; y += 0.077f)
                {
                    var p = new Vector2(x, y);
                    Assert.AreEqual(Reference(p, Flat), Nak(p, AllFlat), $"p {p}");
                    n++;
                }
            Assert.Greater(n, 30000);
        }

        [Test]
        public void 사다리꼴_안은_어떤_변_구성이든_낙이_아니다()
        {
            foreach (var k in new[] { BoardEdges.AllLedge, AllFlat, Age25 })
            {
                Assert.IsFalse(Nak(new Vector2(0f, -5f), k));
                Assert.IsFalse(Nak(new Vector2(9.5f, -7.0f), k), "앞 오른쪽 모서리 안쪽");
            }
        }

        [Test]
        public void Ledge_앞변은_허용치만_넘어도_낙이다()
        {
            Assert.IsFalse(Nak(new Vector2(0f, -7.15f - 0.01f), BoardEdges.AllLedge), "0.01 = 허용치 안");
            Assert.IsTrue(Nak(new Vector2(0f, -7.15f - 0.05f), BoardEdges.AllLedge), "0.05 = 낙 (D9)");
            Assert.IsFalse(Nak(new Vector2(0f, -7.15f - 0.5f), AllFlat), "같은 점도 Flat이면 유예");
        }

        [Test]
        public void Flat_변은_유클리드_0점8_유예를_유지한다()
        {
            // 오른쪽 변 바깥 법선 방향으로 0.5 / 0.9
            Vector2 a = Quad[1], b = Quad[3];
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = (b - a).normalized;
            Vector2 outN = new Vector2(-dir.y, dir.x);
            Vector2 centroid = (Quad[0] + Quad[1] + Quad[2] + Quad[3]) * 0.25f;
            if (Vector2.Dot(outN, mid - centroid) < 0f) outN = -outN;   // 중심에서 멀어지는 쪽
            Assert.IsFalse(Nak(mid + outN * 0.5f, Age25), "+0.5 생존");
            Assert.IsTrue(Nak(mid + outN * 0.9f, Age25), "+0.9 낙");
        }

        [Test]
        public void Ledge_뒷변은_하늘_면제가_없다_D9b()
        {
            var p = new Vector2(0f, -2.67f + 0.5f);   // 뒷변 위 0.5 (하늘)
            Assert.IsTrue(Nak(p, BoardEdges.AllLedge), "Ledge 뒷변: 면제 해제 → 낙");
            Assert.IsFalse(Nak(p, Age25), "Flat 뒷변: 유예 띠 안 → 생존");
            Assert.IsFalse(Nak(new Vector2(0f, -2.67f + 3f), Age25), "Flat 뒷변: 하늘 면제 유지");
            Assert.IsTrue(BoardEdgeMath.IsNakScreen(Quad, new Vector2(0f, -2.67f + 3f), Age25, Flat, Eps, true, SkyFloor) == false);
        }

        [Test]
        public void 하늘_면제를_전부_유지하는_설정이면_Ledge_뒷변도_면제된다()
        {
            var high = new Vector2(0f, -2.67f + 3f);
            Assert.IsFalse(BoardEdgeMath.IsNakScreen(Quad, high, BoardEdges.AllLedge, Flat, Eps, false, SkyFloor), "D9-b B");
            Assert.IsTrue(BoardEdgeMath.IsNakScreen(Quad, high, BoardEdges.AllLedge, Flat, Eps, true, SkyFloor), "D9-b A");
        }

        [Test]
        public void 모서리_25살_앞Ledge_우Flat()
        {
            // 앞변 아래로 0.1 + 오른쪽 바깥 → Ledge 앞선을 넘었으니 낙
            Assert.IsTrue(Nak(new Vector2(10.5f, -7.25f), Age25));
            // 앞선 안쪽, 오른쪽만 0.3 바깥 → 유예
            Assert.IsFalse(Nak(new Vector2(10.24f - 0.05f + 0.3f, -7.10f), Age25));
        }

        private static readonly BoardEdges Age35 = new BoardEdges(EdgeKind.Ledge, EdgeKind.Flat, EdgeKind.Ledge, EdgeKind.Ledge);

        [Test]
        public void 모서리_35살_뒤Flat_좌Ledge는_보드좌표_모서리_통일규칙과_같다()
        {
            // 화면에서 옆변 연장선 = 보드 좌표 x=±hw 선(투영이 깊이에 선형 — BoardGeometry.Project).
            // 그래서 뒷변 바로 위, 연장선 바깥(BL 바로 위)은 보드 좌표로 "좌·뒤 모서리" 영역이고,
            // ClassifyOutside의 모서리 통일 규칙(Ledge 선을 하나라도 넘으면 Ledge)과 같이 낙이다.
            var aboveBL = new Vector2(Quad[0].x + 0.05f, Quad[0].y + 0.3f);
            Assert.IsTrue(Nak(aboveBL, Age35), "좌 Ledge 연장선 바깥 = 모서리 → 낙");
            // 같은 높이라도 연장선 안쪽(뒷변 띠 한가운데)은 Flat 뒷변 유예로 생존
            Assert.IsFalse(Nak(new Vector2(0f, Quad[0].y + 0.3f), Age35), "뒷변 띠 안쪽 → 생존");
        }
    }
}
