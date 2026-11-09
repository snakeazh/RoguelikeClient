using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 分配归因用的帧尾标记：最后一个 LateUpdate 之后记一次 GC 计数，
    /// 这样 <see cref="BattleAllocProbe"/> 就能把「脚本 Update/LateUpdate 里」与「引擎自己的帧尾」分开。
    /// 只有冒烟测试打开探针时才有意义，平时只是空转。
    /// </summary>
    [DefaultExecutionOrder(30000)]
    public sealed class BattleFrameTail : MonoBehaviour
    {
        private void LateUpdate()
        {
            BattleAllocProbe.MarkFrameTail();
        }
    }
}
