using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class LineManager : MonoBehaviour
{
    public static LineManager Instance { get; private set; }   // 싱글톤

    [Header("UI References")]
    public Text nameText;            // 캐릭터 이름 UI
    public Text lineText;            // 대사 출력
    public GameObject linePanel;     // 대화 패널 (전체 UI)
    private Image[] slide;           // 슬라이드 이미지들

    [Header("Settings")]
    public float typingSpeed = 0.05f;   // 타이핑 속도

    [Header("Audio")]
    public AudioSource audioSource;     // 음성 재생용

    // ScriptableObject용 대사 큐
    private Queue<Line> lineQueue = new Queue<Line>();

    // NPC 대사 큐
    private Queue<Line> npcLineQueue = new Queue<Line>();

    private Coroutine typingCoroutine;
    private bool isTyping = false;

    // 현재 출력 중인 전체 문장(타이핑 스킵용)
    private string currentFullText = "";

    // ---- GateConversationManager에서 상태 확인용 프로퍼티 ----
    public bool IsTyping => isTyping;
    public bool HasQueuedLines => lineQueue.Count > 0 || npcLineQueue.Count > 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 전역 UI로 쓰고 싶으면 주석 해제
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =============================================================
    // 초기 설정
    // =============================================================
    void Start()
    {
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

        lineQueue.Clear();
        npcLineQueue.Clear();

        foreach (var l in lineSO.dialogueLines)
            lineQueue.Enqueue(l);

        linePanel.SetActive(true);
        StartCoroutine(FadeInSlides(0.6f));

        ShowNextLine();   // 첫 줄은 자동으로 출력
    }

    // =============================================================
    // NPC가 직접 텍스트를 표시할 때 사용
    // =============================================================
    public void ShowNPCLine(string characterName, Color nameColor, string text, AudioClip clip = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Line temp = new Line
        {
            characterName = characterName,
            line = text,
            audioClip = clip,
            nameColor = nameColor
        };

        npcLineQueue.Enqueue(temp);

        // 패널이 꺼져 있으면 켜고 페이드 인
        if (!linePanel.activeSelf)
        {
            linePanel.SetActive(true);
            StartCoroutine(FadeInSlides(0.3f));
        }

        // 현재 아무 것도 출력 중이 아니고, ScriptableObject 큐도 비어있다면
        // 이번에 들어온 NPC 대사를 바로 보여줌
        if (!isTyping && lineQueue.Count == 0 && npcLineQueue.Count == 1)
        {
            ShowNextLine();
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
        // 2순위: NPC 대사
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
    // 플레이어 대사 표시용 함수
    // =============================================================
    public void ShowPlayerLine(string playerName, Color nameColor, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Line temp = new Line
        {
            characterName = playerName,
            line = text,
            audioClip = null,      // 플레이어는 보통 음성 없음
            nameColor = nameColor
        };

        // 대사를 NPC 큐와 동일한 방식으로 처리
        npcLineQueue.Enqueue(temp);

        if (!linePanel.activeSelf)
        {
            linePanel.SetActive(true);
            StartCoroutine(FadeInSlides(0.3f));
        }

        // 지금 아무도 말하고 있지 않고(타이핑 X), SO 대사도 없다면 즉시 출력
        if (!isTyping && lineQueue.Count == 0 && npcLineQueue.Count == 1)
        {
            ShowNextLine();
        }
    }


    // =============================================================
    // 타이핑 코루틴
    // =============================================================
    IEnumerator TypeLine(Line line)
    {
        isTyping = true;

        // 캐릭터 이름 UI 적용
        nameText.text = line.characterName;
        nameText.color = line.nameColor;

        // 오디오 재생
        if (audioSource && line.audioClip)
        {
            audioSource.Stop();
            audioSource.clip = line.audioClip;
            audioSource.Play();
        }

        currentFullText = line.line;
        lineText.text = "";

        foreach (char c in currentFullText)
        {
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

        // 남은 글자 한 번에 출력
        lineText.text = currentFullText;
        isTyping = false;
    }

    // =============================================================
    // 대화 종료
    // =============================================================
    private void EndDialogue()
    {
        StartCoroutine(FadeOutSlides(0.5f));
        linePanel.SetActive(false);

        lineQueue.Clear();
        npcLineQueue.Clear();
        currentFullText = "";
        isTyping = false;
        typingCoroutine = null;
    }

    // =============================================================
    // ⚠️ 키 입력 처리는 이제 여기서 하지 않음
    //    → GateConversationManager가 Space를 처리
    // =============================================================

    // =============================================================
    // 슬라이드 Fade 기능 (기존 코드 그대로)
    // =============================================================
    public IEnumerator FadeInSlides(float duration)
    {
        if (slide == null || slide.Length == 0) yield break;

        foreach (var img in slide)
        {
            if (!img) continue;

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
                if (!img) continue;

                Color c = img.color;
                c.a = Mathf.Lerp(0f, 1f, t);
                img.color = c;
            }

            yield return null;
        }
    }

    public IEnumerator FadeOutSlides(float duration)
    {
        if (slide == null || slide.Length == 0) yield break;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            foreach (var img in slide)
            {
                if (!img) continue;

                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                img.color = c;
            }

            yield return null;
        }

        foreach (var img in slide)
            img?.gameObject.SetActive(false);
    }
}
