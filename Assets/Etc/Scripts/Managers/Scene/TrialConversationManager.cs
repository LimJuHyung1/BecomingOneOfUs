using System.Collections;
using UnityEngine;

public class TrialConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC luke;    // 서기, 검사 역할
    public NPC elder;   // 촌장, 재판장    
    public NPC gard;    // 피고

    // 이 씬에서만 사용하는 임시 호감도 점수
    private int elderScore = 0;
    private int lukeScore = 0;
    private int gardScore = 0;

    /// <summary>
    /// 대략 흐름:
    /// Elder1(오프닝, 재판 설명)
    /// → Luke1(공소 제기)
    /// → Gard1(첫 반응)
    /// → Elder2(1차 질문: 플레이어 기본 입장 질문)
    /// → WaitPlayer1(플레이어 1차 답변, 엘더 호감도 타겟)
    /// → Elder3(플레이어 1차 답변에 대한 엘더의 평가)
    /// → Luke2(2차 질문: 규칙/기회에 대한 검사식 질문)
    /// → WaitPlayer2(플레이어 2차 답변, 루크 호감도 타겟)
    /// → Luke3(플레이어 2차 답변에 대한 루크의 평가)
    /// → Gard2(3차 질문: 가르드 자신에 대한 플레이어의 시선 질문)
    /// → WaitPlayer3(플레이어 3차 답변, 가르드 호감도 타겟)
    /// → Gard3(플레이어 3차 답변에 대한 가르드의 평가)
    /// → Elder4(최종 판결 선언)
    /// → Gard4(판결을 들은 가르드의 심정/다짐)
    /// → Done
    /// </summary>
    private enum Step
    {
        None,
        Elder1,
        Luke1,
        Gard1,
        Elder2,
        WaitPlayer1,
        Elder3,
        Luke2,
        WaitPlayer2,
        Luke3,
        Gard2,
        WaitPlayer3,
        Gard3,
        Elder4,
        Gard4,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Trial";

    private void Start()
    {
        // NPC 이벤트 연결
        if (elder != null) elder.OnReplied += OnNPCReplied;
        if (luke != null) luke.OnReplied += OnNPCReplied;
        if (gard != null) gard.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게 처리
        if (elder != null) elder.acceptPlayerInput = false;
        if (luke != null) luke.acceptPlayerInput = false;
        if (gard != null) gard.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 화면 → 점점 밝게 (임시 재판소 방)
        yield return FadeFromBlack();   // ConversationManagerBase 제공

        StartConversation();
    }

    private void OnDestroy()
    {
        if (elder != null) elder.OnReplied -= OnNPCReplied;
        if (luke != null) luke.OnReplied -= OnNPCReplied;
        if (gard != null) gard.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Elder1;

        // Elder1: 재판 오프닝, 사건 개요, 역할 설명
        StartNpcTurn(
            elder,
            "지금은 마을에서 재판이 열린 사용하고 있는 상황이다. " +
            "촌장으로서 오늘 재판이 가르드의 여관 방값 문제를 다루는 자리임을 설명하고, " +
            "외지인에게 가르드의 변호인 역할을 충실히 행할 수 있도록 주의시켜라."
        );
    }

    // 이 씬에서 대화가 진행 중인지 여부
    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    // NPC가 말했을 때: 호감도 기록 + 서로에게 들려주기
    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        // 1) 플레이어 대답에 대한 평가 턴에서만 호감도 누적
        if (npc == elder && currentStep == Step.Elder3)
        {
            elderScore += res.affinity_change;
        }
        else if (npc == luke && currentStep == Step.Luke3)
        {
            lukeScore += res.affinity_change;
        }
        else if (npc == gard && currentStep == Step.Gard3)
        {
            gardScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == elder)
        {
            if (luke != null) luke.HearLine("Elder", res.message);
            if (gard != null) gard.HearLine("Elder", res.message);
        }
        else if (npc == luke)
        {
            if (elder != null) elder.HearLine("Luke", res.message);
            if (gard != null) gard.HearLine("Luke", res.message);
        }
        else if (npc == gard)
        {
            if (elder != null) elder.HearLine("Gard", res.message);
            if (luke != null) luke.HearLine("Gard", res.message);
        }
    }

    // 플레이어가 말했을 때(ConversationManagerBase 공통 처리 후 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: 엘더의 질문에 대한 답변 (엘더 호감도 타겟)
        if (currentStep == Step.WaitPlayer1)
        {
            // 엘더 외에도 루크, 가르드가 플레이어 말을 듣게 함
            if (luke != null) luke.HearLine("Player", text);
            if (gard != null) gard.HearLine("Player", text);

            // 플레이어 대답 후 → 엘더가 그 대답을 평가하는 턴
            currentStep = Step.Elder3;
            SetPendingNpcTurn(
                elder,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답에 대해 평가해라."
            );
        }
        // 2차 플레이어 답변: 루크의 질문에 대한 답변 (루크 호감도 타겟)
        else if (currentStep == Step.WaitPlayer2)
        {
            // 루크 외에도 엘더, 가르드가 플레이어 말을 듣게 함
            if (elder != null) elder.HearLine("Player", text);
            if (gard != null) gard.HearLine("Player", text);

            // 플레이어 대답 후 → 루크가 그 대답을 평가하는 턴
            currentStep = Step.Luke3;
            SetPendingNpcTurn(
                luke,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 주장에 대해 평가해라."
            );
        }
        // 3차 플레이어 답변: 가르드의 질문에 대한 답변 (가르드 호감도 타겟)
        else if (currentStep == Step.WaitPlayer3)
        {
            // 가르드 외에도 엘더, 루크가 플레이어 말을 듣게 함
            if (elder != null) elder.HearLine("Player", text);
            if (luke != null) luke.HearLine("Player", text);

            // 플레이어 대답 후 → 가르드가 그 대답을 평가하는 턴
            currentStep = Step.Gard3;
            SetPendingNpcTurn(
                gard,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대답에 대해 자신의 솔직한 감정을 말해라."
            );
        }
    }

    // 각 NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Elder1 → Luke1 (공소 제기)
            case Step.Elder1:
                currentStep = Step.Luke1;
                StartNpcTurn(
                    luke,
                    "여관 주인인 아이비가 제출한 장부와 기록을 토대로, " +
                    "가르드가 여관 방값과 술값 등을 여러 달 동안 지불하지 않은 상황을 설명해라."
                );
                break;

            // Luke1 → Gard1 (첫 반응)
            case Step.Luke1:
                currentStep = Step.Gard1;
                StartNpcTurn(
                    gard,
                    "자신의 미안함을 표현해라."
                );
                break;

            // Gard1 → Elder2 (1차 질문: 기본 입장)
            case Step.Gard1:
                currentStep = Step.Elder2;
                StartNpcTurn(
                    elder,
                    "가르드와 루크의 말을 모두 들은 뒤, " +
                    "외지인에게 가르드가 어떻게 하면 좋을지 구체적인 해결안을 물어라."
                );
                break;

            // Elder2 → WaitPlayer1 (플레이어 1차 답변, 엘더 질문에 대한 답)
            case Step.Elder2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 답변은 엘더 호감도에 직접 영향
                answerTargetNpc = elder;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Elder3(1차 답변 평가) → Luke2(2차 질문)
            case Step.Elder3:
                currentStep = Step.Luke2;
                StartNpcTurn(
                    luke,                    
                    "가르드는 마을에서 추방해야 한다는 주장을 해라." +
                    "그리고 외지인에게 자신의 주장에 대해 어떻게 생각하는지 물어라."
                );
                break;

            // Luke2 → WaitPlayer2 (플레이어 2차 답변, 루크 질문에 대한 답)
            case Step.Luke2:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 답변은 루크 호감도에 직접 영향
                answerTargetNpc = luke;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Luke3(2차 답변 평가) → Gard2(3차 질문)
            case Step.Luke3:
                currentStep = Step.Gard2;
                StartNpcTurn(
                    gard,
                    "외지인에게 솔직하게 묻고 싶은 말을 해라."
                );
                break;

            // Gard2 → WaitPlayer3 (플레이어 3차 답변, 가르드 질문에 대한 답)
            case Step.Gard2:
                currentStep = Step.WaitPlayer3;
                expectedNpc = null;

                // 3차 답변은 가르드 호감도에 직접 영향
                answerTargetNpc = gard;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Gard3(3차 답변 평가) → Elder4(최종 판결)
            case Step.Gard3:
                currentStep = Step.Elder4;
                StartNpcTurn(
                    elder,
                    "지금까지의 모든 대화를 떠올리고, 가르드를 마을에서 추방할지의 대한 여부를 판결해라."
                );
                break;

            case Step.Elder4:
                currentStep = Step.Gard4;
                StartNpcTurn(
                    gard,
                    "촌장의 판결을 들은 직후의 심정을 말해라. "
                );
                break;

            // Gard4 → Done
            case Step.Gard4:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[Trial] 씬6 재판 대화 종료. " +
                  "ElderScore=" + elderScore +
                  ", LukeScore=" + lukeScore +
                  ", GardScore=" + gardScore);

        // 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();   // ConversationManagerBase 제공

        // TODO: 여기서 elderScore / lukeScore / gardScore 를 GameManager 등에 넘겨서
        //       이후 분기(가르드 루트, 아이비 루트, 마을 규칙 루트 등)를 나누면 됨.
        // 예: SceneManager.LoadScene("NextScene");
    }
}
