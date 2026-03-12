using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Shared;
    public class DreamObject : IDisposable, IBindable
    {
        protected readonly object _lock = new();
        protected IUiBindingService? _bindingService;
        public virtual ObjectType? ObjectType { get; set; }

        protected readonly IVariableStore _variableStore;
        protected readonly IVariableStore _committedStore;

        private long _version;
        public long Version { get => Interlocked.Read(ref _version); set => Interlocked.Exchange(ref _version, value); }
        protected virtual void IncrementVersion() => Interlocked.Increment(ref _version);

        public DreamObject(ObjectType? objectType)
        {
            _variableStore = new FlatVariableStore();
            _committedStore = new FlatVariableStore();

            if (objectType != null && objectType.DefaultValuesArray == null) objectType.FinalizeVariables();
            Initialize(objectType);
        }

        protected void Initialize(ObjectType? objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                var defaults = objectType.DefaultValuesArray;
                if (defaults != null)
                {
                    _variableStore.CopyFrom(defaults);
                    _committedStore.CopyFrom(defaults);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            if (index == -1) return DreamValue.Null;

            lock (_lock)
            {
                return _variableStore.Get(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SetVariable(string name, DreamValue value)
        {
            if (ObjectType == null) return;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                SetVariableDirect(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariable(int index)
        {
            lock (_lock)
            {
                return _variableStore.Get(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariableDirect(int index)
        {
            lock (_lock)
            {
                return _variableStore.Get(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVariable(int index, DreamValue value)
        {
            SetVariableDirect(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SetVariableDirect(int index, DreamValue value)
        {
            if (index < 0) return;

            // Note: SetVariableDirect still uses a lock for consistency when updating and notifying
            lock (_lock)
            {
                var current = _variableStore.Get(index);
                if (!current.Equals(value))
                {
                    _variableStore.Set(index, value);
                    IncrementVersion();

                    var binding = _bindingService;
                    if (binding != null)
                    {
                        binding.NotifyPropertyChanged(this, index, value);
                    }
                }
            }
        }

        public void SetBindingService(IUiBindingService bindingService)
        {
            _bindingService = bindingService;
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }

        public virtual void Dispose()
        {
            _variableStore.Dispose();
            _committedStore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
