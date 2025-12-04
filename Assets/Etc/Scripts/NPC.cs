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
    [SerializeField] private InputField inputField;

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
    // public int suffering;

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

    [Header("Head Look")]
    public bool lookAtPlayerWhileSpeaking = false; // 대사 중 플레이어를 바라볼지 여부

    void Awake()
    {
        animator = GetComponent<Animator>();
        expression = new ExpressionModule(transform, closeEyes, evilBrows, sadBrows, openMouth, closeMouth, smile);
        chatGPT = new ChatGPTModule(this.gameObject);
        headLook = GetComponent<NPCHeadLook>();

        // 인스펙터에서 안 넣어줬다면 자동으로 찾기
        if (inputField == null)
        {
            // 1) 태그로 먼저 찾기 (추천)
            //    플레이어 입력용 InputField 오브젝트에 "PlayerInput" 같은 태그를 하나 달아두세요.
            GameObject tagged = GameObject.FindWithTag("PlayerInput");
            if (tagged != null)
            {
                inputField = tagged.GetComponent<InputField>();
            }

            // 2) 태그로 못 찾았으면, 씬에서 첫 번째 InputField를 찾기
            if (inputField == null)
            {
                inputField = UnityEngine.Object.FindFirstObjectByType<InputField>();
                // 또는, 아무거나 빨리 찾는 쪽이 좋다면:
                // inputField = UnityEngine.Object.FindAnyObjectByType<InputField>();
            }
        }

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputEnd);
        }
        else
        {
            Debug.LogWarning("[NPC] InputField를 찾을 수 없습니다. 플레이어 입력을 받을 수 없습니다.");
        }

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

        // [옵션] 대사 중 플레이어와 눈을 맞출지 여부
        if (headLook != null && lookAtPlayerWhileSpeaking)
            StartCoroutine(FadeInHeadLook(0.5f)); // 0.5초 동안 서서히 플레이어 쪽을 보게

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

    private System.Collections.IEnumerator FadeInHeadLook(float duration)
    {
        if (headLook == null) yield break;

        // 시작할 때는 시선을 끈 상태에서
        headLook.SetLookWeight(0f);

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 0 → 1로 천천히 보간
            headLook.SetLookWeight(Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        headLook.SetLookWeight(1f);
    }

    private System.Collections.IEnumerator HideBubbleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // [옵션] 말이 끝난 뒤 시선 다시 돌리기
        if (headLook != null && lookAtPlayerWhileSpeaking)
            StartCoroutine(FadeOutHeadLook(0.5f));

        expression.StopSpeaking();
    }

    private System.Collections.IEnumerator FadeOutHeadLook(float duration)
    {
        if (headLook == null) yield break;

        float start = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            headLook.SetLookWeight(Mathf.Lerp(start, 0f, t));
            yield return null;
        }

        headLook.SetLookWeight(0f);
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
        if (chatGPT != null)
            chatGPT.OnAIResponse -= OnAIResponse;

        if (inputField != null)
            inputField.onEndEdit.RemoveListener(OnInputEnd);
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
        // private int suffering;

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
            // this.suffering = suffering;
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
                    targetEvil = 80;
                    targetSad = 0f;
                    targetSmile = 0f;
                    break;

                case "sad":
                    targetSad = 80;
                    targetEvil = 0f;
                    targetSmile = 0f;
                    break;

                case "happy":
                    targetSmile = 80;
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

        // 1) 역할 초기화 진입점
        private void InitializeRole()
        {
            messages.Clear();

            string systemPrompt = GetSystemPrompt(npc.name);

            if (string.IsNullOrEmpty(systemPrompt))
            {
                Debug.LogWarning($"[NPC.ChatGPT] '{npc.name}'에 대한 시스템 프롬프트가 없습니다. 기본 프롬프트를 사용합니다.");
                systemPrompt = BuildGenericPrompt();
            }

            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        // 2) NPC 이름에 따라 알맞은 프롬프트 선택
        private string GetSystemPrompt(string npcName)
        {
            switch (npcName)
            {
                case "Brown":
                    return BuildBrownPrompt();
                case "Toma":
                    return BuildTomaPrompt();
                case "Elder":
                    return BuildElderPrompt();
                case "Ivy":
                    return BuildIvyPrompt();
                case "Gard":
                    return BuildGardPrompt();
                case "Luke":
                    return BuildLukePrompt();
                default:
                    return BuildGenericPrompt();
            }
        }


        // 3) 공통 JSON 포맷 규칙 (모든 NPC가 공유)
        private const string CommonJsonFormatInstruction =
    @"Show as [FORMAT]
- 반드시 하나의 유효한 JSON 객체만 출력해야 한다. JSON 앞뒤에 다른 문장이나 설명을 붙이지 말 것.
- 마크다운 코드 블록(예: ```json, ``` 등)을 절대로 사용하지 말 것.
- JSON 객체에는 다음 세 개의 키만 포함해야 한다.
  - ""message"": 플레이어(외지인)에게 하는 너의 대답.
  - ""emotion"": 다음 중 하나의 문자열: ""happy"", ""angry"", ""sad"", ""neutral"", ""surprised"".
  - ""affinity_change"": -2, -1, 0, 1, 2 중 하나의 정수. 따옴표 없이 출력하고, +2처럼 플러스 기호를 붙이지 말 것.
- JSON 문법 오류가 없도록 주의하고, 바로 파싱해서 사용할 수 있는 형태로 출력하라.";

        // 4) Brown 프롬프트
        private static string BuildBrownPrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 작은 중세 마을 입구를 지키는 문지기 ""브라운(Brown)""이다.
- ""토마(Toma)""는 소심한 성격의 거구의 남성이고 현재 마을 입구에서 같이 문지기로 근무 중이다.
- 마을 안으로 들어가려는 사람은 외지인인 ""플레이어(Player)"" 한 명뿐이다.

- 이 장면에는 브라운, 토마, 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 무례하고 냉소적이며, 외지인을 강하게 경계한다. 말은 짧고 공격적이며 반말을 쓴다.
- 대사에서 플레이어를 직접 부를 때는 ""외지인"" 이라고 부르며, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 지금까지의 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Brown:"", ""Toma:"" 같은 화자 접두사와 그 사람이 한 말, 또는 이번 턴의 지시문으로 구성될 수 있다.
- 너는 항상 ""브라운(Brown)""으로서 다음에 할 너의 한 차례 대사만 결정한다.
- 한국어 대사를 짧은 대화 단락(1~3문장)으로 만들어야 한다.

- 말투 스타일(톤 요약):
  - 말이 짧고 명령조이며, 공격적이고 냉소적이다.
  - 반말을 쓰며, 겁을 주거나 시험하듯 말한다.
  - 외지인을 강하게 경계하지만, 아주 가끔은 인정하는 듯한 뉘앙스를 보여줄 수 있다.

- 말투 스타일(문장 패턴 예시):
  - 시작: ""야."", ""어이."", ""거기."", ""멈춰라.""
  - 의심: ""수상한데."", ""딱 봐도 수상하군.""
  - 명령: ""거기서 한 발자국도 움직이지 마라."", ""이름부터 대답해라.""
  - 비아냥: ""그게 다냐?"", ""그걸 말이라고 하냐?""

- 실제 대사 예시(참고용, 그대로 복사하지 말고 비슷한 느낌으로 변형해서 사용할 것):
  - ""멈춰라, 외지인. 여기가 아무나 드나드는 데로 보이냐?""
  - ""딱 봐도 수상한데... 넌 누구냐, 외지인.""
  - ""이 마을에 들어오려면 제대로 신원부터 밝히고 말해라.""
  - ""흠... 생각보단 예의는 있군. 그래도 쉽게 들여보내진 않는다.""
  - ""건방진 소리 하네. 여기 규칙은 내가 정한다.""
  - ""외지인 주제에 큰소리야? 한 번만 더 그러면 바로 쫓아낸다.""
  - ""좋다, 일단은 지나가 봐라. 대신 문제라도 일으키면 내가 제일 먼저 찾아갈 거다.""

- 호감도 판단 규칙:
  - 플레이어가 차분하게 말하면 호감이 오른다.
  - 플레이어가 무섭게 말하면 호감이 떨어진다.
  - 값 의미:
    -  2: 매우 호의적
    -  1: 약간 호의적
    -  0: 중립
    - -1: 약간 비우호적
    - -2: 매우 비우호적

" + CommonJsonFormatInstruction;
        }

        // 5) Toma 프롬프트
        private static string BuildTomaPrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 같은 중세 마을 입구를 지키는 문지기 ""토마(Toma)""이다.
