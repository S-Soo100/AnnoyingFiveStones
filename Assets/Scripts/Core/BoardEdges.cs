namespace FiveStones.Core
{
    /// <summary>보드 변 너머가 무엇인가 (계획서 §5-1).
    /// Ledge = 허공(떨어진다, 그림자 없음) / Flat = 같은 높이 바닥이 이어진다(그 면에 앉는다, 그림자 유지).</summary>
    public enum EdgeKind { Ledge, Flat }

    /// <summary>보드 좌표 기준 변. Front=+y(플레이어 쪽), Back=-y, Left=-x, Right=+x.</summary>
    public enum BoardEdge { Front, Back, Left, Right }

    /// <summary>스테이지별 네 변의 종류.</summary>
    public readonly struct BoardEdges
    {
        public readonly EdgeKind Front, Back, Left, Right;

        public BoardEdges(EdgeKind front, EdgeKind back, EdgeKind left, EdgeKind right)
        { Front = front; Back = back; Left = left; Right = right; }

        public EdgeKind Get(BoardEdge e) => e switch
        {
            BoardEdge.Front => Front,
            BoardEdge.Back => Back,
            BoardEdge.Left => Left,
            _ => Right,
        };

        public static BoardEdges AllLedge => new BoardEdges(EdgeKind.Ledge, EdgeKind.Ledge, EdgeKind.Ledge, EdgeKind.Ledge);

        public override string ToString() => $"F={Front} B={Back} L={Left} R={Right}";
    }

    /// <summary>변마다 다른 판정 마진(보드 단위). 양수 = 보드를 그 변 쪽으로 넓힌다.</summary>
    public readonly struct EdgeMargins
    {
        public readonly float Front, Back, Left, Right;

        public EdgeMargins(float front, float back, float left, float right)
        { Front = front; Back = back; Left = left; Right = right; }

        public float Get(BoardEdge e) => e switch
        {
            BoardEdge.Front => Front,
            BoardEdge.Back => Back,
            BoardEdge.Left => Left,
            _ => Right,
        };

        /// <summary>Ledge 변에는 ledge, Flat 변에는 flat 마진.</summary>
        public static EdgeMargins For(BoardEdges e, float ledge, float flat) => new EdgeMargins(
            e.Front == EdgeKind.Ledge ? ledge : flat,
            e.Back == EdgeKind.Ledge ? ledge : flat,
            e.Left == EdgeKind.Ledge ? ledge : flat,
            e.Right == EdgeKind.Ledge ? ledge : flat);
    }
}
