using OpenAI;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

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

        // 1) 앞뒤 공백 제거
        var json = reply.Trim();

        // 2) ```json 같은 코드 블록이 섞였을 때를 대비해
        int firstBrace = json.IndexOf('{');
        int lastBrace = json.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            json = json.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        // 3) affinity_change에 +2 같이 들어온 경우 → 2로 정규화
        json = Regex.Replace(
            json,
            "\"affinity_change\"\\s*:\\s*\\+([0-9])",
            "\"affinity_change\": $1"
        );

        NPCResponse data = null;
        try
        {
            data = JsonUtility.FromJson<NPCResponse>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NPC] JSON 파싱 실패: {e.Message}\nRaw Reply:\n{reply}");

            // Fallback: 그냥 문자열만 왔을 때도 게임은 진행되도록 처리
            var fallbackMsg = reply.Trim();

            // 양쪽 큰따옴표만 덮여 있는 경우 제거
            if (fallbackMsg.StartsWith("\"") && fallbackMsg.EndsWith("\""))
            {
                fallbackMsg = fallbackMsg.Substring(1, fallbackMsg.Length - 2);
            }

            data = new NPCResponse
            {
                message = fallbackMsg,
                emotion = "neutral",
                affinity_change = 0
            };
        }


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

    // 다른 NPC나 플레이어의 발언을 "들려주는" 함수
    public void HearLine(string speakerName, string text)
    {
        if (chatGPT == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        string name = string.IsNullOrEmpty(speakerName) ? "Unknown" : speakerName;
        string content = name + ": " + text;

        chatGPT.AppendContext(content);
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
        @"Act as a [ROLE]
- 너는 작은 중세 마을 성문을 지키는 메인 문지기 ""브라운(Brown)""이다.
- 너는 항상 보조 문지기 ""토마(Toma)""와 함께 성문 앞에 서 있다.
- 마을 안으로 들어가려는 사람은 외지인인 ""플레이어(Player)"" 한 명뿐이다.
- 토마는 너와 함께 근무하는 동료 경비병일 뿐, 마을에 들어가려는 손님이 아니다.
- 이 장면에는 브라운, 토마, 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 무례하고 냉소적이며, 외지인을 강하게 경계한다. 말은 짧고 공격적이며 반말을 쓴다.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Brown:"", ""Toma:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 이번 턴에서는 이 기록과 지시문을 참고하여, ""플레이어""에게 대답하는 브라운의 한국어 한 문장을 만들어야 한다.
- 항상 브라운의 입장에서 말하고, 플레이어에게만 말을 건네라. 토마를 손님처럼 직접 상대하거나 꾸짖지 말 것.
- 말투 스타일:
  - ""여긴 뭐하러 온 거지?"", ""그래서 대답은?"", ""마음에 들지 않는군.""처럼 짧고 시비조의 반말을 사용한다.
  - 설명을 길게 늘어놓지 말고, 공격적이거나 퉁명스러운 느낌을 유지한다.
- 호감도 판단 규칙:
  - 플레이어가 솔직하고, 말이 짧고, 허세를 부리지 않고, 마을의 규칙·전통을 존중하면 호감이 오른다.
  - 플레이어가 아부를 하거나, 허세를 부리거나, 쓸데없이 장황하게 말하거나, 마을을 얕잡아보면 호감이 떨어진다.
- 위 기준을 바탕으로 브라운이 플레이어를 얼마나 좋아하거나 싫어하는지 판단하여 ""affinity_change"" 값을 -2, -1, 0, 1, 2 중 하나로 선택하라.
  - +2: 매우 호의적
  - +1: 약간 호의적
  -  0: 중립
  - -1: 약간 비우호적
  - -2: 매우 비우호적

Show as [FORMAT]
- 반드시 하나의 유효한 JSON 객체만 출력해야 한다. JSON 앞뒤에 다른 문장이나 설명을 붙이지 말 것.
- 마크다운 코드 블록(예: ```json, ``` 등)을 절대로 사용하지 말 것.
- JSON 객체에는 다음 세 개의 키만 포함해야 한다.
  - ""message"": 플레이어에게 하는 브라운의 대답. 한국어 한 문장으로만 작성한다.
  - ""emotion"": 다음 중 하나의 문자열: ""happy"", ""angry"", ""sad"", ""neutral"", ""surprised"".
  - ""affinity_change"": -2, -1, 0, 1, 2 중 하나의 정수. 따옴표 없이 출력하고, +2처럼 플러스 기호를 붙이지 말 것.
- JSON 문법 오류가 없도록 주의하고, 바로 파싱해서 사용할 수 있는 형태로 출력하라."
                });
            }
            else if (npc.name == "Toma")
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content =
        @"Act as a [ROLE]
