using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손가락 접기 리그 — **실제 사람 손의 굽힘**을 그대로 옮긴다.
///
/// 왜 새로 썼나 (구 로직의 문제):
///  - 모든 손가락을 **화면 가로축(world right)** 하나로 돌렸다. 사람 손가락은 저마다
///    다른 방향으로 뻗어 있어서, 공통 축으로 돌리면 옆으로 벌어지거나 비틀린다.
///    특히 엄지는 손바닥을 가로질러 뻗어 있어 완전히 다른 평면에서 움직인다.
///  - **첫 마디만** 돌렸다. 사람 손은 세 마디(MCP·PIP·DIP)가 함께 말려야 "쥐는" 모양이 된다.
///  - 뼈 순서가 모델 순서(검지→소지→엄지) 그대로여서, 포즈 배열(엄지→소지)과 어긋나
///    "검지를 펴라"가 엉뚱한 손가락을 폈다.
///
/// 새 규칙 — 축을 **리그에서 유도**한다(하드코딩 금지):
///  1. 손바닥 평면 = (검지관절→소지관절) × (손목→중지관절)
///  2. 손가락은 **자기 뼈 방향 ⟂ 손바닥 평면** 축으로 굽는다 → 손바닥 쪽으로 말린다
///  3. 엄지는 **손바닥을 가로질러**(소지 쪽으로) 모인다 — 굽힘 평면 자체가 다르다
///  4. 각 마디는 사람 가동범위 비율대로 서로 다른 각도로 굽는다
///
/// 기획 의도(유지): 접기는 **줍기 피드백**이다. 꾹 누르면 접히고 놓으면 펴진다.
/// 5단 한붓그리기는 손가락별 각도를 따로 준다.
/// </summary>
public class HandRig
{
    public const int FingerCount = 5;
    public const int Thumb = 0, Index = 1, Middle = 2, Ring = 3, Pinky = 4;

    /// <summary>손가락 첫 마디 뼈 이름 — **엄지→소지 순서**로 정렬해 둔다.
    /// (모델 원본 순서는 검지·중지·약지·소지·엄지. dedo = 스페인어 '손가락')
    /// 손가락별 판별 근거: 손을 가로지르는 축 좌표 순서 — 엄지·검지가 한쪽 끝, 소지가 반대 끝.</summary>
    private static readonly string[] ProximalBoneNames =
        { "dedo1.003", "dedo1", "dedo1.000", "dedo1.001", "dedo1.002" };

    private const string WristBoneName = "Bone";
    private const string BonePrefix = "dedo";
    private const int MaxJointsPerFinger = 3;

    /// <summary>마디별 최대 굽힘각(도). 사람 손 가동범위(MCP 90 / PIP 100 / DIP 70)를 줄여 잡았다 —
    /// 손바닥 두께가 없는 저폴리 모델이라 끝까지 말면 손가락이 손바닥을 뚫는다.</summary>
    private static readonly float[] FingerJointAngles = { 75f, 90f, 45f };

    /// <summary>엄지는 가동범위가 훨씬 작다. 크게 주면 손바닥을 관통한다.</summary>
    private static readonly float[] ThumbJointAngles = { 35f, 40f, 30f };

    /// <summary>엄지가 모일 때 손바닥 쪽으로 함께 눕는 정도. 0이면 평면 안에서만 움직여 뻣뻣하다.</summary>
    private const float ThumbPalmwardBlend = 0.5f;

    /// <summary>프리미티브 폴백의 접힘각(구 동작 그대로).</summary>
    private const float PrimitiveFoldAngle = 90f;

    private struct Joint
    {
        public Transform bone;
        public Quaternion rest;   // 기본 자세 — 여기에 **곱해서** 굽힌다 (덮어쓰면 손 모양이 무너진다)
        public Vector3 axis;      // 뼈 로컬 공간의 굽힘 축
        public float maxAngle;
    }

    private readonly Joint[][] chains = new Joint[FingerCount][];
    private readonly float[] fold = new float[FingerCount];

