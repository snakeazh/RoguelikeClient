using App.Config;
using UnityEngine;
using UnityEngine.UI;

namespace App
{
    /// <summary>
    /// 全局 UI 色板：品质主色 / 标题色、空槽底色、不可购买价签色。
    /// 界面统一从这里取色，不要在 prefab 里写死 hex，改色只改这一处。
    /// </summary>
    public static class ThemeColors
    {
        public static readonly Color Ordinary = ParseHex("FBF5DF");
        public static readonly Color OrdinaryTitle = ParseHex("F5E8B1");
        public static readonly Color Rare = ParseHex("DFE8FB");
        public static readonly Color RareTitle = ParseHex("B1D1F5");
        public static readonly Color Epic = ParseHex("F4DFFB");
        public static readonly Color EpicTitle = ParseHex("CB91FF");
        public static readonly Color Legend = ParseHex("FFCD7D");
        public static readonly Color LegendTitle = ParseHex("FF9243");

        public static readonly Color Player = Ordinary;
        public static readonly Color PlayerTitle = OrdinaryTitle;
        public static readonly Color Enemy = ParseHex("F6393C");
        public static readonly Color EnemyTitle = ParseHex("B20003");
        public static readonly Color EquipEmpty = Color.black;
        public static readonly Color ShopPriceUnaffordable = ParseHex("EA1E1E");

        public static Color ForQuality(QualityType type)
        {
            switch (type)
            {
                case QualityType.Rare:
                    return Rare;
                case QualityType.Epic:
                    return Epic;
                case QualityType.Legend:
                    return Legend;
                default:
                    return Ordinary;
            }
        }

        public static Color ForQualityTitle(QualityType type)
        {
            switch (type)
            {
                case QualityType.Rare:
                    return RareTitle;
                case QualityType.Epic:
                    return EpicTitle;
                case QualityType.Legend:
                    return LegendTitle;
                default:
                    return OrdinaryTitle;
            }
        }

        public static Color EquipSlot(bool occupied, QualityType type)
        {
            return occupied ? ForQuality(type) : EquipEmpty;
        }

        public static void ApplyCard(QualityType type, Image iconBg, Image titleBg, Image circle)
        {
            ApplyCard(ForQuality(type), ForQualityTitle(type), iconBg, titleBg, circle);
        }

        public static void ApplyCard(Color background, Color title, Image iconBg, Image titleBg, Image circle)
        {
            if (iconBg != null)
            {
                iconBg.color = background;
            }

            if (titleBg != null)
            {
                titleBg.color = title;
            }

            if (circle != null)
            {
                circle.color = title;
            }
        }

        public static Color ParseHex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            color.a = 1f;
            return color;
        }
    }
}
