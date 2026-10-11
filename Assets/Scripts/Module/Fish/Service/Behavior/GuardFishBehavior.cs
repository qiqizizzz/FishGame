/*
* ┌──────────────────────────────────┐
* │  描    述: 警戒漂行行为，障碍专用，绕出生岗位定幅横向巡守
* │  类    名: GuardFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 挡路但不能定死：钉在一点的河豚会让「规划穿行路线」退化成认死一个静态路障，看穿就没有博弈
* 横向绝不做绝对定位，只做速度积分加折返，否则被惊散游远后会在逃逸结束的当帧瞬移回岗位
*/

using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class GuardFishBehavior : IFishBehavior
    {
        // 巡守速度，慢到玩家看得出在动、也来得及照着它的走位挑穿行的时机
        private const float PATROL_SPEED = 0.25f;
        // 离岗位的横向半径，鱼的游动半宽也就 3.3 米左右，0.7 已经够让一整条鱼挪出能穿过去的缝
        private const float PATROL_RANGE = 0.7f;
        // 超出半径多半是刚被惊散回来，回岗比平时巡守快点，免得那个位置长时间空着
        private const float RETURN_SPEED = 0.5f;

        private const float HOVER_AMPLITUDE = 0.45f;
        private const float HOVER_FREQUENCY = 0.5f;

        public FishBehaviorType Behavior => FishBehaviorType.Guard;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth, in HookProbe hook)
        {
            fish.Phase += deltaTime;

            patrolToPost(fish, deltaTime, swimHalfWidth);

            fish.Depth = fish.BaseDepth + Mathf.Sin(fish.Phase * HOVER_FREQUENCY) * HOVER_AMPLITUDE;
        }

        // 定幅巡守：绕岗位两侧折返，游出半径就掉头回岗，撞壁顺着壁滑，全程不动坐标基准
        private static void patrolToPost(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            float offset = fish.X - fish.BaseX;
            bool returning = Mathf.Abs(offset) > PATROL_RANGE;

            if (returning)
                fish.Direction = offset > 0f ? -1 : 1;

            float speed = returning ? RETURN_SPEED : PATROL_SPEED;
            fish.X += fish.Direction * speed * deltaTime;

            if (Mathf.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.Direction = -fish.Direction;
            }
        }
    }
}
