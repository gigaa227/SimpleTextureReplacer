using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using BepInEx.Logging;

namespace SimpleResourceReplacer
{
    [BepInPlugin("com.aidanamite.SimpleTextureReplacer", "Simple Resource Replacer", "2.3.1")]
    public class Main : BaseUnityPlugin
    {
        public const string CustomBundleName = "RS_SHARED/customassets";
        public static string CustomResources = Environment.CurrentDirectory + "\\Resource Packs";
        public static Dictionary<ResouceKey, ReplacementAssets> SingleAssets = new Dictionary<ResouceKey, ReplacementAssets>();
        public static Dictionary<ResouceKey, Dictionary<string, ReplacementAssets>> GameObjects = new Dictionary<ResouceKey, Dictionary<string, ReplacementAssets>>();
        public static Dictionary<string, (Object obj, Keys keys)> Loaded = new Dictionary<string, (Object, Keys)>();
        public static Dictionary<string, HashSet<Object>> Generated = new Dictionary<string, HashSet<Object>>();
        public static Dictionary<string, Dictionary<string, object>> zippedFiles = new Dictionary<string, Dictionary<string, object>>();
        public static Dictionary<string, CustomDragonEquipment> equipmentFiles = new Dictionary<string, CustomDragonEquipment>();
        public static ConcurrentQueue<string> filechanges = new ConcurrentQueue<string>();
        public static Dictionary<string,FileLoader> acceptableFileTypes = new Dictionary<string, FileLoader>
        {
            { ".png", new TextureLoader() },
            {".jpg", new TextureLoader() },
            {".jpeg", new TextureLoader() },
            {".bundle", new BundleLoader() }
        };
        public static Dictionary<string, SpecialFileLoader> specialFileTypes = new Dictionary<string, SpecialFileLoader>
        {
            { ".zip", new ZipLoader() },
            { ".skin", new EquipmentLoader<CustomSkin>() },
            { ".saddle", new EquipmentLoader<CustomSaddle>() }
        };
        public static AssetBundle dummyBundle;
        public static ManualLogSource logger;
        public static bool logging = false;
        public void Awake()
        {
            logger = Logger;
            dummyBundle = AssetBundle.LoadFromMemory(Convert.FromBase64String("VW5pdHlGUwAAAAAINS54LngAMjAyMS4zLjEwZjEAAAAAAAAAAzoAAABBAAAAWwAAAkMAAAAAAAAAAAAAAAAAAB4AAQCyAQAAELgAAAKqAEEOAAgBAAAaAPAYAARDQUItOGIxMDcwNGM4NjVkOGI3MzcxMzlmN2NhZDZkM2NiYTcAAAAAAAAAAAAAAAAAAAAAXQAACAAAAGwBI/rxOpWEFHzxDRrTnOhI8XlREygob66j57yEEQfe5bzbrmAWZe0GuLIci0vzjDf5kHyc5opfaSKyo6Ns8MjBaVWdSLtmQA8Yyomz/Fov/N1lfpNaV3cwq1Sw7jM5UWWWk8Fd2sGk5pJYXLsmyB2/h7agLFrwB7Texk0MBOvikwPxiSEoHdWG8c07kPCTY8BOsFPh3tc0TnQKaOCbvF/4f95bko7VnL+hB+2hy+tue5j7zIytzEq6xsBDmQWAKrV17F4BkI9Hff/b3a286ts7PHhhN175M7lHAE1TGMiGJTzAPfVHcuXDYuojtM1hrxVV+sD7feLCJ0yjGyW3AM+pWgUfDk/Ue4+DpAKRzDnA6m3q/ud/yyWoH/i1KPuHvVP9nvEE4zF29p7DZwxFxiE0bt4pTYHjdIe4WEXHS9WSZJh1xu5wX0xYT1cX1+WGe+VvZ6PQ0H5oZ/SZUUqHrOYaEHJiFrmjMCSl9jl/s/GR+thSg96QLjab2OCm68OOkfELj5aFuPATiHHj7KmTB3Zvw5b1mzz6YdJMu25uSX7FUwvjEyjDYadPoRLtAyA4XqweiVILxmLklicCwk3wEU53DHU5/aK0ZTWNoFlx4VUPOWgpk+Y3scLNi2Q5XiEJhP+5koyMZgy6bEvOqZvUiTRFm4l4Bv5ZyzoR9dzf+mIesLzXD8mW0haIsq3Hx372MgbHsQNiBkdHXHSIbdS4hSw0g9FirzKSx733LeIVMFuIA6TiY82pAPS49iPJxyxP5qcVZWkSE0eU07YgqX9uBMLf0b3LkwC2kV0vWd/comduCG5UQ9HlzD+VxG0aTP4Q6bt0gWTmffpyK1Cyh1rJFNLsBip8gbmIE9mD8aZRFRgta8lMzY4Lzc7izHFNwof/3bqJFA=="));
            dummyBundle.name = CustomBundleName;
            if (!Directory.Exists(CustomResources))
                Directory.CreateDirectory(CustomResources);
            if (File.Exists(CustomResources + "\\DEBUG"))
                logging = true;
            foreach (var ext in acceptableFileTypes.Keys)
                foreach (var f in Directory.GetFiles(CustomResources, "*" + ext, SearchOption.AllDirectories))
                    try
                    {
                        CheckFile(f);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e);
                    }
            foreach (var ext in specialFileTypes.Keys)
                foreach (var f in Directory.GetFiles(CustomResources, "*" + ext, SearchOption.AllDirectories))
                    try
                    {
                        CheckFile(f);
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
            Logger.LogInfo("Loaded");
        }