    /// <summary>손가락 첫 마디 Transform (엄지→소지). 시각 처리용 참조.</summary>
    public Transform[] Proximal { get; } = new Transform[FingerCount];

    private HandRig() { }

    // ==========================================
    // 빌드
    // ==========================================

    /// <summary>3D 손 모델의 뼈대에서 리그를 만든다. 뼈를 못 찾으면 null (호출부가 폴백).</summary>
    /// <param name="modelRoot">FBX 인스턴스의 루트. 손 루트의 자식이어야 한다.</param>
    public static HandRig BuildFromBones(Transform modelRoot)
    {
        if (modelRoot == null) return null;

        var all = modelRoot.GetComponentsInChildren<Transform>(true);
        var wrist = FindByName(all, WristBoneName);
        if (wrist == null)
        {
            Debug.LogWarning($"[HandRig] 손목 뼈('{WristBoneName}') 없음 → 폴백");
            return null;
        }

        var rig = new HandRig();
        for (int i = 0; i < FingerCount; i++)
        {
            rig.Proximal[i] = FindByName(all, ProximalBoneNames[i]);
            if (rig.Proximal[i] == null)
            {
                Debug.LogWarning($"[HandRig] 손가락 뼈 '{ProximalBoneNames[i]}' 없음 → 폴백");
                return null;
            }
        }

        // ── 손바닥 평면 ──
        // 관절이 늘어선 방향과 손의 길이 방향, 두 벡터가 손바닥 평면을 만든다.
        // (관절끼리는 거의 일직선이라 관절 3개로는 평면이 안 나온다 — 길이 방향이 반드시 필요)
        Vector3 across = rig.Proximal[Pinky].position - rig.Proximal[Index].position; // 검지 → 소지
        Vector3 along = rig.Proximal[Middle].position - wrist.position;               // 손목 → 중지 관절
        Vector3 palmNormal = Vector3.Cross(across, along);
        if (palmNormal.sqrMagnitude < 1e-8f)
        {
            Debug.LogWarning("[HandRig] 손바닥 평면을 못 구함(뼈가 일직선) → 폴백");
            return null;
        }
        palmNormal.Normalize();

        // 굽는 쪽 결정: **화면 앞쪽(카메라 방향)**.
        // 게임은 손바닥을 카메라로 향한 채 보여주므로, 손가락은 카메라 쪽으로 말려야
        // 손바닥 위로 접히는 게 보인다. 반대로 잡으면 손등 뒤로 숨어 접힘이 안 보인다.
        // ⚠️ 이 모델의 뼈대는 완전 평면이라 손바닥/손등을 뼈 좌표만으로는 구분할 수 없다.
        //    그래서 "보이는 쪽으로 만다"는 연출 기준으로 부호를 잡는다.
        Vector3 towardCamera = modelRoot.parent != null ? -modelRoot.parent.forward : Vector3.back;
        if (Vector3.Dot(palmNormal, towardCamera) < 0f) palmNormal = -palmNormal;

        // 엄지가 모이는 방향: 손바닥을 가로질러 소지 쪽으로 + 약간 손바닥 쪽으로.
        Vector3 acrossDir = Vector3.ProjectOnPlane(across, palmNormal).normalized;
        Vector3 thumbTarget = (acrossDir + palmNormal * ThumbPalmwardBlend).normalized;

        for (int i = 0; i < FingerCount; i++)
        {
            bool isThumb = i == Thumb;
            rig.chains[i] = BuildChain(rig.Proximal[i],
                                       isThumb ? thumbTarget : palmNormal,
                                       isThumb ? ThumbJointAngles : FingerJointAngles);
        }
        return rig;
    }

    /// <summary>구 프리미티브(원기둥 손가락)용 리그 — 피벗 하나를 X축으로 돌리던 기존 동작 그대로.</summary>
    public static HandRig BuildFromPivots(Transform[] pivots)
    {
        if (pivots == null || pivots.Length < FingerCount) return null;
        var rig = new HandRig();
        for (int i = 0; i < FingerCount; i++)
        {
            rig.Proximal[i] = pivots[i];
            rig.chains[i] = pivots[i] == null
                ? new Joint[0]
                : new[] { new Joint {
                    bone = pivots[i],
                    rest = pivots[i].localRotation,
                    axis = Vector3.right,
                    maxAngle = PrimitiveFoldAngle } };
        }
        return rig;
    }

