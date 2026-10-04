# .codex/tmp 目录约定

AI 协作过程产生的临时文件统一放这里，全部按主题建子目录，不允许往根目录堆散文件。

## 目录结构

```text
.codex/tmp/
├─ scene-blockout/     钓鱼场景搭景阶段的相机截图
├─ water-surface/      水面与水体表现迭代的截图
├─ m1-descent/         下潜手感与鱼线调细阶段的截图
├─ m2-capture/         捕获链路的无头自测源码与运行输出
├─ tool-payload/       当前还在复用的 MCP 工具 .json 入参
│  └─ archive/         已执行完的一次性入参，只留档不再复用
└─ <新主题>/           新主题按上面格式建目录，一个主题一个目录

单目录文件数同样控制在 8 个以内，超了就按用途再分子目录
```

## 命名规则

- 文件名：`<语义>-<两位序号>.<扩展名>`，小写 kebab-case，序号按时间递增
  - 例：`layout-01.png`、`gradient-compare-01.png`、`no-post-01.png`
- 目录名：小写 kebab-case，表示主题，不用日期命名（日期查 git/mtime）
- 禁止：`xxx2.png`、`xxx_final_v3.png`、驼峰、空格、中文文件名
- MCP 入参：`tool-payload/<tool-name>.json`，空入参用 `empty.json`
- 一次性编辑器代码不进仓库，写到系统临时目录 `%TEMP%\fishgame\<任务名>\*.cs.txt`

## 备注

本目录早期被误提交进了版本库，历史文件用 `git mv` 保留追溯；
新的临时文件建议后续在 `.gitignore` 加 `.codex/tmp/` 后不再入库。