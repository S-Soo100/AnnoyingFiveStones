using NUnit.Framework;
using UnityEngine;
using FiveStones.Core;

namespace FiveStones.Tests
{
    /// <summary>
    /// M1-S5 — 변 프로필(Ledge/Flat)과 그 판정 수학 (계획서 §5-1·§5-2).
    ///
    /// 잠그는 관계:
    ///  - 모든 변 마진이 같으면 기존 ContainsWithMargin과 **완전히 같다** (Flat 쪽은 현행 그대로)
    ///  - Ledge 마진 0 / Flat 마진 0.2가 변마다 따로 걸린다 (D9 뿌리기분)
    ///  - 선분이 처음 넘는 변(종류 필터 포함)
    ///  - 모서리 통일 규칙: "Ledge 선을 하나라도 넘었으면 Ledge", 대표 변은 Front 우선 → 초과량 → 좌우 우선
    /// </summary>
    public class BoardEdgeMathTests
    {
        private const float HW = 8f, HD = 4.5f;   // 테스트용 반폭·반깊이 (관계만 본다)

        private static BoardEdges Age25 => new BoardEdges(EdgeKind.Ledge, EdgeKind.Flat, EdgeKind.Flat, EdgeKind.Flat);

        // ── BoardEdges / EdgeMargins ─────────────────────────────────────────

        [Test]
        public void AllLedge는_네_변이_모두_Ledge다()
        {
            var e = BoardEdges.AllLedge;
            foreach (BoardEdge b in System.Enum.GetValues(typeof(BoardEdge)))
                Assert.AreEqual(EdgeKind.Ledge, e.Get(b), b.ToString());
        }

        [Test]
        public void EdgeMargins_For는_변_종류대로_마진을_준다()
        {
            var m = EdgeMargins.For(Age25, 0f, 0.2f);
            Assert.AreEqual(0f, m.Front, 1e-6f);
            Assert.AreEqual(0.2f, m.Back, 1e-6f);
            Assert.AreEqual(0.2f, m.Left, 1e-6f);
            Assert.AreEqual(0.2f, m.Right, 1e-6f);
            Assert.AreEqual(m.Right, m.Get(BoardEdge.Right), 1e-6f);
        }

        // ── ① ContainsEdgeAware ──────────────────────────────────────────────

        [Test]
        public void 마진이_모두_같으면_ContainsWithMargin과_같다()
        {
            var geo = new BoardGeometry(HW * 2f, HD * 2f, 4f, 8f, -2f, -7f, 0f, 1f);
            foreach (float margin in new[] { 0f, 0.2f, 0.5f })
            {
                var m = EdgeMargins.For(BoardEdges.AllLedge, margin, margin);
                for (float x = -9.5f; x <= 9.5f; x += 0.137f)
                    for (float y = -6f; y <= 6f; y += 0.113f)
                    {
                        var p = new Vector2(x, y);
                        Assert.AreEqual(geo.ContainsWithMargin(p, margin),
                            BoardEdgeMath.ContainsEdgeAware(p, HW, HD, m), $"margin {margin} p {p}");
                    }
            }
        }

        [Test]
        public void Ledge_변은_마진_0_Flat_변은_마진_0점2()
        {
            var m = EdgeMargins.For(Age25, 0f, 0.2f);
            // 오른쪽(Flat) 0.1 바깥 = 안전, 앞(Ledge) 0.1 바깥 = 밖
            Assert.IsTrue(BoardEdgeMath.ContainsEdgeAware(new Vector2(HW + 0.1f, 0f), HW, HD, m), "Flat 우 +0.1");
            Assert.IsFalse(BoardEdgeMath.ContainsEdgeAware(new Vector2(HW + 0.25f, 0f), HW, HD, m), "Flat 우 +0.25");
            Assert.IsFalse(BoardEdgeMath.ContainsEdgeAware(new Vector2(0f, HD + 0.1f), HW, HD, m), "Ledge 앞 +0.1");
            Assert.IsTrue(BoardEdgeMath.ContainsEdgeAware(new Vector2(0f, HD - 0.01f), HW, HD, m), "Ledge 앞 -0.01");
            Assert.IsTrue(BoardEdgeMath.ContainsEdgeAware(new Vector2(0f, HD), HW, HD, m), "경계 위는 안");
        }

        // ── ② TryFirstCrossing ───────────────────────────────────────────────

        [Test]
        public void 앞으로_나가는_선분은_앞변에서_교차한다()
        {
            var m = EdgeMargins.For(BoardEdges.AllLedge, 0f, 0f);
            bool hit = BoardEdgeMath.TryFirstCrossing(new Vector2(0f, 0f), new Vector2(0f, 9f), HW, HD, m,
                BoardEdges.AllLedge, null, out float t, out BoardEdge e);
            Assert.IsTrue(hit);
            Assert.AreEqual(BoardEdge.Front, e);
            Assert.AreEqual(0.5f, t, 1e-5f);
        }

        [Test]
        public void 안에서_안으로는_교차가_없다()
        {
            var m = EdgeMargins.For(BoardEdges.AllLedge, 0f, 0f);
            Assert.IsFalse(BoardEdgeMath.TryFirstCrossing(new Vector2(-1f, -1f), new Vector2(2f, 3f), HW, HD, m,
                BoardEdges.AllLedge, null, out _, out _));
        }

