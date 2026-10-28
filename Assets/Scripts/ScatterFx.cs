using UnityEngine;

/// <summary>
/// M1-S2 — 뿌리기 착지 먼지 (계획서 §5-5 "먼지").
///
/// 돌이 보드에 **처음 닿는 순간에만** 흰 먼지 5개가 바닥을 따라 퍼지며 사라진다(T1: 튈 때마다 X).
/// 무게감은 변형이 아니라 소리·먼지·짧은 정지로 낸다 — 효과는 적게, 정확하게.
///
/// - 위치는 **보드 좌표**로 퍼뜨린 뒤 투영한다 → 사다리꼴 원근(뒤쪽은 좁게)이 자동으로 따라온다.
/// - 2.5D 좌표: X=좌우, Y=상하, Z=깊이(카메라 z=-10, 작을수록 앞).
///   먼지 z=-0.07: 그림자(-0.06)보다 앞, Spotlight 덮개(-1)보다 뒤.
/// - 풀 50개(최대 산개 10 × 5). 다 차면 가장 오래된 것을 재사용한다.
/// - 텍스처는 코드로 그린 부드러운 원(AI 이미지 생성 없음).
/// </summary>
public class ScatterFx : MonoBehaviour
{
    public const int PoolSize = 50;
    private const int PuffsPerContact = 5;
    private const float AngleJitterDeg = 20f;
    private const float SpreadBoard = 0.35f;     // 보드 단위 반경
    private const float Duration = 0.28f;        // ease-out
    private const float PuffHeight = 0.05f;      // 투영 높이
    private const float SizeStart = 0.10f, SizeEnd = 0.22f; // 화면 지름 × 원근 배율
    private const float AlphaStart = 0.40f;
    private const float PuffZ = -0.07f;
    private static readonly Color DustColor = new Color(1.00f, 0.97f, 0.92f);

    private struct Puff
    {
        public Transform Tr;
        public Renderer R;
        public bool Active;
        public float T;
        public Vector2 Origin, Dir;
        public float Persp;
        public int Serial;
    }

    private Puff[] pool;
    private int serial;
    private MaterialPropertyBlock mpb;
    private static Material sharedMat;
    private static Texture2D sharedTex;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (sharedMat == null) sharedMat = BuildMaterial();

        pool = new Puff[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); // 노말 (0,0,-1) = 카메라 쪽. 회전하지 않는다
            go.name = "Dust";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);   // 판정 불필요 — 돌을 밀면 안 된다
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = sharedMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            pool[i] = new Puff { Tr = go.transform, R = r };
        }
    }

    /// <summary>첫 접촉점 boardPos에 먼지 5개. 각도는 균등 + rng ±20°(연출 난수 — 목표 계산과 분리, T6).</summary>
    public void SpawnDust(Vector2 boardPos, System.Random rng)
    {
        if (pool == null) return;
        float persp = BoardSpace.Current.PerspectiveScale(boardPos, 0f);
        float step = 360f / PuffsPerContact;
        float baseAng = (float)rng.NextDouble() * step;
        for (int k = 0; k < PuffsPerContact; k++)
        {
            float ang = (baseAng + step * k + ((float)rng.NextDouble() * 2f - 1f) * AngleJitterDeg) * Mathf.Deg2Rad;
            int idx = AcquireSlot();
            ref Puff p = ref pool[idx];
            p.Active = true;
            p.T = 0f;
            p.Origin = boardPos;
            p.Dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            p.Persp = persp;
            p.Serial = ++serial;
            p.Tr.gameObject.SetActive(true);
            Apply(ref p, 0f);
        }
    }

    /// <summary>모든 먼지 즉시 제거 (스테이지 전환·실패 확정·뿌리기 강제 종료).</summary>
    public void ClearAll()
    {
        if (pool == null) return;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].Active) continue;
            pool[i].Active = false;
            pool[i].Tr.gameObject.SetActive(false);
        }
    }

    private int AcquireSlot()
    {
        int oldest = 0, oldestSerial = int.MaxValue;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].Active) return i;
            if (pool[i].Serial < oldestSerial) { oldestSerial = pool[i].Serial; oldest = i; }
        }
        return oldest;   // 다 찼으면 가장 오래된 것을 재사용
    }

    private void Update()
    {
        if (pool == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].Active) continue;
            ref Puff p = ref pool[i];
            p.T += dt;
            float u = p.T / Duration;
            if (u >= 1f)
            {
                p.Active = false;
                p.Tr.gameObject.SetActive(false);
                continue;
            }
            Apply(ref p, u);
        }
    }

    private void Apply(ref Puff p, float u)
    {
        float e = 1f - (1f - u) * (1f - u);   // ease-out
        Vector2 b = p.Origin + p.Dir * (SpreadBoard * e);
        Vector2 s = BoardSpace.ToScreen(b, PuffHeight);
        p.Tr.position = new Vector3(s.x, s.y, PuffZ);
        float size = Mathf.Lerp(SizeStart, SizeEnd, e) * p.Persp;
        p.Tr.localScale = new Vector3(size, size, 1f);

        var c = DustColor;
        c.a = AlphaStart * (1f - u);
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        p.R.SetPropertyBlock(mpb);
    }

    // ── 머티리얼: StoneShadow와 같은 URP Unlit 투명 설정 (같은 셰이더 경로 → 빌드 정합 동일) ──
    private static Material BuildMaterial()
    {
        if (sharedTex == null) sharedTex = BuildSoftCircle(64);
        return StoneShadow.CreateTransparentUnlit(sharedTex);
    }

    /// <summary>흰색 RGB + 부드럽게 빠지는 알파. 색은 PropertyBlock(_BaseColor)이 곱한다.
    /// (StoneShadow의 원 텍스처는 RGB가 검정이라 흰 먼지에 쓸 수 없다)</summary>
    private static Texture2D BuildSoftCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep — 가장자리가 부드럽게 사라진다
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
