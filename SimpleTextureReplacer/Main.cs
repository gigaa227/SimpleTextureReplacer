using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using BepInEx.Logging;

namespace SimpleTextureReplacer
{
    [BepInPlugin("com.aidanamite.SimpleTextureReplacer", "Simple Texture Replacer", "1.0.1")]
    [BepInDependency("com.aidanamite.ConfigTweaks")]
    public class Main : BaseUnityPlugin
    {
        public static string CustomResources = Environment.CurrentDirectory + "\\ReplacementTextures";
        public static Dictionary<(string bundle, string resource), TextureData> replacements = new Dictionary<(string, string), TextureData>();
        public static Dictionary<(string bundle, string resource, string texture), TextureData> replacements2 = new Dictionary<(string, string, string), TextureData>();
        public static HashSet<(string bundle, string resource)> replacement2lookup = new HashSet<(string, string)>();
        public static Dictionary<string, (Texture texture, Keys keys)> loaded = new Dictionary<string, (Texture, Keys)>();
        public static Dictionary<Texture, List<Texture>> generated = new Dictionary<Texture, List<Texture>>();
        public static ConcurrentQueue<string> filechanges = new ConcurrentQueue<string>();

        public static ManualLogSource logger;
        public static bool logging = false;
        public void Awake()
        {
            logger = Logger;
            if (!Directory.Exists(CustomResources))
                Directory.CreateDirectory(CustomResources);
            foreach (var f in Directory.GetFiles(CustomResources,"*.png",SearchOption.AllDirectories))
                try
                {
                    CheckImageFile(f);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            var listener = new FileSystemWatcher(CustomResources);
            FileSystemEventHandler handler = (x, y) => filechanges.Enqueue(y.FullPath);
            listener.Created += handler;
            listener.Changed += handler;
            listener.Deleted += handler;
            listener.Renamed += (x, y) =>
            {
                filechanges.Enqueue(y.OldFullPath);
                filechanges.Enqueue(y.FullPath);
            };
            listener.IncludeSubdirectories = true;
            listener.EnableRaisingEvents = true;
            new Harmony("com.aidanamite.SimpleTextureReplacer").PatchAll();
            if (File.Exists(CustomResources + "\\DEBUG"))
                logging = true;
            Logger.LogInfo("Loaded");
        }

        public void Update()
        {
            while (filechanges.TryDequeue(out var f))
                CheckImageFile(f);
        }

        public static void CheckImageFile(string file)
        {
            if (file == null)
                return;
            file = file.ToLowerInvariant().ToLower();
            if (file.EndsWith(".meta"))
                file = file.Remove(file.Length - 5);
            if (!file.EndsWith(".png"))
                return;
            if (loaded.TryGetValue(file, out var r) && r.texture)
            {
                loaded.Remove(file);
                void UpdateSets<X,Y>(Dictionary<X,TextureData> d, FieldInfo keyField, HashSet<Y> lookup, Func<X,Y> convert) where X : struct
                {
                    var key = (X?)keyField.GetValue(r.keys);
                    if (key != null)
                    {
                        if (d.TryGetValue(key.Value, out var td) && td.qualities.Any(x => x.name == r.texture.name))
                        {
                            Texture[] last = new Texture[r.keys.qualityIndex + 1];
                            foreach (var p in loaded.Values)
                            {
                                var v = (X?)keyField.GetValue(p.keys);
                                if (v != null && v.Value.Equals(key.Value) && last.Length > p.keys.qualityIndex && (!last[p.keys.qualityIndex] || p.texture.name.CompareTo(last[p.keys.qualityIndex].name) < 0))
                                    last[p.keys.qualityIndex] = p.texture;
                            }
                            for (int i = 0; i < last.Length; i++)
                                if (td.qualities[i].name == r.texture.name)
                                    td.qualities[i] = last[i];
                            if (last.Any(x => x))
                                logger.LogInfo($"Image \"{r.texture.name}\" unloaded and replaced by existing images\n{key.Value.GetTupleString(keyField)}");
                            else
                                logger.LogInfo($"Image \"{r.texture.name}\" unloaded\n{key.Value.GetTupleString(keyField)}");
                            if (td.qualities.All(x => !x))
                            {
                                d.Remove(key.Value);
                                if (lookup != null && !d.Any(x => convert(x.Key).Equals(convert(key.Value))))
                                    lookup.Remove(convert(key.Value));
                            }
                        }
                        else
                            logger.LogInfo($"Image \"{r.texture.name}\" unloaded\n{key.Value.GetTupleString(keyField)}");
                    }
                }
                void UpdateSet<T>(Dictionary<T, TextureData> d, FieldInfo keyField) where T : struct => UpdateSets<T, bool>(d, keyField, null, null);
                UpdateSet(replacements, Keys.Image);
                UpdateSets(replacements2, Keys.Material, replacement2lookup, x => (x.bundle,x.resource));
                if (generated.TryGetValue(r.texture, out var l))
                {
                    foreach (var i in l)
                        if (i)
                            Destroy(i);
                    generated.Remove(r.texture);
                }
                Destroy(r.texture);
            }
            var metaFile = file + ".meta";
            if (!File.Exists(file) || !File.Exists(metaFile))
                return;
            var meta = File.ReadAllLines(metaFile);
            if (meta.Length < 2)
            {
                logger.LogError($"Could not load custom resource \"{file}\". Reason: Invalid meta file");
                return;
            }
            var type = meta[0].ToLowerInvariant() == "texture" ? 0 : meta[0].ToLowerInvariant() == "gameobject" ? 1 : 2;
            if (type == 2)
            {
                logger.LogError($"Could not load custom resource \"{file}\". Reason: Invalid asset type \"{meta[0].ToLowerInvariant()}\"");
                return;
            }
            if (meta.Length < 3 + type)
            {
                logger.LogError($"Could not load custom resource \"{file}\". Reason: Invalid meta file. \"{meta[0].ToLowerInvariant()}\" must have at least {2 + type} values");
                return;
            }
            var q = Qualities.Count - 1;
            if (meta.Length > 3 + type)
            {
                if (ushort.TryParse(meta[3 + type],out var n))
                {
                    if (n > q)
                    {
                        logger.LogError($"Could not load custom resource \"{file}\". Reason: Quality index is too high. Max: {q}");
                        return;
                    }
                    q = n;
                }
                else
                {
                    var p = Qualities.FirstOrDefault(x => x.Key.ToLowerInvariant() == meta[3 + type].ToLowerInvariant()).Key;
                    if (p != null && Qualities.TryGetValue(p, out var nq))
                        q = nq;
                    else
                    {
                        logger.LogError($"Could not load custom resource \"{file}\". Reason: \"{meta[3 + type]}\" was not a valid quality index or name");
                        return;
                    }
                }
            }
            var t = new Texture2D(0, 0, TextureFormat.RGBA32, false);
            t.name = file;
            try
            {
                t.LoadImage(File.ReadAllBytes(file));
                t.Compress(true);
            }
            catch (Exception e)
            {
                Destroy(t);
                logger.LogError($"Could not load custom resource \"{file}\". Reason: {e}");
                return;
            }
            var keys = type == 0 ? new Keys() { image = (meta[1], meta[2]) } : new Keys() { material = (meta[1], meta[2], meta[3]) };
            keys.qualityIndex = q;
            loaded[file] = (t, keys);
            var data = type == 0 ? replacements.GetOrCreate(keys.image.Value) : replacements2.GetOrCreate(keys.material.Value);
            if (type == 1)
                replacement2lookup.Add((keys.material.Value.bundle, keys.material.Value.resource));
            if (!data.qualities[q] || data.qualities[q].name.CompareTo(file) < 0)
            {
                if (data.qualities[q])
                    logger.LogInfo($"{meta[0].CapitalizeInvariant()} image \"{t.name}\" loaded and replaced existing image \"{data.qualities[q].name}\"\n{(type == 0 ? keys.image.Value.GetTupleString(Keys.Image) : keys.material.Value.GetTupleString(Keys.Material))}");
                else
                    logger.LogInfo($"{meta[0].CapitalizeInvariant()} image \"{t.name}\" loaded\n{(type == 0 ? keys.image.Value.GetTupleString(Keys.Image) : keys.material.Value.GetTupleString(Keys.Material))}");
                data.qualities[q] = t;
            }
            else
                logger.LogInfo($"{meta[0].CapitalizeInvariant()} image \"{t.name}\" loaded but not enabled because \"{data.qualities[q].name}\" has a higher priority\n{(type == 0 ? keys.image.Value.GetTupleString(Keys.Image) : keys.material.Value.GetTupleString(Keys.Material))}");
            
        }

        static Dictionary<string,int> Qualities = new Dictionary<string, int>
        {
            { "Low", 0 },
            { "Mid", 1 },
            { "High", 2 }
        };
        public static int CurrentQualityIndex => Qualities.TryGetValue(ProductConfig.GetBundleQuality(),out var v) ? v : -1;
    }

