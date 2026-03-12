using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Models;

public class BaseStateMachine : IStateMachine
{
    private readonly IGameObject _owner;
    private readonly Dictionary<string, IState> _states = new(StringComparer.OrdinalIgnoreCase);
    private IState? _currentState;

    public IState? CurrentState => _currentState;

    public BaseStateMachine(IGameObject owner)
    {
        _owner = owner;
    }

    public void RegisterState(IState state)
    {
        _states[state.Name] = state;
    }

    public async Task TransitionToAsync(string stateName)
    {
        if (!_states.TryGetValue(stateName, out var nextState))
        {
            throw new ArgumentException($"State '{stateName}' not found.");
        }

        if (_currentState != null)
        {
            await _currentState.ExitAsync(_owner);
        }

        _currentState = nextState;
        await _currentState.EnterAsync(_owner);
    }

    public async Task UpdateAsync()
    {
        if (_currentState != null)
        {
            await _currentState.UpdateAsync(_owner);
        }
    }
}
