/*
* ┌──────────────────────────────────┐
* │  描    述: 布料总表，一块 SO 装下全部稀有度档与全部布料，板块这条链路的唯一数据源
* │  类    名: ClothCatalogSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 一块布就是一行配置：名字、颜色、稀有度、几条 BUFF、能刷出哪几种异形，全在数组里，不再拆成一堆小 SO
* 稀有度只在这里配一次，抽中权重与价格系数同档共用，越稀有越难刷出来也越贵
* 「布 × 异形」拼出来的板块不占资产，由本表按同一对下标缓存成同一个 ClothPieceDef 实例
* 实例必须稳定：BUFF 的封顶归属认的是 ClothBuffEntry 的引用，同一个组合每次给出不同对象就归不了组
* 校验全集中在 OnValidate，配置填错当场报，不留到运行时静默失效
*/

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 布料总表，稀有度档数组加布料数组，商店与背包都从这里取板块数据
    /// </summary>
    [CreateAssetMenu(menuName = "FishGame/Config/Cloth Catalog", fileName = "ClothCatalog")]
    public sealed class ClothCatalogSO : ScriptableObject
    {
        // ==================== 字段[外部设置] ====================
        [Header("稀有度档")]
        [Tooltip("按顺序排，布料用下标引用；一档同时给出抽中权重与价格系数")]
        [SerializeField] private List<ClothRarityTier> RarityTierList = new();

        [Header("布料")]
        [Tooltip("全部可售的布，空白布就是 BUFF 列表留空的那一块")]
        [SerializeField] private List<ClothEntry> ClothList = new();

        // ==================== 字段[私有] ====================
        private readonly Dictionary<long, ClothPieceDef> m_pieceCache = new();

        // ==================== 属性 ====================
        public IReadOnlyList<ClothRarityTier> RarityTiers => RarityTierList;
        public IReadOnlyList<ClothEntry> Cloths => ClothList;
        public int RarityTierCount => RarityTierList.Count;
        public int ClothCount => ClothList.Count;

        // ==================== Public Function ====================
        /// <summary>
        /// 按下标取稀有度档，越界时钳到有效区间，表为空返回空
        /// </summary>
        /// <param name="index">稀有度下标，从 0 开始</param>
        public ClothRarityTier RarityAt(int index)
        {
            if (RarityTierList.Count == 0)
                return null;

            return RarityTierList[Mathf.Clamp(index, 0, RarityTierList.Count - 1)];
        }

        /// <summary>
        /// 按下标取布料，越界返回空，调用方自己跳过这一格
        /// </summary>
        /// <param name="index">布料下标，从 0 开始</param>
        public ClothEntry ClothAt(int index)
        {
            if (index < 0 || index >= ClothList.Count)
                return null;

            return ClothList[index];
        }

        /// <summary>
        /// 按布标识查它在总表里的下标，查不到返回 -1，报不报错由调用方定
        /// </summary>
        /// <param name="clothId">布标识，例如 cloth_blank</param>
        public int IndexOfCloth(string clothId)
        {
            if (string.IsNullOrEmpty(clothId))
                return -1;

            for (int i = 0; i < ClothList.Count; i++)
            {
                ClothEntry cloth = ClothList[i];

                if (cloth != null && cloth.Id == clothId)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 用布标识拼一块板块，配置里写名字不写下标，删掉中间某块布也不会让开局布指错地方
        /// </summary>
        /// <param name="clothId">布标识</param>
        /// <param name="shapeIndex">该块布形状列表里的下标</param>
        public ClothPieceDef ResolveByClothId(string clothId, int shapeIndex)
        {
            int clothIndex = IndexOfCloth(clothId);

            if (clothIndex < 0)
            {
                QLog.Error($"[{nameof(ClothCatalogSO)}] 总表里没有这块布：{clothId}");
                return null;
            }

            return Resolve(clothIndex, shapeIndex);
        }

        /// <summary>
        /// 取指定稀有度档的抽中权重，取不到时给 0，加权表会自动忽略这一项
        /// </summary>
        /// <param name="rarityIndex">稀有度下标</param>
        public float DrawWeightOf(int rarityIndex)
        {
            ClothRarityTier tier = RarityAt(rarityIndex);
            return tier != null ? tier.DrawWeightValue : 0f;
        }

        /// <summary>
        /// 取指定稀有度档的价格系数，取不到时按原价卖
        /// </summary>
        /// <param name="rarityIndex">稀有度下标</param>
        public float PriceScaleOf(int rarityIndex)
        {
            ClothRarityTier tier = RarityAt(rarityIndex);
            return tier != null ? tier.PriceScaleValue : 1f;
        }

        /// <summary>
        /// 把「一块布 × 一个异形」拼成一件可上架的板块，同一对下标永远返回同一个实例
        /// </summary>
        /// <param name="clothIndex">布料下标</param>
        /// <param name="shapeIndex">该块布形状列表里的下标</param>
        public ClothPieceDef Resolve(int clothIndex, int shapeIndex)
        {
            ClothEntry cloth = ClothAt(clothIndex);

            if (cloth == null)
            {
                QLog.Error($"[{nameof(ClothCatalogSO)}] 布料下标越界，拼不出板块：{clothIndex}");
                return null;
            }

            IReadOnlyList<GridShapeSO> shapes = cloth.Shapes;

            if (shapeIndex < 0 || shapeIndex >= shapes.Count)
            {
                QLog.Error($"[{nameof(ClothCatalogSO)}] 形状下标越界，拼不出板块：{cloth.Id} 第 {shapeIndex} 个形状");
                return null;
            }

            GridShapeSO shape = shapes[shapeIndex];

            if (shape == null)
            {
                QLog.Error($"[{nameof(ClothCatalogSO)}] 形状槽位是空的：{cloth.Id} 第 {shapeIndex} 个形状");
                return null;
            }

            long cacheKey = ((long) clothIndex << 32) | (uint) shapeIndex;

            if (m_pieceCache.TryGetValue(cacheKey, out ClothPieceDef cached) && cached.Shape == shape)
                return cached;

            ClothPieceDef piece = new ClothPieceDef(cloth, shape);
            m_pieceCache[cacheKey] = piece;
            return piece;
        }

        /// <summary>
        /// 丢掉全部拼件缓存，配置改动后调用，避免旧实例还指着改前的布
        /// </summary>
        public void InvalidatePieceCache()
        {
            m_pieceCache.Clear();
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            m_pieceCache.Clear();

            if (RarityTierList.Count == 0)
                QLog.Error($"[{nameof(ClothCatalogSO)}] 没有配任何稀有度档，板块抽不出权重也定不了价：{name}");

            if (ClothList.Count == 0)
                QLog.Error($"[{nameof(ClothCatalogSO)}] 布料表是空的，货架上不会出现板块：{name}");

            var seenIds = new HashSet<string>();

            for (int i = 0; i < ClothList.Count; i++)
            {
                ClothEntry cloth = ClothList[i];

                if (cloth == null)
                {
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 第 {i} 行布料是空的：{name}");
                    continue;
                }

                string label = string.IsNullOrEmpty(cloth.Id) ? $"第 {i} 行" : cloth.Id;

                if (string.IsNullOrEmpty(cloth.Id))
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料缺少标识：{name} 第 {i} 行");
                else if (!seenIds.Add(cloth.Id))
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料标识重复，同一对下标会撞缓存：{cloth.Id}");

                if (string.IsNullOrEmpty(cloth.Label))
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料缺少显示名：{label}");

                if (cloth.ShapeCount == 0)
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料没配任何异形，永远刷不上货架：{label}");

                if (RarityTierList.Count == 0)
                    continue;

                if (cloth.RarityIndex < 0 || cloth.RarityIndex >= RarityTierList.Count)
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料引用的稀有度下标越界：{label} 引用 {cloth.RarityIndex}，共 {RarityTierList.Count} 档");

                checkBuffs(cloth, label);
            }
        }

        // 逐条 BUFF 查配置是否自洽：额度与封顶必须同号，负载这种要取整的线必须整块定额且填整数
        private void checkBuffs(ClothEntry cloth, string label)
        {
            IReadOnlyList<ClothBuffEntry> buffs = cloth.Buffs;

            for (int i = 0; i < buffs.Count; i++)
            {
                ClothBuffEntry buff = buffs[i];

                if (buff == null)
                {
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 布料的 BUFF 槽位是空的：{label} 第 {i} 条");
                    continue;
                }

                if (buff.Mod == BackpackStatMod.None)
                {
                    if (buff.PerCellValue != 0f || buff.FlatValueOf != 0f || buff.CapValue != 0f)
                        QLog.Error($"[{nameof(ClothCatalogSO)}] 空白布不该带加成：{label} 第 {i} 条选的是 None 却填了数值");

                    continue;
                }

                bool isFlat = buff.ModeValue == ClothBuffMode.Flat;
                float amount = isFlat ? buff.FlatValueOf : buff.PerCellValue;

                if (isFlat && buff.PerCellValue != 0f)
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 整块定额的 BUFF 不该再填每格额度，取量只会读定额：{label} 第 {i} 条 {buff.Mod}");

                if (amount == 0f)
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 这条 BUFF 给零，等于没配：{label} 第 {i} 条 {buff.Mod} 计价 {buff.ModeValue}");

                if (buff.CapValue == 0f)
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 这条 BUFF 的封顶填了 0，等于把这条线封死：{label} 第 {i} 条 {buff.Mod}");
                else if (Mathf.Sign(amount) != Mathf.Sign(buff.CapValue))
                    QLog.Error($"[{nameof(ClothCatalogSO)}] 加成额度与封顶符号相反，加成会被夹成零：{label} 第 {i} 条 {buff.Mod} 额度 {amount} 封顶 {buff.CapValue}");

                if (buff.Mod == BackpackStatMod.MaxCarryCount)
                    checkCarryBuff(buff, label, i, isFlat, amount);
            }
        }

        // 负载是全工程唯一被取整的能力线：按格给小数会在「3 格 x 0.25 = 0.75」这类组合上被取整吞成 0，
        // 玩家花整件的钱买回一块零收益的布，所以这条线只允许整块定额、整数额度、整数封顶
        private void checkCarryBuff(ClothBuffEntry buff, string label, int index, bool isFlat, float amount)
        {
            if (!isFlat)
                QLog.Error($"[{nameof(ClothCatalogSO)}] 负载线必须用整块定额计价，按格给小数会被取整吞掉：{label} 第 {index} 条 每格 {amount}");

            if (amount <= 0f)
                QLog.Error($"[{nameof(ClothCatalogSO)}] 负载线的额度必须为正整数：{label} 第 {index} 条 额度 {amount}");

            if (!isWholeNumber(amount))
                QLog.Error($"[{nameof(ClothCatalogSO)}] 负载线的额度带小数，卡面显示值与进背包的实际值不一致：{label} 第 {index} 条 额度 {amount}");

            if (!isWholeNumber(buff.CapValue))
                QLog.Error($"[{nameof(ClothCatalogSO)}] 负载线的封顶带小数，取整后这块布的负载上限会缩水：{label} 第 {index} 条 封顶 {buff.CapValue}");
        }

        // 判断一个数是否落在整数上，留一点浮点容差，免得 1.0000001 这类误报
        private bool isWholeNumber(float value)
        {
            return Mathf.Abs(value - Mathf.Round(value)) <= 0.0001f;
        }
    }
}
