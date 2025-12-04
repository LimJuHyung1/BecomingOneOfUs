using System.Collections;
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
        // NPC 이벤트 연결
        if (brown != null) brown.OnReplied += OnNPCReplied;
        if (toma != null) toma.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게
        if (brown != null) brown.acceptPlayerInput = false;
        if (toma != null) toma.acceptPlayerInput = false;

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
        if (brown != null) brown.OnReplied -= OnNPCReplied;
        if (toma != null) toma.OnReplied -= OnNPCReplied;
    }

    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Brown1;

        // Brown1: 플레이어를 처음 보고 까칠하게 시비 거는 느낌
        StartNpcTurn(
            brown,
            "지금은 마을 입구 앞에서 외지인인 플레이어를 처음 마주한 상황이다. " +            
            "이 낯선 사람을 멈춰세워라."
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
            // 브라운이 말하면 토마는 항상 듣는다.
            if (toma != null)
            {
                toma.HearLine("Brown", res.message);
            }
        }
        else if (npc == toma)
        {
            // 토마가 말하면 브라운도 듣는다.
            if (brown != null)
            {
                brown.HearLine("Toma", res.message);
            }
        }
    }

    // 플레이어가 한 번 말했을 때, Step에 따라 다음 턴 설정
    protected override void OnPlayerSpoke(string text)
    {
        // ConversationManagerBase에서 answerTargetNpc 쪽에는 이미 HearLine("Player", ...)가 들어간 상태.
        // 여기서는 "다른 NPC"에게도 플레이어 발언을 들려준다.

        if (currentStep == Step.WaitPlayer1)
        {
            // 첫 번째 플레이어 대답은 브라운에게 직접 들어갔으므로,
            // 토마에게도 플레이어 발언을 들려준다.
            if (toma != null)
            {
                toma.HearLine("Player", text);
            }

            // 첫 번째 플레이어 대답 이후 → Brown3 턴을 pending으로
            currentStep = Step.Brown3;

            SetPendingNpcTurn(
                brown,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인에 대해 평가해라."
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            // 두 번째 플레이어 대답은 토마에게 직접 들어갔으므로,
            // 브라운에게도 플레이어 발언을 들려준다.
            if (brown != null)
            {
                brown.HearLine("Player", text);
            }

            // 두 번째 플레이어 대답 이후 → Toma4 턴을 pending으로
            currentStep = Step.Toma4;

            SetPendingNpcTurn(
                toma,
                "방금 외지인님이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답을 평가해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            case Step.Brown1:
                // 브라운 첫 시비 후 → 토마 첫 반응
                currentStep = Step.Toma1;
                StartNpcTurn(
                    toma,
                    "외지인을 멈춰세워라."
                );
                break;

            case Step.Toma1:
                // 토마의 첫 반응 후 → 브라운이 좀 더 구체적으로 캐묻는 질문(이상형 테스트 포함 가능)
                currentStep = Step.Brown2;
                StartNpcTurn(
                    brown,
                    "외지인의 신원을 파악해라."
                );
                break;

            case Step.Brown2:
                // 브라운의 질문 뒤 → 플레이어 1차 대답 타이밍 (브라운 호감도 결정 구간)
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 이 대답은 직전에 질문을 던진 브라운의 호감도에 직접 영향을 줌
                answerTargetNpc = brown;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Brown3:
                // 브라운이 플레이어 1차 대답에 대한 반응을 한 뒤 → 토마의 코멘트
                currentStep = Step.Toma2;
                StartNpcTurn(
                    toma,                    
                    "외지인에 대한 인상을 말해라."
                );
                break;

            case Step.Toma2:
                // 토마의 코멘트 후 → 브라운이 한 번 더 정리하면서 토마에게도 한마디 시키는 흐름
                currentStep = Step.Brown4;
                StartNpcTurn(
                    brown,
                    "지금까지의 대화를 정리하고 토마에게 질문해라"
                );
                break;

            case Step.Brown4:
                // 브라운이 토마에게도 물어보라 한 뒤 → 토마가 직접 외지인에게 질문 (토마 이상형 테스트용)
                currentStep = Step.Toma3;
                StartNpcTurn(
                    toma,
                    "토마의 질문에 답하며, 외지인에게 너의 마을에 어떻게 왔는지 질문해라." 
                );
                break;

            case Step.Toma3:
                // 토마의 질문 뒤 → 플레이어 2차 대답 타이밍 (토마 호감도 결정 구간)
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 이 대답은 바로 앞에서 질문을 던진 토마의 호감도에 직접 영향
                answerTargetNpc = toma;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Toma4:
                // 토마가 플레이어 2차 대답에 대한 반응을 한 뒤 → 브라운이 마지막 정리 멘트
                currentStep = Step.Brown5;
                StartNpcTurn(
                    brown,
                    "지금까지의 모든 대화 기록을 참고해서, 외지인을 마을에 들일 수 있도록 해라."                    
                );
                break;

            case Step.Brown5:
                // 브라운의 최종 멘트 뒤 → 토마가 마지막으로 한마디
                currentStep = Step.Toma5;
                StartNpcTurn(
                    toma,
                    "지금까지의 모든 대화 기록을 참고해서, 외지인이 너의 마을에 들일 수 있도록 해라." +
                    "너의 마을의 촌장이 맞이할 것임을 외지인에게 언급해라."
                );
                break;

            case Step.Toma5:
                // 토마 마지막 멘트 뒤 → 대화 종료
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
