using UnityEngine;

public class AnimatorDisabler : MonoBehaviour
{
    void Start()
    {
        DisableAllChildAnimators(transform);
    }

    private void DisableAllChildAnimators(Transform parent)
    {
        foreach (Transform child in parent)
        {
            Animator animator = child.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
            // Recursively disable animators in child GameObjects
            DisableAllChildAnimators(child);
        }
    }

    private void OnEnable()
    {
        DisableAllChildAnimators(transform);
    }
}
