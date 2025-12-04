using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VillageSquareConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC elder;   // 촌장
    public NPC ivy;     // 여관 주인 아이비

    // 이 씬에서만 쓰는 임시 호감도 점수
    private int elderScore = 0;
    private int ivyScore = 0;

    /// <summary>
    /// Elder1 → Ivy1 → Elder2 → WaitPlayer1 → Elder3 → Ivy2 → Elder4 → Ivy3 → WaitPlayer2 → Ivy4 → Elder5 → Ivy5 → Done
    /// </summary>
    private enum Step
    {
        None,
        Elder1,
        Ivy1,
        Elder2,
        WaitPlayer1,
        Elder3,
        Ivy2,
        Elder4,
        Ivy3,
        WaitPlayer2,
        Ivy4,
        Elder5,
        Ivy5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "VillageSquare";

    void Start()
    {
        if (elder != null) elder.OnReplied += OnNPCReplied;
        if (ivy != null) ivy.OnReplied += OnNPCReplied;

        if (elder != null) elder.acceptPlayerInput = false;
        if (ivy != null) ivy.acceptPlayerInput = false;

        // 씬 시작 시: 검은 화면 → 페이드 아웃 → 그 뒤 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 스크린에서 점점 투명하게 (Fade From Black)
        yield return FadeFromBlack();   // ConversationManagerBase에서 제공

        // 페이드가 완전히 끝난 뒤에만 대사 시작
        StartConversation();
    }


    private void OnDestroy()
    {
        if (elder != null) elder.OnReplied -= OnNPCReplied;
        if (ivy != null) ivy.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Elder1;

        StartNpcTurn(
            elder,
            "지금은 마을 여관에서 외지인인 플레이어를 처음 대면한 장면이다. " +            
            "촌장으로서 외지인을 맞이해라."
        );
    }

    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    // NPC가 말했을 때 서로에게 어떻게 들려줄지
    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        // 1) 플레이어의 말에 대한 반응일 때만 점수 누적
        if (speakerName == "Player")
        {
            if (npc == elder)
                elderScore += res.affinity_change;
            else if (npc == ivy)
                ivyScore += res.affinity_change;
        }

        // 2) 기존 로직: 서로에게 들려주기
        if (npc == elder)
        {
            if (ivy != null) ivy.HearLine("Elder", res.message);
        }
        else if (npc == ivy)
        {
            if (elder != null) elder.HearLine("Ivy", res.message);
        }
    }


    // 플레이어가 말했을 때(ConversationManagerBase가 공통 처리 후 여기로 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: Elder가 호감도 타겟이지만, Ivy도 플레이어 말을 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (ivy != null)
            {
                ivy.HearLine("Player", text);
            }

            // 플레이어 대답 후 → 촌장이 그 대답에 대한 반응을 하는 턴
            currentStep = Step.Elder3;
            SetPendingNpcTurn(
                elder,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
        // 2차 플레이어 답변: Ivy가 호감도 타겟이지만, Elder도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (elder != null)
            {
                elder.HearLine("Player", text);
            }

            // 플레이어 대답 후 → 아이비가 그 대답에 대한 반응을 하는 턴
            currentStep = Step.Ivy4;
            SetPendingNpcTurn(
                ivy,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답을 평가해라."
            );
        }
    }

    // 각 NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Elder1 → Ivy1
            case Step.Elder1:
                currentStep = Step.Ivy1;
                StartNpcTurn(
                    ivy,
                    "촌장의 말에 맞장구를 치고 너의 소개를 해라."
                );
                break;

            // Ivy1 → Elder2
            case Step.Ivy1:
                currentStep = Step.Elder2;
                StartNpcTurn(
                    elder,
                    "아이비의 대답에 이어 외지인의 인성을 파악하는 질문을 해라."
                );
                break;

            // Elder2 → WaitPlayer1 (1차 플레이어 발언, 촌장 호감도에만 영향)
            case Step.Elder2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 답변은 촌장 호감도만 반영
                answerTargetNpc = elder;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Elder3 → Ivy2
            case Step.Elder3:
                currentStep = Step.Ivy2;
                StartNpcTurn(
                    ivy,
                    "엘더의 대답을 평가하고, 외지인에 대한 인상을 말해라."
                );
                break;

            // Ivy2 → Elder4
            case Step.Ivy2:
                currentStep = Step.Elder4;
                StartNpcTurn(
                    elder,
                    "촌장으로서 외지인이 당분간 이 마을에 머무를 수 있는지에 대해 이야기하고, " +
                    "여관 주인 아이비에게 이 손님을 맡기겠다는 취지의 말을 건네라. "
                );
                break;

            // Elder4 → Ivy3 (아이비가 자기 기준으로 질문하는 턴)
            case Step.Elder4:
                currentStep = Step.Ivy3;
                StartNpcTurn(
                    ivy,                    
                    "플레이어에게 궁금한 것이 있는지 물어봐라."
                );
                break;

            // Ivy3 → WaitPlayer2 (2차 플레이어 발언, 아이비 호감도에만 영향)
            case Step.Ivy3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 답변은 아이비 호감도만 반영
                answerTargetNpc = ivy;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Ivy4(플레이어 2차 답변에 대한 아이비 반응) → Elder5
            case Step.Ivy4:
                currentStep = Step.Elder5;
                StartNpcTurn(
                    elder,
                    "지금까지의 모든 대화 기록을 참고해서, " +
                    "촌장으로서 이 사람이 이 마을에 머무를 수 있도록 하여라. "                    
                );
                break;

            // Elder5 → Ivy5
            case Step.Elder5:
                currentStep = Step.Ivy5;
                StartNpcTurn(
                    ivy,                    
                    "여관 주인 아이비로서 외지인에게 방과 여관 생활에 대한 안내를 건네고, " +
                    "여관에는 다른 손님도 있다는 점을 언급해라."
                );
                break;

            // Ivy5 → Done
            case Step.Ivy5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[VillageSquare] 임시 입촌 심사 대화 종료. 다음 씬으로 진행하세요.");
        // TODO: 씬 전환, 플레이어를 여관 방으로 이동 등

        // 대화가 모두 끝난 뒤: 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();   // ConversationManagerBase에서 제공

        // 페이드가 끝난 뒤에 씬 전환, 플레이어 이동 등 진행
        // 예: SceneManager.LoadScene("NextScene");
    }
}