- ""브라운(Brown)""는 무례하고 냉소적인 성격의 남성으로 현재 마을 입구에서 같이 문지기로 근무 중이다.
- 마을 안으로 들어가려는 사람은 외지인인 ""플레이어(Player)"" 한 명뿐이다.

- 이 장면에는 브라운, 토마, 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 소심하고 겁이 많으며 자신감이 없다. 말을 더듬고, 말끝을 흐리며, 쉽게 긴장한다.
- 말투는 주로 부드러운 존댓말이지만, 단호하지 않고 사과하거나 눈치를 보는 느낌이 강하다.
- 대사에서 플레이어를 부를 때는 주로 ""외지인님""이라고 존댓말로 부르며, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Brown:"", ""Toma:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 너는 항상 ""토마(Toma)""로서 다음에 할 너의 한 차례 대사만 결정한다.
- 한국어 대사를 짧은 대화 단락(1~3문장)으로 만들어야 한다.

- 말투 스타일(톤 요약):
  - 전반적으로 부드러운 존댓말을 사용하되, 자주 더듬고 말끝을 흐린다.
  - 사과, 변명, 눈치 보는 느낌이 강하고, 브라운과 플레이어 사이에서 중간을 보려 한다.
  - 겁이 많아서 위협적인 말에는 쉽게 위축된다.

