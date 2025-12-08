using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InnIvyGardConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC ivy;   // 여관 주인
    public NPC gard;  // 싸움꾼 손님

    // 간단한 호감도 기록 (나중에 GameManager로 넘겨도 됨)
    private int ivyScore = 0;
    private int gardScore = 0;

    /// <summary>
    /// Ivy1 → Gard1 → Ivy2 → WaitPlayer1 → Ivy3 → Gard2 → Ivy4 → Gard3 → WaitPlayer2 → Gard4 → Ivy5 → Gard5 → Done
    /// </summary>
    private enum Step
    {
        None,
        Ivy1,
        Gard1,
        Ivy2,
        WaitPlayer1,
        Ivy3,
        Gard2,
        Ivy4,
        Gard3,
        WaitPlayer2,
        Gard4,
        Ivy5,
        Gard5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "InnIvyGard";

    private void Start()
    {
        // NPC 이벤트 연결
        if (ivy != null) ivy.OnReplied += OnNPCReplied;
        if (gard != null) gard.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게
        if (ivy != null) ivy.acceptPlayerInput = false;
        if (gard != null) gard.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 화면 → 점점 밝아짐
        yield return FadeFromBlack();   // ConversationManagerBase 제공

        StartConversation();
    }

    private void OnDestroy()
    {
        if (ivy != null) ivy.OnReplied -= OnNPCReplied;
        if (gard != null) gard.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Ivy1;

        // Ivy1: 오늘도 일 안 하고 눕는 가르드를 보고 투덜대는 첫 멘트
        StartNpcTurn(
            ivy,            
            "너는 장부를 보여주며 가르드에게 여관 방값에 대한 문제를 제기해라."
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
        if (npc == ivy && currentStep == Step.Ivy3)
        {
            ivyScore += res.affinity_change;
        }
        else if (npc == gard && currentStep == Step.Gard4)
        {
            gardScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == ivy)
        {
            if (gard != null)
            {
                gard.HearLine("Ivy", res.message);
            }
        }
        else if (npc == gard)
        {
            if (ivy != null)
            {
                ivy.HearLine("Gard", res.message);
            }
        }
    }

    // 플레이어가 말했을 때(ConversationManagerBase 공통 처리 후 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: Ivy 호감도 타겟, Gard도 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (gard != null)
            {
                gard.HearLine("Player", text);
            }

            // 플레이어 대답 후 → Ivy가 그 대답을 평가하는 턴
            currentStep = Step.Ivy3;
            SetPendingNpcTurn(
                ivy,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 해결책에 대해 평가해라."
            );
        }
        // 2차 플레이어 답변: Gard 호감도 타겟, Ivy도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (ivy != null)
            {
                ivy.HearLine("Player", text);
            }

            // 플레이어 대답 후 → Gard가 그 대답을 평가하는 턴
            currentStep = Step.Gard4;
            SetPendingNpcTurn(
                gard,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 조언에 대해 평가해라."
            );
        }
    }

    // 각 NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Ivy1 → Gard1
            case Step.Ivy1:
                currentStep = Step.Gard1;
                StartNpcTurn(
                    gard,
                    "아이비의 불만을 듣고 핑계를 대라."
                );
                break;

            // Gard1 → Ivy2
            case Step.Gard1:
                currentStep = Step.Ivy2;
                StartNpcTurn(
                    ivy,
                    "가르드의 변명을 들은 뒤, 옆에서 이 상황을 보고 있는 외지인에게 어떻게 하면 좋을지 해결책을 물어라."
                );
                break;

            // Ivy2 → WaitPlayer1 (1차 플레이어 발언, Ivy 호감도 결정 구간)
            case Step.Ivy2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 답변은 Ivy 호감도만 반영
                answerTargetNpc = ivy;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Ivy3 → Gard2
            case Step.Ivy3:
                currentStep = Step.Gard2;
                StartNpcTurn(
                    gard,
                    "아이비에게 좀 더 시간을 달라고 부탁해라."
                );
                break;

            // Gard2 → Ivy4
            case Step.Gard2:
                currentStep = Step.Ivy4;
                StartNpcTurn(
                    ivy,
                    "가르드에게 더 이상은 못 참는다며 밀린 방값을 주지 않는 이상 마을 재판에 소환할 것임을 알려라."
                );
                break;

            // Ivy4 → Gard3
            case Step.Ivy4:
                currentStep = Step.Gard3;
                StartNpcTurn(
                    gard,
                    "아이비의 말을 듣고 외지인에게 어떻게 하면 좋을 지 조언을 구해라."
                );
                break;

            // Gard3 → WaitPlayer2 (2차 플레이어 발언, Gard 호감도 결정 구간)
            case Step.Gard3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 답변은 Gard 호감도에 직접 영향
                answerTargetNpc = gard;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Gard4 → Ivy5
            case Step.Gard4:
                currentStep = Step.Ivy5;
                StartNpcTurn(
                    ivy,
                    "외지인과 가르드의 대화를 듣고 재판을 열지 안 열지 여부를 판단하라."
                );
                break;

            // Ivy5 → Gard5
            case Step.Ivy5:
                currentStep = Step.Gard5;
                StartNpcTurn(
                    gard,
                    "아이비가 재판을 열기로 결정한다면 외지인에게 자신을 변호해 달라는 요청을 해라. " +
                    "재판이 열리지 않는다면 아이비에게 미안함과 감사함을 전해라."
                );
                break;

            // Gard5 → Done (씬 종료)
            case Step.Gard5:
                currentStep = Step.Done;
                // 필요하면 여기서 GameManager에 ivyScore, gardScore 저장 후 다음 씬 로드
                StartCoroutine(FadeToBlack());
                break;
        }
    }
}
