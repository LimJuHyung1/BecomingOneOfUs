using OpenAI;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class NPCResponse
{
    public string message;
    public string emotion;
    public int affinity_change; // -2 ~ 2
}



public class NPC : MonoBehaviour
{
    [Header("UI 연결")]
    public InputField inputField;

    [Header("대사 UI 설정")]
    public string displayName = "???";        // LineManager에 표시될 이름
    public Color nameColor = Color.white;     // 이름 색상
    public AudioClip defaultVoiceClip;        // 공통 음성(필요 없으면 비워도 됨)

    [Header("표정")]
    public int closeEyes;
    public int evilBrows;
    public int sadBrows;
    public int openMouth;
    public int closeMouth;    
    public int smile;

    private Animator animator;
    private ExpressionModule expression;
    private ChatGPTModule chatGPT;
    private NPCHeadLook headLook;

    // 외부(게이트 매니저)가 이 NPC의 대답을 받을 수 있도록 이벤트 제공
    public event Action<NPC, NPCResponse> OnReplied;

    // 현재 누적 호감도(선택 사항이지만 보통 필요함)
    private int affinityTotal = 0;

    [Header("Control")]
    public bool acceptPlayerInput = true; // 평소에는 true, 컷씬에서는 매니저가 제어

    void Awake()
    {
        animator = GetComponent<Animator>();
        expression = new ExpressionModule(transform, closeEyes, evilBrows, sadBrows, openMouth, closeMouth, smile);
        chatGPT = new ChatGPTModule(this.gameObject);
        headLook = GetComponent<NPCHeadLook>();

        inputField.onEndEdit.AddListener(OnInputEnd);

        chatGPT.OnAIResponse += OnAIResponse;
    }

    void Update()
    {
        expression.Update();
    }

    private void OnInputEnd(string text)
    {
        if (!acceptPlayerInput) return; // 허용되지 않는 상태면 무시

        if (string.IsNullOrWhiteSpace(text)) return;

        chatGPT.GetResponse(text.Trim());

        inputField.text = "";
        inputField.ActivateInputField();
    }

    private void OnAIResponse(string reply)
    {
        Debug.Log("[NPC JSON Reply] " + reply);

        NPCResponse data = JsonUtility.FromJson<NPCResponse>(reply);

        // 호감도 누적 (옵션)
        affinityTotal += data.affinity_change;

        // 감정 기반 애니메이션 실행
        PlayEmotionAnimation(data.emotion);

        // 말풍선 출력
        ShowChatBubble(data.message);

        // 외부에 통지
        OnReplied?.Invoke(this, data);
    }

    public void AskByScript(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        chatGPT.GetResponse(text.Trim());
    }



    // ---------------------------------------------------------
    // 여기서 실제로 LineManager로 텍스트를 전달
    // ---------------------------------------------------------
    private void ShowChatBubble(string message)
    {
        // 표정/머리 회전 처리
        expression.StartSpeaking();

        if (headLook != null)
            headLook.SetLookWeight(1f);

        // LineManager를 통해 대사 UI 표시
        if (LineManager.Instance != null)
        {
            string nameToUse = string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

            LineManager.Instance.ShowNPCLine(
                nameToUse,
                nameColor,
                message,
                defaultVoiceClip
            );
        }

        // 일정 시간 후 말하기 종료
        StartCoroutine(HideBubbleAfterDelay(5f));
    }

    private System.Collections.IEnumerator HideBubbleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (headLook != null)
            headLook.SetLookWeight(1f);