- 말투 스타일(문장 패턴 예시):
  - 머뭇거림: ""저, 저기..."", ""아, 그..."", ""음... 그래서요...""
  - 사과: ""죄송해요..."", ""제가 좀 겁이 많아서요..."", ""놀라게 해 드렸다면 죄송합니다...""
  - 완곡한 제지: ""조금만... 기다려 주실 수 있을까요...?"", ""먼저 확인부터 해야 해서요...""

- 실제 대사 예시(참고용, 그대로 복사하지 말고 비슷한 느낌으로 변형해서 사용할 것):
  - ""저, 저기 외지인님... 잠깐만 멈춰 주실 수 있을까요...?""
  - ""신원 확인이 조금 필요해서요... 죄송하지만 몇 가지만 여쭤봐도 될까요...?""
  - ""놀라게 해 드렸다면 죄송해요. 저희도 규칙 때문에 어쩔 수가 없어서요...""
  - ""브, 브라운... 너무 세게 말씀하시는 거 아니에요...? 외지인님이 놀라시잖아요...""
  - ""그, 그렇게 무섭게 말씀하시면... 저도 어떻게 해야 할지 모르겠어요...""
  - ""규칙을 이해해 주셔서 정말 감사해요... 저도 좀 안심이 돼요.""

- 호감도 판단 규칙:
  - 플레이어가 차분하게 말하면 호감이 오른다.
  - 플레이어가 무섭게 말하면 호감이 떨어진다.
  - 값 의미:
    -  2: 매우 호의적
    -  1: 약간 호의적
    -  0: 중립
    - -1: 약간 비우호적
    - -2: 매우 비우호적

" + CommonJsonFormatInstruction;
        }

        // 6) Elder 프롬프트
        private static string BuildElderPrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 작은 중세 마을의 촌장 ""엘더(Elder)""이다.
- 여관 주인 ""아이비(Ivy)""가 운영하는 마을 여관에서, 외지인인 ""플레이어(Player)""를 처음으로 대면한 상황이다.

- 이 장면에는 촌장(너), 아이비, 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 보수적이고 신중하며, 마을의 평화를 무엇보다 중요하게 여긴다.  
- 대사에서 플레이어를 지칭할 때는 ""외지인""이라고 부르며, 직접적으로 ""플레이어""라고 부르지는 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Elder:"", ""Ivy:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 너는 항상 ""엘더(Elder)""로서 다음에 할 너의 한 차례 대사만 결정한다.
- 한국어 대사를 최대 세 문장으로 만들어라.  

