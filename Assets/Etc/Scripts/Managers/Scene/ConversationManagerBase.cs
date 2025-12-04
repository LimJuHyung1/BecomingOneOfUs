using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class ConversationManagerBase : MonoBehaviour
{
    [Header("UI")]
    public InputField playerInput;
    public Image screen;      // 전체 화면을 덮는 검은 Image
    private float fadeDuration = 3f;     // 기본 3초

    // 공통 상태
    protected NPC expectedNpc;
    protected bool npcReplyReceived;

    protected bool waitingForPlayerInput;
    protected string lastPlayerText;

    protected bool pendingNpcTurn;
    protected NPC pendingNpc;
    protected string pendingPrompt;

    // 이번 플레이어 발언이 직접 영향을 줄 NPC (호감도 타겟)
    protected NPC answerTargetNpc;

    // 디버그용 로그
    protected readonly List<string> conversationLog = new List<string>();

    // 로그 태그 (자식 클래스에서 정의)
    protected abstract string LogTag { get; }

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
    }

    // 공통 NPC 턴 시작
    protected void StartNpcTurn(NPC who, string promptInstruction)
    {
        expectedNpc = who;
        npcReplyReceived = false;
        waitingForPlayerInput = false;

        if (who != null && !string.IsNullOrWhiteSpace(promptInstruction))
        {
            who.AskByScript(promptInstruction);
        }
    }

    // 공통 pending 설정
    protected void SetPendingNpcTurn(NPC who, string prompt)
    {
        pendingNpcTurn = true;
        pendingNpc = who;
        pendingPrompt = prompt;
    }

    // 플레이어 입력 활성/비활성
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

    // 플레이어 입력 종료 처리 (공통)
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

    // Space 키 처리 (공통)
    protected virtual void Update()
    {
        if (LineManager.Instance == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 1) 아직 텍스트가 타이핑 중이거나, 큐에 남아 있으면 → 텍스트 먼저 처리
            if (LineManager.Instance.IsTyping || LineManager.Instance.HasQueuedLines)
            {
                LineManager.Instance.ShowNextLine();
                return;
            }

            // 2) 플레이어 발언 후, 대기 중인 NPC 턴이 있다면 이 시점에서 시작
            if (pendingNpcTurn && !waitingForPlayerInput && pendingNpc != null)
            {
                pendingNpcTurn = false;
                StartNpcTurn(pendingNpc, pendingPrompt);
                return;
            }

            // 3) NPC 턴이 끝났고, 플레이어 입력 대기가 아니라면 → 다음 Step
            if (npcReplyReceived && !waitingForPlayerInput)
            {
                npcReplyReceived = false;
                OnNpcTurnFinished();
            }
        }
    }

    // NPC.OnReplied 이벤트에서 직접 연결할 메서드 (공통)
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

        npcReplyReceived = true;
    }

    // ------------------------------------------------------
    // 공통 페이드 코루틴: 검은 화면에서 밝게 / 밝은 화면에서 검게
    // ------------------------------------------------------
    protected IEnumerator FadeFromBlack()
    {
        // 검은 화면(알파 1)에서 투명(알파 0)으로
        if (screen != null && FadeUtility.Instance != null)
        {
            screen.gameObject.SetActive(true);

            // 1) screen 페이드 아웃 시작
            FadeUtility.Instance.FadeOut(screen, fadeDuration);

            // 2) 같은 타이밍에 대사 슬라이드도 페이드 인 시작
            if (LineManager.Instance != null)
            {
                LineManager.Instance.OpenPanelIfNeeded(fadeDuration);
            }

            // 3) 둘 다 duration 동안 진행
            yield return new WaitForSeconds(fadeDuration);
        }
        else
        {
            // 참조가 없으면 한 프레임만 대기
            yield return null;
        }
    }

    protected IEnumerator FadeToBlack()
    {
        // 투명(알파 0)에서 검은 화면(알파 1)으로
        if (screen != null && FadeUtility.Instance != null)
        {
            screen.gameObject.SetActive(true);
            // 초기 알파는 FadeUtility에서 0으로 맞춰줌
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
