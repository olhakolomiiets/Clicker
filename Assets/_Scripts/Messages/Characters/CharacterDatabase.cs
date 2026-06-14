using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetBuilder.Messages.Characters
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "PlanetBuilder/Messages/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private List<CharacterData> _characters = new();

        private readonly Dictionary<string, CharacterData> _charactersById = new();
        private bool _isInitialized;

        public bool TryGetCharacter(string speakerId, out CharacterData character)
        {
            if (string.IsNullOrEmpty(speakerId))
            {
                character = null;
                return false;
            }

            EnsureInitialized();
            return _charactersById.TryGetValue(speakerId, out character);
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            _isInitialized = false;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            _charactersById.Clear();

            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterData character = _characters[i];

                if (character == null || string.IsNullOrEmpty(character.SpeakerId))
                    continue;

                if (_charactersById.ContainsKey(character.SpeakerId))
                {
                    Debug.LogWarning($"Duplicate character SpeakerId: {character.SpeakerId}", this);
                    continue;
                }

                _charactersById.Add(character.SpeakerId, character);
            }

            _isInitialized = true;
        }
    }

    [Serializable]
    public class CharacterData
    {
        [SerializeField] private string _speakerId;
        [SerializeField] private string _characterName;
        [SerializeField] private List<CharacterPortrait> _portraits = new();

        public string SpeakerId => _speakerId;
        public string CharacterName => _characterName;

        public GameObject GetPortraitPrefab(CharacterMood mood)
        {
            GameObject neutralPortraitPrefab = null;

            for (int i = 0; i < _portraits.Count; i++)
            {
                CharacterPortrait portrait = _portraits[i];

                if (portrait == null)
                    continue;

                if (portrait.Mood == mood)
                    return portrait.Prefab;

                if (portrait.Mood == CharacterMood.Neutral)
                    neutralPortraitPrefab = portrait.Prefab;
            }

            return neutralPortraitPrefab;
        }
    }

    [Serializable]
    public class CharacterPortrait
    {
        [SerializeField] private CharacterMood _mood;
        [SerializeField] private GameObject _prefab;

        public CharacterMood Mood => _mood;
        public GameObject Prefab => _prefab;
    }
}