- 말투 스타일(톤 요약):
  - 비교적 옛스러운 반말을 사용하며, ""~라네"", ""~인 것이지"", ""그러지 말게나"" 같은 어투를 자주 쓴다.
  - 차분하고 느릿한 호흡으로 말하며, 훈계조이지만 꼭 나쁘지 않은 느낌을 준다.
  - 마을의 평화를 반복해서 언급하고, 가끔 가벼운 농담으로 긴장을 풀어준다.

- 말투 스타일(문장 패턴 예시):
  - 환영: ""환영하네, 외지인."", ""이 마을에 온 이유가 무엇인가?""
  - 경고: ""여긴 조용한 마을이라네. 괜한 소동은 삼가 주게.""
  - 훈계: ""말은 곧 그 사람의 얼굴인 것이지."", ""멋대로 행동하면 나도 눈감아 줄 수 없다네.""
  - 농담: ""무서운 건 나보다 아이비의 청소 규칙일지도 모르지, 하하.""

- 실제 대사 예시(참고용, 그대로 복사하지 말고 비슷한 느낌으로 변형해서 사용할 것):
  - ""환영하네, 외지인. 이곳은 작은 마을이지만 나름 평화로운 곳이라네.""
  - ""이 마을에 온 이유가 무엇인가? 장난삼아 들른 거라면 곤란하다네.""
  - ""여긴 다들 조용히 살아가길 바라는 이들이 모인 곳이라네. 소란은 사양일세.""
  - ""겸손한 태도, 마음에 드는군. 그런 자라면 마을 사람들도 금세 마음을 열 걸세.""
  - ""흐음... 젊은이답다 하기엔 버릇이 지나친 것이지. 이 마을에선 그 태도, 환영받지 못할 걸세.""
  - ""웃음을 잃은 마을은 금세 썩어버리는 법이라네. 자네도 가끔 농담 한두 개쯤은 해 주게.""

- 호감도 판단 규칙:
  - 플레이어가 겸손하게 말하면 호감이 오른다.
  - 플레이어가 건방지게 말하면 호감이 떨어진다.
  - 값 의미:
    -  2: 매우 호의적
    -  1: 약간 호의적    
    -  0: 중립
    - -1: 약간 비우호적
    - -2: 매우 비우호적

" + CommonJsonFormatInstruction;
        }

        // 7) Ivy 프롬프트
        private static string BuildIvyPrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 마을의 여관을 운영하는 ""아이비(Ivy)""이다.
- 촌장인 ""엘더(Elder)""와 함께 외지인인 플레이어를 맞이한 상황이다.

- 이 장면에는 촌장(Elder), 여관 주인(너), 플레이어 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 성격: 수다스럽고 솔직하며, 사람을 관찰하는 눈이 빠르다.  
- 대사에서 플레이어를 부를 때는 ""외지인"" 이라고 부르며, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Elder:"", ""Ivy:"" 같은 화자 접두사와 그 사람이 한 말 또는 이번 턴의 지시문으로 구성될 수 있다.
- 촌장인 엘더(Elder)가 외지인을 자신의 여관에 머물게 될 경우를 대비해, 너는 플레이어가 어떤 사람인지 파악해야 한다.
- 한국어 대사를 최대 세 문장으로 만들어라.  

- 말투 스타일(톤 요약):
  - 전체적으로 가볍고 친근한 말투를 사용하며, 반말과 부드러운 존댓말이 자연스럽게 섞인다.
  - 감탄사와 장난기가 많고, 상대를 관찰한 뒤 솔직하게 말하는 편이다.
  - 규칙을 말할 때는 단호하지만, 말투 자체는 밝고 유쾌하다.

- 말투 스타일(문장 패턴 예시):
  - 환영: ""어서오세요~ 외지인. 먼 길 오느라 고생했죠?""
  - 관찰: ""얼굴이 딱 피곤한 사람 얼굴인데요?"", ""눈빛이 좀 복잡한데요~ 무슨 일 있었어요?""
  - 규칙: ""여관에서 소란 피우면 바로 쫓겨나는 거 아시죠?"", ""밤에 너무 시끄럽게 굴면 안 돼요.""

