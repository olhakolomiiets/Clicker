using System;
using System.Collections.Generic;
using UnityEngine;

public static class OfflinePlanetSaveReader
{
    private const string PendingTransactionKey = "PendingPassiveIncomeTransaction_v1";

    [Serializable]
    private sealed class PlanetSaveEnvelope
    {
        public List<string> savedData;
    }

    public static bool TryRead(
        string saveKey,
        out GameDataSave save,
        out string error)
    {
        save = default;
        error = null;

        if (string.IsNullOrWhiteSpace(saveKey))
        {
            error = "SaveKey is missing.";
            return false;
        }

        // Read only after the existing transaction recovery has completed.
        if (PlayerPrefs.HasKey(PendingTransactionKey))
        {
            error = "Passive income transaction recovery is required.";
            return false;
        }

        if (!PlayerPrefs.HasKey(saveKey))
        {
            error = $"Planet save was not found: {saveKey}.";
            return false;
        }

        try
        {
            string json = PlayerPrefs.GetString(saveKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Planet save is empty.";
                return false;
            }

            PlanetSaveEnvelope envelope =
                JsonUtility.FromJson<PlanetSaveEnvelope>(json);

            if (envelope == null ||
                envelope.savedData == null ||
                envelope.savedData.Count == 0 ||
                string.IsNullOrWhiteSpace(envelope.savedData[0]))
            {
                error = "Planet save envelope is invalid.";
                return false;
            }

            save = JsonUtility.FromJson<GameDataSave>(
                envelope.savedData[0]);

            if (save.ItemCount == null ||
                save.ItemBonusMultiplayer == null ||
                save.ItemMaxCountHelper == null)
            {
                save = default;
                error = "Required planet save fields are missing.";
                return false;
            }

            return true;
        }
        catch (ArgumentException)
        {
            save = default;
            error = "Planet save JSON is invalid.";
            return false;
        }
    }
}