    public class Keys
    {
        public static FieldInfo Image = typeof(Keys).GetField(nameof(image));
        public static FieldInfo Material = typeof(Keys).GetField(nameof(material));
        public (string bundle, string resource)? image;
        public (string bundle, string resource, string texture)? material;
        public int qualityIndex;
    }

    public class TextureData
    {
        public Texture[] qualities = new Texture[3];
        public Texture GetCurrent()
        {
            for (var i = Main.CurrentQualityIndex;i < qualities.Length;i++)
                if (qualities[i])
                {
                    if (i == Main.CurrentQualityIndex)
                        return qualities[i];
                    var factor = Math.Pow(2, Main.CurrentQualityIndex - i);
                    var prev = RenderTexture.active;
                    var tmp = RenderTexture.active = RenderTexture.GetTemporary((int)(qualities[i].width * factor), (int)(qualities[i].height * factor));
                    Graphics.Blit(qualities[i], tmp);
                    var nt = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, false);
                    nt.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    nt.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(tmp);
                    nt.name = qualities[i].name;
                    Main.generated.GetOrCreate(qualities[i]).Add(nt);
                    qualities[Main.CurrentQualityIndex] = nt;
                    return qualities[i];
                }
            for (var i = Main.CurrentQualityIndex; i >= 0; i--)
                if (qualities[i])
                    return qualities[i];
            return null;
        }
    }

