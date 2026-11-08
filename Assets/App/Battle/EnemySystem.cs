using System.Collections.Generic;

namespace App.Battle
{
    /// <summary>
    /// 敌人规则（P3-1b 第 4 步）：行为调度 → 位移。
    ///
    /// 只读写 <see cref="EnemyState"/> 与行为注册表，不认识任何 MonoBehaviour（6b 起敌人列表的权威就是状态）。
    /// 接触伤害在 <see cref="ContactSystem"/>，拆开是因为它关心的是「和玩家的距离」，和移动是两件事。
    /// Boss 的二阶段也没有留在这里的 if：它是 <see cref="BossBehavior"/> 叠在走位行为之上，
    /// 由 <see cref="EnemyBehaviors.Get(EnemyState)"/> 决定（P2-4 收尾）。
    /// </summary>
    public sealed class EnemySystem
    {
        private readonly List<EnemyState> _buffer = new List<EnemyState>(256);
        private readonly List<EnemyState> _removed = new List<EnemyState>(64);

        /// <summary>本帧被移除的敌人（死亡）：表现层据此销毁 GameObject，逻辑层下一帧不再看见它们。</summary>
        public IReadOnlyList<EnemyState> Removed => _removed;

        /// <summary>冻结（沙漏）时整段跳过 —— 冻结是「敌人全部不动」，包括行为和 Boss 弹幕。</summary>
        public void Tick(float deltaTime, IEnemyContext context, List<EnemyState> enemies, bool frozen)
        {
            if (frozen || context == null)
            {
                return;
            }

            _removed.Clear();

            // 先拷一份：行为里可能连锁击杀/分裂/爆炸，从而在遍历中增删 enemies。
            _buffer.Clear();
            _buffer.AddRange(enemies);

            for (var i = 0; i < _buffer.Count; i++)
            {
                var state = _buffer[i];
                if (state == null || state.IsDead)
                {
                    continue;
                }

                TickOne(state, deltaTime, context);
            }

            // 死亡清理放在规则层末尾：武器/弹体本帧不再以尸体为目标，表现层随后销毁视图。
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var state = enemies[i];
                if (state == null || state.IsDead)
                {
                    if (state != null)
                    {
                        _removed.Add(state);
                    }

                    enemies.RemoveAt(i);
                }
            }
        }

        private static void TickOne(EnemyState state, float deltaTime, IEnemyContext context)
        {
            // 顺序与拆分前一致：先吃掉上一帧的击退位移，再让行为读新位置，最后统一应用移动。
            state.TickKnockback(deltaTime);

            var move = EnemyBehaviors.Get(state).Tick(state, context, deltaTime);

            // 行为可能已经把这只怪打死了（自爆），别再推进位置
            if (state.IsDead || move.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            // 移动意图用长度表达倍率；超过 1 截断，避免行为写错方向长度导致瞬移
            if (move.sqrMagnitude > 1f)
            {
                move = move.normalized;
            }

            var next = state.Position + move * (state.SpeedAt(context.ElapsedSeconds) * deltaTime);
            state.Position = BattleMap.ClampToWalkable(next, state.Radius);
        }
    }
}
