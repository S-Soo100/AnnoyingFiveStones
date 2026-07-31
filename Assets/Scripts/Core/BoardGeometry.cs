using UnityEngine;

namespace FiveStones.Core
{
    /// <summary>
    /// v17 공간 구조의 핵심 — **판정 공간과 표현 공간의 분리**.
    ///
    /// - **Board Space (논리)**: 완전한 직사각형. 모든 게임 로직이 여기서만 돈다.
    ///   원점은 보드 중심, +x=오른쪽, +y=앞(플레이어 쪽). 높이는 별도 인자 `height`.
    /// - **Screen Space (표현)**: 위를 사다리꼴로 투영한 결과. 렌더링에만 쓴다.
    ///
    /// 불변 규칙 — 변환은 양 끝에서 한 번씩만:
    ///   입력 ──Unproject──▶ [ 로직 전부 Board Space ] ──Project──▶ 렌더
    ///
    /// 이 구조가 없애는 것:
    ///  - `SkyFloorY` 개념. 던진 돌은 height &gt; 0이라 판정 대상이 아니고,
    ///    height == 0이 되는 순간에만 <see cref="Contains"/>로 안/밖을 본다.
    ///    → "이 화면 y 위는 하늘이라 면제" 같은 예외가 불필요해진다.
    ///  - 판정 폴리곤의 복수성. 판정은 언제나 이 직사각형 하나뿐이다.
    ///
    /// 순수 계산만 담는다(MonoBehaviour 아님). 그래야 EditMode 테스트로 잠글 수 있다.
    /// </summary>
    public readonly struct BoardGeometry
    {
        /// <summary>보드 가로 (보드 단위). 논리 직사각형의 폭.</summary>
        public readonly float Width;

        /// <summary>보드 세로/깊이 (보드 단위). 논리 직사각형의 앞뒤 길이.</summary>
        public readonly float Depth;

        // ── 표현(투영) 파라미터 — 렌더링에만 영향, 판정에는 일절 관여하지 않는다 ──
        private readonly float backHalfWidth;   // 화면상 뒷변 반폭
        private readonly float frontHalfWidth;  // 화면상 앞변 반폭 (> backHalfWidth → 원근)
        private readonly float backScreenY;     // 화면상 뒷변 y
        private readonly float frontScreenY;    // 화면상 앞변 y (< backScreenY → 아래쪽)
        private readonly float centerScreenX;   // 화면상 보드 중심 x
        private readonly float heightScale;     // height 1단위가 화면 y로 올라가는 양

        public BoardGeometry(
            float width, float depth,
            float backHalfWidth, float frontHalfWidth,
            float backScreenY, float frontScreenY,
            float centerScreenX, float heightScale)
        {
            Width = width;
            Depth = depth;
            this.backHalfWidth = backHalfWidth;
            this.frontHalfWidth = frontHalfWidth;
            this.backScreenY = backScreenY;
            this.frontScreenY = frontScreenY;
            this.centerScreenX = centerScreenX;
            this.heightScale = heightScale;
        }

        // ── 정규화 좌표 ↔ 보드 좌표 ─────────────────────────────────────────────
        // uv: u 0=좌 1=우, v 0=뒤 1=앞. 배치·기믹이 해상도 무관하게 쓰기 위한 좌표계.

        /// <summary>정규화 uv(0~1) → 보드 좌표(중심 원점).</summary>
        public Vector2 UvToBoard(Vector2 uv)
            => new Vector2((uv.x - 0.5f) * Width, (uv.y - 0.5f) * Depth);

        /// <summary>보드 좌표(중심 원점) → 정규화 uv(0~1).</summary>
        public Vector2 BoardToUv(Vector2 boardPos)
            => new Vector2(boardPos.x / Width + 0.5f, boardPos.y / Depth + 0.5f);

        // ── 판정 ────────────────────────────────────────────────────────────────

        /// <summary>보드 좌표가 직사각형 안인가. **이것이 유일한 낙 판정 근거다.**
        /// height는 인자로 받지 않는다 — 떠 있는 돌은 애초에 이 함수를 호출하지 않는다
        /// (height == 0이 되는 순간에만 판정). 그래서 "하늘 면제" 예외가 필요 없다.</summary>
        public bool Contains(Vector2 boardPos) => ContainsWithMargin(boardPos, 0f);

        /// <summary>마진을 준 내부 판정. margin &gt; 0이면 보드가 그만큼 넓어진 것으로 친다
        /// (경계에 아슬아슬하게 걸친 돌을 살려주는 용도).
        ///
        /// ⚠️ <see cref="Contains"/>의 오버로드로 만들지 않았다. Contains가 인자를 하나만 받는다는 것
        /// 자체가 불변식(테스트로 잠겨 있음)이라 — 높이를 받는 순간 "하늘 면제" 예외가 되살아난다.
        /// 이름을 분리해 그 잠금을 유지한다.</summary>
        public bool ContainsWithMargin(Vector2 boardPos, float margin)
            => Mathf.Abs(boardPos.x) <= Width * 0.5f + margin
            && Mathf.Abs(boardPos.y) <= Depth * 0.5f + margin;

        // ── 표현 ────────────────────────────────────────────────────────────────

        /// <summary>보드 좌표 + 높이 → 화면 좌표. 렌더링 전용.
        /// 같은 보드 좌표라면 height가 달라도 화면 x는 변하지 않는다(수직 궤적).</summary>
        public Vector2 Project(Vector2 boardPos, float height)
        {
            // v: 0=뒤, 1=앞. 원근은 "앞으로 올수록 가로로 넓어진다"로만 표현한다.
            float v = boardPos.y / Depth + 0.5f;
            float halfW = Mathf.LerpUnclamped(backHalfWidth, frontHalfWidth, v);

            // x는 보드 폭에 대한 비율(-1~1)을 그 깊이의 반폭에 적용 → 사다리꼴.
            float nx = boardPos.x / (Width * 0.5f);
            float screenX = centerScreenX + nx * halfW;

            // 높이는 화면에서 위로만 작용한다. boardPos에는 영향을 주지 않으므로
            // 같은 보드 좌표라면 height가 얼마든 screenX가 불변 → 수직 궤적이 보장된다.
            float screenY = Mathf.LerpUnclamped(backScreenY, frontScreenY, v) + height * heightScale;

            return new Vector2(screenX, screenY);
        }

        /// <summary>그 깊이에서의 원근 축소 배율. 뒤(v=0)에서 가장 작고 앞(v=1)에서 1.
        ///
        /// 보드가 사다리꼴로 보이는 것과 **정확히 같은 비율**(뒷변폭/앞변폭)이다.
        /// 보드만 좁아지고 그 위의 물체는 안 작아지면 뒤쪽 돌이 앞쪽 돌과 같은 크기로 보여
        /// 원근이 깨진다. 물체 스케일에 이 값을 곱해 그 어긋남을 없앤다.</summary>
        public float PerspectiveScale(Vector2 boardPos)
        {
            float v = boardPos.y / Depth + 0.5f;
            return Mathf.LerpUnclamped(backHalfWidth, frontHalfWidth, v) / frontHalfWidth;
        }

        /// <summary>화면 좌표 → 보드 좌표. 지면(height 0) 기준의 역변환 — 마우스 입력용.
        /// <see cref="Project"/>의 정확한 역함수다. 이 왕복이 깨지면
        /// "화면에서 클릭한 곳"과 "실제 판정 위치"가 어긋난다(= 반복돼온 버그 클래스).</summary>
        public Vector2 Unproject(Vector2 screenPos)
        {
            float v = (screenPos.y - backScreenY) / (frontScreenY - backScreenY);
            float halfW = Mathf.LerpUnclamped(backHalfWidth, frontHalfWidth, v);

            float nx = (screenPos.x - centerScreenX) / halfW;
            return new Vector2(nx * (Width * 0.5f), (v - 0.5f) * Depth);
        }
    }
}
