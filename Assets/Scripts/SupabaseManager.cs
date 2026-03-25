using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Supabase REST API 연동 싱글톤.
/// 기록 업로드(PostRecord), 상위 기록 조회(GetTopRecords), 내 순위 조회(GetPlayerRank).
/// 네트워크 실패 시 Debug.LogWarning 후 콜백으로 실패 통보 — 게임 진행에 영향 없음.
/// </summary>
public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance { get; private set; }

    private const string supabaseUrl = "https://sevvsxnixlhjeyoqymhs.supabase.co";
    private const string supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNldnZzeG5peGxoamV5b3F5bWhzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzQxNjAwNTksImV4cCI6MjA4OTczNjA1OX0.kADdsTmSGWxD2aLAbvVpqbcusjXX1VZLlAv8gW12EYk";
    private string baseEndpoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        baseEndpoint = supabaseUrl + "/rest/v1/five_stones_records";
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // 공개 API
    // ------------------------------------------------------------------

    /// <summary>
    /// 기록 업로드. 성공/실패를 onComplete(bool)로 통보.
    /// 실패 시 Debug.LogWarning만 — 게임 진행 차단 없음.
    /// </summary>
    public void PostRecord(string playerName, float clearTimeSeconds, Action<bool> onComplete)
    {
        StartCoroutine(CoPostRecord(playerName, clearTimeSeconds, onComplete));
    }

    /// <summary>
    /// 상위 limit개 기록 조회. 실패 시 onComplete(null).
    /// </summary>
    public void GetTopRecords(int limit, Action<List<RecordEntry>> onComplete)
    {
        StartCoroutine(CoGetTopRecords(limit, onComplete));
    }

    /// <summary>
    /// 내 기록보다 빠른 기록 수 조회 → rank = count + 1.
    /// 실패 시 onComplete(-1).
    /// </summary>
    public void GetPlayerRank(float clearTimeSeconds, Action<int> onComplete)
    {
        StartCoroutine(CoGetPlayerRank(clearTimeSeconds, onComplete));
    }

    /// <summary>
    /// 전체 기록 조회 (최대 1000개, created_at 오름차순).
    /// 실패 시 onComplete(null).
    /// </summary>
    public void GetAllRecords(Action<List<RecordEntry>> onComplete)
    {
        StartCoroutine(CoGetAllRecords(onComplete));
    }

    // ------------------------------------------------------------------
    // 코루틴 구현
    // ------------------------------------------------------------------

    private IEnumerator CoPostRecord(string playerName, float clearTimeSeconds, Action<bool> onComplete)
    {
        string json = $"{{\"player_name\":\"{EscapeJson(playerName)}\",\"clear_time_seconds\":{clearTimeSeconds}}}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(baseEndpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 10;

        SetCommonHeaders(req);
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Prefer", "return=minimal");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[SupabaseManager] PostRecord success: {playerName}, {clearTimeSeconds}s");
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogWarning($"[SupabaseManager] PostRecord failed: {req.error} ({req.responseCode})");
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator CoGetTopRecords(int limit, Action<List<RecordEntry>> onComplete)
    {
        string url = $"{baseEndpoint}?select=player_name,clear_time_seconds,created_at&order=clear_time_seconds.asc&limit={limit}";

        using var req = UnityWebRequest.Get(url);
        req.timeout = 10;
        SetCommonHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SupabaseManager] GetTopRecords failed: {req.error}");
            onComplete?.Invoke(null);
            yield break;
        }

        string raw = req.downloadHandler.text;
        // JsonUtility는 배열 직접 파싱 불가 → 래핑
        string wrapped = "{\"items\":" + raw + "}";

        RecordEntryArray parsed = null;
        try
        {
            parsed = JsonUtility.FromJson<RecordEntryArray>(wrapped);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SupabaseManager] GetTopRecords JSON parse error: {e.Message}");
            onComplete?.Invoke(null);
            yield break;
        }

        if (parsed?.items == null)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        var list = new List<RecordEntry>(parsed.items);
        onComplete?.Invoke(list);
    }

    private IEnumerator CoGetAllRecords(Action<List<RecordEntry>> onComplete)
    {
        string url = $"{baseEndpoint}?select=player_name,clear_time_seconds,created_at&order=created_at.asc&limit=1000";

        using var req = UnityWebRequest.Get(url);
        req.timeout = 15;
        SetCommonHeaders(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SupabaseManager] GetAllRecords failed: {req.error}");
            onComplete?.Invoke(null);
            yield break;
        }

        string raw = req.downloadHandler.text;
        string wrapped = "{\"items\":" + raw + "}";

        RecordEntryArray parsed = null;
        try
        {
            parsed = JsonUtility.FromJson<RecordEntryArray>(wrapped);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SupabaseManager] GetAllRecords JSON parse error: {e.Message}");
            onComplete?.Invoke(null);
            yield break;
        }

        if (parsed?.items == null)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        var list = new List<RecordEntry>(parsed.items);
        onComplete?.Invoke(list);
    }

    private IEnumerator CoGetPlayerRank(float clearTimeSeconds, Action<int> onComplete)
    {
        // clear_time_seconds가 내 기록보다 엄격히 작은 행 수를 센다 → rank = count + 1
        string url = $"{baseEndpoint}?clear_time_seconds=lt.{clearTimeSeconds}&select=id";

        using var req = UnityWebRequest.Get(url);
        req.timeout = 10;
        SetCommonHeaders(req);
        req.SetRequestHeader("Prefer", "count=exact");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SupabaseManager] GetPlayerRank failed: {req.error}");
            onComplete?.Invoke(-1);
            yield break;
        }

        // content-range: 0-N/TOTAL  또는  */TOTAL  형태
        string contentRange = req.GetResponseHeader("content-range");
        int rank = ParseRankFromContentRange(contentRange);
        onComplete?.Invoke(rank);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private void SetCommonHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey", supabaseAnonKey);
        req.SetRequestHeader("Authorization", "Bearer " + supabaseAnonKey);
    }

    /// <summary>
    /// content-range 헤더에서 총 행 수 추출.
    /// "0-9/42" → count=42 → rank=43
    /// "*/0"   → count=0  → rank=1
    /// 파싱 실패 시 -1 반환.
    /// </summary>
    private int ParseRankFromContentRange(string contentRange)
    {
        if (string.IsNullOrEmpty(contentRange))
        {
            Debug.LogWarning("[SupabaseManager] content-range header missing.");
            return -1;
        }

        // "range/total" 또는 "*/total"
        int slashIdx = contentRange.IndexOf('/');
        if (slashIdx < 0 || slashIdx + 1 >= contentRange.Length)
        {
            Debug.LogWarning($"[SupabaseManager] Unexpected content-range format: {contentRange}");
            return -1;
        }

        string totalStr = contentRange.Substring(slashIdx + 1).Trim();
        if (int.TryParse(totalStr, out int total))
            return total + 1; // rank = 나보다 빠른 수 + 1

        Debug.LogWarning($"[SupabaseManager] Cannot parse total from content-range: {contentRange}");
        return -1;
    }

    /// <summary>JSON 문자열 내 따옴표/백슬래시 이스케이프</summary>
    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

// ------------------------------------------------------------------
// 데이터 클래스
// ------------------------------------------------------------------

[System.Serializable]
public class RecordEntry
{
    public string player_name;
    public float clear_time_seconds;
    public string created_at;
}

[System.Serializable]
public class RecordEntryArray
{
    public RecordEntry[] items;
}
