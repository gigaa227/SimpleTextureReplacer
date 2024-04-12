using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using BepInEx.Logging;

namespace SimpleResourceReplacer
{
    public abstract class FileLoader
    {
        public abstract Object LoadFile(byte[] data, out Exception exception);
        public abstract bool TryLoadMeta(Object obj, Keys keys, string file, string metaName, string[] meta, ManualLogSource logger);
    }
    
    public class TextureLoader : FileLoader
    {
        public override Object LoadFile(byte[] data, out Exception exception)
        {
            var t = new Texture2D(0, 0, TextureFormat.ARGB32, false);
            try
            {
                t.LoadImage(data, false);
            }
            catch (Exception e)
            {
                Object.Destroy(t);
                exception = e;
                return null;
            }
            exception = null;
            return t;
        }
        public override bool TryLoadMeta(Object obj, Keys keys, string file, string metaName, string[] meta, ManualLogSource logger)
        {
            if (meta.Length < 2)
            {
                logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta is invalid... You really shouldn't be seeing this message");
                return false;
            }
            var type = (meta[0].ToLowerInvariant() == "texture" || meta[0].ToLowerInvariant() == "asset") ? 0 : meta[0].ToLowerInvariant() == "gameobject" ? 1 : 2;
            if (type == 2)
            {
                logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta asset type \"{meta[0].ToLowerInvariant()}\" is invalid");
                return false;
            }
            if (meta.Length < 3 + type)
            {
                logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta is invalid. \"{meta[0].ToLowerInvariant()}\" must have at least {2 + type} values");
                return false;
            }
            ResouceKey rKey = (meta[1],meta[2]);
            var q = Main.Qualities.Count - 1;
            if (meta.Length > 3 + type)
            {
                if (ushort.TryParse(meta[3 + type], out var n))
                {
                    if (n > q)
                    {
                        logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta quality index is too high. Max: {q}");
                        return false;
                    }
                    q = n;
                }
                else
                {
                    var p = Main.Qualities.FirstOrDefault(x => x.Key.ToLowerInvariant() == meta[3 + type].ToLowerInvariant()).Key;
                    if (p != null && Main.Qualities.TryGetValue(p, out var nq))
                        q = nq;
                    else
                    {
                        logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta quality value \"{meta[3 + type]}\" was not a valid quality index or name");
                        return false;
                    }
                }
            }
            if (meta.Length > 4 + type)
            {
                if (Enum.TryParse(meta[4 + type], true, out FilterMode n))
                {
                    (obj as Texture2D).filterMode = n;
                }
                else
                {
                    logger.LogError($"Problem loading custom texture resource \"{file}\". Reason: {metaName} meta filter mode value \"{meta[3 + type]}\" was not a valid filter mode");
                    return false;
                }
            }
            var data = type == 0 ? Main.SingleAssets.GetOrCreate(rKey) : Main.GameObjects.GetOrCreate(rKey).GetOrCreate(meta[3]);
            if (type == 0)
                keys.SingleAssets.Add(rKey);
            else
                keys.GameObjects.Add((rKey, meta[3]));
            data.AddAsset(obj, q);
            logger.LogInfo($"{meta[0].CapitalizeInvariant()} texture \"{obj.name}\" loaded\n{(type == 0 ? $"[bundle={rKey.bundle},resource={rKey.resource}]" : $"[bundle={rKey.bundle},resource={rKey.resource},value={meta[3]}]")}");
            return true;
        }
    }

    public class BundleLoader : FileLoader
    {
        public override Object LoadFile(byte[] data, out Exception exception)
        {
            try
            {
                exception = null;
                return AssetBundle.LoadFromMemory(data);
            }
            catch (Exception e)
            {
                exception = e;
                return null;
            }
        }
        public override bool TryLoadMeta(Object obj, Keys keys, string file, string metaName, string[] meta, ManualLogSource logger)
        {
            if (meta.Length < 3)
            {
                logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta is invalid... You really shouldn't be seeing this message");
                return false;
            }
            if (!(obj as AssetBundle).Contains(meta[0]))
            {
                logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta asset \"{meta[0]}\" not found in bundle");
                return false;
            }
            var type = (meta[1].ToLowerInvariant() == "texture" || meta[1].ToLowerInvariant() == "asset") ? 0 : meta[1].ToLowerInvariant() == "gameobject" ? 1 : 2;
            if (type == 3)
            {
                logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta asset type \"{meta[1].ToLowerInvariant()}\" is invalid");
                return false;
            }
            if (meta.Length < 4 + type)
            {
                logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta is invalid. \"{meta[1].ToLowerInvariant()}\" must have at least {2 + type} values");
                return false;
            }
            ResouceKey rKey = (meta[2], meta[3]);
            var q = Main.Qualities.Count - 1;
            if (meta.Length > 4 + type)
            {
                if (ushort.TryParse(meta[4 + type], out var n))
                {
                    if (n > q)
                    {
                        logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta quality index is too high. Max: {q}");
                        return false;
                    }
                    q = n;
                }
                else
                {
                    var p = Main.Qualities.FirstOrDefault(x => x.Key.ToLowerInvariant() == meta[4 + type].ToLowerInvariant()).Key;
                    if (p != null && Main.Qualities.TryGetValue(p, out var nq))
                        q = nq;
                    else
                    {
                        logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta quality value \"{meta[4 + type]}\" was not a valid quality index or name");
                        return false;
                    }
                }
            }
            var loaded = (obj as AssetBundle).LoadAsset(meta[0]);
            if (loaded is Component || loaded is GameObject)
            {
                if (loaded is Component c)
                    Object.Destroy(c.gameObject);
                else
                    Object.Destroy(loaded);
                logger.LogError($"Problem loading custom bundle resource \"{file}\". Reason: {metaName} meta asset is not a supported type. Components and GameObjects cannot be used");
                return false;
            }
            loaded.name = file + "|" + meta[0];
            var data = type == 0 ? Main.SingleAssets.GetOrCreate(rKey) : Main.GameObjects.GetOrCreate(rKey).GetOrCreate(meta[4]);
            if (type == 0)
                keys.SingleAssets.Add(rKey);
            else
                keys.GameObjects.Add((rKey, meta[4]));
            Main.Generated.GetOrCreate(obj.name).Add(loaded);
            data.AddAsset(loaded, q);
            logger.LogInfo($"{meta[1].CapitalizeInvariant()} {ReplacementAssets.SimplfyType(loaded.GetType()).Name.ToLowerInvariant()} \"{loaded.name}\" loaded from bundle {obj.name}\n{(type == 0 ? $"[bundle={rKey.bundle},resource={rKey.resource}]" : $"[bundle={rKey.bundle},resource={rKey.resource},value={meta[3]}]")}");
            return true;
        }
    }
}