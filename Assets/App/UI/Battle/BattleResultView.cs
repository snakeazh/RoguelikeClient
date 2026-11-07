using App.Resources;
using Framework.UI.Binding;
using Framework.UI.Navigation;
using Framework.UI.View;
using TMPro;
using UnityEngine.UI;

namespace App.UI.Battle
{
    /// <summary>
    /// 战斗结算（Popup 层）。预制体键名：Title / Subtitle / Stats / BtnReturn。
    /// </summary>
    [AutoScreen(AppScreenIds.BattleResult, UILayer.Popup, ResResourcePaths.BattleResult)]
    public sealed class BattleResultView : ViewBase<BattleResultViewModel>
    {
        protected override void OnBind()
        {
            Binding.BindText(UI.Get<TMP_Text>("Title"), ViewModel.Title);
            Binding.BindText(UI.Get<TMP_Text>("Subtitle"), ViewModel.Subtitle);
            Binding.BindText(UI.Get<TMP_Text>("Stats"), ViewModel.Stats);
            Binding.BindCommand(UI.Get<Button>("BtnReturn"), ViewModel.ReturnCommand);
        }
    }
}
