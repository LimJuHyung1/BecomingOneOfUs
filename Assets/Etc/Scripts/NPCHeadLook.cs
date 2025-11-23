using UnityEngine;

public class NPCHeadLook : MonoBehaviour
{
    private Animator animator;
    public Transform lookTarget;

    [Range(0, 1f)] public float lookWeight = 1f;   // 외부에서 설정하는 목표 값
    private float currentLookWeight = 0f;          // 실제 IK에 적용되는 값
    public float smoothSpeed = 2f;                 // 부드럽게 변하는 속도

    [Range(0, 1f)] public float bodyWeight = 0.3f;
    [Range(0, 1f)] public float headWeight = 1f;
    [Range(0, 1f)] public float eyesWeight = 1f;
    [Range(0, 1f)] public float clampWeight = 0.5f;

    private Vector3 smoothTargetPos;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || lookTarget == null)
            return;

        // 1) lookWeight를 currentLookWeight로 부드럽게 보간
        currentLookWeight = Mathf.Lerp(
            currentLookWeight,
            lookWeight,
            Time.deltaTime * smoothSpeed
        );

        // 2) 부드럽게 목표 위치 보간
        smoothTargetPos = Vector3.Lerp(
            smoothTargetPos,
            lookTarget.position,
            Time.deltaTime * smoothSpeed
        );

        animator.SetLookAtWeight(
            currentLookWeight,
            bodyWeight,
            headWeight,
            eyesWeight,
            clampWeight
        );

        animator.SetLookAtPosition(smoothTargetPos);
    }

    // 외부에서 자연스럽게 lookWeight를 변경하기 위한 메소드
    public void SetLookWeight(float value)
    {
        lookWeight = Mathf.Clamp01(value);
    }
}