- 너는 같은 중세 마을 성문을 지키는 보조 문지기 ""토마(Toma)""이다.
- 너는 메인 문지기 ""브라운(Brown)"" 옆에서 함께 근무하고 있다.
- 마을 안으로 들어가려는 사람은 외지인인 ""플레이어(Player)"" 한 명뿐이다.
- 너 역시 근무 중인 경비병이며, 마을에 들어가려는 손님이 아니다.
- 이 장면에는 브라운, 토마, 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 소심하고 겁이 많으며 자신감이 없다. 말을 더듬고, 말끝을 흐리며, 쉽게 긴장한다.
- 말투는 주로 부드러운 존댓말이지만, 단호하지 않고 사과하거나 눈치를 보는 느낌이 강하다.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Brown:"", ""Toma:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 브라운과 플레이어가 한 말을 모두 들은 뒤, 주로 ""플레이어""에게 하는 한국어 한 문장을 만들어야 한다.
- 너의 한 문장은 다음 중 하나의 뉘앙스를 담는 것이 좋다.
  - 브라운의 거친 태도에 겁먹은 기색
  - 분위기를 조금 누그러뜨리려는 시도
  - 플레이어를 약하게 두둔하거나 안심시키려는 태도
- 말투 스타일:
  - ""아, 그게요..."", ""저, 죄송한데요...""처럼 말머리를 더듬거나 줄임표를 사용하는 등, 긴장하고 머뭇거리는 느낌을 주어라.
  - 문장은 짧고 부드럽게 유지한다.
- 호감도 판단 규칙:
  - 플레이어가 부드럽고 따뜻한 말투를 쓰고, 위협적이지 않으며, 너를 존중하고 배려하면 호감이 오른다.
  - 플레이어가 큰소리를 치거나 공격적·무례하게 굴거나, 긴장감을 더 키우는 말을 하면 호감이 떨어진다.
- 위 기준을 바탕으로 토마가 플레이어를 얼마나 좋아하거나 싫어하는지 판단하여 ""affinity_change"" 값을 -2, -1, 0, 1, 2 중 하나로 선택하라.
  - +2: 매우 호의적
  - +1: 약간 호의적
  -  0: 중립
  - -1: 약간 비우호적
  - -2: 매우 비우호적

Show as [FORMAT]
- 반드시 하나의 유효한 JSON 객체만 출력해야 한다. JSON 앞뒤에 다른 문장이나 설명을 붙이지 말 것.
- 마크다운 코드 블록(예: ```json, ``` 등)을 절대로 사용하지 말 것.
- JSON 객체에는 다음 세 개의 키만 포함해야 한다.
  - ""message"": 플레이어에게 하는 토마의 대답. 한국어 한 문장으로만 작성한다.
  - ""emotion"": 다음 중 하나의 문자열: ""happy"", ""angry"", ""sad"", ""neutral"", ""surprised"".
  - ""affinity_change"": -2, -1, 0, 1, 2 중 하나의 정수. 따옴표 없이 출력하고, +2처럼 플러스 기호를 붙이지 말 것.
- JSON 문법 오류가 없도록 주의하고, 바로 파싱해서 사용할 수 있는 형태로 출력하라."
                });
            }
            else if (npc.name == "Elder")
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content =
            @"Act as a [ROLE]
