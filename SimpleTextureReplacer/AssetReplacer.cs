using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleResourceReplacer
{
    public abstract class AssetReplacer
    {
        public abstract bool TryReplace(out Object replace);
        public abstract void AddAsset(Object asset, int quality);
        public abstract void RemoveAsset(Object asset);
        public abstract bool IsEmpty { get; }
    }

    public class GenericReplacer : AssetReplacer
    {
        SortedDictionary<string, Object> Added = new SortedDictionary<string, Object>();
        public override void AddAsset(Object asset, int quality) => Added[asset.name] = asset;
        public override void RemoveAsset(Object asset) => Added.Remove(asset.name);
        public override bool TryReplace(out Object replace)
        {
            foreach (var i in Added)
            {
                replace = i.Value;
                return true;
            }
            replace = null;
            return false;
        }
        public override bool IsEmpty => Added.Count == 0;
    }

    public class TextureData : AssetReplacer
    {
        SortedDictionary<string, Texture>[] Qualities = new SortedDictionary<string, Texture>[] { new SortedDictionary<string, Texture>(), new SortedDictionary<string, Texture>(), new SortedDictionary<string, Texture>() };
        Texture[] cache = new Texture[3];
        Texture this[int index]
        {
            get
            {
                if (cache[index])
                    return cache[index];
                foreach (var p in Qualities[index])
                    return cache[index] = p.Value;
                return null;
            }
        }
        Texture GetCurrent()
        {
            for (var i = Main.CurrentQualityIndex; i < Qualities.Length; i++)
                if (this[i])
                {
                    if (i == Main.CurrentQualityIndex || !(this[i] is Texture2D))
                        return this[i];
                    var factor = Math.Pow(2, Main.CurrentQualityIndex - i);
                    var prev = RenderTexture.active;
                    var tmp = RenderTexture.active = RenderTexture.GetTemporary((int)(this[i].width * factor), (int)(this[i].height * factor));
                    Graphics.Blit(this[i], tmp);
                    var nt = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, false);
                    nt.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    nt.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(tmp);
                    nt.name = this[i].name;
                    nt.Compress(true);
                    Main.Generated.GetOrCreate(this[i].name).Add(nt);
                    Qualities[Main.CurrentQualityIndex][this[i].name] = nt;
                    return nt;
                }
            for (var i = Main.CurrentQualityIndex; i >= 0; i--)
                if (this[i])
                    return this[i];
            return null;
        }
        public override void AddAsset(Object asset, int quality)
        {
            if (!(asset is Texture))
                return;
            var n = this[quality] ? this[quality].name : null;
            if (n != null && Main.Generated.TryGetValue(n, out var check))
            {
                foreach (var d in Qualities)
                    foreach (var p in d)
                    {
                        if (check.Contains(p.Value))
                        {
                            check.Remove(p.Value);
                            Object.Destroy(p.Value);
                            d.Remove(p.Key);
                        }
                        break;
                    }
                if (check.Count == 0)
                    Main.Generated.Remove(n);
            }
            Qualities[quality][asset.name] = (Texture)asset;
            cache[quality] = null;
        }
        public override void RemoveAsset(Object asset)
        {
            if (!(asset is Texture))
                return;
            for (int i = 0; i < Qualities.Length; i++)
            {
                Qualities[i].Remove(asset.name);
                cache[i] = null;
            }    
        }
        public override bool TryReplace(out Object replace)
        {
            var cur = GetCurrent();
            if (cur)
            {
                replace = cur;
                return true;
            }
            replace = null;
            return false;
        }
        public override bool IsEmpty => Qualities.All(x => x.Count == 0);
    }
}