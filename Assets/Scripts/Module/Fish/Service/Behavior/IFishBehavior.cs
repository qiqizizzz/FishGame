/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼行为策略接口，每种行为模板一个实现
* │  类    名: IFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 策略模式：鱼种行为差异不靠 if 分支，新增行为只加一个实现并注册
*/

using Module.Fish.Model;
using Module.Fish.Type;

namespace Module.Fish.Service
{
    public interface IFishBehavior
    {
        FishBehaviorType Behavior { get; }

        /// <summary>
        /// 推进一条鱼的位置，只改实例数据，写变换由 FishAiService 统一做
        /// </summary>
        /// <param name="hook">本帧钩子探针，不感知钩子的行为直接忽略</param>
        void Advance(FishInstance fish, float deltaTime, float swimHalfWidth, in HookProbe hook);
    }
}