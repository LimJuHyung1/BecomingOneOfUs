using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FieldConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC lana;   // 농부 아주머니 라나
    public NPC taren;  // 라나의 아들 타렌

    // 이 씬에서만 사용하는 임시 호감도 점수
    private int lanaScore = 0;
    private int tarenScore = 0;

    /// <summary>
    /// 대화 흐름:
    /// Lana1  (밭에서 첫 대면, 일손 제안)
    /// → Taren1 (새 사람에게 일 떠넘기는 상황을 투덜거림)
    /// → Lana2  (밭일·마을에 대한 짧은 설명 + 플레이어에게 질문)
    /// → WaitPlayer1 (플레이어 1차 발언, 라나 호감도 타겟)
    /// → Lana3  (플레이어 대답에 대한 라나의 평가, 호감도 반영)
    /// → Taren2 (그 대답을 들은 타렌의 코멘트)
    /// → Lana4  (일을 어느 정도 마무리하며, 밭이 마을을 먹여 살린다는 밝은 면)
    /// → Taren3 (라나가 잠시 자리를 비운 사이, 마을이 답답하다는 속마음 고백 + 질문)
    /// → WaitPlayer2 (플레이어 2차 발언, 타렌 호감도 타겟)
    /// → Taren4 (플레이어 대답에 대한 타렌의 평가, 호감도 반영)
    /// → Lana5  (오늘 도와준 것에 대한 감사, 플레이어를 하나의 손으로 인정)
    /// → Taren5 (플레이어를 향한 짧은 한마디/속마음)
    /// → Done
    /// </summary>
    private enum Step
    {
        None,
        Lana1,
        Taren1,
        Lana2,
        WaitPlayer1,
        Lana3,
        Taren2,
        Lana4,
        Taren3,
        WaitPlayer2,
        Taren4,
        Lana5,
        Taren5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Field";

    private void Start()
    {
        // NPC 이벤트 연결
        if (lana != null) lana.OnReplied += OnNPCReplied;
        if (taren != null) taren.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게 처리
        if (lana != null) lana.acceptPlayerInput = false;
        if (taren != null) taren.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 화면 → 점점 밝게 (밭으로 장면 전환)
        yield return FadeFromBlack();  // ConversationManagerBase 제공

        StartConversation();
    }

    private void OnDestroy()
    {
        if (lana != null) lana.OnReplied -= OnNPCReplied;
        if (taren != null) taren.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Lana1;

        // Lana1: 아침, 밭에서 플레이어와 첫 대면. 일손을 제안하고, 손을 써볼 줄 아는지 시험하는 질문.
        StartNpcTurn(
            lana,
            "지금은 아침, 마을 외곽의 작은 밭이다. " +
            "외지인에게 인사를 건네라."
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
        if (npc == lana && currentStep == Step.Lana3)
        {
            lanaScore += res.affinity_change;
        }
        else if (npc == taren && currentStep == Step.Taren4)
        {
            tarenScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == lana)
        {
            if (taren != null)
            {
                taren.HearLine("Lana", res.message);
            }
        }
        else if (npc == taren)
        {
            if (lana != null)
            {
                lana.HearLine("Taren", res.message);
            }
        }
    }

    // 플레이어가 말했을 때(ConversationManagerBase 공통 처리 후 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: 라나 호감도 타겟, 타렌도 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (taren != null)
            {
                taren.HearLine("Player", text);
            }

            // 플레이어 대답 후 → 라나가 그 대답을 평가하는 턴
            currentStep = Step.Lana3;
            SetPendingNpcTurn(
                lana,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
        // 2차 플레이어 답변: 타렌 호감도 타겟, 라나도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (lana != null)
            {
                lana.HearLine("Player", text);
            }

            // 플레이어 대답 후 → 타렌이 그 대답을 평가하는 턴
            currentStep = Step.Taren4;
            SetPendingNpcTurn(
                taren,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
    }

    // 각 NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Lana1 → Taren1
            case Step.Lana1:
                currentStep = Step.Taren1;
                StartNpcTurn(
                    taren,
                    "라나의 발언에 불만을 토해라."
                );
                break;

            // Taren1 → Lana2
            case Step.Taren1:
                currentStep = Step.Lana2;
                StartNpcTurn(
                    lana,
                    "타렌에 말을 받아치며 외지인이 노동에 대해 어떻게 생각하는지 질문해라. "
                );
                break;

            // Lana2 → WaitPlayer1 (라나 호감도 결정 구간)
            case Step.Lana2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 답변은 라나 호감도에 직접 영향
                answerTargetNpc = lana;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Lana3(플레이어 1차 답변 평가) → Taren2
            case Step.Lana3:
                currentStep = Step.Taren2;
                StartNpcTurn(
                    taren,                    
                    "마을이 답답하고 마을 밖으로 나가고 싶어하는 모습을 드러내라."
                );
                break;

            // Taren2 → Lana4
            case Step.Taren2:
                currentStep = Step.Lana4;
                StartNpcTurn(
                    lana,
                    "타렌에 말에 반박해라."
                );
                break;

            // Lana4 → Taren3
            case Step.Lana4:
                currentStep = Step.Taren3;
                StartNpcTurn(
                    taren,
                    "외지인에게 자신의 답답함을 호소해라."
                );
                break;

            // Taren3 → WaitPlayer2 (타렌 호감도 결정 구간)
            case Step.Taren3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 답변은 타렌 호감도에 직접 영향
                answerTargetNpc = taren;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Taren4(플레이어 2차 답변 평가) → Lana5
            case Step.Taren4:
                currentStep = Step.Lana5;
                StartNpcTurn(
                    lana,
                    "외지인에게 타렌에 말에 진지하게 답해줘서 고맙다고 말해라."
                );
                break;

            // Lana5 → Taren5
            case Step.Lana5:
                currentStep = Step.Taren5;
                StartNpcTurn(
                    taren,
                    "자신의 미래에 대해 꿈꾸며 대화를 마무리해라."
                );
                break;

            // Taren5 → Done
            case Step.Taren5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[Field] 씬4 대화 종료. LanaScore=" + lanaScore + ", TarenScore=" + tarenScore);

        // TODO: 여기서 다음 씬 전환, 플래그 저장 등 처리
        // 예: GameManager에 lanaScore, tarenScore 전달

        // 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();  // ConversationManagerBase 제공

        // TODO: 페이드가 끝난 뒤에 씬 전환 또는 플레이어 이동 처리
        // 예: SceneManager.LoadScene("NextSceneName");
    }
}
