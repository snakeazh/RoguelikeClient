using App.Config;
using Framework.Log;
using Framework.Save;

namespace App.Battle
{
    /// <summary>
    /// 「本局用哪个角色」的选择结果。存在 ISaveService 里，老存档没有这个键时退回
    /// <see cref="BattleSession.DefaultCharacterId"/>，不抛异常。
    /// </summary>
    public sealed class CharacterSelection
    {
        public const string SaveKey = "battle.character.selected.v1";

        private readonly ISaveService _save;

        public CharacterSelection(ISaveService save)
        {
            _save = save;
        }

        /// <summary>当前选中的角色 Id；读表校验过，表里没有的 Id 会被纠回默认值。</summary>
        public int SelectedId
        {
            get
            {
                var id = _save.GetInt(SaveKey, BattleSession.DefaultCharacterId);
                if (CharacterDef.Get(id) != null)
                {
                    return id;
                }

                return BattleSession.DefaultCharacterId;
            }
        }

        public CharacterDef Selected => CharacterDef.Get(SelectedId);

        /// <summary>改选择并立刻落盘（Home 上的角色卡片用）。</summary>
        public void Select(int characterId)
        {
            var def = CharacterDef.Get(characterId);
            if (def == null)
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] character {characterId} not found in Characters.json");
                return;
            }

            _save.SetInt(SaveKey, characterId);
            _save.Save();
            AppLog.Info(LogChannel.Game, $"[Battle] character selected {def.Name} ({def.Id})");
        }
    }
}
