using NUnit.Framework;
using UnityEngine;
using FiveStones.Core;

namespace FiveStones.Tests
{
    /// <summary>
    /// BoardGeometry 불변식 테스트.
    ///
    /// ⚠️ **여기 있는 것은 전부 "수치와 무관한 관계"다.**
    /// v17은 모든 밸런스 수치를 백지에서 재튜닝하므로, 특정 좌표값을 정답으로 박제하는
    /// 스냅샷 테스트는 쓸 수 없다(재튜닝하면 전부 빨간불이 된다).
    /// 대신 "숫자가 뭐로 바뀌든 항상 참이어야 하는 것"만 잠근다.
    ///
    /// 잠그는 대상 — 기획서 v11 §2, §6:
    ///  - 판정은 직사각형 하나뿐이다
    ///  - 보이는 것과 판정이 일치한다 (Project ↔ Unproject 왕복)
    ///  - 던진 돌의 보드 좌표는 높이가 변해도 불변이다 (수직 궤적)
    /// </summary>
    public class BoardGeometryTests
    {
        /// <summary>테스트용 보드. 구체 수치는 아무 의미 없다 — 관계만 검증하므로
        /// 여기 값을 바꿔도 모든 테스트가 그대로 통과해야 한다.</summary>
        private static BoardGeometry MakeBoard() => new BoardGeometry(
            width: 16f, depth: 9f,
            backHalfWidth: 4f, frontHalfWidth: 8f,   // 뒤가 좁다 = 원근
            backScreenY: -2f, frontScreenY: -7f,     // 앞이 화면 아래
            centerScreenX: 0f, heightScale: 1f);

        // ── 판정: 직사각형 하나뿐 ────────────────────────────────────────────

        [Test]
        public void 보드_중심은_항상_안쪽이다()
        {
            var b = MakeBoard();
            Assert.IsTrue(b.Contains(Vector2.zero));
        }

        [Test]
        public void 네_모서리_안쪽은_안_바깥은_밖이다()
        {
            var b = MakeBoard();
            float hw = b.Width * 0.5f, hd = b.Depth * 0.5f;
            const float eps = 0.01f;

            // 모서리 바로 안쪽 = 안
            Assert.IsTrue(b.Contains(new Vector2(hw - eps, hd - eps)), "우앞 안쪽");
            Assert.IsTrue(b.Contains(new Vector2(-hw + eps, -hd + eps)), "좌뒤 안쪽");
            // 모서리 바로 바깥 = 밖
            Assert.IsFalse(b.Contains(new Vector2(hw + eps, 0f)), "우측 바깥");
            Assert.IsFalse(b.Contains(new Vector2(0f, -hd - eps)), "뒤쪽 바깥");
        }

        [Test]
        public void 판정은_높이와_무관하다_Contains는_보드좌표만_받는다()
        {
            // 컴파일 시점 불변식: Contains에 height 인자가 없다.
            // 떠 있는 돌은 애초에 판정 대상이 아니므로 "하늘 면제" 예외가 존재할 수 없다.
            var m = typeof(BoardGeometry).GetMethod(nameof(BoardGeometry.Contains));
            Assert.AreEqual(1, m.GetParameters().Length,
                "Contains는 보드 좌표 하나만 받아야 한다. 높이를 받는 순간 '면제 규칙'이 되살아난다.");
        }

        [Test]
        public void 마진은_보드를_넓히기만_한다()
        {
            // 경계 밖 점이라도 마진 안이면 살아난다. 마진 0이면 Contains와 완전히 같아야 한다.
            var b = MakeBoard();
            var justOutside = new Vector2(b.Width * 0.5f + 0.1f, 0f);

            Assert.IsFalse(b.Contains(justOutside), "마진 없으면 밖");
            Assert.IsFalse(b.ContainsWithMargin(justOutside, 0.05f), "마진이 모자라면 여전히 밖");
            Assert.IsTrue(b.ContainsWithMargin(justOutside, 0.2f), "마진이 충분하면 안");

            foreach (var p in new[] { Vector2.zero, justOutside, new Vector2(0f, 99f) })
                Assert.AreEqual(b.Contains(p), b.ContainsWithMargin(p, 0f),
                    "마진 0은 Contains와 동일해야 한다");
        }

