using System.Collections.Generic;

namespace SimpleResourceReplacer
{
    public class Keys
    {
        public List<ResouceKey> SingleAssets = new List<ResouceKey>();
        public List<(ResouceKey resource, string texture)> GameObjects = new List<(ResouceKey, string)>();
    }

    public struct ResouceKey
    {
        public string bundle;
        public string resource;
        public static implicit operator ResouceKey((string bundle, string resource) tuple) => new ResouceKey() { bundle = tuple.bundle, resource = tuple.resource };
    }
}