using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WitchTowerMonetizationConfigurator
{
    private const string AdsDefine = "WITCHTOWER_ADS_ENABLED";
    private const string IapDefine = "WITCHTOWER_IAP_ENABLED";

    [MenuItem("WitchTower/Monetization/Enable iOS Ads")]
    public static void EnableIosAds()
    {
        SetIosDefine(AdsDefine, true);
    }

    [MenuItem("WitchTower/Monetization/Enable iOS IAP")]
    public static void EnableIosIap()
    {
        SetIosDefine(IapDefine, true);
    }

    [MenuItem("WitchTower/Monetization/Disable iOS Ads")]
    public static void DisableIosAds()
    {
        SetIosDefine(AdsDefine, false);
    }

    [MenuItem("WitchTower/Monetization/Disable iOS IAP")]
    public static void DisableIosIap()
    {
        SetIosDefine(IapDefine, false);
    }

    private static void SetIosDefine(string define, bool enabled)
    {
        string current = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
        var symbols = new HashSet<string>(
            (current ?? string.Empty).Split(
                new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        if (enabled)
        {
            symbols.Add(define);
        }
        else
        {
            symbols.Remove(define);
        }

        var ordered = new List<string>(symbols);
        ordered.Sort(StringComparer.Ordinal);
        PlayerSettings.SetScriptingDefineSymbols(
            NamedBuildTarget.iOS,
            string.Join(";", ordered));
        AssetDatabase.SaveAssets();
        Debug.Log($"[Monetization] iOS define '{define}' enabled={enabled}.");
    }
}
