using App.Battle;
using App.Config;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// Boss 二阶段的回归检查（P2-4 收尾）：二阶段从 <c>EnemySystem</c> 的 if 搬成
    /// <see cref="BossBehavior"/> 装饰器之后，用一只合成的 Boss 状态直接跑行为，
    /// 确认「血量过半 → 加速 + 一圈弹幕 + 提示一次」这条链路还在，且非 Boss 不会被误触发。
    ///
    /// 用 <c>-batchmode -executeMethod App.EditorTools.BossPhaseCheck.Run</c> 跑。
    /// </summary>
    public static class BossPhaseCheck
    {
        private const float DeltaTime = 1f / 60f;

        public static void Run()
        {
            var failures = 0;
            failures += CheckPhaseTwoTriggers();
            failures += CheckPhaseTwoDoesNotTriggerAboveHalf();
            failures += CheckNormalEnemyIgnoresPhaseTwo();

            if (failures > 0)
            {
                Debug.LogError($"[BossPhaseCheck] FAIL: {failures} check(s) failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[BossPhaseCheck] PASS: boss phase two accelerates, bursts once per interval, announces once.");
            EditorApplication.Exit(0);
        }

        private static int CheckPhaseTwoTriggers()
        {
            var state = CreateBoss(0.45f);
            var context = new FakeContext();

            EnemyBehaviors.Get(state).Tick(state, context, DeltaTime);

            var ok = true;
            ok &= Expect(context.Projectiles == BossBehavior.BurstCount,
                $"first tick should fire {BossBehavior.BurstCount} projectiles, got {context.Projectiles}");
            ok &= Expect(context.Toasts == 1, $"phase two should be announced once, got {context.Toasts}");
            ok &= Expect(Mathf.Abs(state.Speed - state.Def.Speed * BossBehavior.EnrageSpeedScale) < 0.001f,
                $"speed should be {state.Def.Speed * BossBehavior.EnrageSpeedScale}, got {state.Speed}");

            // 计时器没到，下一帧不该再打一圈，也不该重复提示
            EnemyBehaviors.Get(state).Tick(state, context, DeltaTime);
            ok &= Expect(context.Projectiles == BossBehavior.BurstCount,
                $"second tick inside the interval must not fire again, got {context.Projectiles}");
            ok &= Expect(context.Toasts == 1, $"toast must not repeat, got {context.Toasts}");

            return ok ? 0 : 1;
        }

        private static int CheckPhaseTwoDoesNotTriggerAboveHalf()
        {
            var state = CreateBoss(0.8f);
            var context = new FakeContext();

            EnemyBehaviors.Get(state).Tick(state, context, DeltaTime);

            var ok = true;
            ok &= Expect(context.Projectiles == 0, $"boss above half hp must not burst, got {context.Projectiles}");
            ok &= Expect(context.Toasts == 0, $"boss above half hp must not announce, got {context.Toasts}");
            ok &= Expect(Mathf.Abs(state.Speed - state.Def.Speed) < 0.001f,
                $"speed must stay {state.Def.Speed}, got {state.Speed}");
            return ok ? 0 : 1;
        }

        private static int CheckNormalEnemyIgnoresPhaseTwo()
        {
            var state = CreateBoss(0.2f);
            state.IsBoss = false;
            var context = new FakeContext();

            EnemyBehaviors.Get(state).Tick(state, context, DeltaTime);

            var ok = true;
            ok &= Expect(context.Projectiles == 0, $"normal enemy must not burst, got {context.Projectiles}");
            ok &= Expect(context.Toasts == 0, $"normal enemy must not announce, got {context.Toasts}");
            return ok ? 0 : 1;
        }

        private static EnemyState CreateBoss(float hpRatio)
        {
            var def = new EnemyDef
            {
                Id = BattleSession.BossEnemyId,
                Name = "回归测试 Boss",
                Behavior = EnemyBehavior.Tank,
                Hp = 900f,
                Speed = 0.75f,
                Damage = 16f,
                Radius = 0.85f,
                ProjectileSpeed = 6f
            };

            return new EnemyState
            {
                Def = def,
                IsBoss = true,
                MaxHp = def.Hp,
                Hp = def.Hp * hpRatio,
                Speed = def.Speed,
                Damage = def.Damage,
                Radius = def.Radius,
                Position = Vector2.zero,
                AttackTimer = 0f
            };
        }

        private static bool Expect(bool condition, string message)
        {
            if (condition)
            {
                return true;
            }

            Debug.LogError("[BossPhaseCheck] " + message);
            return false;
        }

        /// <summary>只记账的上下文：行为要的副作用都记次数，避免真去刷怪或改玩家。</summary>
        private sealed class FakeContext : IEnemyContext
        {
            public int Projectiles;
            public int Toasts;

            public System.Random Rng { get; } = new System.Random(1);

            public Vector2 PlayerPosition => Vector2.zero;

            public float PlayerRadius => 0.45f;

            public float ElapsedSeconds => 12f;

            public void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage)
            {
                Projectiles++;
            }

            public void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer)
            {
            }

            public void SpawnEnemy(int defId, Vector2 position, EliteAffix affix, float elapsed)
            {
            }

            public void DespawnEnemy(EnemyState state)
            {
            }

            public void DamagePlayer(float amount)
            {
            }

            public void ApplySlowToPlayer(float duration)
            {
            }

            public void Toast(string message)
            {
                Toasts++;
            }
        }
    }
}