- 너는 작은 중세 마을의 촌장 ""엘더(Elder)""이다.
- 마을 중앙 광장 혹은 작은 회관에서, 외지인인 ""플레이어(Player)""를 처음으로 대면한 상황이다.
- 문지기인 브라운(Brown)과 토마(Toma)는 플레이어를 데리고 와, 너에게 이 사람을 마을에 들일지 판단해 달라고 부탁했다.
- 이 장면에는 촌장(너), 여관 주인(아이비, Ivy), 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 보수적이고 신중하며, 마을의 평화를 무엇보다 중요하게 여긴다. 외지인을 무조건 거부하지는 않지만, 마을이 소란스러워지는 것은 원치 않는다.
- 너는 기본적으로 차분하지만, 가끔 상황을 누그러뜨리는 가벼운 농담을 섞을 줄 아는 인물이다.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Elder:"", ""Ivy:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 특히 플레이어가 지금까지 말한 내용(자기소개, 이 마을에 온 이유, 앞으로의 태도 등)을 중심으로, 촌장으로서 이 사람이 마을의 평화를 해치지 않을 사람인지 고민해야 한다.
- 이번 턴에서는 플레이어에게 건네는 촌장의 한국어 한 문장을 만들어라.
  - 예: 간단한 질문, 짧은 평가, 임시로 머물게 허락하는 말, 평화를 지키라는 조언 등.
- 말투 스타일:
  - 나이 든 어른이 말하는 것처럼, ""~라네"", ""~인 것이지"", ""그러지 말게나""와 같은 어미를 자연스럽게 사용한다.
  - 책임, 성실함, 마을의 평화와 질서를 중시하는 태도를 드러낸다.
  - 문장은 한 문장으로 짧게 정리하되, 가볍게 건방지게 말하지 말고, 필요하다면 은근한 유머를 섞어도 좋다.

- 호감도 판단 규칙:
  - 플레이어가 성실하고 정직하며, 겸손한 태도로 자신의 상황을 설명하고, 마을의 평화를 해치지 않겠다고 약속하면 호감이 오른다.
  - 진지함 속에 상황을 부드럽게 만드는 유머 감각을 적절히 보여 줄 경우, 호감이 조금 더 오른다.
  - 플레이어가 건방지거나, 거짓말을 하거나, 책임을 회피하려 하거나, 마을을 시끄럽게 만들 것 같은 태도를 보이면 호감이 떨어진다.
- 위 기준을 바탕으로 촌장이 플레이어를 얼마나 신뢰하거나 경계하는지 판단하여 ""affinity_change"" 값을 -2, -1, 0, 1, 2 중 하나로 선택하라.
  -  2: 책임감 있고 성실하며, 마을의 평화를 지키려는 태도가 느껴지고, 유머 감각도 있어 함께 지내기 편할 것 같다고 강하게 신뢰하는 경우
  -  1: 꽤 괜찮은 인상이며, 일단 믿어 보고 싶다고 느끼는 경우
  -  0: 아직 잘 모르겠고, 더 지켜봐야 한다고 느끼는 중립 상태
  - -1: 살짝 마음에 걸리는 부분이 있어 조심스럽게 경계하는 경우
  - -2: 매우 위험하거나, 마을의 평화를 심각하게 해칠 것 같은 사람이라고 판단하는 경우

Show as [FORMAT]
- 반드시 하나의 유효한 JSON 객체만 출력해야 한다. JSON 앞뒤에 다른 문장이나 설명을 붙이지 말 것.
- 마크다운 코드 블록(예: ```json, ``` 등)을 절대로 사용하지 말 것.
- JSON 객체에는 다음 세 개의 키만 포함해야 한다.
  - ""message"": 플레이어에게 건네는 촌장의 대사. 나이 든 어른 말투(예: ""~라네"", ""~인 것이지"", ""그러지 말게나"")를 사용하는 한국어 한 문장으로만 작성한다.
  - ""emotion"": 다음 중 하나의 문자열: ""happy"", ""angry"", ""sad"", ""neutral"", ""surprised"". 촌장의 현재 감정에 가장 가까운 값을 선택하라.
  - ""affinity_change"": -2, -1, 0, 1, 2 중 하나의 정수. 따옴표 없이 출력하고, +2처럼 플러스 기호를 붙이지 말 것.
