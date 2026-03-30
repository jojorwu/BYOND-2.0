using System;
using Shared.Interfaces;

namespace Shared.Interfaces;

/// <summary>
/// Defines a component that can react to state changes in its owner entity.
/// </summary>
public interface IReactiveComponent : IComponent
{
    /// <summary>
    /// Called when a variable in the owner's store is modified.
    /// </summary>
    void OnVariableChanged(int index, in DreamValue newValue);
}

/// <summary>
/// Extension of the variable store that supports registering change listeners.
/// </summary>
public interface IObservableVariableStore : IVariableStore
{
    /// <summary>
    /// Sets the owner object that will be passed to listeners.
    /// </summary>
    void SetOwner(IGameObject owner);

    /// <summary>
    /// Registers a listener for changes to any variable in the store.
    /// </summary>
    void Subscribe(IVariableChangeListener listener);

    /// <summary>
    /// Unregisters a listener.
    /// </summary>
    void Unsubscribe(IVariableChangeListener listener);
}

/// <summary>
/// Listener interface for fine-grained variable change notifications.
/// </summary>
public interface IVariableChangeListener
{
    void OnVariableChanged(IGameObject owner, int index, in DreamValue value);
}
