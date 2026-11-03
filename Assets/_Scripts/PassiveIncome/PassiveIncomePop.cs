
using System.Collections;
using UnityEngine;

public class PassiveIncomePop : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private ParticleSystem currencyBurst;
    [SerializeField] private Collider clickCollider;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("Pop Animation")]
    [SerializeField] private float squashDuration = 0.09f;
    [SerializeField] private float growDuration = 0.16f;
    [SerializeField] private float popDuration = 0.07f;

    [SerializeField] private float squashScale = 0.9f;
    [SerializeField] private float growScale = 1.3f;

    private Vector3 originalScale;
    private bool isPlaying;

    private void Awake()
    {
        if (visual != null)
            originalScale = visual.localScale;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnMouseDown()
    {
        PlayPop();
    }

    public void PlayPop()
    {
        if (isPlaying || visual == null || currencyBurst == null)
            return;

        isPlaying = true;

        if (clickCollider != null)
            clickCollider.enabled = false;

        StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        // 1. Сжатие
        yield return ScaleTo(squashScale, squashDuration);

        // 2. Увеличение
        yield return ScaleTo(growScale, growDuration);

        // 3. Лопается
        yield return ScaleTo(0f, popDuration);

        visual.gameObject.SetActive(false);

        // 4. Звук хлопка
        if (audioSource != null && popSound != null)
            audioSource.PlayOneShot(popSound, soundVolume);

        // 5. Салют из валюты
        currencyBurst.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        currencyBurst.Play(true);
    }

    private IEnumerator ScaleTo(float multiplier, float duration)
    {
        Vector3 start = visual.localScale;
        Vector3 target = originalScale * multiplier;

        float elapsed = 0f;
        duration = Mathf.Max(0.001f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            visual.localScale = Vector3.Lerp(start, target, t);

            yield return null;
        }

        visual.localScale = target;
    }

    [ContextMenu("Test Pop")]
    private void TestPop()
    {
        PlayPop();
    }

    public void ResetPop()
    {
        StopAllCoroutines();

        if (audioSource != null)
            audioSource.Stop();

        if (currencyBurst != null)
        {
            currencyBurst.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (visual != null)
        {
            visual.gameObject.SetActive(true);
            visual.localScale = originalScale;
        }

        if (clickCollider != null)
            clickCollider.enabled = true;

        isPlaying = false;
    }
}
