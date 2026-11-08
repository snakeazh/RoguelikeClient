using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>只弹一条提示，用来把「回血 + 文案」这类表现从效果逻辑里拆开。</summary>
    public sealed class ToastEffect : IPickupEffect
    {
        private readonly string _defaultText;

        public ToastEffect(string defaultText = "")
        {
            _defaultText = defaultText;
        }

        public void Apply(IPickupContext context, PickupUnit pickup, PickupArgs args)
        {
            var text = !string.IsNullOrEmpty(args.Text) ? args.Text : _defaultText;
            if (!string.IsNullOrEmpty(text))
            {
                context.Toast(text);
            }
        }
    }
}
