using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FestivalAlleyConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC amon;  // 불량배 1 - 로크
    public NPC milo;  // 불량배 2 - 밀로

    // 이 씬에서만 사용하는 임시 호감도 점수
    private int amonScore = 0;
    private int miloScore = 0;

    private enum Step
    {
        None,
        Amon1,
        Milo1,
        Amon2,
        WaitPlayer1,
        Amon3,
        Milo2,
        Amon4,
        Milo3,
        WaitPlayer2,
        Milo4,
        Amon5,
        Milo5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Scene8Alley";

    private void Start()
    {
        // NPC 이벤트 연결
        if (amon != null) amon.OnReplied += OnNPCReplied;
        if (milo != null) milo.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게 처리
        if (amon != null) amon.acceptPlayerInput = false;
        if (milo != null) milo.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    private IEnumerator SceneStartRoutine()
    {
        // 광장의 축제 화면에서 어두운 골목으로 전환된 상태라고 가정하고,
        // 검은 화면 → 점점 밝게 (골목 장면 페이드 인)
        yield return FadeFromBlack();

        StartConversation();
    }

    private void OnDestroy()
    {
        if (amon != null) amon.OnReplied -= OnNPCReplied;
        if (milo != null) milo.OnReplied -= OnNPCReplied;
    }

    // 대화 시작
    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Amon1;

        // Roke1: 축제 밤, 골목에서 외지인에게 처음 시비를 거는 장면
        StartNpcTurn(
            amon,
            "지금은 마을 축제가 한창인 밤이다. " +
            "광장에서는 음악과 웃음소리가 들리지만, 너와 외지인은 사람들 시선에서 조금 떨어진 골목에 있다. " +
            "외지인에게 시비를 걸며 자신을 소개해라. "
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
        if (npc == amon && currentStep == Step.Amon3)
        {
            amonScore += res.affinity_change;
        }
        else if (npc == milo && currentStep == Step.Milo4)
        {
            miloScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == amon)
        {
            if (milo != null)
            {
                milo.HearLine("Roke", res.message);
            }
        }
        else if (npc == milo)
        {
            if (amon != null)
            {
                amon.HearLine("Milo", res.message);
            }
        }
    }

    // 플레이어가 입력을 마쳤을 때
    protected override void OnPlayerSpoke(string text)
    {
        // 1차 플레이어 답변: 로크 호감도 타겟, 밀로도 듣게 함
        if (currentStep == Step.WaitPlayer1)
        {
            if (milo != null)
            {
                milo.HearLine("Player", text);
            }

            // 플레이어의 첫 대답을 들은 뒤, 로크가 평가하는 턴
            currentStep = Step.Amon3;
            SetPendingNpcTurn(
                amon,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답을 평가해라."
            );
        }
        // 2차 플레이어 답변: 밀로 호감도 타겟, 로크도 듣게 함
        else if (currentStep == Step.WaitPlayer2)
        {
            if (amon != null)
            {
                amon.HearLine("Player", text);
            }

            // 플레이어의 두 번째 대답을 들은 뒤, 밀로가 평가하는 턴
            currentStep = Step.Milo4;
            SetPendingNpcTurn(
                milo,
                "방금 외지인이 이렇게 대답했다: \"" + lastPlayerText + "\" " +
                "외지인의 대답을 평가해라."
            );
        }
    }

    // NPC 턴이 끝난 뒤 Step 전환
    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            // Roke1 → Milo1
            case Step.Amon1:
                currentStep = Step.Milo1;
                StartNpcTurn(
                    milo,
                    "외지인에게 요즘 마을에서 너무 눈에 띈다고 시비를 걸어라."
                );
                break;

            // Milo1 → Roke2
            case Step.Milo1:
                currentStep = Step.Amon2;
                StartNpcTurn(
                    amon,
                    "축제는 마을의 일원이 된 사람들이 피땀흘려 준비했다는 점을 강조하며, " +
                    "외지인에게 너는 이 축제를 즐길 자격이 없다고 말해라."                    
                );
                break;

            // Roke2 → WaitPlayer1 (1차 플레이어 발언)
            case Step.Amon2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;
                npcReplyReceived = false;

                // 1차 답변은 로크 호감도에 직접 영향
                answerTargetNpc = amon;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Roke3(플레이어 1차 답변 평가) → Milo2
            case Step.Amon3:
                currentStep = Step.Milo2;
                StartNpcTurn(
                    milo,
                    "흥분한 아몬을 진정시키며 외지인에게 가진 게 없는지 물어봐라."
                );
                break;

            // Milo2 → Roke4
            case Step.Milo2:
                currentStep = Step.Amon4;
                StartNpcTurn(
                    amon,
                    "밀로의 발언에 동의하며 외지인에게 돈 같은 것을 갈취할 것처럼 말해라. "
                );
                break;

            // Roke4 → Milo3 (2차 질문 준비)
            case Step.Amon4:
                currentStep = Step.Milo3;
                StartNpcTurn(
                    milo,
                    "아몬에게 외지인은 마을의 서기인 루크(Luke)와 친분이 있다는 점을 언급하며, " +
                    "외지인에게 오늘은 이만 갈테니 이번 일을 발설하지 말라고 주의시켜라."
                );
                break;

            // Milo3 → WaitPlayer2 (2차 플레이어 발언)
            case Step.Milo3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;
                npcReplyReceived = false;

                // 2차 답변은 밀로 호감도에 직접 영향
                answerTargetNpc = milo;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            // Milo4(플레이어 2차 답변 평가) → Roke5
            case Step.Milo4:
                currentStep = Step.Amon5;
                StartNpcTurn(
                    amon,
                    "오늘은 축제니까 여기까지만 하겠다는 식으로 상황을 정리해라."
                );
                break;

            // Roke5 → Milo5 (마지막 한마디)
            case Step.Amon5:
                currentStep = Step.Milo5;
                StartNpcTurn(
                    milo,
                    "외지인의 아직 마을의 일원이 아니라는 점을 언급하며 주의시켜라."
                );
                break;

            // Milo5 → Done (씬 종료)
            case Step.Milo5:
                currentStep = Step.Done;
                expectedNpc = null;
                npcReplyReceived = false;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        Debug.Log("[Scene8Alley] 축제 골목 불량배 대화 종료. " +
                  "RokeScore=" + amonScore +
                  ", MiloScore=" + miloScore);

        // TODO: 여기서 GameManager에 rokeScore, miloScore 저장 등 처리 가능
        // 예:
        // GameManager.Instance.SetAffinity("Roke", rokeScore);
        // GameManager.Instance.SetAffinity("Milo", miloScore);

        // 화면을 다시 검게 닫는 연출
        StartCoroutine(SceneEndRoutine());
    }

    private IEnumerator SceneEndRoutine()
    {
        // 밝은 화면 → 검은 화면으로 페이드
        yield return FadeToBlack();

        // TODO: 페이드가 끝난 뒤에 다음 씬 전환 또는 플레이어 이동 처리
        // 예: SceneManager.LoadScene("Scene9");
    }
}
