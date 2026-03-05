using System.Collections.Generic;

namespace Shared.Config;

public interface IConfigProvider
{
    string Name { get; }
    void Load(IDictionary<string, object> settings);
    void Save(IDictionary<string, object> settings);
    bool CanSave { get; }
}
