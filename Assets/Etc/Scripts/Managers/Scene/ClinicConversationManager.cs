using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClinicConversationManager : ConversationManagerBase
{
    [Header("NPC References")]
    public NPC toma;
    public NPC doctor;

    // 이 씬에서만 사용하는 임시 호감도 점수
    private int tomaScore = 0;
    private int doctorScore = 0;

    private enum Step
    {
        None,
        Toma1,
        Doctor1,
        Toma2,
        WaitPlayer1,
        Toma3,
        Doctor2,
        Toma4,
        Doctor3,
        WaitPlayer2,
        Doctor4,
        Toma5,
        Doctor5,
        Done
    }

    private Step currentStep = Step.None;

    protected override string LogTag => "Scene9Clinic";

    private bool endRoutineStarted = false;

    private void Start()
    {
        // NPC 이벤트 연결
        if (toma != null) toma.OnReplied += OnNPCReplied;
        if (doctor != null) doctor.OnReplied += OnNPCReplied;

        // 이 씬에서는 NPC가 직접 InputField를 쓰지 않게 처리
        if (toma != null) toma.acceptPlayerInput = false;
        if (doctor != null) doctor.acceptPlayerInput = false;

        // 화면 페이드 후 대화 시작
        StartCoroutine(SceneStartRoutine());
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (toma != null) toma.OnReplied -= OnNPCReplied;
        if (doctor != null) doctor.OnReplied -= OnNPCReplied;
    }

    private IEnumerator SceneStartRoutine()
    {
        // 검은 화면 → 점점 밝아짐 (임시 진료소)
        yield return FadeFromBlack();

        StartConversation();
    }

    private void StartConversation()
    {
        ResetConversationState();

        currentStep = Step.Toma1;

        // Toma1: 배탈로 힘들어하는 오프닝
        StartNpcTurn(
            toma,
            "지금은 마을 한쪽 임시 진료소이다. 너는 침상에 반쯤 누워 배를 움켜쥐고 있다. " +
            "축제에서 과식해서 밤새 고생했고, 민망하지만 플레이어에게 고맙다고 생각한다. " +
            "말을 하려다 배가 아파 끙끙대는 소심한 말투로 사과부터 해라."
        );
    }

    protected override bool IsConversationActive()
    {
        return currentStep != Step.None && currentStep != Step.Done;
    }

    protected override void OnNpcHeard(NPC npc, NPCResponse res, string speakerName)
    {
        // 1) 플레이어 발언에 대한 평가/반응 턴에서만 호감도 누적
        if (npc == toma && (currentStep == Step.Toma3 || currentStep == Step.Toma5))
        {
            tomaScore += res.affinity_change;
        }
        else if (npc == doctor && (currentStep == Step.Doctor2 || currentStep == Step.Doctor4))
        {
            doctorScore += res.affinity_change;
        }

        // 2) 서로의 말은 항상 공유
        if (npc == toma)
        {
            if (doctor != null) doctor.HearLine("Toma", res.message);
        }
        else if (npc == doctor)
        {
            if (toma != null) toma.HearLine("Doctor", res.message);
        }
    }

    protected override void OnPlayerSpoke(string text)
    {
        // Base에서 answerTargetNpc에게는 이미 HearLine("Player", text)가 호출된 상태.
        // 여기서는 "다른 NPC"에게도 들려주고, 다음 Step을 예약한다.

        if (currentStep == Step.WaitPlayer1)
        {
            // 첫 번째 답변은 의사 판단에 영향(솔직함/관찰력) + 토마도 듣게 함
            if (toma != null)
            {
                toma.HearLine("Player", text);
            }

            // 플레이어 대답 후 → Toma3에서 토마가 민망해하며 반응
            currentStep = Step.Toma3;
            SetPendingNpcTurn(
                toma,
                "방금 플레이어가 이렇게 말했다: \"" + lastPlayerText + "\" " +
                "플레이어의 대답에 대한 솔직한 생각을 말하고, 에드런(의사)의 질문에 솔직하게 답해라."
            );
        }
        else if (currentStep == Step.WaitPlayer2)
        {
            // 두 번째 답변은 의사 반응(신뢰/인상) + 토마도 듣게 함
            if (toma != null)
            {
                toma.HearLine("Player", text);
            }

            // 플레이어 대답 후 → Doctor4에서 의사가 반응 + 처방
            currentStep = Step.Doctor4;
            SetPendingNpcTurn(
                doctor,
                "방금 플레이어가 이렇게 말했다: \"" + lastPlayerText + "\" " +
                "플레이어의 대답을 평가해라."
            );
        }
    }

    protected override void OnNpcTurnFinished()
    {
        switch (currentStep)
        {
            case Step.Toma1:
                currentStep = Step.Doctor1;
                StartNpcTurn(
                    doctor,                    
                    "토마의 상태를 확인하며 첫 마디를 던져라. " +
                    "그리고 언제부터 아팠는지, 축제에서 무엇을 얼마나 먹었는지 물어라."
                );
                break;

            case Step.Doctor1:
                currentStep = Step.Toma2;
                StartNpcTurn(
                    toma,
                    "에드런(의사)의 질문에 솔직하게 대답하기 창피하다고 답해라."                    
                );
                break;

            case Step.Toma2:
                currentStep = Step.WaitPlayer1;
                expectedNpc = null;

                // 1차 플레이어 답변은 의사(Doctor) 판단에 직접 영향
                answerTargetNpc = doctor;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Toma3:
                currentStep = Step.Doctor2;
                StartNpcTurn(
                    doctor,
                    "토마의 대답에 대한 자신의 의견을 말해라."
                );
                break;

            case Step.Doctor2:
                currentStep = Step.Toma4;
                StartNpcTurn(
                    toma,
                    "에드런(의사)의 대답에 대한 답변을 하고, 에드런(의사)가 쓰고 있는 역병 가면에 대한 솔직한 감상을 내놓아라."
                );
                break;

            case Step.Toma4:
                currentStep = Step.Doctor3;
                StartNpcTurn(
                    doctor,
                    "토마의 대답에 반응하며, 외지인에게 토마가 잘 치유될 수 있도록 도와줄 수 있는지 물어봐라."
                );
                break;

            case Step.Doctor3:
                currentStep = Step.WaitPlayer2;
                expectedNpc = null;

                // 2차 플레이어 답변은 의사(Doctor) 판단에 직접 영향
                answerTargetNpc = doctor;

                EnablePlayerInput(true);
                waitingForPlayerInput = true;
                break;

            case Step.Doctor4:
                currentStep = Step.Toma5;
                StartNpcTurn(
                    toma,
                    "에드런(의사)가 내려준 처방을 잘 따르겠다고 다짐해라."
                );
                break;

            case Step.Toma5:
                currentStep = Step.Doctor5;
                StartNpcTurn(
                    doctor,
                    "토마의 증상이 금방 완화될 것임을 알려주고, 외지인에게 성곽에 오게 된다면 자신을 볼 수 있을 거라고 말해라."
                );
                break;

            case Step.Doctor5:
                currentStep = Step.Done;
                HandleConversationEnd();
                break;
        }
    }

    private void HandleConversationEnd()
    {
        if (endRoutineStarted)
            return;

        endRoutineStarted = true;
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
