using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using BepInEx.Logging;

namespace SimpleResourceReplacer
{
    [BepInPlugin("com.aidanamite.SimpleTextureReplacer", "Simple Resource Replacer", "2.0.0")]
    public class Main : BaseUnityPlugin
    {
        public static string CustomResources = Environment.CurrentDirectory + "\\Resource Packs";
        public static Dictionary<ResouceKey, ReplacementAssets> SingleAssets = new Dictionary<ResouceKey, ReplacementAssets>();
        public static Dictionary<ResouceKey, Dictionary<string, ReplacementAssets>> GameObjects = new Dictionary<ResouceKey, Dictionary<string, ReplacementAssets>>();
        public static Dictionary<string, (Object obj, Keys keys)> Loaded = new Dictionary<string, (Object, Keys)>();
        public static Dictionary<string, HashSet<Object>> Generated = new Dictionary<string, HashSet<Object>>();
        public static Dictionary<string, Dictionary<string, object>> zippedFiles = new Dictionary<string, Dictionary<string, object>>();
        public static ConcurrentQueue<string> filechanges = new ConcurrentQueue<string>();
        public static Dictionary<string,FileLoader> acceptableFileTypes = new Dictionary<string, FileLoader>
        {
            { ".png", new TextureLoader() },
            {".jpg", new TextureLoader() },
            {".jpeg", new TextureLoader() },
            {".bundle", new BundleLoader() }
        };

        public static ManualLogSource logger;
        public static bool logging = false;
        public void Awake()
        {
            logger = Logger;
            if (!Directory.Exists(CustomResources))
                Directory.CreateDirectory(CustomResources);
            if (File.Exists(CustomResources + "\\DEBUG"))
                logging = true;
            foreach (var ext in acceptableFileTypes.Keys)
            {
                foreach (var f in Directory.GetFiles(CustomResources, "*" + ext, SearchOption.AllDirectories))
                    try
                    {
                        Debug.Log(f);
                        CheckImageFile(f);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e);
                    }
            }
            foreach (var f in Directory.GetFiles(CustomResources, "*.zip", SearchOption.AllDirectories))
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
            file = file.ToLowerInvariant();
            if (file.EndsWith(".zip"))
            {
                if (zippedFiles.TryGetValue(file,out var h))
                {
                    zippedFiles.Remove(file);
                    foreach (var k in h.Keys)
                        if (acceptableFileTypes.Any(x => k.EndsWith(x.Key)))
                            CheckImageFile(k);
                }
                if (File.Exists(file))
                {
                    h = null;
                    try
                    {
                        if (logging)
                            logger.LogInfo($"Starting read of zip file {file}");
                        using (var read = File.OpenRead(file))
                        using (var zip = new ZipArchive(read, ZipArchiveMode.Read))
                        {
                            h = zippedFiles.GetOrCreate(file);
                            foreach (var entry in zip.Entries)
                            {
                                var fullName = entry.FullName.ToLowerInvariant();
                                if (acceptableFileTypes.Any(x => fullName.EndsWith(x.Key) || fullName.EndsWith(x.Key + ".meta")))
                                    using (var stream = entry.Open())
                                    {
                                        if (fullName.EndsWith(".meta"))
                                        {
                                            using (var mem = new StreamReader(stream, Encoding.UTF8))
                                            {
                                                var l = new List<string>();
                                                var line = "";
                                                while ((line = mem.ReadLine()) != null)
                                                    l.Add(line);
                                                h[file + "|" + fullName] = l.ToArray();
                                                if (logging)
                                                    logger.LogInfo($"Read meta entry {entry.FullName} - Length: {l.Count}");
                                            }
                                        }
                                        else
                                        {
                                            var mem = new List<byte>();
                                            var val = 0;
                                            while ((val = stream.ReadByte()) != -1)
                                                mem.Add((byte)val);
                                            h[file + "|" + fullName] = mem.ToArray();
                                            if (logging)
                                                logger.LogInfo($"Read image entry {entry.FullName} - Length: {mem.Count}");
                                        }
                                    }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        zippedFiles.Remove(file);
                        logger.LogError(e);
                        return;
                    }
                    if (logging)
                        logger.LogInfo("Zip read complete. Attmpting to load contents");
                    foreach (var k in h.Keys)
                        if (acceptableFileTypes.Any(x => k.EndsWith(x.Key)))
                            CheckImageFile(k);
                    foreach (var k in h.Keys.ToArray())
                        h[k] = null;
                }
                return;
            }
            if (file.EndsWith(".meta"))
                file = file.Remove(file.Length - 5);
            var loader = acceptableFileTypes.FirstOrDefault(x => file.EndsWith(x.Key)).Value;
            if (loader == null)
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

        static bool FileExists(string filename)
        {
            var i = filename.IndexOf('|');
            if (i == -1)
                return File.Exists(filename);
            return zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.ContainsKey(filename);
        }

        static string[] FileReadAllLines(string filename)
        {
            var i = filename.IndexOf('|');
            if (i == -1)
                return File.ReadAllLines(filename);
            if (zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.TryGetValue(filename, out var value) && value is string[] lines)
                return lines;
            throw new FileNotFoundException("The zipped file could not be found",filename);
        }

        static byte[] FileReadAllBytes(string filename)
        {
            var i = filename.IndexOf('|');
            if (i == -1)
                return File.ReadAllBytes(filename);
            if (zippedFiles.TryGetValue(filename.Remove(i), out var contents) && contents.TryGetValue(filename, out var value) && value is byte[] data)
                return data;
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
    }
}