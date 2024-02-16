using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SimpleResourceReplacer.Main;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;

namespace SimpleResourceReplacer
{
    public abstract class SpecialFileLoader
    {
        public abstract void Handle(string file);
    }

    public class ZipLoader : SpecialFileLoader
    {
        public override void Handle(string file)
        {
            if (zippedFiles.TryGetValue(file, out var h))
            {
                zippedFiles.Remove(file);
                foreach (var k in h.Keys)
                    if (acceptableFileTypes.TryGetFileHandler(k,out _) || specialFileTypes.TryGetFileHandler(k, out _))
                        CheckFile(k);
            }
            if (FileExists(file))
            {
                h = null;
                try
                {
                    if (logging)
                        logger.LogInfo($"Starting read of zip file {file}");
                    using (var read = FileOpenRead(file))
                    using (var zip = new ZipArchive(read, ZipArchiveMode.Read))
                    {
                        h = zippedFiles.GetOrCreate(file);
                        foreach (var entry in zip.Entries)
                        {
                            var fullName = entry.FullName.ToLowerInvariant();
                            if (specialFileTypes.TryGetFileHandler(fullName,out _))
                                using (var stream = entry.Open())
                                {
                                    var mem = new List<byte>();
                                    var val = 0;
                                    while ((val = stream.ReadByte()) != -1)
                                        mem.Add((byte)val);
                                    h[file + "|" + fullName] = mem.ToArray();
                                    if (logging)
                                        logger.LogInfo($"Read image entry {entry.FullName} - Length: {mem.Count}");
                                }
                            else if (acceptableFileTypes.TryGetFileHandler(fullName.EndsWith(".meta") ? fullName.Remove(fullName.Length - 5) : fullName, out _))
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
                    if (acceptableFileTypes.TryGetFileHandler(k,out _) || specialFileTypes.TryGetFileHandler(k, out _))
                        CheckFile(k);
                foreach (var k in h.Keys.ToArray()) // Release resources for GC but keep keys for later reference
                    h[k] = null;
            }
        }
    }

    public class EquipmentLoader<T> : SpecialFileLoader where T : CustomDragonEquipment, new() 
    {
        static DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(T));
        public override void Handle(string file)
        {
            if (equipmentFiles.TryGetValue(file, out var skin))
            {
                skin.Destroy();
                equipmentFiles.Remove(file);
            }
            if (FileExists(file))
                using (var stream = FileOpenRead(file))
                    try
                    {
                        equipmentFiles[file] = skin = (CustomDragonEquipment)deserializer.ReadObject(stream);
                        skin.Init();
                        logger.LogInfo($"Loaded custom skin data for {skin.Name} from \"{file}\"");
                    }
                    catch (Exception e)
                    {
                        equipmentFiles.Remove(file);
                        logger.LogError(e);
                        return;
                    }
        }
    }
}