        // ── 좌표계 왕복 ──────────────────────────────────────────────────────

        [Test]
        public void uv와_보드좌표는_왕복해도_같다()
        {
            var b = MakeBoard();
            foreach (var uv in new[] {
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.23f, 0.77f) })
            {
                var back = b.BoardToUv(b.UvToBoard(uv));
                Assert.AreEqual(uv.x, back.x, 1e-4f, $"u 왕복 실패 {uv}");
                Assert.AreEqual(uv.y, back.y, 1e-4f, $"v 왕복 실패 {uv}");
            }
        }

        [Test]
        public void uv_0과_1은_보드_경계에_대응한다()
        {
            var b = MakeBoard();
            Assert.IsTrue(b.Contains(b.UvToBoard(new Vector2(0.5f, 0.5f))), "uv 중앙은 안");
            // uv 경계를 살짝 넘으면 보드 밖이어야 한다 = uv[0,1]이 정확히 보드 범위
            Assert.IsFalse(b.Contains(b.UvToBoard(new Vector2(1.01f, 0.5f))), "u>1은 밖");
            Assert.IsFalse(b.Contains(b.UvToBoard(new Vector2(0.5f, -0.01f))), "v<0은 밖");
        }

        // ── 보이는 것 = 판정 (핵심 불변식) ───────────────────────────────────

        [Test]
        public void 지면에서_투영과_역투영은_왕복해도_같다()
        {
            // 이것이 "보이는 보드 = 판정 경계"의 수학적 표현이다.
            // 이 왕복이 깨지면 화면에서 클릭한 곳과 실제 판정 위치가 어긋난다
            // = v11·v12·v14·v16에서 반복된 버그 클래스.
            var b = MakeBoard();
            foreach (var p in new[] {
                Vector2.zero,
                new Vector2(3f, 2f), new Vector2(-5f, -3f), new Vector2(7.9f, 4.4f) })
            {
                var back = b.Unproject(b.Project(p, 0f));
                Assert.AreEqual(p.x, back.x, 1e-3f, $"x 왕복 실패 {p}");
                Assert.AreEqual(p.y, back.y, 1e-3f, $"y 왕복 실패 {p}");
            }
        }

        // ── 던지기: 수직 궤적 ────────────────────────────────────────────────

        [Test]
        public void 높이가_변해도_화면_x는_변하지_않는다()
        {
            // 기획서 v11 §5: 던진 돌은 제자리에서 수직으로 솟는다.
            // 그림자가 조준선 역할을 하려면 이 불변식이 반드시 성립해야 한다.
            var b = MakeBoard();
            var p = new Vector2(3.5f, -1.2f);
            float x0 = b.Project(p, 0f).x;
            foreach (float h in new[] { 0.5f, 2f, 5f, 12f })
                Assert.AreEqual(x0, b.Project(p, h).x, 1e-4f, $"height {h}에서 x가 흔들림");
        }

        [Test]
        public void 높이가_올라가면_화면에서_위로_간다()
        {
            var b = MakeBoard();
            var p = new Vector2(1f, 1f);
            Assert.Greater(b.Project(p, 3f).y, b.Project(p, 0f).y);
        }

        [Test]
        public void 그림자는_지면에_남고_돌만_올라간다()
        {
            // 그림자 = height 0으로 투영한 위치. 돌이 아무리 높아도 그림자는 지면에 남는다.
            // 이 관계가 성립해야 그림자가 "조준선"으로 기능한다 (기획서 v11 §5).
            var b = MakeBoard();
            var p = new Vector2(-2f, 3f);
            var shadow = b.Project(p, 0f);
            foreach (float h in new[] { 1f, 4f, 9f })
            {
                var stone = b.Project(p, h);
                Assert.AreEqual(shadow.x, stone.x, 1e-4f, $"height {h}: 돌이 그림자 바로 위에 있어야 한다");
                Assert.Greater(stone.y, shadow.y, $"height {h}: 돌만 올라가고 그림자는 지면에 남아야 한다");
            }
        }