        public void Update()
        {
            while (filechanges.TryDequeue(out var f))
                CheckFile(f);
        }

        public static void CheckFile(string file)
        {
            if (file == null)
                return;
            file = file.ToLowerInvariant();
            if (specialFileTypes.TryGetFileHandler(file,out var specialLoader))
            {
                specialLoader.Handle(file);
                return;
            }

            if (file.EndsWith(".meta"))
                file = file.Remove(file.Length - 5);
            if (!acceptableFileTypes.TryGetFileHandler(file,out var loader))
            {
                if (logging)
                    logger.LogInfo($"No suitable loader found for \"{file}\"");
                return;
            }
            if (Loaded.TryGetValue(file, out var r) && r.obj)
            {
                Loaded.Remove(file);
                foreach (var key in r.keys.SingleAssets)
                    if (SingleAssets.TryGetValue(key, out var v))
                        v.RemoveAsset(r.obj);
                foreach (var key in r.keys.GameObjects)
                    if (GameObjects.TryGetValue(key.resource, out var d) && d.TryGetValue(key.texture,out var v))
                        v.RemoveAsset(r.obj);
                if (Generated.TryGetValue(file, out var l))
                {
                    foreach (var i in l)
                        if (i)
                            SafeDestroy(i);
                    Generated.Remove(file);
                }
                SafeDestroy(r.obj);
            }
            var metaFile = file + ".meta";
            if (!FileExists(file) || !FileExists(metaFile))
                return;
            var metas = FileReadAllLines(metaFile).SplitMetas();
            var keys = new Keys();
            var hasLoaded = false;
            var load = loader.LoadFile(FileReadAllBytes(file),out var err);
            if (!load)
            {
                logger.LogError($"Could not load custom resource \"{file}\". Reason: {err}");
                return;
            }
            load.name = file;
            for (int i = 0; i < metas.Count; i++)
                hasLoaded = loader.TryLoadMeta(load, keys, file, i.ToPlaceString(), metas[i], logger) || hasLoaded;
            if (hasLoaded)
                Loaded[file] = (load, keys);
            else
                logger.LogError($"Could not load custom resource \"{file}\". Reason: No valid meta data");
        }

        public static bool FileExists(string filename)
        {
            var i = filename.LastIndexOf('|');
            if (i == -1)
                return File.Exists(filename);
            return zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.ContainsKey(filename);
        }

        public static string[] FileReadAllLines(string filename)
        {
            var i = filename.LastIndexOf('|');
            if (i == -1)
                return File.ReadAllLines(filename);
            if (zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.TryGetValue(filename, out var value) && value is string[] lines)
                return lines;
            throw new FileNotFoundException("The zipped file could not be found",filename);
        }

        public static byte[] FileReadAllBytes(string filename)
        {
            var i = filename.LastIndexOf('|');
            if (i == -1)
                return File.ReadAllBytes(filename);
            if (zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.TryGetValue(filename, out var value) && value is byte[] data)
                return data;
            throw new FileNotFoundException("The zipped file could not be found", filename);
        }

        public static Stream FileOpenRead(string filename)
        {
            var i = filename.LastIndexOf('|');
            if (i == -1)
                return File.OpenRead(filename);
            if (zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.TryGetValue(filename, out var value) && value is byte[] data)
                return new MemoryStream(data);
            throw new FileNotFoundException("The zipped file could not be found", filename);
        }

        public static Dictionary<string,int> Qualities = new Dictionary<string, int>
        {
            { "Low", 0 },
            { "Mid", 1 },
            { "High", 2 }
        };
        public static int CurrentQualityIndex => Qualities.TryGetValue(ProductConfig.GetBundleQuality(),out var v) ? v : -1;

        public static void SafeDestroy(Object obj)
        {
            if (!obj)
                return;
            if (obj is AssetBundle a)
                a.Unload(true);
            else
                Destroy(obj);
        }

        static FieldInfo _itemDataCache = typeof(ItemData).GetField("mItemDataCache", ~BindingFlags.Default);
        public static void ItemData_RemoveFromCache(int itemId) => (_itemDataCache.GetValue(null) as Dictionary<int, ItemData>)?.Remove(itemId);
    }
}