
using System;
using System.Collections.Generic;
using UnityEngine;

public class PassiveIncomeChestController : MonoBehaviour
{
    [Header("Chest Configuration")]
    [SerializeField] private List<Transform> _spawnPoints = new();
    [SerializeField] private List<GameObject> _chestPrefabs = new();

    public event Action<string, string> OnChestClicked;

    private GeneralGameData _generalData;
    private string _planetKey;

    private GameObject _activeChest;
    private string _activeRewardId;
    private int _activeSpawnIndex = -1;
    private int _activePrefabIndex = -1;

    private bool _initialized;

    public void Initialize(GeneralGameData generalData, string planetKey)
    {
        if (_planetKey != planetKey)
            ClearChest();

        _generalData = generalData;
        _planetKey = planetKey;

        _initialized = generalData != null &&
                       !string.IsNullOrEmpty(planetKey);

        if (!_initialized)
        {
            ClearChest();
            Debug.LogError("[PassiveIncomeChest] Invalid initialization.");
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (!_initialized)
            return;

        if (!TryGetReward(out OfflinePlanetRewardData reward))
        {
            ClearChest();
            return;
        }

        if (!ValidateConfiguration())
            return;

        if (!OfflinePlanetSaveReader.TryRead(_planetKey, out GameDataSave save, out string error))
        {
            Debug.LogWarning($"[PassiveIncomeChest] {error}");
            return;
        }

        int spawnIndex = Mathf.Max(0,
            save.PassiveIncomeChestSpawnPointIndex) % _spawnPoints.Count;

        int prefabIndex = Mathf.Max(0,
            save.PassiveIncomeChestPrefabIndex) % _chestPrefabs.Count;

        if (_activeChest != null &&
            _activeRewardId == reward.RewardId &&
            _activeSpawnIndex == spawnIndex &&
            _activePrefabIndex == prefabIndex)
        {
            return;
        }

        ClearChest();
        SpawnChest(spawnIndex, prefabIndex, reward.RewardId);
    }

    private bool TryGetReward(out OfflinePlanetRewardData reward)
    {
        reward = null;

        if (_generalData == null || !_generalData.OfflinePlanetRewardsEnabled || _generalData.OfflinePlanetRewards == null)
        {
            return false;
        }

        reward = _generalData.OfflinePlanetRewards.Find(r =>
            r != null &&
            r.PlanetKey == _planetKey &&
            !string.IsNullOrEmpty(r.RewardId) &&
            r.CreditedSeconds >= 60 &&
            r.MoneyX1 > 0d &&
            !double.IsNaN(r.MoneyX1) &&
            !double.IsInfinity(r.MoneyX1));

        return reward != null;
    }

    private bool ValidateConfiguration()
    {
        if (_spawnPoints.Count == 0 || _chestPrefabs.Count == 0)
        {
            Debug.LogError("[PassiveIncomeChest] Empty spawn points or prefabs list.");
            return false;
        }

        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            if (_spawnPoints[i] == null)
            {
                Debug.LogError($"[PassiveIncomeChest] Missing spawn point {i}.");
                return false;
            }
        }

        for (int i = 0; i < _chestPrefabs.Count; i++)
        {
            if (_chestPrefabs[i] == null)
            {
                Debug.LogError($"[PassiveIncomeChest] Missing prefab {i}.");
                return false;
            }
        }

        return true;
    }

    private void SpawnChest(int spawnIndex, int prefabIndex, string rewardId)
    {
        Transform point = _spawnPoints[spawnIndex];
        GameObject prefab = _chestPrefabs[prefabIndex];

        GameObject instance = Instantiate(prefab, point);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Collider collider = instance.GetComponentInChildren<Collider>();

        if (collider == null)
        {
            Debug.LogError("[PassiveIncomeChest] Chest prefab has no active 3D Collider.");
            Destroy(instance);
            return;
        }

        PassiveIncomePop pop = collider.GetComponent<PassiveIncomePop>();

        if (pop == null)
        {
            Debug.LogError("[PassiveIncomeChest] PassiveIncomePop is missing.");

            Destroy(instance);
            return;
        }

        pop.InitializeChest(this);
        PassiveIncomeChestClick oldClick = collider.GetComponent<PassiveIncomeChestClick>();

        if (oldClick != null)
            oldClick.enabled = false;

        _activeChest = instance;
        _activeRewardId = rewardId;
        _activeSpawnIndex = spawnIndex;
        _activePrefabIndex = prefabIndex;

        Debug.Log($"[PassiveIncomeChest] Spawned point={spawnIndex}, prefab={prefabIndex}");
    }

    public void HandleChestClick()
    {
        if (!_initialized || _activeChest == null)
            return;

        if (!TryGetReward(out OfflinePlanetRewardData reward))
        {
            Refresh();
            return;
        }

        if (reward.RewardId != _activeRewardId)
        {
            Refresh();
            return;
        }

        OnChestClicked?.Invoke(_planetKey, reward.RewardId);

        Debug.Log(
            $"[PassiveIncomeChest] Clicked: {_planetKey}, reward={reward.RewardId}");
    }

    private void ClearChest()
    {
        if (_activeChest != null)
            Destroy(_activeChest);

        _activeChest = null;
        _activeRewardId = null;
        _activeSpawnIndex = -1;
        _activePrefabIndex = -1;
    }
}
