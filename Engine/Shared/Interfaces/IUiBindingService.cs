using Shared;

namespace Shared.Interfaces;

/// <summary>
/// Service for managing data bindings between game objects and UI components.
/// </summary>
public interface IUiBindingService
{
    void NotifyPropertyChanged(DreamObject obj, int index, DreamValue value);
}

/// <summary>
/// Interface for objects that can be bound to UI components.
/// </summary>
public interface IBindable
{
    void SetBindingService(IUiBindingService bindingService);
}