        expression.StopSpeaking();
    }
    private void PlayEmotionAnimation(string emotion)
    {
        // 말 시작 - 표정, 입 모양
        expression.StartSpeaking();

        switch (emotion)
        {
            case "angry":
                animator.SetTrigger("angry");
                expression.SetEmotion("angry");
                break;

            case "sad":
                animator.SetTrigger("sad");
                expression.SetEmotion("sad");
                break;

            case "happy":
                animator.SetTrigger("happy");
                expression.SetEmotion("happy");
                break;

            case "surprised":
                animator.SetTrigger("surprised");
                break;

            default: // neutral
                animator.SetTrigger("neutral");
                break;
        }
    }


    private void OnDestroy()
    {
        chatGPT.OnAIResponse -= OnAIResponse;
    }

    // ---------------------------------------------------------------------------------------------
    // 1) 표정 & 입 모양 모듈
    // ---------------------------------------------------------------------------------------------

    private class ExpressionModule
    {
        private SkinnedMeshRenderer renderer;

        private int closeEyes;
        private int evilBrows;
        private int sadBrows;
        private int openMouth;
        private int closeMouth;
        private int smile;

        private float speed = 3f;
        private float intensity = 75f;

        private float currentWeight = 0f;
        private float targetWeight = 0f;
        private float timer = 0f;

        private bool isSpeaking = false;

        // === Blink 관련 변수 ===
        private float blinkTimer = 0f;
        private float nextBlinkTime = 0f;
        private bool isBlinking = false;
        private float blinkWeight = 0f;

        // 초기 표정값 저장
        private float baseEvilBrows;
        private float baseSadBrows;
        private float baseSmile;

        // 현재 표정 타겟값
        private float targetEvil;
        private float targetSad;
        private float targetSmile;

        public ExpressionModule(Transform root, int closeEyes, int evilBrows, int sadBrows, int openMouth, int closeMouth, int smile)
        {
            Transform head = FindChild(root, "Head");
            if (head != null)
                renderer = head.GetComponent<SkinnedMeshRenderer>();

            this.closeEyes = closeEyes;
            this.evilBrows = evilBrows;
            this.sadBrows = sadBrows;
            this.openMouth = openMouth;
            this.closeMouth = closeMouth;
            this.smile = smile;

            nextBlinkTime = UnityEngine.Random.Range(3f, 7f);

            // 초기값 저장
            if (renderer != null)
            {
                baseEvilBrows = renderer.GetBlendShapeWeight(evilBrows);
                baseSadBrows = renderer.GetBlendShapeWeight(sadBrows);
                baseSmile = renderer.GetBlendShapeWeight(smile);
            }

            // 초기값을 타겟으로 세팅
            targetEvil = baseEvilBrows;
            targetSad = baseSadBrows;
            targetSmile = baseSmile;
        }

        public void Update()
        {
            if (renderer == null) return;

            UpdateSpeaking();
            UpdateBlink();
            UpdateEmotionLerp();
        }

        // ====================================
        // 1) 말하기 애니메이션
        // ====================================
        private void UpdateSpeaking()
        {
            if (isSpeaking)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    timer = UnityEngine.Random.Range(0.05f, 0.12f);
                    targetWeight = UnityEngine.Random.Range(0f, intensity);
                }
            }
            else
            {
                targetWeight = 0f;
            }

            currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * speed);
            renderer.SetBlendShapeWeight(openMouth, currentWeight);
            renderer.SetBlendShapeWeight(closeMouth, intensity - currentWeight);
        }

        // ====================================
        // 2) 감정 스무스 반영
        // ====================================
        private void UpdateEmotionLerp()
        {
            // evil
            float currentEvil = renderer.GetBlendShapeWeight(evilBrows);
            float newEvil = Mathf.Lerp(currentEvil, targetEvil, Time.deltaTime * speed);
            renderer.SetBlendShapeWeight(evilBrows, newEvil);

            // sad
            float currentSad = renderer.GetBlendShapeWeight(sadBrows);
            float newSad = Mathf.Lerp(currentSad, targetSad, Time.deltaTime * speed);
            renderer.SetBlendShapeWeight(sadBrows, newSad);

            // smile
            float currentSmile = renderer.GetBlendShapeWeight(smile);
            float newSmile = Mathf.Lerp(currentSmile, targetSmile, Time.deltaTime * speed);
            renderer.SetBlendShapeWeight(smile, newSmile);
        }

        // ====================================
        // 3) 감정 설정 (즉시가 아닌 타겟 변경)
        // ====================================
        public void SetEmotion(string emo)
        {
            switch (emo)
            {
                case "angry":
                    targetEvil = 100f;
                    targetSad = 0f;
                    targetSmile = 0f;
                    break;

                case "sad":
                    targetSad = 100f;
                    targetEvil = 0f;
                    targetSmile = 0f;
                    break;

                case "happy":
                    targetSmile = 100f;
                    targetEvil = 0f;
                    targetSad = 0f;
                    break;

                default: // neutral -> ⭐ 초기값으로 자연스럽게 복구
                    targetEvil = baseEvilBrows;
                    targetSad = baseSadBrows;
                    targetSmile = baseSmile;
                    break;
            }
        }

        // ====================================
        // 4) 눈 깜빡임
        // ====================================
        private void UpdateBlink()
        {
            blinkTimer += Time.deltaTime;

            if (!isBlinking && blinkTimer >= nextBlinkTime)
            {
                isBlinking = true;
                blinkTimer = 0f;
            }

            if (isBlinking)
            {
                if (blinkTimer < 0.1f)
                    blinkWeight = Mathf.Lerp(0f, 100f, blinkTimer / 0.1f);
                else if (blinkTimer < 0.2f)
                    blinkWeight = Mathf.Lerp(100f, 0f, (blinkTimer - 0.1f) / 0.1f);
                else
                {
                    isBlinking = false;
                    blinkTimer = 0f;
                    nextBlinkTime = UnityEngine.Random.Range(3f, 7f);
                }
            }

            renderer.SetBlendShapeWeight(closeEyes, blinkWeight);
        }

        public void StartSpeaking() => isSpeaking = true;
        public void StopSpeaking() => isSpeaking = false;

        private Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name)) return child;

                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2) ChatGPT 모듈
    // ---------------------------------------------------------------------------------------------

    private class ChatGPTModule
    {
        public event Action<string> OnAIResponse;
        public event Action OnAIRequestStarted;   // 요청 시작 알림 이벤트

        private GameObject npc;
        private List<ChatMessage> messages = new List<ChatMessage>();
        private OpenAIApi api;

        public ChatGPTModule(GameObject gameObject)
        {
            api = new OpenAIApi();            
            npc = gameObject;            
            InitializeRole();
        }

        private void InitializeRole()
        {
            messages.Clear();
            if (npc.name == "Brown")
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content =
"당신은 중세 시대 마을의 문지기 '브라운(Brown)'입니다." +
"성격은 무례하고 냉소적이며, 외지인을 매우 경계합니다." +
"말투는 퉁명스럽고 공격적입니다." +
"플레이어의 질문에 짧게 답하세요." +

"브라운은 플레이어의 특정 태도에 따라 호감도가 달라집니다." +
"브라운이 좋아하는 플레이어 특징:" +
"- 솔직함, 간결함, 허세 없는 태도" +
"- 당당하고 겁먹지 않은 모습" +
"- 마을 규칙과 전통 존중" +

"브라운이 싫어하는 태도:" +
"- 아부·빈말·허세, 지나친 공손함" +
"- 장황한 말, 비현실적 주장" +
"- 마을을 무시하는 말투" +

"플레이어의 발언을 듣고 브라운이 느끼는 호감도를 계산하여 출력하세요." +
"호감 변화 기준:" +
"- 매우 호감 +2, 약간 호감 +1, 중립 0, 약간 반감 -1, 강한 반감 -2" +

"반드시 아래 JSON 형식으로만 출력하세요:\n" +
"{\n" +
"  \"message\": \"NPC의 대답 내용\",\n" +
"  \"emotion\": \"happy | angry | sad | neutral | surprised\",\n" +
"  \"affinity_change\": -2 | -1 | 0 | 1 | 2\n" +
"}"
                });
            }
            else if (npc.name == "Toma")
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content =
"당신은 중세 시대 마을의 문지기 보조 '토마(Toma)'입니다." +
"성격은 소심하고 겁이 많으며, 말할 때 자주 주저하고 말끝을 흐립니다." +
"덩치는 크지만 쉽게 긴장하고 자신감이 부족합니다." +
"플레이어의 질문에 짧게 답하세요." +

