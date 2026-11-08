using System.Threading.Tasks;
using App.Resources;
using Framework.Assets;
using Framework.Log;
using UnityEngine;

namespace App.Config
{
    /// <summary>
    /// 配置表统一加载入口。启动顺序里必须在 UIFramework.Create 之前 await 完成，
    /// 因为 ViewModel 构造函数经常直接读表。
    ///
    /// 新增一张表：
    /// 1. 在 <see cref="ResResourcePaths"/> 加 json 路径；
    /// 2. 写一个 ConfigRowBase&lt;T&gt; 子类；
    /// 3. 在 <see cref="LoadAsync"/> 里加一行 LoadArrayAsync。
    /// </summary>
    public static class ConfigTables
    {
        public static async Task LoadAsync(IResourceService resources)
        {
            var demoRows = await LoadArrayAsync<DemoItemConfig>(resources, ResResourcePaths.DemoConfig);
            DemoItemConfig.Load(demoRows);

            await LoadBattleTablesAsync(resources);

            AppLog.Info(
                LogChannel.Bootstrap,
                $"[Config] DemoItemConfig x{DemoItemConfig.Count}, "
                + $"Enemy x{EnemyDef.Count}, Weapon x{WeaponDef.Count}, "
                + $"Passive x{PassiveDef.Count}, Wave x{WaveDef.Count}, Level x{LevelDef.Count}, "
                + $"Character x{CharacterDef.Count} loaded.");
        }

        /// <summary>战斗数值表。缺表说明资源没导出，直接抛错，避免带着空表进战斗。</summary>
        private static async Task LoadBattleTablesAsync(IResourceService resources)
        {
            EnemyDef.Load(await LoadArrayAsync<EnemyDef>(resources, ResResourcePaths.BattleEnemies));
            WeaponDef.Load(await LoadArrayAsync<WeaponDef>(resources, ResResourcePaths.BattleWeapons));
            PassiveDef.Load(await LoadArrayAsync<PassiveDef>(resources, ResResourcePaths.BattlePassives));
            WaveDef.Load(await LoadArrayAsync<WaveDef>(resources, ResResourcePaths.BattleWaves));
            LevelDef.Load(await LoadArrayAsync<LevelDef>(resources, ResResourcePaths.BattleLevels));
            CharacterDef.Load(await LoadArrayAsync<CharacterDef>(resources, ResResourcePaths.BattleCharacters));
        }

        /// <summary>清空所有已加载的表（热重载 / 退出登录时用）。</summary>
        public static void Clear()
        {
            DemoItemConfig.Clear();
            EnemyDef.Clear();
            WeaponDef.Clear();
            PassiveDef.Clear();
            WaveDef.Clear();
            LevelDef.Clear();
            CharacterDef.Clear();
        }

        /// <summary>加载 json 顶层数组并反序列化（JsonUtility 需要外面包一层对象）。</summary>
        private static async Task<T[]> LoadArrayAsync<T>(IResourceService resources, string key)
        {
            var asset = await resources.LoadAsync<TextAsset>(key);
            if (asset == null)
            {
                throw new MissingReferenceException($"Config json not found: Assets/Res/{key}.json");
            }

            return ConfigJson.FromArrayJson<T>(asset.text);
        }
    }
}
