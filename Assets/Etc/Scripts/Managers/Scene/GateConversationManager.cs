using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        Brown3,
        Toma2,
        Brown4,
        Toma3,
        WaitPlayer2,
        Toma4,
        Brown5,
        Toma5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Gate";

    private void Start()
    {
        if (brown != null) brown.OnReplied += OnNPCReplied;
        if (toma != null) toma.OnReplied += OnNPCReplied;

        if (brown != null) brown.acceptPlayerInput = false;
        if (toma != null) toma.acceptPlayerInput = false;

        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        yield return FadeFromBlack();
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

        StartNpcTurn(
            brown,
            "지금은 마을 입구 앞에서 외지인인 플레이어를 처음 마주한 상황이다. " +
            "이 낯선 사람을 멈춰세워라."
        );
    }

    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        // 평가 캡처는 ConversationManagerBase가 자동 처리한다.

        // 상대 NPC에게 들려주기
        if (npc == brown)
        {
            if (toma != null) toma.HearLine("Brown", res.message);
        }
        else if (npc == toma)
        {
            if (brown != null) brown.HearLine("Toma", res.message);
        }
    }

    protected override void OnPlayerSpoke(string text)
    {
        if (currentStep == Step.WaitPlayer1)
        {
            // 다른 NPC도 듣게만 한다 (평가 대상은 brown)
            if (toma != null) toma.HearLine("Player", text);

            currentStep = Step.Brown3;

            // slot 0: 첫 번째 평가
            SetPendingNpcEvaluationTurn(
                brown,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라. " +
                "반드시 JSON의 affinity는 \"호\" 또는 \"불호\" 중 하나로 출력해라.",
                0
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            // 다른 NPC도 듣게만 한다 (평가 대상은 toma)
            if (brown != null) brown.HearLine("Player", text);

            currentStep = Step.Toma4;

            // slot 1: 두 번째 평가
            SetPendingNpcEvaluationTurn(
                toma,
                "방금 외지인님이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답을 평가해라. " +
                "반드시 JSON의 affinity는 \"호\" 또는 \"불호\" 중 하나로 출력해라.",
                1
            );
        }
    }

    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            case Step.Brown1:
                currentStep = Step.Toma1;
                StartNpcTurn(toma, "외지인을 멈춰세워라.");
                break;

            case Step.Toma1:
                currentStep = Step.Brown2;
                StartNpcTurn(brown, "외지인의 신원을 파악해라.");
                break;

            case Step.Brown2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;
                answerTargetNpc = brown;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Brown3:
                currentStep = Step.Toma2;
                StartNpcTurn(toma, "외지인에 대한 인상을 말해라.");
                break;

            case Step.Toma2:
                currentStep = Step.Brown4;
                StartNpcTurn(brown, "지금까지의 대화를 정리하고 토마에게 질문해라");
                break;

            case Step.Brown4:
                currentStep = Step.Toma3;
                StartNpcTurn(toma, "토마의 질문에 답하며, 외지인에게 너의 마을에 어떻게 왔는지 질문해라.");
                break;

            case Step.Toma3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;
                answerTargetNpc = toma;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Toma4:
                currentStep = Step.Brown5;
                StartNpcTurn(brown, "지금까지의 모든 대화 기록을 참고해서, 외지인을 마을에 들일 수 있도록 해라.");
                break;

            case Step.Brown5:
                currentStep = Step.Toma5;
                StartNpcTurn(toma,
                    "지금까지의 모든 대화 기록을 참고해서, 외지인이 너의 마을에 들일 수 있도록 해라." +
                    "너의 마을의 촌장이 맞이할 것임을 외지인에게 언급해라."
                );
                break;

            case Step.Toma5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        // Base에 누적된 (호/불호) 결과를 SceneResultManager로 저장
        SaveSceneResult("Gate");

        string summary = GetSceneOutcomeString();
        string brownEval = sceneAffinities[0];
        string tomaEval = sceneAffinities[1];

        Debug.Log("[Gate] 씬1 결과: Brown=" + brownEval + ", Toma=" + tomaEval + " => " + summary);

        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        yield return FadeToBlack();
        SceneManager.LoadScene("Scene2");        
    }
}
