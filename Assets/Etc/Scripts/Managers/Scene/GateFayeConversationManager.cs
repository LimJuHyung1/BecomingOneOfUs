using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GateFayeConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC brown;   // 기존 문지기
    public NPC faye;    // 새 떠돌이 음유시인

    // 간단한 호감도 기록 (나중에 GameManager로 넘겨도 됨)
    private int brownScore = 0;
    private int fayeScore = 0;

    /// <summary>
    /// Brown1 → Faye1 → Brown2 → WaitPlayer1 → Brown3 → Faye2 → Brown4 → Faye3 → WaitPlayer2 → Faye4 → Brown5 → Faye5 → Done
    /// </summary>
    private enum Step
    {
        None,
        Brown1,
        Faye1,
        Brown2,
        WaitPlayer1,
        Brown3,
        Faye2,
        Brown4,
        Faye3,
        WaitPlayer2,
        Faye4,
        Brown5,
        Faye5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "GateFaye";

    private void Start()
    {
        // NPC 이벤트 연결
        if (brown != null) brown.OnReplied += OnNPCReplied;
        if (faye != null) faye.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게
        if (brown != null) brown.acceptPlayerInput = false;
        if (faye != null) faye.acceptPlayerInput = false;

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
        if (brown != null) brown.OnReplied -= OnNPCReplied;
        if (faye != null) faye.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Brown1;

        // Brown1: 성문 위/옆에서 멀리 오는 페이를 보며 투덜거림
        StartNpcTurn(
            brown,            
            "멀리서 보이는 음유시인에 대해 짜증을 표해라."
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
        // 1) 플레이어 발언에 대한 평가 턴에서만 호감도 누적
        if (npc == brown && currentStep == Step.Brown3)
        {
            brownScore += res.affinity_change;
        }
        else if (npc == faye && currentStep == Step.Faye4)
        {
            fayeScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == brown)
        {
            if (faye != null)
            {
                faye.HearLine("Brown", res.message);
            }
        }
        else if (npc == faye)
        {
            if (brown != null)
            {
                brown.HearLine("Faye", res.message);
            }
        }
    }

    // 플레이어가 말했을 때 (ConversationManagerBase → OnPlayerInputEnd → 여기 호출)
    protected override void OnPlayerSpoke(string text)
    {
        // Base 쪽에서 answerTargetNpc에게는 이미 HearLine("Player", text)가 호출된 상태.
        // 여기서는 "다른 NPC"에게도 같은 발언을 들려주고, 다음 Step을 예약한다.

        // 1차 플레이어 발언: 브라운 호감도 타겟, 페이도 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (faye != null)
            {
                faye.HearLine("Player", text);
            }

            // 플레이어의 첫 인상과 말투를 들은 뒤, 브라운이 외지인에 대한 평가를 내리는 턴
            currentStep = Step.Brown3;
            SetPendingNpcTurn(
                brown,
                "방금 외지인이 저 떠돌이에 대해 이렇게 말했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답에 대해 평가해라."
            );
        }
        // 2차 플레이어 발언: 페이 호감도 타겟, 브라운도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (brown != null)
            {
                brown.HearLine("Player", text);
            }

            // 플레이어의 최종 판단을 들은 뒤, 페이가 그 결정에 대한 반응을 보이는 턴
            currentStep = Step.Faye4;
            SetPendingNpcTurn(
                faye,
                "방금 외지인이 다음과 같이 말했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답에 대해 평가해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환 (Field / Gate / Inn / VillageSquare와 같은 패턴)
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Brown1 → Faye1
            case Step.Brown1:
                currentStep = Step.Faye1;
                StartNpcTurn(
                    faye,
                    "자기 소개를 하며, 이 마을 여관에서 노래를 부르며 머물고 싶다는 뜻을 밝혀라."
                );
                break;

            // Faye1 → Brown2
            case Step.Faye1:
                currentStep = Step.Brown2;
                StartNpcTurn(
                    brown,
                    "방금 들은 페이의 소개를 떠올리며 인상을 찌푸려라. " +                    
                    "외지인에게 저 떠돌이를 어떻게 보는지, 첫인상과 생각을 솔직하게 말해 보라고 요청해라."
                );
                break;

            // Brown2 → WaitPlayer1 (1차 플레이어 발언, 브라운 호감도 결정 구간)
            case Step.Brown2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 답변은 브라운 호감도에 직접 영향 (브라운이 외지인의 눈을 시험하는 구간)
                answerTargetNpc = brown;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Brown3(플레이어 1차 답변 평가) → Faye2
            case Step.Brown3:
                currentStep = Step.Faye2;
                StartNpcTurn(
                    faye,                    
                    "의심이 많더라도 이해한다는 태도를 보이며, " +
                    "마을에서 사람들을 즐겁게 해 줄 수 있다는 점을 어필해라."
                );
                break;

            // Faye2 → Brown4
            case Step.Faye2:
                currentStep = Step.Brown4;
                StartNpcTurn(
                    brown,                    
                    "노래꾼이 한 명쯤 있으면 마을 분위기에는 도움이 될 수도 있겠다는 생각을 드러내라. "
                );
                break;

            // Brown4 → Faye3
            case Step.Brown4:
                currentStep = Step.Faye3;
                StartNpcTurn(
                    faye,
                    "브라운의 대답에 반응하고, " +
                    "외지인에게 어떤 음악을 좋아하는지 물어봐라."
                );
                break;

            // Faye3 → WaitPlayer2 (2차 플레이어 발언, 페이 호감도 결정 구간)
            case Step.Faye3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 답변은 페이 호감도에 직접 영향 (페이가 외지인의 최종 판단을 어떻게 느끼는지)
                answerTargetNpc = faye;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Faye4(플레이어 2차 답변 평가) → Brown5
            case Step.Faye4:
                currentStep = Step.Brown5;
                StartNpcTurn(
                    brown,
                    "외지인과 페이의 대화를 들은 뒤, " +
                    "이번 일로 인해 외지인을 어떻게 보게 되었는지 정리해라. "
                );
                break;

            // Brown5 → Faye5
            case Step.Brown5:
                currentStep = Step.Faye5;
                StartNpcTurn(
                    faye,
                    "자신이 마을에 머물게 된다면 마을 밖으로 가는 경우 도움이 될 것임을 어필해라."
                );
                break;

            // Faye5 → Done
            case Step.Faye5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[GateFaye] 씬6-2 대화 종료. BrownScore=" + brownScore + ", FayeScore=" + fayeScore);

        // 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();   // ConversationManagerBase에서 제공

        // TODO: 페이드가 끝난 뒤에 씬 전환 또는 플래그 저장
        // 예: GameManager.Instance.SetAffinity("Brown", brownScore);
        //     GameManager.Instance.SetAffinity("Faye", fayeScore);
        //     SceneManager.LoadScene("NextScene");
    }
}
