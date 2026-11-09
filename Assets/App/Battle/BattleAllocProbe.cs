using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// GC0 排查工具：按「子系统」统计各自触发了多少次 Gen0 回收，结算时打一张表。
    ///
    /// 为什么按次数而不是按字节：Unity 2022.3 的 Mono 上 <c>GC.GetAllocatedBytesForCurrentThread()</c>
    /// 恒返回 0（已实测），所以退一步用「谁把 gen0 预算耗光」来定位。
    /// 一个区段触发的 GC 次数正比于它的分配量，量级差一个数量级时排序是可靠的。
    /// 字节量只按帧统计（脚本内 vs 帧尾），用 <c>ProfilerRecorder</c> 的 "GC Allocated In Frame"。
    ///
    /// 结论（2026-11）：脚本层约 0.1 KB/帧，剩余的分配几乎全在「最后一个 LateUpdate 之后」
    /// 的引擎/编辑器帧尾，因此 batchmode 的 gc0 不能直接当成游戏客户端的 gc0。
    ///
    /// 默认关闭，只有 <c>BattleSmokeTest</c> 加 <c>-battleAlloc 1</c> 才打开；关闭时所有入口都是空转。
    /// </summary>
    public static class BattleAllocProbe
    {
        private static readonly Dictionary<string, long> Totals = new Dictionary<string, long>(16);
        private static readonly List<string> Order = new List<string>(16);
        private static long _frameStart;
        private static long _afterUpdate;
        private static long _tailStart;
        private static ProfilerRecorder _frameBytes;
        private static long _totalBytes;
        private static long _bytesAtTail;
        private static long _bytesTail;
        private static bool _tailSample;

        public static bool Enabled { get; private set; }

        public static void Enable()
        {
            Enabled = true;
            Totals.Clear();
            Order.Clear();
            _frameStart = GC.CollectionCount(0);
            _afterUpdate = _frameStart;
            _tailStart = _frameStart;
            _totalBytes = 0;
            _bytesTail = 0;
            _bytesAtTail = 0;
            _tailSample = false;
            if (!_frameBytes.Valid)
            {
                _frameBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            }
        }

        /// <summary>帧首：把「上一帧结束 → 这一帧开始」之间产生的 GC 记到 engine-tail。</summary>
        public static void MarkFrameStart()
        {
            if (!Enabled)
            {
                return;
            }

            End("engine-tail", _tailStart);
            _frameStart = GC.CollectionCount(0);

            // 上一帧「LateUpdate 之后到本帧开始」分配了多少：LastValue 是整帧累计值，减掉采样点即可。
            if (!_frameBytes.Valid)
            {
                return;
            }

            if (_tailSample)
            {
                var previousFrame = _frameBytes.LastValue;
                _totalBytes += previousFrame;

                var tail = previousFrame - _bytesAtTail;
                if (tail > 0)
                {
                    _bytesTail += tail;
                }
            }

            _tailSample = true;
        }

        /// <summary>帧尾：把本帧 Update 内的 GC 记到 battle-update。</summary>
        public static void MarkFrameEnd()
        {
            if (!Enabled)
            {
                return;
            }

            End("battle-update", _frameStart);
            _afterUpdate = GC.CollectionCount(0);
        }

        /// <summary>全部 LateUpdate 之后：把「Update 结束 → 最后一个 LateUpdate」的 GC 记到 script-late。</summary>
        public static void MarkFrameTail()
        {
            if (!Enabled)
            {
                return;
            }

            End("script-late", _afterUpdate);
            _tailStart = GC.CollectionCount(0);
            if (_frameBytes.Valid)
            {
                _bytesAtTail = _frameBytes.LastValue;
            }
        }

        public static long Begin()
        {
            return Enabled ? GC.CollectionCount(0) : 0L;
        }

        /// <summary>区段作用域：<c>using var scope = new BattleAllocProbe.Scope("fx");</c>，早返回也安全。</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly string _key;
            private readonly long _start;

            public Scope(string key)
            {
                _key = key;
                _start = Begin();
            }

            public void Dispose()
            {
                End(_key, _start);
            }
        }

        public static void End(string key, long start)
        {
            if (!Enabled)
            {
                return;
            }

            var collections = GC.CollectionCount(0) - start;
            if (Totals.TryGetValue(key, out var total))
            {
                Totals[key] = total + collections;
                return;
            }

            Totals.Add(key, collections);
            Order.Add(key);
        }

        /// <summary>把累计结果打成一行，按 GC 次数从多到少。</summary>
        public static void Dump(int frames)
        {
            if (!Enabled)
            {
                return;
            }

            Order.Sort((a, b) => Totals[b].CompareTo(Totals[a]));
            var builder = new StringBuilder();
            builder.Append("[Alloc] frames=").Append(frames);
            if (_frameBytes.Valid && frames > 0)
            {
                var perFrame = _totalBytes / frames;
                builder.Append(" totalKB=").Append(_totalBytes / 1024)
                    .Append(" perFrameB=").Append(perFrame)
                    .Append(" tailKB=").Append(_bytesTail / 1024)
                    .Append(" scriptKB=").Append((_totalBytes - _bytesTail) / 1024);
            }

            foreach (var key in Order)
            {
                builder.Append(" | ").Append(key).Append('=').Append(Totals[key]).Append("次");
            }

            Debug.Log(builder.ToString());
        }
    }
}
