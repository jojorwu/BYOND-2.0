using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Core.MapLoaders.Ss14.Model
{
    public class Ss14Entity
    {
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        public string Id { get; set; } = string.Empty;

        [YamlMember(Alias = "components", ApplyNamingConventions = false)]
        public List<Dictionary<string, object>> Components { get; set; } = new();
    }
}
