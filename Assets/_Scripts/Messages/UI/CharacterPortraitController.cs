using PlanetBuilder.Messages.Characters;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlanetBuilder.Messages.UI
{
    public class CharacterPortraitController : MonoBehaviour
    {
        [SerializeField] private CharacterDatabase _characterDatabase;
        [SerializeField] private Transform _portraitContainer;
        [SerializeField] private TMP_Text _characterNameText;

        private readonly Dictionary<GameObject, GameObject> _instancesByPrefab = new();
        private GameObject _currentPrefab;
        private GameObject _currentInstance;

        public void ShowCharacter(string speakerId, CharacterMood mood)
        {
            if (_characterDatabase == null ||
                !_characterDatabase.TryGetCharacter(speakerId, out CharacterData character))
            {
                Hide();
                return;
            }

            GameObject portraitPrefab = character.GetPortraitPrefab(mood);

            if (portraitPrefab == null)
            {
                Hide();
                return;
            }

            ShowPortrait(portraitPrefab);

            if (_characterNameText != null)
            {
                _characterNameText.text = character.CharacterName;
                _characterNameText.gameObject.SetActive(!string.IsNullOrEmpty(character.CharacterName));
            }
        }

        public void Hide()
        {
            if (_currentInstance != null)
                _currentInstance.SetActive(false);

            _currentPrefab = null;
            _currentInstance = null;

            if (_portraitContainer != null)
                _portraitContainer.gameObject.SetActive(false);

            if (_characterNameText != null)
            {
                _characterNameText.text = string.Empty;
                _characterNameText.gameObject.SetActive(false);
            }
        }

        private void ShowPortrait(GameObject portraitPrefab)
        {
            if (_portraitContainer == null)
                return;

            if (_currentPrefab == portraitPrefab && _currentInstance != null)
            {
                _portraitContainer.gameObject.SetActive(true);
                _currentInstance.SetActive(true);
                ResetTransform(_currentInstance.transform);
                return;
            }

            if (_currentInstance != null)
                _currentInstance.SetActive(false);

            if (!_instancesByPrefab.TryGetValue(portraitPrefab, out GameObject instance) || instance == null)
            {
                instance = Instantiate(portraitPrefab, _portraitContainer);
                _instancesByPrefab[portraitPrefab] = instance;
            }

            _currentPrefab = portraitPrefab;
            _currentInstance = instance;
            ResetTransform(_currentInstance.transform);
            _portraitContainer.gameObject.SetActive(true);
            _currentInstance.SetActive(true);
        }

        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