    public static class ExtentionMethods
    {
        public static Y GetOrCreate<X,Y>(this IDictionary<X,Y> d, X key) where Y : new()
        {
            if (d.TryGetValue(key, out var v) && v != null)
                return v;
            return d[key] = new Y();
        }
        public static string Capitalize(this string s) => s.Remove(1).ToUpper() + s.Remove(0, 1).ToLower();
        public static string CapitalizeInvariant(this string s) => s.Remove(1).ToUpperInvariant() + s.Remove(0, 1).ToLowerInvariant();

        public static string GetTupleString(this object obj, FieldInfo field)
        {
            var n = field.GetCustomAttribute<TupleElementNamesAttribute>()?.TransformNames;
            var t = obj.GetType();
            var i = 1;
            var s = new StringBuilder();
            s.Append('[');
            while (true)
            {
                var f = t.GetField("Item" + i);
                if (f == null)
                    break;
                if (i != 1)
                    s.Append(",");
                if (n != null && i <= n.Count && !string.IsNullOrEmpty(n[i - 1]))
                    s.Append(n[i - 1]);
                else
                    s.Append(f.Name);
                s.Append('=');
                s.Append(f.GetValue(obj));
                i++;
            }
            s.Append(']');
            return s.ToString();
        }
    }

    [HarmonyPatch(typeof(CoBundleLoader), "LoadTexture")]
    static class Patch_LoadTexture
    {
        static bool Prefix(AssetBundle bd, string s, ref Texture __result)
        {
            if (Main.logging)
                Main.logger.LogInfo($"LoadTexture  bundle={bd.name},resource={s}");
            if (Main.replacements.TryGetValue((bd.name, s), out var td))
            {
                var t = td.GetCurrent();
                if (t)
                {
                    __result = t;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CoBundleLoader), "LoadGameObject")]
    static class Patch_LoadGameObject
    {
        static void Postfix(AssetBundle bd, string s, GameObject __result)
        {
            if (Main.logging)
                Main.logger.LogInfo($"LoadGameObject  bundle={bd.name},resource={s}");
            TryChange(bd.name, s, __result);
        }
        static FieldInfo[] skinMaterials = typeof(DragonSkin).GetFields(~BindingFlags.Default).Where(x => x.Name.Contains("Materials")).ToArray();
        public static void TryChange(string bundle,string resource, GameObject target)
        {
            if (Main.logging)
                Main.logger.LogInfo($"TryChange  bundle={bundle},resource={resource},target={(target ? target.name : "Null")}");
            if (Main.replacement2lookup.Contains((bundle, resource)))
            {
                if (Main.logging)
                    Main.logger.LogInfo($"TryChange lookup found potential key");
                void TryEditMat(Material mat)
                {
                    if (mat)
                        for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                            if (mat.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                            {
                                var ot = mat.GetTexture(mat.shader.GetPropertyName(i));
                                if (ot && Main.replacements2.TryGetValue((bundle, resource, ot.name), out var td))
                                {
                                    var t = td.GetCurrent();
                                    if (t)
                                        mat.SetTexture(mat.shader.GetPropertyName(i), t);
                                }
                            }
                }
                foreach (var rend in target.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in rend.sharedMaterials)
                        TryEditMat(m);
                foreach (var skin in target.GetComponentsInChildren<DragonSkin>(true))
                    foreach (var f in skinMaterials)
                        if (f.GetValue(skin) is Material[] a)
                            foreach (var m in a)
                                TryEditMat(m);
                foreach (var widget in target.GetComponentsInChildren<UITexture>(true))
                {
                    if (widget && widget.mainTexture && Main.replacements2.TryGetValue((bundle, resource, widget.mainTexture.name), out var td))
                    {
                        var t = td.GetCurrent();
                        if (t)
                            widget.mainTexture = t;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(RsResourceManager), "LoadAssetFromBundle", typeof(string), typeof(string), typeof(RsResourceEventHandler), typeof(Type), typeof(bool), typeof(object))]
    static class Patch_LoadAsset
    {
        static void Prefix(string inBundleURL, string inAssetName, ref RsResourceEventHandler inCallback, Type inType, bool inDontDestroy = false, object inUserData = null)
        {
            if (Main.logging)
                Main.logger.LogInfo($"LoadAssetFromBundle  bundle={inBundleURL},resource={inAssetName}");
            if (Main.replacement2lookup.Contains((inBundleURL, inAssetName)) && (typeof(Component).IsAssignableFrom(inType) || inType == typeof(GameObject)))
            {
                inCallback = (url, even, progress, obj, data) =>
                {
                    if (even == RsResourceLoadEvent.COMPLETE && obj as Object)
                        Patch_LoadGameObject.TryChange(inBundleURL, inAssetName, obj is Component c ? c.gameObject : (GameObject)obj);
                } + inCallback;
            }

        }
    }
}