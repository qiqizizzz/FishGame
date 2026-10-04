/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子状态接口，状态模式的可替换单元
* │  类    名: IHookState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

using Module.Fish.Type;

namespace Module.Fish.Model
{
    public interface IHookState
    {
        HookPhase Phase { get; }

        void Enter(IHookRuntime runtime);

        void Update(IHookRuntime runtime, float deltaTime);

        void Exit(IHookRuntime runtime);
    }
}