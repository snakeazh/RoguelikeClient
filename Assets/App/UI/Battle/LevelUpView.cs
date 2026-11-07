using App.Resources;
using Framework.UI.Binding;
using Framework.UI.Navigation;
using Framework.UI.View;
using TMPro;
using UnityEngine.UI;

namespace App.UI.Battle
{
    /// <summary>
    /// 升级三选一（Popup 层）。三个选项槽固定，方便做卡牌外观与动效。
    /// 预制体键名：Header / Opt1Title / Opt1Desc / Opt2Title / Opt2Desc / Opt3Title / Opt3Desc /
    /// BtnOption1 / BtnOption2 / BtnOption3。
    /// </summary>
    [AutoScreen(AppScreenIds.LevelUp, UILayer.Popup, ResResourcePaths.LevelUp)]
    public sealed class LevelUpView : ViewBase<LevelUpViewModel>
    {
        protected override void OnBind()
        {
            Binding.BindText(UI.Get<TMP_Text>("Header"), ViewModel.Header);

            Binding.BindText(UI.Get<TMP_Text>("Opt1Title"), ViewModel.Option1Title);
            Binding.BindText(UI.Get<TMP_Text>("Opt1Desc"), ViewModel.Option1Desc);
            Binding.BindText(UI.Get<TMP_Text>("Opt2Title"), ViewModel.Option2Title);
            Binding.BindText(UI.Get<TMP_Text>("Opt2Desc"), ViewModel.Option2Desc);
            Binding.BindText(UI.Get<TMP_Text>("Opt3Title"), ViewModel.Option3Title);
            Binding.BindText(UI.Get<TMP_Text>("Opt3Desc"), ViewModel.Option3Desc);

            Binding.BindCommand(UI.Get<Button>("BtnOption1"), ViewModel.Choose1Command);
            Binding.BindCommand(UI.Get<Button>("BtnOption2"), ViewModel.Choose2Command);
            Binding.BindCommand(UI.Get<Button>("BtnOption3"), ViewModel.Choose3Command);
        }
    }
}