        // ── 표현: 원근 ───────────────────────────────────────────────────────

        [Test]
        public void 뒤쪽이_앞쪽보다_화면에서_좁다()
        {
            // 논리는 직사각형이지만 화면에는 사다리꼴로 보여야 한다 (결정 6-4).
            var b = MakeBoard();
            float hw = b.Width * 0.5f, hd = b.Depth * 0.5f;
            float backSpan  = b.Project(new Vector2(hw, -hd), 0f).x - b.Project(new Vector2(-hw, -hd), 0f).x;
            float frontSpan = b.Project(new Vector2(hw,  hd), 0f).x - b.Project(new Vector2(-hw,  hd), 0f).x;
            Assert.Less(backSpan, frontSpan, "뒷변이 앞변보다 좁아야 원근으로 보인다");
        }

        [Test]
        public void 뒤쪽_물체가_더_작게_보인다()
        {
            // 보드만 사다리꼴이고 그 위 물체가 안 작아지면 원근이 깨진다.
            var b = MakeBoard();
            float hd = b.Depth * 0.5f;
            float back  = b.PerspectiveScale(new Vector2(0f, -hd)); // 뒤
            float front = b.PerspectiveScale(new Vector2(0f,  hd)); // 앞

            Assert.Less(back, front, "뒤가 앞보다 작아야 한다");
            Assert.AreEqual(1f, front, 1e-4f, "앞변이 기준(1배)이어야 한다");
            // 축소 비율은 보드가 좁아지는 비율과 정확히 같아야 한다 — 따로 놀면 안 된다.
            float backSpan  = b.Project(new Vector2(b.Width * 0.5f, -hd), 0f).x
                            - b.Project(new Vector2(-b.Width * 0.5f, -hd), 0f).x;
            float frontSpan = b.Project(new Vector2(b.Width * 0.5f,  hd), 0f).x
                            - b.Project(new Vector2(-b.Width * 0.5f,  hd), 0f).x;
            Assert.AreEqual(backSpan / frontSpan, back, 1e-4f,
                "물체 축소율 = 보드 폭 축소율이어야 원근이 일관된다");
        }

        [Test]
        public void 위로_뜬_물체는_더_크게_보인다()
        {
            // 내려다보는 시점이라 물체가 뜨면 카메라에 가까워진다.
            // 이걸 빼면 던진 돌이 중간 깊이의 축소율에 묶인 채 하늘에 떠 있어 계속 작아 보인다.
            var b = MakeBoard();
            var p = new Vector2(0f, 0f);

            float ground = b.PerspectiveScale(p, 0f);
            Assert.AreEqual(b.PerspectiveScale(p), ground, 1e-5f, "높이 0은 인자 생략과 같아야 한다");

            float prev = ground;
            foreach (float h in new[] { 1f, 3f, 6f, 10f })
            {
                float s = b.PerspectiveScale(p, h);
                Assert.Greater(s, prev, $"height {h}에서 더 커져야 한다");
                prev = s;
            }
        }

        [Test]
        public void 논리_공간에서는_이동이_균일하다()
        {
            // 사다리꼴이 만들던 문제: 뒤쪽에서 커서를 조금만 움직여도 보드상 훅 지나감.
            // Board Space에서는 같은 거리 이동이 뒤/앞 어디서나 같은 거리여야 한다.
            var b = MakeBoard();
            var backMove  = b.UvToBoard(new Vector2(0.6f, 0.1f)) - b.UvToBoard(new Vector2(0.5f, 0.1f));
            var frontMove = b.UvToBoard(new Vector2(0.6f, 0.9f)) - b.UvToBoard(new Vector2(0.5f, 0.9f));
            Assert.AreEqual(backMove.x, frontMove.x, 1e-4f, "논리 공간 이동량이 앞뒤에서 달라짐");
        }
    }
}