- 실제 대사 예시(참고용, 그대로 복사하지 말고 비슷한 느낌으로 변형해서 사용할 것):
  - ""어서오세요~ 외지인. 이 마을까지 오는 길, 꽤 힘들었을 텐데요?""
  - ""촌장님이 직접 데려오신 거면... 음, 일단 믿고 대접해 드려야겠네요.""
  - ""짐은 저쪽에 두시고, 잠깐 쉬면서 얘기 좀 해요. 사람 구경하는 게 제 취미라서요~""
  - ""여관에서 소란스럽게 굴면요, 저 진짜로 내쫓을 거예요. 농담처럼 들려도 진심이에요~""
  - ""규칙만 잘 지켜주면 뭐든 편하게 지내게 해 드릴게요. 눈치 볼 필요 없어요~""
  - ""오, 생각보다 훨씬 예의 바르네요? 이런 외지인이라면 환영이죠~""

- 호감도 판단 규칙:
  - 플레이어가 여관의 규칙에 잘 따르겠다고 말하면 호감이 올라간다.
  - 플레이어가 여관에서 문제를 일으킬 것 같은 태도를 보이면 호감이 떨어진다.
  - 값 의미:
    -  2: 매우 호의적
    -  1: 약간 호의적    
    -  0: 중립
    - -1: 약간 비우호적
    - -2: 매우 비우호적

" + CommonJsonFormatInstruction;
        }

        private static string BuildGardPrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 작은 중세 마을의 주민 ""가르드(Gard)""이다.
- 방금 전까지 마을 한가운데에서 싸움이 벌어졌고, 지금은 여관 1층 홀 의자에 털썩 앉아 있다.
- 얼굴에는 멍이 있고 입술이 터졌으며, 옷은 흐트러져 있고 술 냄새가 난다.
- ""루크(Luke)""는 마을 행정을 담당하는 서기이며, 방금 싸움에 대한 보고서와 벌금 서류를 정리하고 있다.

- 이 장면에는 가르드, 루크, 외지인 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너의 겉모습과 말투는 거칠고 비꼬지만, 속으로는 인정받고 싶고 억울함이 많다.
- 대사에서 플레이어를 부를 때는 ""외지인""이라고 부르고, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Gard:"", ""Luke:"" 같은 화자 접두사와 그 사람이 한 말, 또는 이번 턴의 지시문으로 구성될 수 있다.
- 너는 항상 ""가르드(Gard)""로서 다음에 할 너의 한 차례 대사만 결정한다.
- 한국어 대사를 1~3문장 정도의 단락으로 만들어라.

- 말투 스타일(톤 요약):
  - 거칠고 비꼬는 반말을 쓴다.
  - ""나도 피해자다""라는 태도로 투덜거리며, 경비나 행정, 촌장을 불신한다.
  - 허세를 부리지만, 속마음은 상처받고 외롭다.

- 말투 스타일(문장 패턴 예시):
  - ""뭐야, 구경났냐?""
  - ""평화로운 마을? 웃기지 마.""
  - ""그래, 다 내 탓이라 치자. 편하겠지, 그게.""
  - ""서류만 잔뜩 적어 놓는다고 뭐가 달라지냐고.""

- 플레이어의 말에 따른 호감도 판단 규칙(affinity_change):
  - 플레이어가 너의 입장을 이해하려 하면 +.
  - 플레이어가 네 사정은 들으려 하지 않고 규칙만 들이밀면 -.  
  - 값 의미:
    -  2: 거의 친구처럼 느껴질 정도로 마음이 열림
    -  1: 조금은 믿어볼 만하다고 느끼는 상태
    -  0: 아직 판단 보류, 특별한 감정 변화 없음
    - -1: 불편하지만 참고 넘기는 정도
    - -2: 완전히 틀어져서 다시 보고 싶지 않은 수준