    /// <summary>첫 마디부터 끝 마디까지 훑으며 각 마디의 굽힘 축을 뼈 로컬 공간으로 캐시한다.</summary>
    private static Joint[] BuildChain(Transform proximal, Vector3 tipMoveDir, float[] angles)
    {
        var bones = new List<Transform>(MaxJointsPerFinger);
        for (var t = proximal; t != null && bones.Count < MaxJointsPerFinger; t = FirstBoneChild(t))
            bones.Add(t);

        var joints = new Joint[bones.Count];
        for (int d = 0; d < bones.Count; d++)
        {
            var bone = bones[d];
            Vector3 dir = BoneDirection(bone, d + 1 < bones.Count ? bones[d + 1] : null);

            // 축 × 뼈방향 = 끝이 움직이는 방향. 그래서 (뼈방향 × 목표)로 잡으면
            // 양수 각도에서 손끝이 목표(손바닥 / 소지 쪽)로 간다.
            Vector3 axisWorld = Vector3.Cross(dir, tipMoveDir);
            if (axisWorld.sqrMagnitude < 1e-8f) axisWorld = bone.right; // 뼈가 목표와 평행한 예외

            joints[d] = new Joint
            {
                bone = bone,
                rest = bone.localRotation,
                // 뼈 로컬로 옮겨 캐시 → 부모가 굽어도 자식 축이 함께 따라간다.
                axis = bone.InverseTransformDirection(axisWorld.normalized).normalized,
                maxAngle = angles[Mathf.Min(d, angles.Length - 1)]
            };
        }
        return joints;
    }

    /// <summary>뼈가 뻗은 방향(월드). 끝 마디는 자식이 없어 부모→자기 방향으로 잇는다.</summary>
    private static Vector3 BoneDirection(Transform bone, Transform child)
    {
        Vector3 dir = child != null
            ? child.position - bone.position
            : bone.position - (bone.parent != null ? bone.parent.position : bone.position - bone.up);
        return dir.sqrMagnitude > 1e-8f ? dir.normalized : bone.up;
    }

    private static Transform FirstBoneChild(Transform t)
    {
        // 원본 리그에는 조작용 위젯 뼈(Controlador*)가 섞여 있다 — 이름으로 걸러야
        // 엉뚱한 뼈를 손가락 마디로 잡는다.
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name.StartsWith(BonePrefix)) return c;
        }
        return null;
    }

    private static Transform FindByName(Transform[] all, string name)
    {
        foreach (var t in all)
            if (t.name == name) return t;
        return null;
    }

    // ==========================================
    // 적용
    // ==========================================

    /// <summary>손가락 하나를 t(0=펼침, 1=완전히 접힘)만큼 굽힌다. 마디마다 각도가 다르다.</summary>
    public void SetFold(int finger, float t)
    {
        if (finger < 0 || finger >= FingerCount) return;
        t = Mathf.Clamp01(t);
        fold[finger] = t;

        var chain = chains[finger];
        if (chain == null) return;
        for (int d = 0; d < chain.Length; d++)
        {
            var j = chain[d];
            if (j.bone == null) continue;
            j.bone.localRotation = j.rest * Quaternion.AngleAxis(t * j.maxAngle, j.axis);
        }
    }

    public float GetFold(int finger)
        => finger >= 0 && finger < FingerCount ? fold[finger] : 0f;

    /// <summary>전부 펼친 기본 자세로 즉시 복원.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < FingerCount; i++) SetFold(i, 0f);
    }

    /// <summary>연결된 마디 수 (로그·진단용).</summary>
    public int JointCount
    {
        get
        {
            int n = 0;
            foreach (var c in chains) if (c != null) n += c.Length;
            return n;
        }
    }
}
