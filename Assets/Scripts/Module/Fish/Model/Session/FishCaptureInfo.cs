/*
* ┌──────────────────────────────────┐
* │  描    述: 捕获事件负载，跨层传给 HUD 与结算的最小信息包
* │  类    名: FishCaptureInfo.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只带展示需要的字段，禁止把 FishInstance 或 FishDataSO 直接塞进事件，避免订阅方反向改玩法数据
*/

using Module.Fish.Type;

namespace Module.Fish.Model
{
    public readonly struct FishCaptureInfo
    {
        public FishCaptureInfo(string fishId, string displayName, FishRarity rarity, float depth)
        {
            FishId = fishId;
            DisplayName = displayName;
            Rarity = rarity;
            Depth = depth;
        }

        /// <summary>鱼种稳定标识</summary>
        public string FishId { get; }

        /// <summary>展示名</summary>
        public string DisplayName { get; }

        /// <summary>价值档位</summary>
        public FishRarity Rarity { get; }

        /// <summary>捕获时深度（米）</summary>
        public float Depth { get; }
    }
}