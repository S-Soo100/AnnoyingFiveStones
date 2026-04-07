using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 돌 오브젝트 풀. 최대 20개 사전 생성, 필요 시 활성화.
/// GameManager.Start()에서 초기화. 기존 씬의 5개 Stone을 풀에 편입하고, 추가 15개를 복제 생성.
/// </summary>
public class StonePool : MonoBehaviour
{
    public static StonePool Instance { get; private set; }

    private List<Stone> allStones = new List<Stone>();
    private List<Stone> activeStones = new List<Stone>();

    public const int MaxPoolSize = 20;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 씬의 기존 Stone들을 풀에 편입하고, MaxPoolSize까지 추가 생성.
    /// GameManager.Start()에서 1회 호출.
    /// </summary>
    public void Initialize(Stone[] existingStones)
    {
        allStones.Clear();
        activeStones.Clear();

        // 기존 씬 돌 편입 + 인덱스 초기화
        for (int i = 0; i < existingStones.Length; i++)
        {
            existingStones[i].Initialize(i);
            allStones.Add(existingStones[i]);
        }

        // 추가 생성: 기존 첫 번째 돌을 템플릿으로 복제
        if (existingStones.Length > 0)
        {
            var template = existingStones[0];
            for (int i = existingStones.Length; i < MaxPoolSize; i++)
            {
                var go = Instantiate(template.gameObject);
                go.name = $"Stone_{i}";
                go.SetActive(false);
                var stone = go.GetComponent<Stone>();
                stone.Initialize(i);
                allStones.Add(stone);
            }
        }

        Debug.Log($"[StonePool] Initialized with {allStones.Count} stones ({existingStones.Length} existing + {allStones.Count - existingStones.Length} pooled)");
    }

    /// <summary>
    /// count개 돌을 활성화하고 배열로 반환. 나머지는 비활성화.
    /// </summary>
    public Stone[] Activate(int count)
    {
        count = Mathf.Clamp(count, 0, allStones.Count);
        activeStones.Clear();

        for (int i = 0; i < allStones.Count; i++)
        {
            if (i < count)
            {
                allStones[i].gameObject.SetActive(true);
                allStones[i].ResetColorAndFake();
                activeStones.Add(allStones[i]);
            }
            else
            {
                allStones[i].gameObject.SetActive(false);
            }
        }

        Debug.Log($"[StonePool] Activated {count} stones.");
        return activeStones.ToArray();
    }

    /// <summary>전부 비활성화</summary>
    public void DeactivateAll()
    {
        foreach (var stone in allStones)
        {
            stone.gameObject.SetActive(false);
        }
        activeStones.Clear();
        Debug.Log("[StonePool] All stones deactivated.");
    }

    /// <summary>
    /// 현재 활성 돌 외에 추가로 count개를 더 활성화하고 추가된 돌 배열 반환.
    /// 기존 활성 돌의 상태는 건드리지 않음.
    /// </summary>
    public Stone[] ActivateAdditional(int count)
    {
        var added = new List<Stone>();
        int remaining = count;
        for (int i = 0; i < allStones.Count && remaining > 0; i++)
        {
            if (!allStones[i].gameObject.activeSelf)
            {
                allStones[i].gameObject.SetActive(true);
                allStones[i].ResetColorAndFake();
                activeStones.Add(allStones[i]);
                added.Add(allStones[i]);
                remaining--;
            }
        }
        Debug.Log($"[StonePool] ActivateAdditional: +{added.Count} stones (total active: {activeStones.Count})");
        return added.ToArray();
    }

    /// <summary>
    /// 지정한 돌들을 비활성화하고 activeStones에서 제거.
    /// </summary>
    public void DeactivateStones(Stone[] stones)
    {
        foreach (var stone in stones)
        {
            if (stone == null) continue;
            stone.gameObject.SetActive(false);
            activeStones.Remove(stone);
        }
        Debug.Log($"[StonePool] DeactivateStones: -{stones.Length} stones (total active: {activeStones.Count})");
    }

    /// <summary>현재 활성 돌 배열</summary>
    public Stone[] ActiveStones => activeStones.ToArray();
}
