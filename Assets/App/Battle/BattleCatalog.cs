using System.Collections.Generic;
using App.Config;

namespace App.Battle
{
    /// <summary>
    /// 一局战斗能读到的配表快照。
    ///
    /// 静态表（<see cref="ConfigRowBase{T}"/>）只负责在启动时把 JSON 灌进来；运行期业务代码
    /// 一律读这个实例，不再调 `WeaponDef.Get(...)` 这类静态入口。这样换表、做测试都只是换一个
    /// 实例，不用改全局状态，也不会出现「某个单例在别的程序集里被静态表偷偷改掉」。
    /// </summary>
    public sealed class BattleCatalog
    {
        private readonly Dictionary<int, WeaponDef> _weapons;
        private readonly Dictionary<int, PassiveDef> _passives;
        private readonly Dictionary<int, CharacterDef> _characters;
        private readonly Dictionary<int, EnemyDef> _enemies;
        private readonly Dictionary<int, PickupDef> _pickups;
        private readonly Dictionary<int, WaveDef> _waves;
        private readonly Dictionary<int, LevelDef> _levels;

        private BattleCatalog(
            Dictionary<int, WeaponDef> weapons,
            Dictionary<int, PassiveDef> passives,
            Dictionary<int, CharacterDef> characters,
            Dictionary<int, EnemyDef> enemies,
            Dictionary<int, PickupDef> pickups,
            Dictionary<int, WaveDef> waves,
            Dictionary<int, LevelDef> levels)
        {
            _weapons = weapons;
            _passives = passives;
            _characters = characters;
            _enemies = enemies;
            _pickups = pickups;
            _waves = waves;
            _levels = levels;
        }

        /// <summary>把当前已加载的静态表复制成一份快照（启动时调一次）。</summary>
        public static BattleCatalog FromConfig()
        {
            return new BattleCatalog(
                Copy(WeaponDef.All),
                Copy(PassiveDef.All),
                Copy(CharacterDef.All),
                Copy(EnemyDef.All),
                Copy(PickupDef.All),
                Copy(WaveDef.All),
                Copy(LevelDef.All));
        }

        public IReadOnlyCollection<WeaponDef> Weapons => _weapons.Values;

        public IReadOnlyCollection<PassiveDef> Passives => _passives.Values;

        /// <summary>波次按 Id 升序给出：刷怪节奏依赖顺序稳定，不能直接依赖字典顺序。</summary>
        public IEnumerable<WaveDef> Waves
        {
            get
            {
                var ids = new List<int>(_waves.Keys);
                ids.Sort();
                foreach (var id in ids)
                {
                    yield return _waves[id];
                }
            }
        }

        public IReadOnlyCollection<CharacterDef> Characters => _characters.Values;

        public IReadOnlyCollection<EnemyDef> Enemies => _enemies.Values;

        public WeaponDef Weapon(int id) => Find(_weapons, id);

        public PassiveDef Passive(int id) => Find(_passives, id);

        public CharacterDef Character(int id) => Find(_characters, id);

        public EnemyDef Enemy(int id) => Find(_enemies, id);

        public PickupDef Pickup(int id) => Find(_pickups, id);

        public WaveDef Wave(int id) => Find(_waves, id);

        public LevelDef Level(int id) => Find(_levels, id);

        /// <summary>表里没有这一行时返回 null，由调用方决定是报错还是退回默认值。</summary>
        private static T Find<T>(Dictionary<int, T> map, int id) where T : class
        {
            return map.TryGetValue(id, out var row) ? row : null;
        }

        private static Dictionary<int, T> Copy<T>(IReadOnlyDictionary<int, T> source)
        {
            var copy = new Dictionary<int, T>(source.Count);
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
