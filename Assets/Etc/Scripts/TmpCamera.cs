using UnityEngine;

public class TmpCamera : MonoBehaviour
{
    [Header("Target NPC to Look At")]
    public Transform targetNPC;

    [Header("Camera Look Settings")]
    public float rotateSpeed = 5f;

    void Start()
    {
        // targetNPC 자동 탐색 (Unity 2023+ 최신 방식)
        if (targetNPC == null)
        {
            NPC npc = Object.FindFirstObjectByType<NPC>();
            if (npc != null)
            {
                targetNPC = npc.transform;
            }
            else
            {
                Debug.LogWarning("[TmpCamera] No NPC found in the scene.");
            }
        }
    }

    void LateUpdate()
    {
        if (targetNPC == null)
            return;

        Vector3 targetPos = targetNPC.position;
        Vector3 direction = targetPos - transform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotateSpeed
            );
        }
    }
}
