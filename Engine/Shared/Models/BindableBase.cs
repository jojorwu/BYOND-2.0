using Shared.Interfaces;
using System.Collections.Generic;

namespace Shared.Models;

/// <summary>
/// Base class for bindable data models.
/// </summary>
public abstract class BindableBase : IBindable
{
    protected IUiBindingService? BindingService;

    public void SetBindingService(IUiBindingService bindingService)
    {
        BindingService = bindingService;
    }

    protected void OnPropertyChanged(DreamObject obj, int index, DreamValue value)
    {
        BindingService?.NotifyPropertyChanged(obj, index, value);
    }
}
