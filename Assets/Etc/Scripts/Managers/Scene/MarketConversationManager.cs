using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MarketConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC luke;   // 장부/행정 담당 루크
    public NPC lana;   // 장터/물자 담당 라나

    // 이 씬에서만 사용하는 임시 호감도 점수
    private int lukeScore = 0;
    private int lanaScore = 0;

    public enum MarketMoodType
    {
        Stable,   // 안정/비축 중심
        Lively,   // 활기/분위기 중심
        Balanced  // 균형형
    }

    public MarketMoodType marketMood = MarketMoodType.Balanced;

    /// <summary>
    /// 대화 흐름:
    /// Luke1  (광장과 정기 장터 상황, 외지 상인 증가 언급)
    /// → Lana1  (숫자보다 분위기를 중시하는 반박)
    /// → Luke2  (플레이어를 불러 세우고, 장터를 안정 vs 활기 구도로 설명 + 첫 질문)
    /// → WaitPlayer1 (플레이어 1차 발언, Luke 호감도 타겟)
    /// → Luke3  (플레이어 대답에 대한 Luke의 평가, 호감도 반영)
    /// → Lana2  (그 대답과 평가를 들은 Lana의 코멘트)
    /// → Luke4  (저장 곡식 비율(예: 70:30) 설명, 구체적인 조정 문제 제기)
    /// → Lana3  (그 비율로는 장터가 휑해 보일 수 있다는 지적 + 두 번째 질문)
    /// → WaitPlayer2 (플레이어 2차 발언, Lana 호감도 타겟)
    /// → Lana4  (플레이어 2차 대답에 대한 Lana의 평가, 호감도 반영)
    /// → Luke5  (최종 물자 계획을 정리하고 외지인의 의견을 인정)
    /// → Lana5  (장터에 꼭 오라고 초대하며, 마무리 한마디)
    /// → Done
    /// </summary>
    private enum Step
    {
        None,
        Luke1,
        Lana1,
        Luke2,
        WaitPlayer1,
        Luke3,
        Lana2,
        Luke4,
        Lana3,
        WaitPlayer2,
        Lana4,
        Luke5,
        Lana5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Scene7Market";

    private void Start()
    {
        // NPC 이벤트 연결
        if (luke != null) luke.OnReplied += OnNPCReplied;
        if (lana != null) lana.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게 처리
        if (luke != null) luke.acceptPlayerInput = false;
        if (lana != null) lana.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 화면 → 점점 밝게 (광장으로 장면 전환)
        yield return FadeFromBlack();

        StartConversation();
    }

    private void OnDestroy()
    {
        if (luke != null) luke.OnReplied -= OnNPCReplied;
        if (lana != null) lana.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Luke1;

        // Luke1: 광장 상황, 정기 장터, 외지 상인 증가 언급
        StartNpcTurn(
            luke,            
            "너는 이번 정기 장터에 쓸 물자를 정리하고 있다. " +
            "이번 장터에는 예전보다 외지 상인들이 더 많이 온다는 소식을 언급하고, " +
            "그래서 이번에는 물자 배분을 평소보다 훨씬 신중하게 해야 한다는 현실적인 이야기를 해라."
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
        if (npc == luke && currentStep == Step.Luke3)
        {
            lukeScore += res.affinity_change;
        }
        else if (npc == lana && currentStep == Step.Lana4)
        {
            lanaScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == luke)
        {
            if (lana != null)
            {
                lana.HearLine("Luke", res.message);
            }
        }
        else if (npc == lana)
        {
            if (luke != null)
            {
                luke.HearLine("Lana", res.message);
            }
        }
    }

    // 플레이어가 입력을 마쳤을 때
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: Luke 호감도 타겟, Lana도 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (lana != null)
            {
                lana.HearLine("Player", text);
            }

            // 플레이어의 첫 대답을 들은 뒤, Luke가 평가하는 턴
            currentStep = Step.Luke3;
            SetPendingNpcTurn(
                luke,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답에 대해 평가해라."
            );
        }
        // 2차 플레이어 답변: Lana 호감도 타겟, Luke도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (luke != null)
            {
                luke.HearLine("Player", text);
            }

            // 플레이어의 두 번째 대답을 들은 뒤, Lana가 평가하는 턴
            currentStep = Step.Lana4;
            SetPendingNpcTurn(
                lana,
                "방금 외지인이 이렇게 제안했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답에 대해 평가해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Luke1 → Lana1
            case Step.Luke1:
                currentStep = Step.Lana1;
                StartNpcTurn(
                    lana,                    
                    "루크가 또 숫자와 비축 이야기부터 시작하는 모습을 보며, " +
                    "광장이 너무 비어 보이면 장터를 기대하는 마음도 같이 줄어든다는 식으로, " +
                    "분위기와 활기를 중시하는 입장에서 부드럽게 반박해라."
                );
                break;

            // Lana1 → Luke2
            case Step.Lana1:
                currentStep = Step.Luke2;
                StartNpcTurn(
                    luke,
                    "라나의 대답에 반응하고, 식량과 저장 물자를 비축해야 한다는 너의 입장을 주장하며, 외지인에게 의견을 물어본다."
                );
                break;

            // Luke2 → WaitPlayer1 (1차 플레이어 발언)
            case Step.Luke2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;
                npcReplyReceived = false;

                // 1차 답변은 Luke 호감도에 직접 영향
                answerTargetNpc = luke;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Luke3(플레이어 1차 답변 평가) → Lana2
            case Step.Luke3:
                currentStep = Step.Lana2;
                StartNpcTurn(
                    lana,                    
                    "마을 사람들의 요즘 표정이 어둡다는 점을 다시 한 번 짚으면서, " +
                    "이번 장터만큼은 조금이라도 사람들에게 기대와 즐거움을 주고 싶다는 마음을 표현해라. "                    
                );
                break;

            // Lana2 → Luke4 (저장 비율과 구체적인 조정 문제 제기)
            case Step.Lana2:
                currentStep = Step.Luke4;
                StartNpcTurn(
                    luke,
                    "라나의 의견을 들은 뒤, 장부를 넘기며 저장 곡식과 물자의 대략적인 계획을 설명해라. " +
                    "예를 들어, 현재 계획으로는 저장 곡식의 70%를 비축하고 30%만 장터에 풀 생각이라는 식으로 말하고, " +
                    "이보다 더 풀면 다음 계절에 위험해질 수 있다는 너의 우려를 덧붙여라. "                    
                );
                break;

            // Luke4 → Lana3 (그 비율로는 장터가 휑해 보일 수 있다는 지적 + 2차 질문 준비)
            case Step.Luke4:
                currentStep = Step.Lana3;
                StartNpcTurn(
                    lana,
                    "루크의 계획을 듣고, 너의 입장을 말하며 외지인에게 조언을 구해라."
                );
                break;

            // Lana3 → WaitPlayer2 (2차 플레이어 발언)
            case Step.Lana3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;
                npcReplyReceived = false;

                // 2차 답변은 Lana 호감도에 직접 영향
                answerTargetNpc = lana;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Lana4(플레이어 2차 답변 평가) → Luke5
            case Step.Lana4:
                currentStep = Step.Luke5;
                StartNpcTurn(
                    luke,
                    "외지인과 라나의 대화를 듣고 이번 정기 장터에서 실제로 어떻게 물자를 풀지 최종 정리를 해라. "
                );
                break;

            // Luke5 → Lana5 (마무리 초대)
            case Step.Luke5:
                currentStep = Step.Lana5;
                StartNpcTurn(
                    lana,
                    "루크와 외지인의 결정을 들은 뒤, " +
                    "이번 장터에 대한 예상을 말해라."
                );
                break;

            // Lana5 → Done (씬 종료)
            case Step.Lana5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        // 장터 전체 이미지 플래그 결정
        if (lukeScore >= lanaScore + 2)
        {
            marketMood = MarketMoodType.Stable;
        }
        else if (lanaScore >= lukeScore + 2)
        {
            marketMood = MarketMoodType.Lively;
        }
        else
        {
            marketMood = MarketMoodType.Balanced;
        }

        Debug.Log("[Scene7Market] 장터 준비 대화 종료. " +
                  "LukeScore=" + lukeScore +
                  ", LanaScore=" + lanaScore +
                  ", MarketMood=" + marketMood);

        // 이후 씬 전환/플래그 저장을 위해 페이드 아웃
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();

        // TODO: 여기서 GameManager에 점수/플래그를 저장하고 다음 씬으로 이동
        // 예:
        // GameManager.Instance.SetAffinity("Luke", lukeScore);
        // GameManager.Instance.SetAffinity("Lana", lanaScore);
        // GameManager.Instance.SetMarketMood(marketMood);
        // SceneManager.LoadScene("Scene8_Market");
    }
}
