using System;
using UnityEngine;
using System.Collections.Generic;
using Firebase.Analytics;

public class MetaPlanetRules : MonoBehaviour
{
    GeneralGameData _currentData;

    public event Action<int> OnActivateVariantItem, OnActivateUpgradeItem, OnToggleVariantItem, OnToggleUpgradeItem;
    public event Action<int, bool> OnToggleVariantItemState, OnToggleUpgradeItemState;
    public event Action<int, GeneralGameData> OnUpdateVariantData, OnUpdateUpgradeData;

    public void AddVariantObject(int index)
    {
        _currentData.VariantItemCount[index] += 1;

        ActivateVariantItem(index);

        FirebaseAnalytics.LogEvent(name: "meta_variant_category_purchased");

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! MetaPlanetRules /// AddVariantObject /// Add to VariantItemCount" + _currentData.VariantItemCount[index]);
    }

    public void AddUpgradeObject(int index)
    {
        _currentData.UpgradeItemCount[index] += 1;

        ActivateUpgradeItem(index);

        FirebaseAnalytics.LogEvent(name: "meta_upgrade_category_purchased");
    }

    public void HandleVariantItem(int index)
    {
        _currentData.Diamonds -= _currentData.VariantItemDataList[index].VariantItemPrice(_currentData.VariantItemCount[index]);
        _currentData.VariantIndex[index] = 1;

        OnToggleVariantItem?.Invoke(index);

        SendItemsData();
    }
    public void HandleUpgradeLevel(int index)
    {
        _currentData.Diamonds -= _currentData.UpgradeItemDataList[index].UpgradeLevelPrice(_currentData.UpgradeItemCount[index]);
        _currentData.UpgradeLevel[index] = 1;
                    
        OnToggleUpgradeItem?.Invoke(index);

        SendItemsData();
    }

    private void ActivateVariantItem(int i)
    {
        OnActivateVariantItem?.Invoke(i);
        SendItemsData();
    }

    private void ActivateUpgradeItem(int i)
    {
        OnActivateUpgradeItem?.Invoke(i);
        SendItemsData();
    }

    public void SendItemsData()
    {
        for (int i = 0; i < _currentData.VariantIndex.Count; i++)
        {
            OnUpdateVariantData?.Invoke(i, _currentData);
        }

        for (int i = 0; i < _currentData.UpgradeLevel.Count; i++)
        {
            OnUpdateUpgradeData?.Invoke(i, _currentData);
        }
    }

    public void LoadMetaPlanet(string gameDataSave)
    {
        if (String.IsNullOrEmpty(gameDataSave))
            return;
        _currentData.SetData(gameDataSave);

        for (int i = 0; i < _currentData.VariantItemCount.Count; i++)
        {
            //if (_currentData.VariantItemCount[i] > 0)
                //ActivateVariantItem(i);
        }

        for (int i = 0; i < _currentData.UpgradeItemCount.Count; i++)
        {
            if (_currentData.UpgradeItemCount[i] > 0)
                ActivateUpgradeItem(i);
        }

        SendItemsData();
    }

    public void PrepareData(GeneralGameData generalData)
    {
        _currentData = generalData;

        for (int i = 0; i < _currentData.VariantsControllerList.Count; i++)
        {
            _currentData.VariantItemCount.Add(i == 0 ? 1 : 0);
        }
        SendItemsData();

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! MetaPlanetRules /// PrepareData /// VariantItemCount: " + _currentData.VariantItemCount);

    }

}