        [Test]
        public void 여러_변을_넘으면_먼저_닿는_변이다()
        {
            var m = EdgeMargins.For(BoardEdges.AllLedge, 0f, 0f);
            // 오른쪽으로 많이, 앞으로 조금 → 오른쪽(x=8)을 먼저 넘는다
            bool hit = BoardEdgeMath.TryFirstCrossing(new Vector2(0f, 0f), new Vector2(16f, 6f), HW, HD, m,
                BoardEdges.AllLedge, null, out float t, out BoardEdge e);
            Assert.IsTrue(hit);
            Assert.AreEqual(BoardEdge.Right, e);
            Assert.AreEqual(0.5f, t, 1e-5f);
        }

        [Test]
        public void Ledge_필터는_Flat_변을_건너뛰고_뒤에_넘는_Ledge_선을_잡는다()
        {
            // 25살: 오른쪽 Flat을 먼저 넘고(t=0.5), 앞 Ledge 선은 t=0.75에서 넘는다
            var m = EdgeMargins.For(Age25, 0f, 0f);
            bool hit = BoardEdgeMath.TryFirstCrossing(new Vector2(0f, 0f), new Vector2(16f, 6f), HW, HD, m,
                Age25, EdgeKind.Ledge, out float t, out BoardEdge e);
            Assert.IsTrue(hit);
            Assert.AreEqual(BoardEdge.Front, e);
            Assert.AreEqual(0.75f, t, 1e-5f);
        }

        [Test]
        public void 마진은_교차선을_밖으로_민다()
        {
            var m = EdgeMargins.For(Age25, 0f, 0.2f);   // 오른쪽 Flat 마진 0.2 → 선 x = 8.2
            bool hit = BoardEdgeMath.TryFirstCrossing(new Vector2(0f, 0f), new Vector2(16.4f, 0f), HW, HD, m,
                Age25, null, out float t, out BoardEdge e);
            Assert.IsTrue(hit);
            Assert.AreEqual(BoardEdge.Right, e);
            Assert.AreEqual(0.5f, t, 1e-5f);
        }

        [Test]
        public void 이미_그_선_밖에서_출발하면_그_변은_교차로_치지_않는다()
        {
            var m = EdgeMargins.For(BoardEdges.AllLedge, 0f, 0f);
            Assert.IsFalse(BoardEdgeMath.TryFirstCrossing(new Vector2(9f, 0f), new Vector2(10f, 0f), HW, HD, m,
                BoardEdges.AllLedge, null, out _, out _));
        }

        // ── ③ ClassifyOutside (모서리 통일 규칙) ────────────────────────────

        [Test]
        public void Ledge_선을_하나라도_넘으면_Ledge다()
        {
            // 25살 앞(Ledge)×우(Flat) 모서리 바깥 → Ledge, 대표 변 Front
            var k = BoardEdgeMath.ClassifyOutside(new Vector2(HW + 2f, HD + 0.1f), HW, HD, Age25, out BoardEdge rep);
            Assert.AreEqual(EdgeKind.Ledge, k);
            Assert.AreEqual(BoardEdge.Front, rep);
        }

        [Test]
        public void Flat_선만_넘었으면_Flat이다()
        {
            var k = BoardEdgeMath.ClassifyOutside(new Vector2(HW + 2f, HD - 0.1f), HW, HD, Age25, out BoardEdge rep);
            Assert.AreEqual(EdgeKind.Flat, k);
            Assert.AreEqual(BoardEdge.Right, rep);
        }

        [Test]
        public void Ledge가_여럿이면_Front가_우선이다()
        {
            // 앞은 조금, 오른쪽은 많이 넘었어도 Front
            var k = BoardEdgeMath.ClassifyOutside(new Vector2(HW + 3f, HD + 0.05f), HW, HD, BoardEdges.AllLedge, out BoardEdge rep);
            Assert.AreEqual(EdgeKind.Ledge, k);
            Assert.AreEqual(BoardEdge.Front, rep);
        }

        [Test]
        public void Front가_없으면_초과량이_큰_변이고_동률이면_좌우가_우선이다()
        {
            var k = BoardEdgeMath.ClassifyOutside(new Vector2(-HW - 0.5f, -HD - 1f), HW, HD, BoardEdges.AllLedge, out BoardEdge rep);
            Assert.AreEqual(EdgeKind.Ledge, k);
            Assert.AreEqual(BoardEdge.Back, rep, "뒤 초과 1.0 > 좌 0.5");

            BoardEdgeMath.ClassifyOutside(new Vector2(-HW - 1f, -HD - 1f), HW, HD, BoardEdges.AllLedge, out rep);
            Assert.AreEqual(BoardEdge.Left, rep, "동률 → 좌우 우선");
        }

        [Test]
        public void 뒤가_Flat이고_좌우가_Ledge면_좌우로_넘은_점은_Ledge다()
        {
            // 35살: 뒤 Flat, 좌우 Ledge
            var age35 = new BoardEdges(EdgeKind.Ledge, EdgeKind.Flat, EdgeKind.Ledge, EdgeKind.Ledge);
            var k = BoardEdgeMath.ClassifyOutside(new Vector2(-HW - 0.1f, -HD - 2f), HW, HD, age35, out BoardEdge rep);
            Assert.AreEqual(EdgeKind.Ledge, k);
            Assert.AreEqual(BoardEdge.Left, rep, "Ledge 변 중에서 대표를 고른다");
        }
    }
}
