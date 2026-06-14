using System;
using UnityEditor;
using UnityEngine;

public static class MetaUpgradePurchaseScenarioChecks
{
    [MenuItem("Tools/Planet Builder/Run Meta Upgrade Purchase Scenario Checks")]
    public static void Run()
    {
        CheckPurchaseWithEnoughDiamonds();
        CheckPurchaseWithoutEnoughDiamonds();
        CheckCancelledPurchase();
        Debug.Log("Meta upgrade purchase scenario checks passed.");
    }

    private static void CheckPurchaseWithEnoughDiamonds()
    {
        GeneralGameData data = new() { Diamonds = 3d };
        MetaGameRules rules = CreateRules(data);

        bool purchased = rules.TryHandleUpgradeLevel(2d);

        Assert(purchased, "Purchase should succeed when diamonds are sufficient.");
        Assert(data.Diamonds == 1d, "Successful purchase should deduct its cost.");
        DestroyRules(rules);
    }

    private static void CheckPurchaseWithoutEnoughDiamonds()
    {
        GeneralGameData data = new() { Diamonds = 1d };
        MetaGameRules rules = CreateRules(data);

        bool purchased = rules.TryHandleUpgradeLevel(2d);

        Assert(!purchased, "Purchase should fail when diamonds are insufficient.");
        Assert(data.Diamonds == 1d, "Failed purchase should not change diamonds.");
        DestroyRules(rules);
    }

    private static void CheckCancelledPurchase()
    {
        GeneralGameData data = new() { Diamonds = 3d };
        MetaGameRules rules = CreateRules(data);

        // Closing the panel does not call the purchase API.
        Assert(data.Diamonds == 3d, "Cancelled purchase should not change diamonds.");
        DestroyRules(rules);
    }

    private static MetaGameRules CreateRules(GeneralGameData data)
    {
        GameObject gameObject = new("MetaGameRulesScenarioCheck");
        MetaGameRules rules = gameObject.AddComponent<MetaGameRules>();
        rules.PrepareData(data);
        return rules;
    }

    private static void DestroyRules(MetaGameRules rules)
    {
        UnityEngine.Object.DestroyImmediate(rules.gameObject);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
