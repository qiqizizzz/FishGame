const fs = require('fs/promises');
const p = 'D:/CCodes/unity/FishGame/Assets/Scripts/Module/Fish/FishingController.cs';
const oldC = `            bool found = catching
                ? m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, true, m_catchFilter, out FishInstance fish)
                : m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, false, null, out fish);

            if (!found)
                return false;`;
const newC = `            FishInstance fish;

            bool found = catching
                ? m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, true, m_catchFilter, out fish)
                : m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, false, null, out fish);

            if (!found)
                return false;`;
(async () => {
  let s = await fs.readFile(p, 'utf8');
  if (!s.includes(oldC)) throw new Error('MISS');
  s = s.replace(oldC, newC);
  await fs.writeFile(p, s);
  console.log('OK');
})().catch(e => console.log('ERR ' + e.message));
