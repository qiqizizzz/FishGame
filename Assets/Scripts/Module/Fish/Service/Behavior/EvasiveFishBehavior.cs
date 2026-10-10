/*
* ┌──────────────────────────────────┐
* │  描    述: 闪避行为，钩子逼近时朝随机方向短冲，冷却窗口内可被预判捕获
* │  类    名: EvasiveFishBehavior.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 躲的只有下潜段：回收段再躲就永远抓不到，玩法就变成「下潜时把它逼跳 → 读落点 → 松手回收去接」
* 短冲主轴取横向背离，因为钩子竖直下潜比鱼快；竖直分量只许朝背离钩子的一侧，往上跳等于自己撞上去
* 落点仍钳在自己的深度带与泳道内，不让一条小鱼被赶得满井道乱窜，撞壁就顺着壁滑
*/

using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Service
{
    public sealed class EvasiveFishBehavior : IFishBehavior
    {
        private const float BOB_AMPLITUDE = 0.1f;
        private const float BOB_FREQUENCY = 1.8f;

        // 触发躲避的距离，要明显大于钩子的实际够得着的半径，否则鱼在要被挂上时才反应
        private const float DODGE_RADIUS = 0.75f;
        private const float DASH_DISTANCE = 1.8f;
        private const float DASH_DURATION = 0.26f;
        private const float DASH_SPEED = DASH_DISTANCE / DASH_DURATION;
        private const float DODGE_COOLDOWN = 0.9f;
        // 横向背离主轴向竖直方向的最大偏角，纯直线跳太容易被预判；70 度时横向仍保有 2.4 米/秒的逃逸速度
        private const float DODGE_CONE_DEGREES = 70f;
        // 钩子几乎贴着鱼的横向距离阈值，小于它就不信背离符号，改朝远离近侧壁的方向跳
        private const float MIN_AWAY_DISTANCE = 0.05f;

        public FishBehaviorType Behavior => FishBehaviorType.Evasive;

        public void Advance(FishInstance fish, float deltaTime, float swimHalfWidth, in HookProbe hook)
        {
            fish.Phase += deltaTime;

            if (fish.DodgeTimer > 0f)
                advanceDash(fish, deltaTime, swimHalfWidth);
            else
                advancePatrol(fish, deltaTime, swimHalfWidth, hook);

            fish.Depth = fish.BaseDepth + Mathf.Sin(fish.Phase * BOB_FREQUENCY) * BOB_AMPLITUDE;
        }

        // 短冲段：按冲出向量位移，撞壁就地反向，深度不许越出本来的水层
        private static void advanceDash(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.DodgeTimer = Mathf.Max(0f, fish.DodgeTimer - deltaTime);
            fish.X += fish.DodgeVector.x * deltaTime;
            fish.BaseDepth += fish.DodgeVector.y * deltaTime;

            if (Mathf.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.DodgeVector.x = -fish.DodgeVector.x;
            }

            if (fish.RoamBand.IsValid)
                fish.BaseDepth = Mathf.Clamp(fish.BaseDepth, fish.RoamBand.MinDepth, fish.RoamBand.MaxDepth);

            fish.Direction = fish.DodgeVector.x >= 0f ? 1 : -1;
        }

        // 平时的横向折返，顺便看一眼钩子有没有逼近
        private static void advancePatrol(FishInstance fish, float deltaTime, float swimHalfWidth, in HookProbe hook)
        {
            if (fish.DodgeCooldown > 0f)
                fish.DodgeCooldown = Mathf.Max(0f, fish.DodgeCooldown - deltaTime);

            fish.X += fish.Direction * fish.Speed * deltaTime;

            if (Mathf.Abs(fish.X) >= swimHalfWidth)
            {
                fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
                fish.Direction = -fish.Direction;
            }

            if (hook.IsThreatening && fish.DodgeCooldown <= 0f
                && FishDetectionService.TouchDistance(hook.X, hook.Depth, fish) <= DODGE_RADIUS)
                startDash(fish, hook);
        }

        // 起跳：横向背离钩子，再向「背离钩子那一侧的深度」叠一个锥形扰动，冲完进冷却窗口给玩家留出预判时间
        private static void startDash(FishInstance fish, in HookProbe hook)
        {
            float awayX = fish.X - hook.X;

            // 钩子几乎压在鱼的正上方时背离符号不可信，朝远离近侧壁的一侧跳，免得当场撞壁
            if (Mathf.Abs(awayX) < MIN_AWAY_DISTANCE)
                awayX = fish.X >= 0f ? -1f : 1f;

            // 竖直分量只许背离钩子：威胁只来自下潜段，往上跳等于自己撞进正在下潜的钩子里
            float awayDepth = fish.BaseDepth >= hook.Depth ? 1f : -1f;
            float angle = Random.Range(0f, DODGE_CONE_DEGREES) * Mathf.Deg2Rad;

            fish.DodgeVector = new Vector2((awayX > 0f ? 1f : -1f) * Mathf.Cos(angle), awayDepth * Mathf.Sin(angle))
                * DASH_SPEED;
            fish.DodgeTimer = DASH_DURATION;
            fish.DodgeCooldown = DASH_DURATION + DODGE_COOLDOWN;
        }
    }
}
