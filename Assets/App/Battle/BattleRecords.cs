using App.Config;
using Framework.Save;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 局外记录：累计金币、出战次数、胜利次数、最高击杀与最长存活。
    /// 只依赖 ISaveService，PC/编辑器走 PlayerPrefs，小游戏平台走平台存档。
    /// </summary>
    public sealed class BattleRecords
    {
        private const string GoldKey = "battle.meta.gold.v1";
        private const string RunsKey = "battle.meta.runs.v1";
        private const string WinsKey = "battle.meta.wins.v1";
        private const string BestKillsKey = "battle.meta.bestKills.v1";
        private const string BestTimeKey = "battle.meta.bestTime.v1";

        private readonly ISaveService _save;

        public BattleRecords(ISaveService save)
        {
            _save = save;
            Gold = _save.GetInt(GoldKey, 0);
            Runs = _save.GetInt(RunsKey, 0);
            Victories = _save.GetInt(WinsKey, 0);
            BestKills = _save.GetInt(BestKillsKey, 0);
            BestTime = _save.GetFloat(BestTimeKey, 0f);
        }

        public int Gold { get; private set; }

        public int Runs { get; private set; }

        public int Victories { get; private set; }

        public int BestKills { get; private set; }

        public float BestTime { get; private set; }

        /// <summary>一局结束后结算到局外存档。</summary>
        public void Commit(BattleSession session)
        {
            if (session == null)
            {
                return;
            }

            Gold += session.Gold;
            Runs++;

            if (session.IsVictory)
            {
                Victories++;
            }

            BestKills = Mathf.Max(BestKills, session.Kills);
            BestTime = Mathf.Max(BestTime, session.Elapsed);

            _save.SetInt(GoldKey, Gold);
            _save.SetInt(RunsKey, Runs);
            _save.SetInt(WinsKey, Victories);
            _save.SetInt(BestKillsKey, BestKills);
            _save.SetFloat(BestTimeKey, BestTime);
            _save.Save();
        }

        public string Summary =>
            $"累计金币 {Gold} · 出战 {Runs} 次 · 胜利 {Victories} 次 · 最高击杀 {BestKills} · 最长 {FormatTime(BestTime)}";

        private static string FormatTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
