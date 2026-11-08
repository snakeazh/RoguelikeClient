using App.Resources;
using Framework.UI.Binding;
using Framework.UI.Navigation;
using Framework.UI.View;
using TMPro;
using UnityEngine.UI;

namespace App.UI.Home
{
    /// <summary>
    /// Demo 首页 View：只做"把 prefab 上的控件和 ViewModel 接起来"这一件事。
    /// 预制体 Assets/Res/UI/Home.prefab 的键名：Title / Subtitle / Content / Status / CharacterInfo /
    /// BtnBattle / BtnDialog / BtnToast / BtnClick / BtnCharacterRanger / BtnCharacterWarrior
    /// （UIBind 的 key 默认取节点名）。
    /// </summary>
    [AutoScreen(AppScreenIds.Home, UILayer.Page, ResResourcePaths.Home)]
    public sealed class HomeView : ViewBase<HomeViewModel>
    {
        protected override void OnBind()
        {
            Binding.BindText(UI.Get<TMP_Text>("Title"), ViewModel.Title);
            Binding.BindText(UI.Get<TMP_Text>("Subtitle"), ViewModel.Subtitle);
            Binding.BindText(UI.Get<TMP_Text>("Content"), ViewModel.Content);
            Binding.BindText(UI.Get<TMP_Text>("Status"), ViewModel.Status);
            Binding.BindText(UI.Get<TMP_Text>("CharacterInfo"), ViewModel.CharacterInfo);

            Binding.BindCommand(UI.Get<Button>("BtnBattle"), ViewModel.StartBattleCommand);
            Binding.BindCommand(UI.Get<Button>("BtnDialog"), ViewModel.OpenDialogCommand);
            Binding.BindCommand(UI.Get<Button>("BtnToast"), ViewModel.ShowToastCommand);
            Binding.BindCommand(UI.Get<Button>("BtnClick"), ViewModel.PlayClickCommand);
            Binding.BindCommand(UI.Get<Button>("BtnCharacterRanger"), ViewModel.SelectRangerCommand);
            Binding.BindCommand(UI.Get<Button>("BtnCharacterWarrior"), ViewModel.SelectWarriorCommand);
        }
    }
}
