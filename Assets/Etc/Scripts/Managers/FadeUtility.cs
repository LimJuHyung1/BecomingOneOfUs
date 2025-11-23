using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/*
======================================================================
FadeUtility 사용 가이드 (리팩토링 버전)
======================================================================

이 스크립트는 UI의 Image, Text 등 Graphic 요소를 자연스럽게
Fade In / Fade Out / Fade In-Out 반복시키기 위한 유틸리티입니다.

싱글톤 구조이므로 다음과 같이 어디서든 호출할 수 있습니다:
    FadeUtility.Instance.함수명();


----------------------------------------------------------------------
1. Fade In (UI가 서서히 나타남)
----------------------------------------------------------------------
FadeUtility.Instance.FadeIn(targetGraphic, duration);

예시:
FadeUtility.Instance.FadeIn(myImage, 1.0f);
// → 1초간 UI가 0 → 1로 점점 보임


----------------------------------------------------------------------
2. Fade Out (UI가 서서히 사라짐)
----------------------------------------------------------------------
FadeUtility.Instance.FadeOut(targetGraphic, duration);

예시:
FadeUtility.Instance.FadeOut(myText, 1.2f);
// → 1.2초간 UI가 1 → 0으로 점점 사라지고,
//    사라진 뒤 GameObject는 자동 비활성화됨


----------------------------------------------------------------------
3. Fade In-Out 반복
     (UI가 깜빡이는 효과, 알림/주의 메시지 등에 활용)
----------------------------------------------------------------------
FadeUtility.Instance.FadeInOut(targetGraphic, duration, delay);

예시:
FadeUtility.Instance.FadeInOut(myIcon, 0.8f, 0.3f);
// → 0.8초 페이드인 → 0.3초 대기 → 0.8초 페이드아웃 반복


----------------------------------------------------------------------
4. 반복 중단
----------------------------------------------------------------------
FadeUtility.Instance.StopFadeInOut();

예시:
FadeUtility.Instance.StopFadeInOut();
// → 깜빡이는 효과 종료


----------------------------------------------------------------------
5. 진행 중인 단일 Fade 강제 중단
----------------------------------------------------------------------
FadeUtility.Instance.StopCurrentFade();

예시:
FadeUtility.Instance.StopCurrentFade();
// → 실행 중이던 FadeIn 또는 FadeOut 즉시 종료


----------------------------------------------------------------------
6. Graphic 타입 주의사항
----------------------------------------------------------------------
Fade 기능은 다음 UI 컴포넌트들과 함께 사용 가능합니다:

- Image
- Text
- RawImage
- Outline/Shadow 포함 Graphic 파생 UI
- TMP_Text (TextMeshPro) → 단, TMP_Text는 "올바른 Graphic 서브클래스"이며 정상 작동함

CanvasGroup도 가능하지만 이 스크립트는 Graphic 기반입니다.


----------------------------------------------------------------------
7. 주로 활용되는 패턴 예시
----------------------------------------------------------------------

// 화면 전체 페이드용 Black Image
FadeUtility.Instance.FadeIn(blackPanel, 1f);       // 화면 어둡게 등장
FadeUtility.Instance.FadeOut(blackPanel, 1f);      // 화면 밝아지면서 사라짐

// 경고 메시지 깜빡임
FadeUtility.Instance.FadeInOut(warningText, 0.5f);

// 특정 조건에서 깜빡임 종료
FadeUtility.Instance.StopFadeInOut();

======================================================================
*/


public class FadeUtility : MonoBehaviour
{
    public static FadeUtility Instance { get; private set; }

    private Coroutine fadeLoopCoroutine;   // FadeInOut 루프 코루틴 저장
    private Coroutine currentFadeCoroutine; // 단일 Fade 실행 코루틴 저장

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --------------------------------------------------------------
    // 기본 페이드 처리 함수
    // --------------------------------------------------------------
    private IEnumerator FadeRoutine(
        Graphic graphic,
        float duration,
        float startAlpha,
        float endAlpha,
        bool deactivateOnEnd = false)
    {
        if (graphic == null) yield break;

        graphic.gameObject.SetActive(true);

        Color color = graphic.color;
        color.a = startAlpha;
        graphic.color = color;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            graphic.color = color;

            yield return null;
        }

        // 보정
        color.a = endAlpha;
        graphic.color = color;

        if (deactivateOnEnd)
            graphic.gameObject.SetActive(false);
    }

    // --------------------------------------------------------------
    // FadeIn
    // --------------------------------------------------------------
    public void FadeIn(Graphic graphic, float duration, float targetAlpha = 1f)
    {
        StopCurrentFade();
        currentFadeCoroutine = StartCoroutine(FadeRoutine(graphic, duration, 0f, targetAlpha));
    }

    // --------------------------------------------------------------
    // FadeOut
    // --------------------------------------------------------------
    public void FadeOut(Graphic graphic, float duration, float startAlpha = 1f)
    {
        StopCurrentFade();
        currentFadeCoroutine = StartCoroutine(FadeRoutine(graphic, duration, startAlpha, 0f, true));
    }

    // --------------------------------------------------------------
    // FadeInOut 반복
    // --------------------------------------------------------------
    public void FadeInOut(Graphic graphic, float duration, float delay = 0.5f)
    {
        StopFadeInOut(); // 중복 실행 방지
        fadeLoopCoroutine = StartCoroutine(FadeInOutLoopRoutine(graphic, duration, delay));
    }

    private IEnumerator FadeInOutLoopRoutine(Graphic graphic, float duration, float delay)
    {
        while (true)
        {
            yield return FadeRoutine(graphic, duration, 0f, 1f);
            yield return new WaitForSeconds(delay);
            yield return FadeRoutine(graphic, duration, 1f, 0f);
            yield return new WaitForSeconds(delay);
        }
    }

    // --------------------------------------------------------------
    // 중단 함수
    // --------------------------------------------------------------
    public void StopCurrentFade()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = null;
        }
    }

    public void StopFadeInOut()
    {
        if (fadeLoopCoroutine != null)
        {
            StopCoroutine(fadeLoopCoroutine);
            fadeLoopCoroutine = null;
        }
    }
}
