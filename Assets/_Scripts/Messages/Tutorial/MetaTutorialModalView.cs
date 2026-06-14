using System;
using PlanetBuilder.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Tutorial
{
    public class MetaTutorialModalView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private Button _button;

        private Action _onConfirmed;

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(Confirm);

            SetModalOpen(true);
        }

        private void OnDisable()
        {
            MessageTutorialTrace.LogHide(
                "MetaTutorialModalView.OnDisable",
                gameObject,
                "_onConfirmed cleared by disable");

            if (_button != null)
                _button.onClick.RemoveListener(Confirm);

            SetModalOpen(false);
            _onConfirmed = null;
        }

        public void Show(
            string title,
            string description,
            string reward,
            string buttonText,
            Action onConfirmed)
        {
            if (_titleText != null)
                _titleText.text = title;

            if (_descriptionText != null)
                _descriptionText.text = description;

            if (_rewardText != null)
            {
                _rewardText.text = reward;
                _rewardText.gameObject.SetActive(!string.IsNullOrEmpty(reward));
            }

            if (_buttonText != null)
                _buttonText.text = buttonText;

            _onConfirmed = onConfirmed;
            gameObject.SetActive(true);
        }

        private void Confirm()
        {
            Action confirmed = _onConfirmed;
            MessageTutorialTrace.LogHide(
                "MetaTutorialModalView.Confirm",
                gameObject,
                $"hasConfirmedCallback={confirmed != null}");
            gameObject.SetActive(false);
            confirmed?.Invoke();
        }

        private static void SetModalOpen(bool isOpen)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.SetModalWindowOpen(isOpen);
        }
    }
}
