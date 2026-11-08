using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体视图池（P3 收尾）。弹体是全场最频繁生灭的对象：20 倍速下一局上万发，
    /// 每次 `Instantiate` + `Destroy` 都会带一次堆分配与一次 GC 压力，所以复用到手就复用。
    /// 只池「表现」，逻辑状态仍由 <see cref="ProjectileSystem"/> 每次新建/回收。
    /// </summary>
    public sealed class ProjectileViewPool
    {
        private readonly Transform _root;
        private readonly Stack<ProjectileUnit> _idle = new Stack<ProjectileUnit>(64);

        public ProjectileViewPool(Transform root)
        {
            _root = root;
        }

        /// <summary>取一个视图并绑定到新状态；池空时新建。</summary>
        public ProjectileUnit Rent(ProjectileState state, string spriteKey, string name)
        {
            var unit = _idle.Count > 0 ? _idle.Pop() : CreateNew();
            unit.gameObject.SetActive(true);
            unit.name = name;
            unit.Init(state, spriteKey);
            return unit;
        }

        /// <summary>状态已结束：解绑、隐藏、放回池子。</summary>
        public void Return(ProjectileUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.ClearState();
            unit.gameObject.SetActive(false);
            _idle.Push(unit);
        }

        /// <summary>整场结束：把仍在外面的视图也收回（不销毁，下一局继续用）。</summary>
        public void ReturnAll(List<ProjectileUnit> active)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                Return(active[i]);
            }

            active.Clear();
        }

        private ProjectileUnit CreateNew()
        {
            var go = BattleVisuals.Spawn(BattleVisuals.ProjectilePrefab, "Projectile", _root);
            return go.GetComponent<ProjectileUnit>() ?? go.AddComponent<ProjectileUnit>();
        }
    }

    /// <summary>拾取物视图池：一整局会掉几百个经验糖，理由同 <see cref="ProjectileViewPool"/>。</summary>
    public sealed class PickupViewPool
    {
        private readonly Transform _root;
        private readonly Stack<PickupUnit> _idle = new Stack<PickupUnit>(64);

        public PickupViewPool(Transform root)
        {
            _root = root;
        }

        public PickupUnit Rent(PickupState state)
        {
            var unit = _idle.Count > 0 ? _idle.Pop() : CreateNew();
            unit.gameObject.SetActive(true);
            unit.Init(state);
            return unit;
        }

        public void Return(PickupUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.ClearState();
            unit.gameObject.SetActive(false);
            _idle.Push(unit);
        }

        public void ReturnAll(List<PickupUnit> active)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                Return(active[i]);
            }

            active.Clear();
        }

        private PickupUnit CreateNew()
        {
            var go = BattleVisuals.Spawn(BattleVisuals.PickupPrefab, "Pickup", _root);
            return go.GetComponent<PickupUnit>() ?? go.AddComponent<PickupUnit>();
        }
    }
}
