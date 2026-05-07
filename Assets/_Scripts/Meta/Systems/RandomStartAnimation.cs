using UnityEngine;

public class RandomStartAnimation : MonoBehaviour
{
    private Animation anim;

    void Start()
    {
        anim = GetComponent<Animation>();

        if (anim != null && anim.clip != null)
        {
            AnimationState state = anim[anim.clip.name];

            float randomTime = Random.Range(0f, state.length);
            state.time = randomTime;

            anim.Play(); // перезапуск с нового времени
        }
    }
}
