using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleResourceReplacer
{
    public class ReplacementAssets
    {
        Dictionary<Type, AssetReplacer> replacers = new Dictionary<Type, AssetReplacer>();
        public static Type SimplfyType(Type type)
        {
            if (typeof(Texture).IsAssignableFrom(type))
                return typeof(Texture);
            return type;
        }
        public bool TryReplace(Type type, out Object replace)
        {
            var t = SimplfyType(type);
            if (replacers.TryGetValue(t, out var replacer))
                return replacer.TryReplace(out replace);
            replace = null;
            return false;
        }
        public bool TryReplace<T>(out T replace) where T : Object
        {
            if (TryReplace(typeof(T), out var r))
            {
                replace = (T)r;
                return true;
            }
            replace = null;
            return false;
        }
        public void AddAsset(Object asset, int quality)
        {
            var t = SimplfyType(asset.GetType());
            if (!replacers.TryGetValue(t,out var replacer))
                replacers[t] = replacer = t == typeof(Texture) ? (AssetReplacer)new TextureData() : new GenericReplacer();
            replacer.AddAsset(asset, quality);
        }
        public void RemoveAsset(Object asset)
        {
            if (Main.Generated.TryGetValue(asset.name, out var children) && !children.Contains(asset))
                foreach (var child in children)
                    RemoveAsset(child);
            var t = SimplfyType(asset.GetType());
            if (replacers.TryGetValue(t,out var replacer))
                replacer.RemoveAsset(asset);
        }
    }
}