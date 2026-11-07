# ThemeColors · 全局 UI 色板

路径：`Assets/App/ThemeColors.cs`  
命名空间：`App`

界面的品质色、标题色、空槽底色、不可购买价签色都从这里取，**不要在 prefab 或界面脚本里写死 hex**，改色只改这一处。

| `QualityType` | 底色 | 标题色 / 圆形底 |
|---------------|------|-----------------|
| `Ordinary` 普通 | `#FBF5DF` | `#F5E8B1` |
| `Rare` 稀有 | `#DFE8FB` | `#B1D1F5` |
| `Epic` 史诗 | `#F4DFFB` | `#CB91FF` |
| `Legend` 传说 | `#FFCD7D` | `#FF9243` |

未知品质回退普通。另有 `Player` / `Enemy` / `EquipEmpty` / `ShopPriceUnaffordable` 等固定色。

## 用法

```csharp
var bg = ThemeColors.ForQuality(type);
var title = ThemeColors.ForQualityTitle(type);

// 一次给「底板 + 标题条 + 圆形框」上色
ThemeColors.ApplyCard(type, iconBg, titleBg, circle);

// 空槽用 EquipEmpty 压暗
slotBg.color = ThemeColors.EquipSlot(occupied: false, type);

// 自定义 hex 也走这里，保证 alpha 统一
var color = ThemeColors.ParseHex("FF9243");
```

`QualityType` 定义在 `Assets/App/Config/QualityType.cs`（`Ordinary = 0` 起）。
如果项目接了品质配表，导出后把枚举值对到这张表上即可。
