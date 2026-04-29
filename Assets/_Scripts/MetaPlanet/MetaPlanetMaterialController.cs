using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaPlanetMaterialController : MonoBehaviour
{
    [SerializeField] private string controllerId = "PlanetMaterials";
    [SerializeField] private MetaPlanetMaterialData data;

    [Header("Targets")]
    [SerializeField] private List<MeshRenderer> largeRenderers = new();
    [SerializeField] private List<MeshRenderer> smallRenderers = new();

    [SerializeField] private int activeMaterialId = -1;
    public List<PlanetMaterialSaveData> state = new();

    private const string ES3_FILE = "meta_materials.es3";
    private string Es3Key => $"materials.{controllerId}";

    public event Action OnMaterialSelected;

    private void Start()
    {
        InitStateIfNeeded();
        LoadFromES3();
        ApplyVisualStateFromFlags();
    }

    private void InitStateIfNeeded()
    {
        if (data == null || data.Materials == null)
        {
            Debug.LogWarning($"[MetaPlanetMaterialController] Data missing on '{name}'");
            return;
        }

        if (state != null && state.Count == data.Materials.Count)
            return;

        state = new List<PlanetMaterialSaveData>();

        for (int i = 0; i < data.Materials.Count; i++)
            state.Add(new PlanetMaterialSaveData());
    }

    public void BuyMaterial(int index)
    {
        if (!IsValidIndex(index)) return;

        state[index].isBought = true;
        SelectMaterial(index);
    }

    public void SelectMaterial(int index)
    {
        if (!IsValidIndex(index)) return;
        if (!state[index].isBought) return;

        for (int i = 0; i < state.Count; i++)
            state[i].isActive = i == index;

        activeMaterialId = index;

        ApplyMaterial(index);
        SaveToES3();

        OnMaterialSelected?.Invoke();
    }

    private void ApplyVisualStateFromFlags()
    {
        activeMaterialId = -1;

        for (int i = 0; i < state.Count; i++)
        {
            if (state[i].isActive)
            {
                activeMaterialId = i;
                break;
            }
        }

        if (activeMaterialId >= 0)
            ApplyMaterial(activeMaterialId);
    }

    private void ApplyMaterial(int index)
    {
        if (!IsValidIndex(index)) return;

        PlanetMaterialVariant variant = data.Materials[index];

        ApplyMaterialToRenderers(largeRenderers, variant.LargeObjectsMaterial);
        ApplyMaterialToRenderers(smallRenderers, variant.SmallObjectsMaterial);
    }

    private void ApplyMaterialToRenderers(List<MeshRenderer> renderers, Material material)
    {
        if (material == null) return;

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.sharedMaterial = material;
        }
    }

    private bool IsValidIndex(int index)
    {
        return data != null &&
               data.Materials != null &&
               state != null &&
               index >= 0 &&
               index < data.Materials.Count &&
               index < state.Count;
    }

    public bool IsMaterialBought(int index)
    {
        return IsValidIndex(index) && state[index].isBought;
    }

    public bool IsMaterialActive(int index)
    {
        return IsValidIndex(index) && state[index].isActive;
    }

    public PlanetMaterialControllerSaveData GetSaveData()
    {
        return new PlanetMaterialControllerSaveData
        {
            id = controllerId,
            materials = state
        };
    }

    public void LoadFromSave(PlanetMaterialControllerSaveData save)
    {
        if (save == null || save.materials == null) return;

        InitStateIfNeeded();

        for (int i = 0; i < save.materials.Count && i < state.Count; i++)
        {
            state[i].isBought = save.materials[i].isBought;
            state[i].isActive = save.materials[i].isActive;
        }

        ApplyVisualStateFromFlags();
    }

    public void SaveToES3()
    {
        var json = JsonUtility.ToJson(GetSaveData(), true);
        ES3.Save(Es3Key, json, ES3_FILE);
    }

    public void LoadFromES3()
    {
        if (!ES3.KeyExists(Es3Key, ES3_FILE)) return;

        var json = ES3.Load<string>(Es3Key, ES3_FILE);
        if (string.IsNullOrEmpty(json)) return;

        var save = JsonUtility.FromJson<PlanetMaterialControllerSaveData>(json);
        LoadFromSave(save);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveToES3();
    }

    private void OnDisable()
    {
        SaveToES3();
    }
}

[Serializable]
public class PlanetMaterialSaveData
{
    public bool isBought;
    public bool isActive;
}

[Serializable]
public class PlanetMaterialControllerSaveData
{
    public string id;
    public List<PlanetMaterialSaveData> materials = new();
}