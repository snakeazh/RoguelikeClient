using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 武器表现的出口（端口）。
    ///
    /// 规则层（<see cref="IWeaponBehavior"/> 的实现）只说「这里要一颗炸弹 / 一团毒雾 / 一片刀叶 / 一台炮台」，
    /// 以及「这一帧它的姿态、透明度该怎么变」；真正的 GameObject、精灵、排序号都在表现层实现。
    /// headless 回归时换成 <see cref="NullWeaponViews"/>，规则层就能在零 Unity 对象的情况下跑完一整局。
    /// </summary>
    public interface IWeaponViews
    {
        IBombView CreateBomb(Vector2 position, float scale);

        IPoisonView CreatePoison(Vector2 position, float radius);

        IBladeView CreateBlade(float size);

        ICannonView CreateCannon();

        /// <summary>原地爆一下（毒雾落点、炸弹爆炸、闪电跳击）。</summary>
        void Flash(Vector2 position, float diameter, Color color, float life);

        /// <summary>扇形刀光（大剑）。</summary>
        void Fan(Vector2 origin, float angleDegrees, float radius, float spreadDegrees, Color color, float life);

        /// <summary>电弧（雷电法典）：两点之间一条抖动折线。</summary>
        void Bolt(Vector2 from, Vector2 to, Color color, float life);

        /// <summary>魔法飞弹的范围光环：贴在玩家脚下，跟随半径变化。</summary>
        void ShowAura(bool visible, float radius);
    }

    /// <summary>地上的定时炸弹视图。规则层只关心它在哪、引信还剩多久。</summary>
    public interface IBombView
    {
        Vector2 Position { get; }

        /// <summary>引信脉冲：越接近爆炸抖得越快。</summary>
        void Pulse(float elapsedSeconds);

        void Dispose();
    }

    /// <summary>毒雾视图。淡出与旋转是纯表现，规则层只给剩余寿命比例。</summary>
    public interface IPoisonView
    {
        void Tick(float elapsedSeconds, float remainRatio, float deltaTime);

        void Dispose();
    }

    /// <summary>环绕刃视图。位置与朝向由规则层算好，这里只负责落到 Transform 上。</summary>
    public interface IBladeView
    {
        void SetPose(Vector2 position, float degrees);

        void Dispose();
    }

    /// <summary>
    /// 随身炮台视图。位姿（含转向平滑）由规则层算好，这里只照着摆：
    /// 视图一旦参与「炮口在哪」，弹体出生点就会跟着帧率变，同种子就打不出同一局。
    /// </summary>
    public interface ICannonView
    {
        void SetPose(Vector2 bodyPosition, Vector2 direction);

        void SetCharge(float value);

        void Kick();

        void Dispose();
    }
}
