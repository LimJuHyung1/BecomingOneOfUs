using UnityEngine;
using UnityEngine.UI;

public class VillageSquareConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC elder;   // 촌장
    public NPC ivy;     // 여관 주인 아이비

    /// <summary>
    /// Elder1 → Ivy1 → Elder2 → WaitPlayer1 → Ivy2 → Elder3 → Ivy3 → WaitPlayer2 → Elder4 → Ivy4 → Done
    /// </summary>
    private enum Step
    {
        None,
        Elder1,
        Ivy1,
        Elder2,
        WaitPlayer1,
        Ivy2,
        Elder3,
        Ivy3,
        WaitPlayer2,
        Elder4,
        Ivy4,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "VillageSquare";

    private void Start()
    {
        if (elder != null) elder.OnReplied += OnNPCReplied;
        if (ivy != null) ivy.OnReplied += OnNPCReplied;

        if (elder != null) elder.acceptPlayerInput = false;
        if (ivy != null) ivy.acceptPlayerInput = false;

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
            "지금은 마을 광장(또는 작은 회관)에서 외지인인 플레이어를 처음 대면한 장면이다. " +
            "문지기들이 플레이어를 너에게 맡기고 물러난 직후 상황이다. " +
            "촌장으로서 손님을 맞이해라."
        );
    }

    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    // NPC가 말했을 때 서로에게 어떻게 들려줄지
    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        if (npc == elder)
        {
            // 촌장이 말하면 아이비에게 전달
            if (ivy != null) ivy.HearLine("Elder", res.message);
        }
        else if (npc == ivy)
        {
            // 아이비가 말하면 촌장에게 전달
            if (elder != null) elder.HearLine("Ivy", res.message);
        }
    }

    // 플레이어가 말했을 때(ConversationManagerBase가 공통 처리 후 여기로 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변 이후: 아이비의 첫인상 턴 대기
        if (currentStep == Step.WaitPlayer1)
        {
            currentStep = Step.Ivy2;

            SetPendingNpcTurn(
                ivy,
                "방금 플레이어가 다음과 같이 말했다: \"" + lastPlayerText + "\" " +
                "너는 여관 주인 아이비(Ivy)로서 플레이어를 평가해라."
            );
        }
        // 2차 플레이어 답변 이후: 촌장의 최종 결정 턴 대기
        else if (currentStep == Step.WaitPlayer2)
        {
            currentStep = Step.Elder4;

            SetPendingNpcTurn(
                elder,
                "플레이어가 다음과 같이 말했다: \"" + lastPlayerText + "\" " +
                "이 사람을 며칠 동안 이 마을에 임시로 머무르게 할지에 대한 최종 결정을 알려주는 한 문장을 말해라. "
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
                    "지금은 너가 운영하는 여관에서 촌장이 외지인인 플레이어를 맞이한 직후 상황이다. " +
                    "너는 이 마을 여관을 운영하는 아이비(Ivy)로서 말해라."
                );
                break;

            // Ivy1 → Elder2
            case Step.Ivy1:
                currentStep = Step.Elder2;
                StartNpcTurn(
                    elder,
                    "방금 아이비가 손님에게 인사를 건넸다. " +
                    "이번 턴에서는 마을에서 무엇을 하려는지 물어보아라."
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

            // Ivy2 → Elder3
            case Step.Ivy2:
                currentStep = Step.Elder3;
                StartNpcTurn(
                    elder,                    
                    "이번 턴에서는 촌장으로서 이 마을에서 지내게 한다면 무엇을 지켜보겠는지에 대한 생각을 담아 한 문장을 말해라."
                );
                break;

            // Elder3 → Ivy3
            case Step.Elder3:
                currentStep = Step.Ivy3;
                StartNpcTurn(
                    ivy,                    
                    "이번 턴에서는 여관 주인 아이비로서, 네 여관에서 지낼 손님에게 지켜줬으면 하는 기본적인 규칙을 알려줘라."
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

            // Elder4 직전: OnPlayerSpoke에서 pending으로 호출 → Elder4 턴 끝나면 Ivy4
            case Step.Elder4:
                currentStep = Step.Ivy4;
                StartNpcTurn(
                    ivy,
                    "촌장이 방금 플레이어에게 이 마을에 임시로 지낼지 안 지낼지 결단을 내렸다. " +
                    "여관에는 다른 인물도 살고 있다는 점을 언급해라."
                );
                break;

            // Ivy4 → Done
            case Step.Ivy4:
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
    }
}