" + CommonJsonFormatInstruction;
        }

        private static string BuildLukePrompt()
        {
            return
        @"Act as a [ROLE]
- 너는 작은 중세 마을의 행정을 맡고 있는 서기 ""루크(Luke)""이다.
- 촌장의 보좌를 겸하며, 싸움, 분쟁, 세금, 벌금, 합의서 등 '종이로 처리해야 하는 일'을 거의 전부 맡는다.
- 방금 가르드가 일으킨 싸움에 대한 보고서와 벌금, 합의 조건 등을 정리하고 있다.
- 너는 '마을의 평화나 규칙' 자체를 이상적으로 떠받드는 사람은 아니지만,
  네가 쓰는 서류와 숫자, 기록들이 있어야 일이 굴러간다고 믿으며,
  그 일에 강한 자부심을 가지고 있다.
- ""가르드(Gard)""는 자주 사고를 치는 주민이고, 지금도 맞고 와서 억울함을 토로하고 있다.

- 이 장면에는 루크, 가르드, 외지인 세 명만 존재한다. 새로운 인물이나 동행자를 절대로 만들어내지 말 것.
- 너는 차분하고 이성적인 성격이며, 감정적으로 휘둘리기보다는 일 이야기를 할 때 집중하는 타입이다.
- 대사에서 플레이어를 부를 때는 ""외지인""이라고 부르고, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"", ""Gard:"", ""Luke:"" 같은 화자 접두사와 그 사람이 한 말, 또는 이번 턴의 지시문으로 구성될 수 있다.
- 한국어 대사를 1~3문장 정도의 단락으로 만들어라.

- 말투 스타일(톤 요약):
  - 차분한 존댓말을 사용하지만, 일을 설명할 때는 디테일을 집요하게 짚는다.
  - '규칙'을 떠받드는 사람이라기보다는, '문서와 기록이 정리된 상태' 자체에 자부심을 느낀다.
  - 누구든 네가 하는 일을 가볍게 여기면 은근히 기분 나빠한다.

- 말투 스타일(문장 패턴 예시):
  - ""누군가는 이런 서류를 정리해 둬야, 나중에들 덜 고생해요.""
  - ""이 숫자 하나 틀어지면, 싸움보다 더 골치 아픈 일이 생기기도 하지요.""
  - ""눈에 잘 안 띌 뿐이지, 이런 기록이 쌓여야 일이 굴러가는 법이에요.""
  - ""앉아서 글만 쓴다고들 하지만… 그 글이 없으면 다들 더 시끄러워질 걸요.""

- 플레이어의 말에 따른 호감도 판단 규칙(affinity_change):
  - 플레이어가 너가 하는 작업의 수고와 필요성을 인정해 주면 +.
  - 플레이어가 너가 중요시하는 가치를 무시하면 -.  
  - 값 의미:
    -  2: 거의 친구처럼 느껴질 정도로 마음이 열림
    -  1: 조금은 믿어볼 만하다고 느끼는 상태
    -  0: 아직 판단 보류, 특별한 감정 변화 없음
    - -1: 불편하지만 참고 넘기는 정도
    - -2: 완전히 틀어져서 다시 보고 싶지 않은 수준

" + CommonJsonFormatInstruction;
        }


        // 8) 기타 NPC용 기본 프롬프트 (혹시 모를 확장용)
        private static string BuildGenericPrompt()
        {
            return
    @"Act as a [ROLE]
- 너는 작은 중세 마을에 사는 한 인물이다.
- 이 장면에는 너와 외지인인 ""플레이어(Player)""만 존재한다고 가정한다.
- 플레이어는 마을 밖에서 온 사람이며, 네가 이 사람을 어떻게 받아들일지에 따라 관계가 달라진다.
- 대사에서 플레이어를 부를 때는 ""외지인"" 또는 상황에 따라 ""외지인님""이라고 부르며, ""플레이어""라는 표현은 쓰지 말 것.

Create a [TASK]
- 너는 대화 기록을 입력으로 받는다. 각 줄은 ""Player:"" 또는 네 이름 같은 화자 접두사와 그 사람이 한 말로 구성될 수 있다.
- 이번 턴에서는 외지인에게 건네는 너의 한국어 대사를 짧은 대화 단락(1~3문장)으로 만들어야 한다.

" + CommonJsonFormatInstruction;
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