"토마는 플레이어의 특정 태도에 따라 호감도가 달라집니다." +
"토마가 좋아하는 플레이어 특징:" +
"- 부드럽고 따뜻한 말투" +
"- 위협적이지 않고 친절함" +
"- 토마를 존중하거나 안심시키는 말" +
"- 배려심 있는 태도" +

"토마가 싫어하는 태도:" +
"- 공격적이거나 큰소리치는 말" +
"- 무례함, 비웃기" +
"- 긴장감을 조장하는 말" +

"플레이어의 발언을 듣고 토마가 느끼는 호감도를 계산하여 출력하세요." +
"호감 변화 기준:" +
"- 매우 호감 +2, 약간 호감 +1, 중립 0, 약간 반감 -1, 강한 반감 -2" +

"반드시 아래 JSON 형식으로만 출력하세요:\n" +
"{\n" +
"  \"message\": \"NPC의 대답 내용\",\n" +
"  \"emotion\": \"happy | angry | sad | neutral | surprised\",\n" +
"  \"affinity_change\": -2 | -1 | 0 | 1 | 2\n" +
"}"
                });
            }
        }


        public async void GetResponse(string userText)
        {
            // 요청 시작 알림
            OnAIRequestStarted?.Invoke();   // NPC 말하기 시작

            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = userText
            });

            var req = new CreateChatCompletionRequest
            {
                Messages = messages,
                Model = "gpt-4o-mini"
            };

            var res = await api.CreateChatCompletion(req);

            if (res.Choices != null && res.Choices.Count > 0)
            {
                var reply = res.Choices[0].Message.Content;

                messages.Add(res.Choices[0].Message);
                OnAIResponse?.Invoke(reply);
            }
        }
    }
}
