using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class ConversationManagerBase : MonoBehaviour
{
    [Header("UI")]
    public InputField playerInput;
    public Image screen;
    [SerializeField] private float fadeDuration = 3f;

    // 공통 상태
    protected NPC expectedNpc;
    protected bool npcReplyReceived;

    protected bool waitingForPlayerInput;
    protected string lastPlayerText;

    protected bool pendingNpcTurn;
    protected NPC pendingNpc;
    protected string pendingPrompt;

    // 이번 플레이어 발언이 직접 영향을 줄 NPC (대답을 HearLine으로 받는 대상)
    protected NPC answerTargetNpc;

    // 디버그용 로그
    protected readonly List<string> conversationLog = new List<string>();

    // 로그 태그 (자식 클래스에서 정의)
    protected abstract string LogTag { get; }

    // ---------------------------------------------------------------------
    // Scene 결과(호/불호) 추적 공통 기능
    // - evaluation slot 0: 첫 번째 플레이어 발언 평가
    // - evaluation slot 1: 두 번째 플레이어 발언 평가
    // ---------------------------------------------------------------------
    protected int sceneLikeCount;
    protected int sceneDislikeCount;
    protected readonly string[] sceneAffinities = new string[2]; // "호"/"불호"

    private bool currentTurnIsEvaluation;
    private int currentEvaluationSlot = -1;

    private bool pendingTurnIsEvaluation;
    private int pendingEvaluationSlot = -1;

    protected virtual void Awake()
    {
        if (playerInput != null)
        {
            playerInput.onEndEdit.AddListener(OnPlayerInputEnd);
            playerInput.gameObject.SetActive(false);
        }
    }

    protected virtual void OnDestroy()
    {
        if (playerInput != null)
        {
            playerInput.onEndEdit.RemoveListener(OnPlayerInputEnd);
        }
    }

    protected void ResetConversationState()
    {
        expectedNpc = null;
        npcReplyReceived = false;

        waitingForPlayerInput = false;
        lastPlayerText = string.Empty;

        pendingNpcTurn = false;
        pendingNpc = null;
        pendingPrompt = string.Empty;

        answerTargetNpc = null;

        conversationLog.Clear();

        ResetSceneOutcomeTracking();
    }

    private void ResetSceneOutcomeTracking()
    {
        sceneLikeCount = 0;
        sceneDislikeCount = 0;
        sceneAffinities[0] = string.Empty;
        sceneAffinities[1] = string.Empty;

        currentTurnIsEvaluation = false;
        currentEvaluationSlot = -1;

        pendingTurnIsEvaluation = false;
        pendingEvaluationSlot = -1;
    }

    // ---------------------------------------------------------------------
    // NPC 턴 시작 (공통)
    // ---------------------------------------------------------------------
    protected void StartNpcTurn(NPC who, string promptInstruction)
    {
        StartNpcTurnInternal(who, promptInstruction, false, -1);
    }

    // 평가 턴을 "즉시" 시작하고 싶을 때 사용 (필요한 씬만)
    protected void StartNpcEvaluationTurn(NPC who, string promptInstruction, int evaluationSlot)
    {
        StartNpcTurnInternal(who, promptInstruction, true, ClampEvaluationSlot(evaluationSlot));
    }

    private void StartNpcTurnInternal(NPC who, string promptInstruction, bool isEvaluation, int evaluationSlot)
    {
        expectedNpc = who;
        npcReplyReceived = false;
        waitingForPlayerInput = false;

        currentTurnIsEvaluation = isEvaluation;
        currentEvaluationSlot = isEvaluation ? evaluationSlot : -1;

        if (who != null && !string.IsNullOrWhiteSpace(promptInstruction))
        {
            who.AskByScript(promptInstruction);
        }
    }

    // ---------------------------------------------------------------------
    // pending 설정 (공통)
    // ---------------------------------------------------------------------
    protected void SetPendingNpcTurn(NPC who, string prompt)
    {
        pendingNpcTurn = true;
        pendingNpc = who;
        pendingPrompt = prompt;

        // 일반 pending은 평가 플래그 제거
        pendingTurnIsEvaluation = false;
        pendingEvaluationSlot = -1;
    }

    // 플레이어 발언 "평가 턴"을 pending으로 등록할 때 사용
    // evaluationSlot: 0(첫 평가), 1(둘째 평가)
    protected void SetPendingNpcEvaluationTurn(NPC who, string prompt, int evaluationSlot)
    {
        pendingNpcTurn = true;
        pendingNpc = who;
        pendingPrompt = prompt;

        pendingTurnIsEvaluation = true;
        pendingEvaluationSlot = ClampEvaluationSlot(evaluationSlot);
    }

    private int ClampEvaluationSlot(int slot)
    {
        return Mathf.Clamp(slot, 0, sceneAffinities.Length - 1);
    }

    // ---------------------------------------------------------------------
    // 플레이어 입력 활성/비활성
    // ---------------------------------------------------------------------
    protected void EnablePlayerInput(bool enable)
    {
        if (playerInput == null)
            return;

        playerInput.gameObject.SetActive(enable);
        playerInput.interactable = enable;

        if (enable)
        {
            playerInput.text = string.Empty;
            playerInput.ActivateInputField();
        }
    }

    // ---------------------------------------------------------------------
    // 플레이어 입력 종료 처리 (공통)
    // ---------------------------------------------------------------------
    protected virtual void OnPlayerInputEnd(string text)
    {
        if (!waitingForPlayerInput)
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

        string trimmed = text.Trim();
        lastPlayerText = trimmed;

        // UI에 플레이어 대사 출력
        if (LineManager.Instance != null)
        {
            LineManager.Instance.ShowPlayerLine(
                "Player",
                Color.white,
                trimmed
            );
        }

        // 이번 턴의 대상 NPC에게만 직접 들려줌
        if (answerTargetNpc != null)
        {
            answerTargetNpc.HearLine("Player", trimmed);
        }

        if (playerInput != null)
        {
            playerInput.text = string.Empty;
            playerInput.gameObject.SetActive(false);
            playerInput.interactable = false;
        }

        waitingForPlayerInput = false;

        // 자식 클래스에게 알림
        OnPlayerSpoke(trimmed);
    }

    // ---------------------------------------------------------------------
    // Space 키 처리 (공통)
    // ---------------------------------------------------------------------
    protected virtual void Update()
    {
        if (LineManager.Instance == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 1) 텍스트 타이핑 중이거나 큐에 남아 있으면 텍스트 먼저 처리
            if (LineManager.Instance.IsTyping || LineManager.Instance.HasQueuedLines)
            {
                LineManager.Instance.ShowNextLine();
                return;
            }

            // 2) pending NPC 턴 시작
            if (pendingNpcTurn && !waitingForPlayerInput && pendingNpc != null)
            {
                bool isEval = pendingTurnIsEvaluation;
                int evalSlot = pendingEvaluationSlot;

                pendingNpcTurn = false;
                pendingTurnIsEvaluation = false;
                pendingEvaluationSlot = -1;

                StartNpcTurnInternal(pendingNpc, pendingPrompt, isEval, evalSlot);
                return;
            }

            // 3) NPC 턴이 끝났고, 플레이어 입력 대기가 아니라면 다음 Step
            if (npcReplyReceived && !waitingForPlayerInput)
            {
                npcReplyReceived = false;
                OnNpcTurnFinished();
            }
        }
    }

    // ---------------------------------------------------------------------
    // NPC.OnReplied 이벤트에서 직접 연결할 메서드 (공통)
    // ---------------------------------------------------------------------
    protected void OnNPCReplied(NPC npc, NPCResponse res)
    {
        if (npc == null || res == null)
            return;

        if (!IsConversationActive())
            return;

        if (expectedNpc != null && npc != expectedNpc)
            return;

        string speakerName = string.IsNullOrEmpty(npc.displayName) ? npc.name : npc.displayName;
        conversationLog.Add(speakerName + ": " + res.message);

        Debug.Log("[" + LogTag + "] " + speakerName + " replied: " + res.message);

        // 자식 클래스가 HearLine 분배 등 추가 처리
        OnNpcHeard(npc, res, speakerName);

        // 평가 턴이면 호/불호 결과를 공통으로 캡처
        TryCaptureEvaluationAffinity(res);

        npcReplyReceived = true;
    }

    private void TryCaptureEvaluationAffinity(NPCResponse res)
    {
        if (!currentTurnIsEvaluation)
            return;

        if (currentEvaluationSlot < 0 || currentEvaluationSlot >= sceneAffinities.Length)
        {
            currentTurnIsEvaluation = false;
            currentEvaluationSlot = -1;
            return;
        }

        // 이미 해당 slot에 값이 있으면 중복 카운트 방지
        if (!string.IsNullOrEmpty(sceneAffinities[currentEvaluationSlot]))
        {
            currentTurnIsEvaluation = false;
            currentEvaluationSlot = -1;
            return;
        }

        string affinity = NormalizeAffinityText(res.affinity);
        sceneAffinities[currentEvaluationSlot] = affinity;

        if (affinity == "호") sceneLikeCount++;
        else sceneDislikeCount++;

        Debug.Log("[" + LogTag + "] Evaluation#" + (currentEvaluationSlot + 1) + ": " + affinity);

        // 한 번 캡처했으면 평가 턴 종료
        currentTurnIsEvaluation = false;
        currentEvaluationSlot = -1;
    }

    private static string NormalizeAffinityText(string affinity)
    {
        if (string.IsNullOrWhiteSpace(affinity))
            return "불호";

        string a = affinity.Trim();

        if (string.Equals(a, "호", StringComparison.OrdinalIgnoreCase)) return "호";
        if (string.Equals(a, "불호", StringComparison.OrdinalIgnoreCase)) return "불호";

        // 혹시 영어로 섞여 나오면 방어
        if (string.Equals(a, "like", StringComparison.OrdinalIgnoreCase)) return "호";
        if (string.Equals(a, "dislike", StringComparison.OrdinalIgnoreCase)) return "불호";

        return "불호";
    }

    // 씬 결과 요약 문자열 (디버그/엔딩 분기용)
    protected string GetSceneOutcomeString()
    {
        if (sceneLikeCount >= 2) return "호 2번";
        if (sceneLikeCount == 1 && sceneDislikeCount == 1) return "호 1번 / 불호 1번";
        return "불호 2번";
    }

    // 씬 결과 저장 (SceneResultManager로 넘김)
    protected void SaveSceneResult(string sceneId)
    {
        if (SceneResultManager.Instance == null)
        {
            Debug.LogWarning("[" + LogTag + "] SceneResultManager.Instance is null. 결과 저장을 건너뜁니다.");
            return;
        }

        SceneResultManager.Instance.SetSceneResult(
            sceneId,
            sceneLikeCount,
            sceneDislikeCount,
            sceneAffinities[0],
            sceneAffinities[1]
        );
    }

    // ------------------------------------------------------
    // 공통 페이드 코루틴
    // ------------------------------------------------------
    protected IEnumerator FadeFromBlack()
    {
        if (screen != null && FadeUtility.Instance != null)
        {
            screen.gameObject.SetActive(true);

            FadeUtility.Instance.FadeOut(screen, fadeDuration);

            if (LineManager.Instance != null)
            {
                LineManager.Instance.OpenPanelIfNeeded(fadeDuration);
            }

            yield return new WaitForSeconds(fadeDuration);
        }
        else
        {
            yield return null;
        }
    }

    protected IEnumerator FadeToBlack()
    {
        if (screen != null && FadeUtility.Instance != null)
        {
            screen.gameObject.SetActive(true);
            FadeUtility.Instance.FadeIn(screen, fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }
        else
        {
            yield return null;
        }
    }

    // 자식 클래스에서 구현해야 하는 부분들
    protected abstract bool IsConversationActive();
    protected abstract void OnNpcHeard(NPC npc, NPCResponse res, string speakerName);
    protected abstract void OnPlayerSpoke(string text);
    protected abstract void OnNpcTurnFinished();
}
