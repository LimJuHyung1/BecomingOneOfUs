using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별 결과(호/불호 2회 평가)를 저장하고,
/// 전체 씬(예: 10개) 완료 후 최다 outcome을 계산하는 전역 매니저.
/// DontDestroyOnLoad로 씬 전환 후에도 유지된다.
/// </summary>
public class SceneResultManager : MonoBehaviour
{
    // 전역 싱글톤 인스턴스
    public static SceneResultManager Instance { get; private set; }

    /// <summary>
    /// 각 씬 결과를 3가지 케이스로 분류
    /// Like2: 호 2번
    /// Mixed: 호 1번 / 불호 1번
    /// Dislike2: 불호 2번
    /// </summary>
    public enum SceneOutcome
    {
        Like2,
        Mixed,
        Dislike2
    }

    /// <summary>
    /// 씬별 저장 데이터
    /// sceneId: 씬 식별자(예: "Gate", "VillageSquare" 등)
    /// likeCount/dislikeCount: 0~2
    /// evalFirst/evalSecond: 각 평가 결과 문자열("호"/"불호")
    /// outcome: Like2/Mixed/Dislike2
    /// savedAt: 디버그용 저장 시각 문자열
    /// </summary>
    [Serializable]
    public class SceneResult
    {
        public string sceneId;
        public int likeCount;
        public int dislikeCount;
        public string evalFirst;
        public string evalSecond;
        public SceneOutcome outcome;
        public string savedAt;
    }

    // 씬 결과 저장소 (sceneId -> SceneResult)
    private readonly Dictionary<string, SceneResult> resultsByScene = new Dictionary<string, SceneResult>();

    // 게임 전체가 10개의 씬으로 구성되어 있다면 기본값 10
    [SerializeField] private int expectedSceneCount = 10;

    /// <summary>
    /// 씬 로드 전에 자동으로 매니저를 생성해서
    /// 어떤 씬에서든 Instance를 바로 쓸 수 있게 한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("SceneResultManager");
        Instance = go.AddComponent<SceneResultManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        // 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 모든 결과 초기화(새 게임 시작 시 호출)
    /// </summary>
    public void ResetAll()
    {
        resultsByScene.Clear();
    }

    /// <summary>
    /// 기대 씬 개수 설정(필요하면 외부에서 변경 가능)
    /// </summary>
    public void SetExpectedSceneCount(int count)
    {
        expectedSceneCount = Mathf.Max(1, count);
    }

    public int GetExpectedSceneCount()
    {
        return expectedSceneCount;
    }

    /// <summary>
    /// 씬 결과 저장.
    /// sceneId가 비어 있으면 현재 씬 이름을 사용.
    /// like/dislike는 0~2로 clamp.
    /// outcome은 like/dislike로 자동 계산.
    /// </summary>
    public void SetSceneResult(string sceneId, int likeCount, int dislikeCount, string evalFirst, string evalSecond)
    {
        if (string.IsNullOrWhiteSpace(sceneId))
        {
            sceneId = SceneManager.GetActiveScene().name;
        }

        likeCount = Mathf.Clamp(likeCount, 0, 2);
        dislikeCount = Mathf.Clamp(dislikeCount, 0, 2);

        SceneOutcome outcome = ComputeOutcome(likeCount, dislikeCount);

        SceneResult r = new SceneResult
        {
            sceneId = sceneId,
            likeCount = likeCount,
            dislikeCount = dislikeCount,
            evalFirst = evalFirst ?? "",
            evalSecond = evalSecond ?? "",
            outcome = outcome,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        resultsByScene[sceneId] = r;
    }

    /// <summary>
    /// 특정 씬 결과 가져오기
    /// </summary>
    public bool TryGetSceneResult(string sceneId, out SceneResult result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(sceneId))
            return false;

        return resultsByScene.TryGetValue(sceneId, out result);
    }

    /// <summary>
    /// 저장된 씬 결과 개수(완료 진행도 확인용)
    /// </summary>
    public int GetSavedSceneCount()
    {
        return resultsByScene.Count;
    }

    /// <summary>
    /// "모든 씬을 완료했는지"를 개수 기준으로 확인.
    /// 기본 expectedSceneCount(보통 10)와 비교한다.
    /// </summary>
    public bool HasAllSceneResults()
    {
        return resultsByScene.Count >= expectedSceneCount;
    }

