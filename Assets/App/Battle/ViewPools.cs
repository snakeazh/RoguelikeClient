using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗内所有「用一次就收」的表现对象共用一套池子（P2 收尾）。
    ///
    /// 池化的是**表现**：弹体、拾取物、敌人、瞬时特效、电弧都在这里租还，
    /// 逻辑状态仍由各自的 System 每次新建/回收。这样整场只有一种生命周期规则：
    /// 租出来 → 绑定状态或播放一次 → 用完还回池子（不销毁）。
    ///
    /// 为什么值得池化：20 倍速冒烟里一局有上万发弹体、几百个特效，
    /// 每次 `Instantiate`/`Destroy` 都是堆分配 + GC 压力，而且销毁/生成会让渲染批次抖动。
    /// </summary>
    public sealed class ViewPool<T> where T : Component
    {
        private readonly Transform _root;
        private readonly Func<T> _create;
        private readonly Stack<T> _idle = new Stack<T>(64);
        private readonly int _maxIdle;

        /// <param name="root">池内对象挂在谁下面（传 null 表示挂在场景根）。</param>
        /// <param name="create">池空时怎么造一个新的。</param>
        /// <param name="maxIdle">空闲上限：超过就真销毁，避免一次 Boss 弹幕把池子撑到几千个。</param>
        public ViewPool(Transform root, Func<T> create, int maxIdle = 128)
        {
            _root = root;
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _maxIdle = Mathf.Max(8, maxIdle);
        }

        public int IdleCount => _idle.Count;

        /// <summary>取一个视图并激活；池空时新建。调用方负责绑定状态（`Init`/`Play`）。</summary>
        public T Rent()
        {
            var view = _idle.Count > 0 ? _idle.Pop() : _create();
            if (view == null)
            {
                return null;
            }

            if (_root != null && view.transform.parent != _root)
            {
                view.transform.SetParent(_root, false);
            }

            view.gameObject.SetActive(true);
            return view;
        }

        /// <summary>用完还回来：先解绑状态、再隐藏。调用方应在还之前清掉自己的引用。</summary>
        public void Return(T view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);

            if (_idle.Count >= _maxIdle)
            {
                UnityEngine.Object.Destroy(view.gameObject);
                return;
            }

            _idle.Push(view);
        }

        /// <summary>整场结束：把还挂在场景里的视图也收回（下一局继续用）。</summary>
        public void ReturnAll(List<T> active)
        {
            if (active == null)
            {
                return;
            }

            for (var i = active.Count - 1; i >= 0; i--)
            {
                Return(active[i]);
            }

            active.Clear();
        }

        /// <summary>真正销毁池内对象（Runner 销毁、贴图/材质要重建时用）。</summary>
        public void Clear()
        {
            while (_idle.Count > 0)
            {
                var view = _idle.Pop();
                if (view != null)
                {
                    UnityEngine.Object.Destroy(view.gameObject);
                }
            }
        }
    }
}
