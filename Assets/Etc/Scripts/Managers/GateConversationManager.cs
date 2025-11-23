using UnityEngine;
using UnityEngine.UI;

public class GateConversationManager : MonoBehaviour
{
    public NPC brown;
    public NPC toma;
    public InputField playerInput; // 게이트 씬용 플레이어 입력필드

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
        Done
    }

    private Step currentStep = Step.None;

    // 이번 턴에 말을 해야 하는 NPC
    private NPC expectedNpc = null;

    // 이번 Step에서 NPC 답이 도착했는지
    private bool npcReplyReceived = false;

    // 지금 플레이어 입력 차례인지
    private bool waitingForPlayerInput = false;

    // 플레이어가 마지막으로 한 말
    private string lastPlayerText = "";

    private void Start()
    {
        // NPC 답변 이벤트
        brown.OnReplied += OnNPCReplied;
        toma.OnReplied += OnNPCReplied;

        // 게이트 씬 동안에는 NPC가 직접 InputField로 질문 받지 않도록 막고 싶으면:
        brown.acceptPlayerInput = false;
        toma.acceptPlayerInput = false;

        // 플레이어 입력
        playerInput.onEndEdit.AddListener(OnPlayerInputEnd);
        playerInput.gameObject.SetActive(false);

        StartConversation();
    }

    private void OnDestroy()
    {
        if (brown != null) brown.OnReplied -= OnNPCReplied;
        if (toma != null) toma.OnReplied -= OnNPCReplied;

        if (playerInput != null)
            playerInput.onEndEdit.RemoveListener(OnPlayerInputEnd);
    }

    // ---------------------------------------------------------
    // 대화 시작: 브라운 1차 발언
    // ---------------------------------------------------------
    private void StartConversation()
    {
        currentStep = Step.Brown1;
        StartNpcTurn(
            brown,
            "지금은 마을 입구다. 낯선 외지인이 성문 앞에 서 있다. " +
            "그 외지인을 경계하며, 문지기답게 거칠고 짧게 말을 걸어라."
        );
    }

    // NPC 한 턴 시작
    private void StartNpcTurn(NPC who, string prompt)
    {
        expectedNpc = who;
        npcReplyReceived = false;
        waitingForPlayerInput = false;

        who.AskByScript(prompt);
    }

    // ---------------------------------------------------------
    // NPC가 GPT 응답을 받은 직후 호출됨
    // → 여기서는 "이번 턴 끝났다"만 표시하고, Step은 바꾸지 않음
    // ---------------------------------------------------------
    private void OnNPCReplied(NPC npc, NPCResponse res)
    {
        Debug.Log($"[Gate] {npc.name} replied: {res.message}");

        if (currentStep == Step.None || currentStep == Step.Done)
            return;

        // 이번 턴에 말해야 하는 NPC가 아니면 무시
        if (expectedNpc != null && npc != expectedNpc)
            return;

        // 이번 Step의 NPC 발언이 끝났다는 표시만
        npcReplyReceived = true;
    }

    // ---------------------------------------------------------
    // 플레이어가 InputField에 입력 후 Enter
    // ---------------------------------------------------------
    private void OnPlayerInputEnd(string text)
    {
        if (!waitingForPlayerInput)
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

        string trimmed = text.Trim();
        lastPlayerText = trimmed;

        // 플레이어 대사도 LineManager로 출력
        if (LineManager.Instance != null)
        {
            LineManager.Instance.ShowPlayerLine(
                "Player",
                Color.cyan,
                trimmed
            );
        }

        playerInput.text = "";
        EnablePlayerInput(false);
        waitingForPlayerInput = false;

        // 플레이어 발언 후 다음 Step으로 전환
        if (currentStep == Step.WaitPlayer1)
        {
            currentStep = Step.Toma2;
            StartNpcTurn(
                toma,
                "방금 플레이어가 이렇게 말했다: \"" + lastPlayerText + "\" " +
                "이 말을 듣고, 너답게 주저하면서 반응해라. " +
                "플레이어의 말투와 태도에 따라 호감도를 계산해라."
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            currentStep = Step.Done;
            expectedNpc = null;
            npcReplyReceived = false;

            Debug.Log("[Gate] 대화 종료. 이제 마을로 입장 처리.");
            // TODO: 실제 마을 입장 로직 (씬 전환, 문 열기 등)
        }
    }

    private void EnablePlayerInput(bool enable)
    {
        playerInput.gameObject.SetActive(enable);
        playerInput.interactable = enable;

        if (enable)
        {
            playerInput.text = "";
            playerInput.ActivateInputField();
        }
    }

    // ---------------------------------------------------------
    // Space 입력 처리: 텍스트 스킵 & 다음 Step 진행
    // ---------------------------------------------------------
    private void Update()
    {
        if (LineManager.Instance == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 1) 아직 텍스트가 타이핑 중이거나, 큐에 남아 있으면 → 텍스트 먼저 처리
            if (LineManager.Instance.IsTyping || LineManager.Instance.HasQueuedLines)
            {
                LineManager.Instance.ShowNextLine();
                return;
            }

            // 2) 모든 텍스트가 다 나온 상태 + NPC 발언이 끝난 상태라면 → 다음 Step으로 진행
            if (npcReplyReceived && !waitingForPlayerInput)
            {
                npcReplyReceived = false;
                GoToNextStepAfterNpc();
            }
        }
    }

    // ---------------------------------------------------------
    // NPC 턴이 끝난 뒤, Space를 눌렀을 때 Step 전환
    // ---------------------------------------------------------
    private void GoToNextStepAfterNpc()
    {
        switch (currentStep)
        {
            case Step.Brown1:
                currentStep = Step.Toma1;
                StartNpcTurn(
                    toma,
                    "방금 브라운이 외지인에게 한 말을 들었다. " +
                    "너는 소심하고 겁이 많지만, 상황이 불편해서 한마디 한다. " +
                    "너답게 주저하면서 반응해라."
                );
                break;

            case Step.Toma1:
                currentStep = Step.Brown2;
                StartNpcTurn(
                    brown,
                    "토마가 한 말을 들었다. 너는 여전히 외지인을 의심한다. " +
                    "토마를 약간 무시하면서, 외지인에게 다시 한 번 날카롭게 묻는다."
                );
                break;

            case Step.Brown2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;
                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Toma2:
                currentStep = Step.Brown3;
                StartNpcTurn(
                    brown,
                    "플레이어와 토마의 대화를 모두 들었다. " +
                    "외지인에 대한 첫 인상이 조금은 바뀌었거나 더 나빠졌을 수 있다. " +
                    "너의 호감도에 맞게 반응해라."
                );
                break;

            case Step.Brown3:
                currentStep = Step.Toma3;
                StartNpcTurn(
                    toma,
                    "지금까지의 대화를 떠올리며, 외지인에 대한 너의 솔직한 인상을 말해라. " +
                    "너는 여전히 소심하지만, 조금은 마음이 열렸을 수도 있다."
                );
                break;

            case Step.Toma3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;
                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;
        }
    }
}
