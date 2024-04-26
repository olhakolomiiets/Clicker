using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CBS.Utils
{
    public static class TextUtils
    {
        public static bool ContainSpecialSymbols(string input)
        {
            return input.Any(ch => !Char.IsLetterOrDigit(ch));
        }

        public static bool ContainUpperCase(string input)
        {
            return input.Any(char.IsUpper);
        }

        public static string GetCustomDataAsReadableText(this CBSInventoryItem inventoryItem, Color titleColor, Color valueColor)
        {
            var customData = inventoryItem.GetCustomDataAsDictionary();
            return ConvertCustomDataToReadableText(customData, titleColor, valueColor);
        }

        public static string GetCurrentUpgradeCustomDataAsReadableText(this CBSInventoryItem inventoryItem, Color titleColor, Color valueColor)
        {
            var customData = inventoryItem.GetCurrentUpgradeCustomDataAsDictionary();
            return ConvertCustomDataToReadableText(customData, titleColor, valueColor);
        }

        public static string GetNextUpgradeCustomDataAsReadableText(this CBSInventoryItem inventoryItem, Color titleColor, Color valueColor)
        {
            if (!inventoryItem.IsUpgradable || inventoryItem.IsMaxUpgrade())
                return string.Empty;
            var currentCustomData = inventoryItem.GetCurrentUpgradeCustomDataAsDictionary();
            var nextCustomData = inventoryItem.GetNextUpgradeCustomDataAsDictionary();
            var sBuilder = new StringBuilder();
            var colorTagStart = string.Format("<color=#{0}><b>", ColorUtility.ToHtmlStringRGB(valueColor));
            var colorTagEnd = "</b></color>";
            foreach (var dataPair in nextCustomData)
            {
                var titleText = string.Format("<color=#{0}><b>{1}</b></color>", ColorUtility.ToHtmlStringRGB(titleColor), dataPair.Key);
                sBuilder.Append(titleText);
                if (TypeUtils.IsSupportedList(dataPair.Value))
                {
                    var targetList = dataPair.Value as IEnumerable;
                    foreach (var line in targetList)
                    {
                        sBuilder.Append(Environment.NewLine);
                        sBuilder.Append(colorTagStart);
                        sBuilder.Append(" - ");
                        sBuilder.Append(line.ToString());
                        sBuilder.Append(colorTagEnd);
                    }
                }
                else
                {
                    sBuilder.Append(colorTagStart);
                    sBuilder.Append(" - ");
                    sBuilder.Append(dataPair.Value == null ? string.Empty : dataPair.Value.ToString());
                    sBuilder.Append(colorTagEnd);
                    if (TypeUtils.IsFloat(dataPair.Value) || TypeUtils.IsInt(dataPair.Value))
                    {
                        if (currentCustomData.ContainsKey(dataPair.Key))
                        {
                            var oldData = currentCustomData[dataPair.Key];
                            var newData = nextCustomData[dataPair.Key];
                            if (oldData != newData)
                            {
                                var dif = float.Parse(newData.ToString()) - float.Parse(oldData.ToString());
                                var colorStart = string.Format("<color=#{0}><b>", ColorUtility.ToHtmlStringRGB(dif > 0 ? Color.green : Color.red));
                                var colorEnd = "</b></color>";
                                sBuilder.Append(" ");
                                sBuilder.Append(colorStart);
                                sBuilder.Append(dif > 0 ? "+" : "-");
                                sBuilder.Append(dif);
                                sBuilder.Append(colorEnd);
                            }
                        }
                    }
                }
                if (nextCustomData.Last().Key != dataPair.Key)
                    sBuilder.Append(Environment.NewLine);
            }
            return sBuilder.ToString();
        }

        private static string ConvertCustomDataToReadableText(Dictionary<string, object> customData, Color titleColor, Color valueColor)
        {
            var sBuilder = new StringBuilder();
            var colorTagStart = string.Format("<color=#{0}><b>", ColorUtility.ToHtmlStringRGB(valueColor));
            var colorTagEnd = "</b></color>";
            foreach (var dataPair in customData)
            {
                var titleText = string.Format("<color=#{0}><b>{1}</b></color>", ColorUtility.ToHtmlStringRGB(titleColor), dataPair.Key);
                sBuilder.Append(titleText);
                if (TypeUtils.IsSupportedList(dataPair.Value))
                {
                    var targetList = dataPair.Value as IEnumerable;
                    foreach (var line in targetList)
                    {
                        sBuilder.Append(Environment.NewLine);
                        sBuilder.Append(colorTagStart);
                        sBuilder.Append(" - ");
                        sBuilder.Append(line.ToString());
                        sBuilder.Append(colorTagEnd);
                    }
                }
                else
                {
                    sBuilder.Append(colorTagStart);
                    sBuilder.Append(" - ");
                    sBuilder.Append(dataPair.Value == null ? string.Empty : dataPair.Value.ToString());
                    sBuilder.Append(colorTagEnd);
                }
                if (customData.Last().Key != dataPair.Key)
                    sBuilder.Append(Environment.NewLine);
            }
            return sBuilder.ToString();
        }

        public static string GetDiscountText(int discount)
        {
            var sBuilder = new StringBuilder();
            sBuilder.Append("-");
            sBuilder.Append(discount);
            sBuilder.Append("%");
            return sBuilder.ToString();
        }

        public static void Clear(this StringBuilder value)
        {
            value.Length = 0;
            value.Capacity = 0;
        }
    }
}
