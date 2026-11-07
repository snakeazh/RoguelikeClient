using App.Resources;
using Framework.UI.Binding;
using Framework.UI.Navigation;
using Framework.UI.View;
using TMPro;
using UnityEngine.UI;

namespace App.UI.Battle
{
    /// <summary>
    /// 战斗 HUD（Hud 层，战斗全程常驻）。
    /// 预制体键名：Timer / Level / Kills / Weapons / Passives / BossHint / HpText / HpBar / ExpBar / BtnDash。
    /// </summary>
    [AutoScreen(AppScreenIds.BattleHud, UILayer.Hud, ResResourcePaths.BattleHud)]
    public sealed class BattleHudView : ViewBase<BattleHudViewModel>
    {
        protected override void OnBind()
        {
            Binding.BindText(UI.Get<TMP_Text>("Timer"), ViewModel.Timer);
            Binding.BindText(UI.Get<TMP_Text>("Level"), ViewModel.LevelText);
            Binding.BindText(UI.Get<TMP_Text>("Kills"), ViewModel.KillText);
            Binding.BindText(UI.Get<TMP_Text>("Weapons"), ViewModel.WeaponText);
            Binding.BindText(UI.Get<TMP_Text>("Passives"), ViewModel.PassiveText);
            Binding.BindText(UI.Get<TMP_Text>("BossHint"), ViewModel.BossHint);
            Binding.BindText(UI.Get<TMP_Text>("HpText"), ViewModel.HpText);

            Binding.BindSlider(UI.Get<Slider>("HpBar"), ViewModel.HpFill, false);
            Binding.BindSlider(UI.Get<Slider>("ExpBar"), ViewModel.ExpFill, false);

            Binding.BindCommand(UI.Get<Button>("BtnDash"), ViewModel.DashCommand);
        }
    }
}
