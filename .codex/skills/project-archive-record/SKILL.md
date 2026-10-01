---
name: project-archive-record
description: Create or update Protocol_Evac AI archive handoff records under 设计文档/AI归档记录. Use when the user asks to 归档, 写交接记录, 总结今天/本次开发内容, continue the AI archive log, or preserve current Unity/C# architecture progress, decisions, issues, next steps, and collaboration preferences in the project's existing archive style.
---

# Project Archive Record

## Core Workflow

1. Read project instructions first:
   - `AGENTS.md`
   - `.codex/功能汇总.md`
   - `.codex/work_init_CodexAI/04-编码规范-CSharp.md` when C# changes are discussed
2. Read recent archive records in `设计文档/AI归档记录/`, especially the latest date folder and any directly preceding record.
3. Inspect the actual current files, assets, packages, scene state, or git status needed to avoid writing stale handoff notes.
4. Create or update a markdown file under:
   - `设计文档/AI归档记录/YYYY-M-D/`
5. Name records with an ordered prefix and clear topic:
   - `1-Player移动闭环与InputSystem接入记录.md`
   - `2-PlayerTransitionEvaluator开发交接记录.md`
6. Keep the archive factual: record what was confirmed, what changed, what remains unfinished, and the next recommended step.

## Record Structure

Use the existing archive style: concise Chinese headings, numbered sections, and fenced `text` blocks for architecture or ordered flows.

In `## 一、记录范围`, keep `本记录接续：` narrow:

- Link only the latest record from the most recent date folder before the new record.
- If the latest date folder contains multiple records, use the highest ordered prefix, such as `3-xxx.md`.
- Do not list every related historical archive in `本记录接续：`; older context can be summarized in prose if necessary.
- Keep `主设计文档：` as usual, linking the relevant standing design document when applicable.

Preferred section set:

```text
# Protocol_Evac <主题>记录

## 一、记录范围
## 二、本次确认的设计 / 协作偏好
## 三、当前实现状态
## 四、关键架构边界
## 五、场景 / 资源 / 配置状态
## 六、当前需要注意的问题
## 七、当前尚未完成
## 八、下一步建议
## 九、工作区注意事项
```

Only include sections that are useful for the specific archive. Do not force all sections into a small note.

## Content Rules

- Write as a handoff for the next development session, not as a diary.
- Prefer exact paths for important files and assets.
- Use the project's real terms: `PlayerContext`, `PlayerMotor`, `PlayerStateMachine`, `PlayerInputReader`, `PlayerTransitionEvaluator`, `Ability System`, `Input Buffer`.
- Keep ownership boundaries explicit:
  - `Controller` only initializes and schedules
  - `InputReader` reads input and writes current input facts
  - `InputBuffer` stores discrete buffered input
  - `TransitionEvaluator` decides state transitions
  - `State` writes intent
  - `Motor` executes movement
  - `AnimatorDriver` updates presentation
- Record user preferences that affect future coding style, especially if they override generic advice.
- Record temporary code separately from intended architecture.
- If the work touched Unity Editor state, record scene names, GameObject hierarchy, component placement, package versions, and asset paths.
- If there are unrelated git changes, mention them as cautionary notes instead of treating them as part of the completed work.

## What Not To Do

- Do not invent completed work; mark uncertain items as unverified.
- Do not write broad roadmap content unless it affects the immediate next step.
- Do not mix Enemy, Ability, Input Buffer, or Transition work into a Player movement archive unless that work actually happened.
- Do not modify C# code just to make the archive cleaner.
- Do not create extra project instruction files outside the requested archive or skill/index update.

## Validation

Before finishing:

1. Confirm the archive file exists in the intended date folder.
2. Re-open the first part of the file to catch obvious formatting errors.
3. If code was discussed, ensure the final response includes the project-required code change summary. For archive-only work, state that no code was modified.
