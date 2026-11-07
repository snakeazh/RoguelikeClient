using System;

namespace App.Config
{
    /// <summary>
    /// 示例配置表（Res/Config/DemoConfig.json）。保留这张表的目的是演示完整链路：
    /// json → ConfigJson.FromArrayJson → ConfigRowBase.Load → 界面读取。
    /// 新增配置表：照抄本文件与 ConfigTables.LoadAsync 里的一行即可。
    /// 注意：JsonUtility 按字段名精确匹配，json 的 key 必须和 C# 字段同名（PascalCase），
    /// 用 camelCase 会全部反序列化成默认值。
    /// </summary>
    [Serializable]
    public sealed class DemoItemConfig : ConfigRowBase<DemoItemConfig>
    {
        public string Name;

        public int Value;

        public string Desc;
    }
}
