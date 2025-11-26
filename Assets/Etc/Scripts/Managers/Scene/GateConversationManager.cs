using UnityEngine;
using UnityEngine.UI;

public class GateConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC brown;
    public NPC toma;

    private enum Step
    {
        None,
        Brown1,
        Toma1,
        Brown2,
        WaitPlayer1,
        Toma2,
        Brown3,
        Toma3,
        WaitPlayer2,
        Brown4,
        Toma4,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Gate";

    private void Start()
    {
        // NPC 이벤트 연결
        if (brown != null) brown.OnReplied += OnNPCReplied;
        if (toma != null) toma.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게
        if (brown != null) brown.acceptPlayerInput = false;
        if (toma != null) toma.acceptPlayerInput = false;

        StartConversation();
    }

    private void OnDestroy()
    {
        if (brown != null) brown.OnReplied -= OnNPCReplied;
        if (toma != null) toma.OnReplied -= OnNPCReplied;
    }

    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Brown1;

        // Brown1: 플레이어를 처음 보고 시비 거는 느낌
        StartNpcTurn(
            brown,
            "지금은 성문 앞에서 외지인인 플레이어를 처음 마주한 상황이다. " +
            "이번 턴에서는 이 낯선 사람에게 까칠하게 말을 걸며, 왜 여기 왔는지 묻는 짧은 한 문장을 말해라."
        );
    }

    // 대화가 진행 중인지 여부
    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    // NPC가 말했을 때, 다른 NPC에게 어떻게 들려줄지
    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        if (npc == brown)
        {
            // 브라운이 말하면 토마는 듣지만, 브라운은 토마 말을 듣지 않는다.
            if (toma != null) toma.HearLine("Brown", res.message);
        }
        else if (npc == toma)
        {
            // 토마가 말한 내용은 브라운에게는 전달하지 않는다.
            // 필요하다면 여기서 브라운에게도 전달하도록 바꿀 수 있다.
        }
    }

    // 플레이어가 한 번 말했을 때, Step에 따라 다음 턴 설정
    protected override void OnPlayerSpoke(string text)
    {
        if (currentStep == Step.WaitPlayer1)
        {
            // 첫 번째 플레이어 대답 이후 → Toma2 턴을 pending으로
            currentStep = Step.Toma2;

            SetPendingNpcTurn(
                toma,
                "방금 플레이어가 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "너는 브라운 옆에 서 있는 보조 문지기로서, 이 대답을 들은 뒤의 솔직한 인상을 한 문장으로 말해라."
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            // 두 번째 플레이어 대답 이후 → Brown4 턴을 pending으로
            currentStep = Step.Brown4;

            SetPendingNpcTurn(
                brown,
                "방금 플레이어가 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "너는 성문을 지키는 메인 문지기로서, 이 사람을 마을에 들여보낼지 말지 거의 결정을 내린 상태다. " +
                "이번 턴에서는 최종적인 태도나 판단이 느껴지는 한 문장을 말해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            case Step.Brown1:
                currentStep = Step.Toma1;
                StartNpcTurn(
                    toma,
                    "방금 브라운이 외지인인 플레이어에게 날카롭게 말을 걸었다. " +
                    "너는 겁이 많아 이 상황이 조금 불안하다. " +
                    "플레이어를 걱정하거나, 브라운 눈치를 보게 되는 짧은 한 문장을 말해라."
                );
                break;

            case Step.Toma1:
                currentStep = Step.Brown2;
                StartNpcTurn(
                    brown,
                    "방금 토마가 긴장한 기색으로 한마디 했다. " +
                    "너는 여전히 플레이어를 의심하고 있다. " +
                    "이번 턴에서는 플레이어에게 조금 더 구체적으로 목적이나 정체를 캐묻는 한 문장을 말해라."
                );
                break;

            case Step.Brown2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 이 대답은 직전에 질문을 던진 브라운의 호감도에만 직접 영향을 줌
                answerTargetNpc = brown;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Toma2:
                currentStep = Step.Brown3;
                StartNpcTurn(
                    brown,
                    "지금까지의 플레이어 대답과 토마의 반응을 모두 들었다. " +
                    "너는 여전히 의심과 경계심을 버리지는 못했지만, 어느 정도 판단이 서기 시작했다. " +
                    "이번 턴에서는 플레이어를 시험하는 듯한 한 문장을 말해라."
                );
                break;

            case Step.Brown3:
                currentStep = Step.Toma3;
                StartNpcTurn(
                    toma,
                    "방금 브라운이 플레이어를 시험하는 말을 했다. " +
                    "너는 브라운과 플레이어 사이에서 눈치를 보며, 조심스럽게 분위기를 살피는 한 문장을 말해라."
                );
                break;

            case Step.Toma3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 두 번째 대답은 바로 앞에서 플레이어를 바라보던 토마의 호감도에 직접 영향
                answerTargetNpc = toma;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Brown4:
                currentStep = Step.Toma4;
                StartNpcTurn(
                    toma,
                    "브라운이 플레이어에 대한 최종적인 태도를 드러냈다. " +
                    "너는 이 분위기 속에서, 플레이어가 너무 상처받지 않기를 바라거나 " +
                    "조심스럽게 한마디를 덧붙이는 짧은 문장을 말해라."
                );
                break;

            case Step.Toma4:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[Gate] 게이트 인트로 대화 종료. 다음 씬으로 진행하세요.");
        // TODO: 씬 전환, 플레이어 이동 등
    }
}
