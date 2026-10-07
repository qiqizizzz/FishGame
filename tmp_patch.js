const fs = require('fs/promises');
const p = 'D:/CCodes/unity/FishGame/Assets/Scripts/Module/Fish/FishingController.cs';

const oldDoc = `        /// <summary>
        /// 裁决钩子与周围鱼的一次接触，规则见 01 篇 3.3
        /// </summary>
        /// <param name="catching">true 为回收阶段的鱼头捕获，false 为下潜阶段的任意接触即失误</param>
        /// <returns>需要进入缠绕态时返回 true</returns>`;

const newDoc = `        /// <summary>
        /// 裁决钩子与周围鱼的一次接触，规则见 01 篇 3.3
        /// 判定取钩尖捕获点而非钩子根节点，回收阶段按鱼身线段接触捕获
        /// </summary>
        /// <param name="catching">true 为回收阶段的捕获，false 为下潜阶段的任意接触即失误</param>
        /// <returns>需要进入缠绕态时返回 true</returns>`;

const oldBody = `            HookConfigSO config = hook.HookConfig;

            if (!m_detection.TryFindTouched(hook.Horizontal, hook.Depth, config.HookRadius, catching, out FishInstance fish))
                return false;

            m_contactCooldown = CONTACT_COOLDOWN;

            if (fish.Data != null && fish.Data.Rarity == FishRarity.Hazard)
            {
                penalizeHazard(fish);
                return true;
            }

            if (catching)
                attachFish(fish);
            else
                scareFish(fish);

            return false;`;

const newBody = `            HookConfigSO config = hook.HookConfig;

            bool found = catching
                ? m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, true, m_catchFilter, out FishInstance fish)
                : m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, false, null, out fish);

            if (!found)
                return false;

            if (fish.Data != null && fish.Data.Rarity == FishRarity.Hazard)
            {
                m_contactCooldown = CONTACT_COOLDOWN;
                penalizeHazard(fish);
                return true;
            }

            if (catching)
            {
                // 容量不足时不吃冷却，钩子继续往上走还能钩住后面能装下的鱼
                if (!attachFish(fish))
                    return false;

                m_contactCooldown = CONTACT_COOLDOWN;
                return false;
            }

            m_contactCooldown = CONTACT_COOLDOWN;
            scareFish(fish);

            return false;`;

const oldAttach = `        private void attachFish(FishInstance fish)
        {
            CatchRecord record = m_carry.Attach(fish);

            if (record == null)
                return;

            EventManager.PublishEvent(EventDefines.FishingFishCaptured, captureInfo(fish, record.AttachedDepth));
        }`;

const newAttach = `        // 只有没上钩且容量装得下的鱼才参与捕获判定
        private bool canCatchFish(FishInstance fish)
        {
            if (fish == null || fish.IsHooked)
                return false;

            return fish.Data != null &&
                (fish.Data.Rarity == FishRarity.Hazard || m_carry.CanAttach(fish));
        }

        private bool attachFish(FishInstance fish)
        {
            CatchRecord record = m_carry.Attach(fish);

            if (record == null)
                return false;

            EventManager.PublishEvent(EventDefines.FishingFishCaptured, captureInfo(fish, record.AttachedDepth));

            return true;
        }`;

(async () => {
  const src = await fs.readFile(p, 'utf8');
  let s = src;
  for (const [a, b] of [[oldDoc, newDoc], [oldBody, newBody], [oldAttach, newAttach]]) {
    if (!s.includes(a)) throw new Error('MISS block:\n' + a.slice(0, 80));
    s = s.replace(a, b);
  }
  await fs.writeFile(p, s);
  console.log('OK patched, changed =', s !== src);
})().catch(e => console.log('ERR ' + e.message));