    /// <summary>
    /// outcome 별 개수를 계산해서 리턴.
    /// </summary>
    public void GetOutcomeCounts(out int like2Count, out int mixedCount, out int dislike2Count)
    {
        like2Count = 0;
        mixedCount = 0;
        dislike2Count = 0;

        foreach (var kv in resultsByScene)
        {
            switch (kv.Value.outcome)
            {
                case SceneOutcome.Like2: like2Count++; break;
                case SceneOutcome.Mixed: mixedCount++; break;
                case SceneOutcome.Dislike2: dislike2Count++; break;
            }
        }
    }

    /// <summary>
    /// "최다 outcome"을 리턴.
    /// 동점이면 Like2 > Mixed > Dislike2 우선순위로 결정한다.
    /// 주의: 씬 완료 여부와 무관하게 현재 저장된 것만 보고 계산한다.
    /// </summary>
    public SceneOutcome GetMostCommonOutcome()
    {
        GetOutcomeCounts(out int like2, out int mixed, out int dislike2);

        if (like2 >= mixed && like2 >= dislike2) return SceneOutcome.Like2;
        if (mixed >= dislike2) return SceneOutcome.Mixed;
        return SceneOutcome.Dislike2;
    }

    /// <summary>
    /// 10개(또는 expectedSceneCount) 씬이 모두 끝난 뒤에만
    /// 최다 outcome을 "확정 결과"로 반환하고 싶을 때 사용하는 메서드.
    ///
    /// 반환값 true: 모든 씬 결과가 저장됨(>= expectedSceneCount) + majority 계산 완료
    /// 반환값 false: 아직 씬 결과가 부족함(majority 계산은 하지 않음)
    /// </summary>
    public bool TryGetFinalMajorityOutcome(out SceneOutcome majority,
                                          out int like2Count,
                                          out int mixedCount,
                                          out int dislike2Count)
    {
        majority = SceneOutcome.Mixed;
        GetOutcomeCounts(out like2Count, out mixedCount, out dislike2Count);

        if (!HasAllSceneResults())
        {
            // 아직 10개 씬을 다 돌지 않았으면 확정 결과를 내지 않는다.
            return false;
        }

        // 다 돌았으면 최다 outcome 확정
        majority = GetMostCommonOutcome();
        return true;
    }

    /// <summary>
    /// 디버그/엔딩 분기용으로 편한 요약 문자열 생성.
    /// 예: "Like2=4, Mixed=3, Dislike2=3, Majority=Like2"
    /// </summary>
    public string BuildFinalSummaryString()
    {
        GetOutcomeCounts(out int like2, out int mixed, out int dislike2);
        SceneOutcome majority = GetMostCommonOutcome();

        return "Like2=" + like2 + ", Mixed=" + mixed + ", Dislike2=" + dislike2 + ", Majority=" + majority;
    }

    /// <summary>
    /// 모든 씬의 호/불호 총합(원하면 엔딩 판정에 같이 활용 가능)
    /// </summary>
    public int GetTotalLikeCount()
    {
        int sum = 0;
        foreach (var kv in resultsByScene)
            sum += kv.Value.likeCount;
        return sum;
    }

    public int GetTotalDislikeCount()
    {
        int sum = 0;
        foreach (var kv in resultsByScene)
            sum += kv.Value.dislikeCount;
        return sum;
    }

    /// <summary>
    /// like/dislike로 outcome 계산
    /// 일반적으로 2턴이면 (2,0)/(1,1)/(0,2) 형태가 된다.
    /// </summary>
    private SceneOutcome ComputeOutcome(int likeCount, int dislikeCount)
    {
        if (likeCount >= 2) return SceneOutcome.Like2;
        if (likeCount == 1 && dislikeCount == 1) return SceneOutcome.Mixed;
        return SceneOutcome.Dislike2;
    }

    // 씬 10 - 추가하기
    /*
     if (SceneResultManager.Instance.TryGetFinalMajorityOutcome(out var majority, out var like2, out var mixed, out var dislike2))
{
    Debug.Log("Final Majority = " + majority);
    Debug.Log("Counts: Like2=" + like2 + ", Mixed=" + mixed + ", Dislike2=" + dislike2);
}
else
{
    Debug.Log("Not finished yet: " + SceneResultManager.Instance.GetSavedSceneCount() + "/" + SceneResultManager.Instance.GetExpectedSceneCount());
}
     */
}
