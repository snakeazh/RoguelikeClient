using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取物规则（P3-1）：寿命、磁力吸附、拾取判定、效果派发。
    /// 只写 <see cref="PickupState"/>；表现（精灵/位置）由 <see cref="PickupUnit"/> 从 State 同步。
    /// </summary>
    public sealed class PickupSystem
    {
        /// <summary>吸附到多近算捡到。</summary>
        public const float CollectRadius = 0.5f;

        private readonly PickupEffects _effects;
        private readonly List<PickupState> _states = new List<PickupState>(256);

        public PickupSystem(PickupEffects effects)
        {
            _effects = effects;
        }

        public IReadOnlyList<PickupState> States => _states;

        public PickupState Spawn(PickupDef def, int amount, Vector2 position)
        {
            var state = new PickupState
            {
                Def = def,
                Amount = amount,
                Position = position,
                Life = def != null && def.Lifetime > 0f ? def.Lifetime : 90f
            };

            _states.Add(state);
            return state;
        }


        public void Tick(float deltaTime, Vector2 playerPosition, bool hasPlayer, float magnetRadius, IPickupContext context)
        {
            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                if (state == null || state.Done)
                {
                    _states.RemoveAt(i);
                    continue;
                }

                TickOne(state, deltaTime, playerPosition, hasPlayer, magnetRadius, context);

                if (state.Done)
                {
                    _states.RemoveAt(i);
                }
            }
        }

        /// <summary>被磁铁道具强制吸取（效果原语调用）。</summary>
        public void Attract(PickupState state)
        {
            if (state != null)
            {
                state.Attracted = true;
            }
        }

        public void Clear()
        {
            _states.Clear();
        }

        private void TickOne(
            PickupState state,
            float deltaTime,
            Vector2 playerPosition,
            bool hasPlayer,
            float magnetRadius,
            IPickupContext context)
        {
            state.Life -= deltaTime;
            if (state.Life <= 0f)
            {
                state.Done = true;
                return;
            }

            if (!hasPlayer)
            {
                return;
            }

            var distance = Vector2.Distance(state.Position, playerPosition);

            if (distance <= magnetRadius)
            {
                state.Attracted = true;
            }

            if (state.Attracted)
            {
                // 用 MoveTowards 而不是 += direction*step：固定大步长（1/60 × 20 倍速）下后者会冲过玩家
                // 落在另一侧，永远进不了拾取半径，表现就是「经验条不动」。
                var speed = 10f + Mathf.Max(0f, 4f - distance) * 4f;
                state.Position = Vector2.MoveTowards(state.Position, playerPosition, speed * deltaTime);
                distance = Vector2.Distance(state.Position, playerPosition);
            }

            if (distance > CollectRadius)
            {
                return;
            }

            state.Collected = true;
            state.Done = true;

            ApplyEffects(state, context);
        }

        private void ApplyEffects(PickupState state, IPickupContext context)
        {
            var def = state.Def;
            if (def == null || def.Effects == null)
            {
                return;
            }

            // 效果是数据：一个拾取物可以挂多条原语，顺序由 Pickups.json 决定。
            for (var i = 0; i < def.Effects.Count; i++)
            {
                var entry = def.Effects[i];
                _effects.Get(entry.Key).Apply(context, state, entry.Args ?? new PickupArgs());
            }
        }
    }
}
