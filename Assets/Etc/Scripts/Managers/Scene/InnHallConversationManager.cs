using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InnHallConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC gard;   // 맞고 온 싸움꾼
    public NPC luke;   // 행정 서기

    // 간단한 호감도 기록(필요하면 GameManager 등으로 넘겨서 저장)
    private int gardScore = 0;
    private int lukeScore = 0;

    /// <summary>
    /// Gard1 → Luke1 → Gard2 → WaitPlayer1 → Gard3 → Luke2 → Gard4 → Luke3 → WaitPlayer2 → Luke4 → Gard5 → Luke5 → Done
    /// </summary>
    private enum Step
    {
        None,
        Gard1,
        Luke1,
        Gard2,
        WaitPlayer1,
        Gard3,
        Luke2,
        Gard4,
        Luke3,
        WaitPlayer2,
        Luke4,
        Gard5,
        Luke5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "InnHall";

    private void Start()
    {
        // NPC 이벤트 연결
        if (gard != null) gard.OnReplied += OnNPCReplied;
        if (luke != null) luke.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게
        if (gard != null) gard.acceptPlayerInput = false;
        if (luke != null) luke.acceptPlayerInput = false;

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
        if (gard != null) gard.OnReplied -= OnNPCReplied;
        if (luke != null) luke.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Gard1;

        // Gard1: 서류 비꼬는 첫 멘트
        StartNpcTurn(
            gard,
            "지금은 밤, 아이비 여관 1층 홀이다. 가르드는 방금 싸움에서 맞고 와 의자에 앉아 있고, " +
            "루크는 근처 테이블에서 싸움에 대한 보고서와 벌금 서류를 정리하고 있다. " +
            "루크에게 비꼬는 말투로 말해라."
        );
    }

    // 대화가 진행 중인지 여부
    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    // NPC가 말했을 때: 호감도 기록 + 다른 NPC에게 들려주기
    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        // 1) 플레이어 답변에 대한 반응 구간에서만 affinity 누적
        if (npc == gard && currentStep == Step.Gard3)
        {
            gardScore += res.affinity_change;
        }
        else if (npc == luke && currentStep == Step.Luke4)
        {
            lukeScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == gard)
        {
            if (luke != null) luke.HearLine("Gard", res.message);
        }
        else if (npc == luke)
        {
            if (gard != null) gard.HearLine("Luke", res.message);
        }
    }

    // 플레이어가 말했을 때 (ConversationManagerBase → OnPlayerInputEnd → 여기 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // Base 쪽에서 answerTargetNpc에게는 이미 HearLine("Player", text)가 호출된 상태.
        // 여기서는 "다른 NPC"에게도 같은 발언을 들려주고, 다음 Step을 예약한다.

        if (currentStep == Step.WaitPlayer1)
        {
            // 첫 번째 답변은 가르드 호감도 타겟 → 루크도 이 말을 듣는다.
            if (luke != null)
            {
                luke.HearLine("Player", text);
            }

            // Gard3에서 가르드가 외지인에 대한 평가 발언
            currentStep = Step.Gard3;

            SetPendingNpcTurn(
                gard,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            // 두 번째 답변은 루크 호감도 타겟 → 가르드도 이 말을 듣는다.
            if (gard != null)
            {
                gard.HearLine("Player", text);
            }

            // Luke4에서 루크가 외지인에 대한 평가 발언
            currentStep = Step.Luke4;

            SetPendingNpcTurn(
                luke,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환 (Gate / VillageSquare와 같은 패턴)
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Gard1 → Luke1
            case Step.Gard1:
                currentStep = Step.Luke1;
                StartNpcTurn(
                    luke,
                    "방금 가르드가 불만을 터뜨렸다. " +
                    "루크로서 차분하게 설명해라."
                );
                break;

            // Luke1 → Gard2
            case Step.Luke1:
                currentStep = Step.Gard2;
                StartNpcTurn(
                    gard,
                    "외지인에게 어떻게 생각하냐고 물어봐라."
                );
                break;

            // Gard2 → WaitPlayer1 (가르드 호감도 결정 구간)
            case Step.Gard2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 이 대답은 가르드 호감도에 직접 영향
                answerTargetNpc = gard;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Gard3 → Luke2
            case Step.Gard3:
                currentStep = Step.Luke2;
                StartNpcTurn(
                    luke,
                    "외지인에게 자신을 소개하고 가르드에 대해서도 소개해라."
                );
                break;

            // Luke2 → Gard4
            case Step.Luke2:
                currentStep = Step.Gard4;
                StartNpcTurn(
                    gard,
                    "루크의 소개에 감사하다고 하고, 자신의 소외감을 드러내라."                    
                );
                break;

            // Gard4 → Luke3
            case Step.Gard4:
                currentStep = Step.Luke3;
                StartNpcTurn(
                    luke,                    
                    "외지인에게 이 상황을 어떻게 보는지 질문해라."
                );
                break;

            // Luke3 → WaitPlayer2 (루크 호감도 결정 구간)
            case Step.Luke3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 이 대답은 루크 호감도에 직접 영향
                answerTargetNpc = luke;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Luke4 → Gard5 (플레이어 2차 답변에 대한 루크 반응 이후)
            case Step.Luke4:
                currentStep = Step.Gard5;
                StartNpcTurn(
                    gard,
                    "마을의 어두운 면, 불량한 사람들이 있다는 것을 언급해라."
                );
                break;

            // Gard5 → Luke5
            case Step.Gard5:
                currentStep = Step.Luke5;
                StartNpcTurn(
                    luke,
                    "가르드의 대답에 맞장구치고 외지인에게 이에 대해 주의시켜라."
                );
                break;

            // Luke5 → Done
            case Step.Luke5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[InnHall] 씬3 대화 종료. GardScore=" + gardScore + ", LukeScore=" + lukeScore);

        // 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();   // ConversationManagerBase에서 제공

        // TODO: 여기서 다음 씬 전환, 플래그 저장 등 처리
        // 예: SceneManager.LoadScene("NextScene");
    }
}
