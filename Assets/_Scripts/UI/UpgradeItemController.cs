using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItemController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private TextMeshProUGUI _itemScore;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    public event Action OnUpgradeItemBuyButtonClicked, OnUpdateWorkFinished;

    private void Awake()
    {
        _buyButton.onClick.AddListener(HandleBuyButton);
        ToggleBuyButton(false);
    }

    public void Prepare(Sprite icon)
    {
        _itemImage.sprite = icon;
    }

    private void HandleBuyButton()
    {
        OnUpgradeItemBuyButtonClicked?.Invoke();
        _buyButton.gameObject.SetActive(false);
    }

    public void SetBuyPrice(double price)
    {
        _buyButtonText.text = $"{price.ToString("N0")}";
    }

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;

    public void SetIncome(double score)
    {
        _itemScore.text = $"{score.ToString("N0")}";
    }

    public void StartWork(float delay)
    => StartCoroutine(MakeProgress(delay));

    private IEnumerator MakeProgress(float delay)
    {
        float timePassed = 0;
        while (timePassed < delay)
        {
            timePassed += Time.deltaTime;
            float progress = Mathf.Clamp01(timePassed / delay);
            yield return null;
        }
        OnUpdateWorkFinished?.Invoke();
    }

    public void ResetProgress()
    {
        StopAllCoroutines();
    }

    private void OnDisable()
    {
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }
}