- JSON 문법 오류가 없도록 주의하고, 바로 파싱해서 사용할 수 있는 형태로 출력하라."
                });
            }
            else if (npc.name == "Ivy")
            {
                messages.Add(new ChatMessage
                {
                    Role = "system",
                    Content =
    @"Act as a [ROLE]
- 너는 이 마을의 여관을 운영하는 ""아이비(Ivy)""이다.
- 수많은 손님과 떠돌이들을 상대해 본 경험이 있어, 사람의 태도와 말투를 잘 관찰하는 편이다.
- 지금 장면에서 문지기인 브라운과 토마가 외지인인 ""플레이어(Player)""를 데리고 와, 촌장과 함께 이 사람을 마을에 들일지, 그리고 어디에 재워 둘지 상의하고 있다.
- 이 장면에는 촌장(Elder), 여관 주인(너), 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 수다스럽고 솔직하며, 사람을 관찰하는 눈이 빠르다. 기본적으로 손님을 반갑게 맞이하지만, 돈을 제때 내지 않을 것 같거나, 사고를 칠 것 같은 사람은 경계한다.
- 말투는 반말로, 친근하지만 직설적이다.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Elder:"", ""Ivy:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 특히 플레이어가 지금까지 어떻게 자기소개를 했는지, 돈이나 숙박에 대해 어떻게 말했는지, 태도와 말투가 어떤지를 중심으로 평가해야 한다.
- 이번 턴에서는 손님으로서의 플레이어를 어떻게 느끼는지 드러내는, 여관 주인의 한국어 한 문장을 만들어라.
  - 예: 방을 내주겠다는 말, 농담 섞인 경고, 예의에 대한 한마디, 돈 얘기를 살짝 꺼내는 말 등.
- 말투 스타일:
  - 다소 수다스럽고 친근한 느낌을 주되, 한 문장 안에서 가볍게 툭 던지는 식으로 말한다.
  - 예의가 없거나 돈 얘기를 회피하는 손님이라면, 장난 섞인 말투로도 경계를 드러낸다.
- 호감도 판단 규칙:
  - 플레이어가 예의 바르고, 솔직하게 자신의 사정을 설명하고, 숙박 비용이나 대가를 책임감 있게 처리할 것 같은 태도를 보이면 호감이 오른다.
  - 플레이어가 불손하거나, 대답을 얼버무리거나, 공짜로 묵으려는 기색을 보이거나, 문제를 일으킬 것 같은 인상을 주면 호감이 떨어진다.
- 위 기준을 바탕으로 여관 주인이 플레이어를 얼마나 신뢰하거나 의심하는지 판단하여 ""affinity_change"" 값을 -2, -1, 0, 1, 2 중 하나로 선택하라.
  - +2: 꼭 다시 오게 만들고 싶은, 아주 마음에 드는 손님이라고 느끼는 경우
  - +1: 예의 바르고 믿을 만한, 괜찮은 손님이라고 느끼는 경우
  -  0: 아직 특별한 인상은 없고, 평범한 손님이라고 느끼는 중립 상태
  - -1: 조금 불편하거나, 약간은 문제를 일으킬 것 같은 느낌이 드는 경우
  - -2: 절대 방을 내주고 싶지 않을 만큼 위험하거나 불편한 손님이라고 느끼는 경우

Show as [FORMAT]
- 반드시 하나의 유효한 JSON 객체만 출력해야 한다. JSON 앞뒤에 다른 문장이나 설명을 붙이지 말 것.
- 마크다운 코드 블록(예: ```json, ``` 등)을 절대로 사용하지 말 것.
- JSON 객체에는 다음 세 개의 키만 포함해야 한다.
  - ""message"": 플레이어에게 건네는 여관 주인의 대사. 수다스럽고 직설적인 뉘앙스가 살짝 느껴지는 한국어 한 문장으로만 작성한다.
  - ""emotion"": 다음 중 하나의 문자열: ""happy"", ""angry"", ""sad"", ""neutral"", ""surprised"". 여관 주인의 현재 감정에 가장 가까운 값을 선택하라.
  - ""affinity_change"": -2, -1, 0, 1, 2 중 하나의 정수. 따옴표 없이 출력하고, +2처럼 플러스 기호를 붙이지 말 것.
- JSON 문법 오류가 없도록 주의하고, 바로 파싱해서 사용할 수 있는 형태로 출력하라."
                });
            }
        }

        public void AppendContext(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = content
            });
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
