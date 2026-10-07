const fs = require('fs/promises');

const detPath = 'D:/CCodes/unity/FishGame/Assets/Scripts/Module/Fish/Service/FishDetectionService.cs';
const viewPath = 'D:/CCodes/unity/FishGame/Assets/Scripts/Module/Fish/View/HookViewController.cs';

const detPairs = [
[`        private const float MIN_TOUCH_RADIUS = 0.25f;`,
 `        // 小鱼自身的半高太小，留一点最低容差，同时不超过收线一帧的位移太多\n        private const float MIN_TOUCH_RADIUS = 0.1f;`],
[`        // 该鱼的有效接触半径：按鱼身实际显示高度留容差，小鱼也保留最低判定范围
        public static float TouchRadius(FishInstance fish, float hookRadius)
        {
            float length = fish.Data != null ? fish.Data.DisplayLength : 1f;
            float height = fish.DisplayHeight > 0f ? fish.DisplayHeight : length * 0.3f;
            float fishRadius = Mathf.Max(height * 0.5f, length * 0.2f, MIN_TOUCH_RADIUS);

            return hookRadius + fishRadius;
        }`,
`        // 该鱼的有效接触半径：横向容差已由鱼身线段提供，这里只按实际显示半高留量
        public static float TouchRadius(FishInstance fish, float hookRadius)
        {
            float length = fish.Data != null ? fish.Data.DisplayLength : 1f;
            float height = fish.DisplayHeight > 0f ? fish.DisplayHeight : length * FALLBACK_HEIGHT_RATIO;
            float fishRadius = Mathf.Max(height * 0.5f, MIN_TOUCH_RADIUS);

            return hookRadius + fishRadius;
        }`],
[`        private const float MIN_TOUCH_RADIUS = 0.1f;`,
 `        private const float MIN_TOUCH_RADIUS = 0.1f;\n        private const float FALLBACK_HEIGHT_RATIO = 0.3f;`]
];

const viewPairs = [
[`        // 竖屏正交镜头下，横向像素到世界米数与纵向同比例，按相机高度换算即可
        // 以钩尖模型最低点作为捕获点，避免根节点中心仍高于鱼身时视觉上已经钩中但判定未触发
        private Vector3 setupCaptureOffset()`,
`        // 竖屏正交镜头下，横向像素到世界米数与纵向同比例，按相机高度换算即可
        // 以钩尖模型最低点作为捕获点，避免根节点中心仍高于鱼身时视觉上已经钩中但判定未触发
        // 偏移必须换算到父级空间：Depth 与 Horizontal 取的是 localPosition，父级缩放与钩子缩放都不能漏掉
        private Vector3 setupCaptureOffset()`],
[`            return transform.InverseTransformPoint(hookTipPoint);`,
`            Vector3 tipInParent = transform.parent != null
                ? transform.parent.InverseTransformPoint(hookTipPoint)
                : hookTipPoint;

            return tipInParent - m_transform.localPosition;`]
];

(async () => {
  for (const [p, pairs] of [[detPath, detPairs], [viewPath, viewPairs]]) {
    let s = await fs.readFile(p, 'utf8');
    for (const [a, b] of pairs) {
      if (!s.includes(a)) throw new Error('MISS in ' + p + ':\n' + a.slice(0, 70));
      s = s.replace(a, b);
    }
    await fs.writeFile(p, s);
    console.log('OK ' + p.split('/').pop());
  }
})().catch(e => console.log('ERR ' + e.message));
