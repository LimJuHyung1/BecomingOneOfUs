using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineManager : MonoBehaviour
{
    public static LineManager Instance { get; private set; }   // 싱글톤

    [Header("UI References")]
    [SerializeField] private Text nameText;        // 캐릭터 이름 UI
    [SerializeField] private Text lineText;        // 대사 출력
    [SerializeField] private GameObject linePanel; // 대화 패널 (전체 UI)
    private Image[] slide;                         // 패널 안의 슬라이드 이미지들

    [Header("Settings")]
    [SerializeField] private float typingSpeed = 0.05f;   // 타이핑 속도

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;     // 음성 재생용

    // ScriptableObject용 대사 큐
    private readonly Queue<Line> lineQueue = new Queue<Line>();

    // NPC/플레이어 대사 큐
    private readonly Queue<Line> npcLineQueue = new Queue<Line>();

    private Coroutine typingCoroutine;
    private bool isTyping = false;

    // 현재 출력 중인 전체 문장(타이핑 스킵용)
    private string currentFullText = "";

    // GateConversationManager 등 외부에서 상태 확인용
    public bool IsTyping => isTyping;
    public bool HasQueuedLines => lineQueue.Count > 0 || npcLineQueue.Count > 0;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 전역 UI로 쓰고 싶으면 주석 해제
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 패널 안의 슬라이드 이미지 캐싱
        if (linePanel != null)
        {
            slide = linePanel.GetComponentsInChildren<Image>(true);
            linePanel.SetActive(false);
        }
    }




    // =============================================================
    // ScriptableObject 기반 대화 시작
    // =============================================================
    public void StartLine(LineSO lineSO)
    {
        if (lineSO == null || lineSO.dialogueLines == null || lineSO.dialogueLines.Length == 0)
            return;

        ClearAllQueues();

        foreach (var l in lineSO.dialogueLines)
        {
            lineQueue.Enqueue(l);
        }

        OpenPanelIfNeeded(0.6f);
        ShowNextLine();   // 첫 줄은 자동으로 출력 (외부에서 Space로 다음 줄 제어)
    }

    // =============================================================
    // NPC 대사 출력
    // =============================================================
    public void ShowNPCLine(string characterName, Color nameColor, string text, AudioClip clip = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Line line = new Line
        {
            characterName = characterName,
            line = text,
            audioClip = clip,
            nameColor = nameColor
        };

        EnqueueToNpcQueue(line, 0.3f);
    }

    // =============================================================
    // 플레이어 대사 출력
    // =============================================================
    public void ShowPlayerLine(string playerName, Color nameColor, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Line line = new Line
        {
            characterName = playerName,
            line = text,
            audioClip = null,   // 플레이어는 기본적으로 음성 없음
            nameColor = nameColor
        };

        EnqueueToNpcQueue(line, 0.3f);
    }

    // NPC/플레이어 공용 큐 로직
    private void EnqueueToNpcQueue(Line line, float fadeDuration)
    {
        npcLineQueue.Enqueue(line);

        OpenPanelIfNeeded(fadeDuration);

        // 현재 아무 것도 출력 중이 아니고, ScriptableObject 큐도 비어있다면
        // 이번에 들어온 대사를 바로 출력
        if (!isTyping && lineQueue.Count == 0 && npcLineQueue.Count == 1)
        {
            ShowNextLine();
        }
    }

    // 패널이 꺼져 있으면 켜고 페이드 인
    private void OpenPanelIfNeeded(float fadeDuration)
    {
        if (linePanel == null)
            return;

        if (!linePanel.activeSelf)
        {
            linePanel.SetActive(true);
            StartCoroutine(FadeInSlides(fadeDuration));
        }
    }

    // =============================================================
    // 다음 대사 출력 (SO + NPC 통합)
    // =============================================================
    public void ShowNextLine()
    {
        // 타이핑 중이면 → 즉시 완성
        if (isTyping)
        {
            FinishTypingInstantly();
            return;
        }

        Line nextLine = null;

        // 1순위: ScriptableObject 대사
        if (lineQueue.Count > 0)
        {
            nextLine = lineQueue.Dequeue();
        }
        // 2순위: NPC/플레이어 대사
        else if (npcLineQueue.Count > 0)
        {
            nextLine = npcLineQueue.Dequeue();
        }

        // 더 이상 대사가 없으면 종료
        if (nextLine == null)
        {
            EndDialogue();
            return;
        }

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLine(nextLine));
    }

    // =============================================================
    // 타이핑 코루틴
    // =============================================================
    private IEnumerator TypeLine(Line line)
    {
        isTyping = true;

        // 캐릭터 이름 UI 적용
        if (nameText != null)
        {
            nameText.text = line.characterName;
            nameText.color = line.nameColor;
        }

        // 오디오 재생
        if (audioSource != null && line.audioClip != null)
        {
            audioSource.Stop();
            audioSource.clip = line.audioClip;
            audioSource.Play();
        }

        currentFullText = line.line;
        if (lineText != null)
            lineText.text = "";

        foreach (char c in currentFullText)
        {
            if (lineText != null)
                lineText.text += c;

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    // =============================================================
    // 타이핑 즉시 완성
    // =============================================================
    private void FinishTypingInstantly()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (lineText != null)
            lineText.text = currentFullText;

        isTyping = false;
    }

    // =============================================================
    // 대화 종료
    // =============================================================
    private void EndDialogue()
    {
        // 패널을 완전히 닫는 것은 ScriptableObject 기반 대화나
        // 큐가 완전히 비었을 때만 발생
        if (linePanel != null)
        {
            StartCoroutine(FadeOutSlides(0.5f));
            linePanel.SetActive(false);
        }

        ClearAllQueues();
        currentFullText = "";
        isTyping = false;
        typingCoroutine = null;
    }

    private void ClearAllQueues()
    {
        lineQueue.Clear();
        npcLineQueue.Clear();
    }

    // =============================================================
    // 슬라이드 Fade 기능
    // =============================================================
    public IEnumerator FadeInSlides(float duration)
    {
        if (slide == null || slide.Length == 0)
            yield break;

        foreach (var img in slide)
        {
            if (img == null) continue;

            Color c = img.color;
            c.a = 0f;
            img.color = c;
            img.gameObject.SetActive(true);
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            foreach (var img in slide)
            {
                if (img == null) continue;

                Color c = img.color;
                c.a = Mathf.Lerp(0f, 1f, t);
                img.color = c;
            }

            yield return null;
        }
    }

    public IEnumerator FadeOutSlides(float duration)
    {
        if (slide == null || slide.Length == 0)
            yield break;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            foreach (var img in slide)
            {
                if (img == null) continue;

                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                img.color = c;
            }

            yield return null;
        }

        foreach (var img in slide)
        {
            if (img != null)
                img.gameObject.SetActive(false);
        }
    }
}
