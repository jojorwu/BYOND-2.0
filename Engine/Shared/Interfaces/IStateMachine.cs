using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IState
{
    string Name { get; }
    Task EnterAsync(IGameObject owner);
    Task UpdateAsync(IGameObject owner);
    Task ExitAsync(IGameObject owner);
}

public interface IStateMachine
{
    IState? CurrentState { get; }
    Task TransitionToAsync(string stateName);
    void RegisterState(IState state);
    Task UpdateAsync();
}
