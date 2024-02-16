using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using System.Globalization;
using System.Reflection;

namespace SimpleResourceReplacer
{
    [Serializable]
    public class CustomSkin : CustomDragonEquipment
    {
        public string[] TargetRenderers;
        public MaterialProperty[] MaterialData;
        [OptionalField]
        public MeshOverrides Mesh;
        [OptionalField]
        public MaterialProperty[] HWMaterialData;
        [OptionalField]
        [NonSerialized]
        public DragonSkin skin;
        [OptionalField]
        [NonSerialized]
        public DragonSkin hwskin;


        protected override void Reapply()
        {
            if (Mesh != null)
            {
                if (!string.IsNullOrEmpty(Mesh.Baby))
                {
                    var k = new ResouceKey(Mesh.Baby);
                    if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Mesh>(out var m))
                    {
                        skin._BabyMesh = m;
                        if (hwskin && skin != hwskin)
                            hwskin._BabyMesh = m;
                    }
                    else
                        Main.logger.LogWarning($"Custom Skin requested baby mesh [bundle={k.bundle},resource={k.resource}] but no mesh was loaded");
                }
                if (!string.IsNullOrEmpty(Mesh.Teen))
                {
                    var k = new ResouceKey(Mesh.Teen);
                    if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Mesh>(out var m))
                    { 
                        skin._TeenMesh = m;
                        if (hwskin && skin != hwskin)
                            hwskin._TeenMesh = m;
                    }
                    else
                        Main.logger.LogWarning($"Custom Skin requested teen mesh [bundle={k.bundle},resource={k.resource}] but no mesh was loaded");
                }
                if (!string.IsNullOrEmpty(Mesh.Adult))
                {
                    var k = new ResouceKey(Mesh.Adult);
                    if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Mesh>(out var m))
                    { 
                        skin._Mesh = m;
                        if (hwskin && skin != hwskin)
                            hwskin._Mesh = m;
                    }
                    else
                        Main.logger.LogWarning($"Custom Skin requested adult mesh [bundle={k.bundle},resource={k.resource}] but no mesh was loaded");
                }
                if (!string.IsNullOrEmpty(Mesh.Titan))
                {
                    var k = new ResouceKey(Mesh.Titan);
                    if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Mesh>(out var m))
                    { 
                        skin._TitanMesh = m;
                        if (hwskin && skin != hwskin)
                            hwskin._TitanMesh = m;
                    }
                    else
                        Main.logger.LogWarning($"Custom Skin requested titan mesh [bundle={k.bundle},resource={k.resource}] but no mesh was loaded");
                }
            }
            ApplyMaterialData(skin, MaterialData);
            if (HWMaterialData != null && HWMaterialData.Length != 0)
            {
                ApplyMaterialData(hwskin, MaterialData);
                ApplyMaterialData(hwskin, HWMaterialData);
            }
        }

        static void ApplyMaterialData(DragonSkin skin, MaterialProperty[] data)
        {
            foreach (var md in data)
            {
                var ms = md.Target.StartsWith("Baby") ? skin._BabyMaterials : md.Target.StartsWith("Teen") ? skin._TeenMaterials : md.Target.StartsWith("Adult") ? skin._Materials : md.Target.StartsWith("Titan") ? skin._TitanMaterials : null;
                if (ms == null)
                {
                    Main.logger.LogWarning($"Custom Skin material property target not found [target={md.Target},property={md.Property},value={md.Value}]");
                    continue;
                }
                var ind = md.Target.EndsWith("Body") ? 0 : md.Target.EndsWith("Eyes") ? 1 : -1;
                object v = null;
                bool flag = false;
                for (int i = 0; i < MyShader.GetPropertyCount(); i++)
                    if (MyShader.GetPropertyName(i) == md.Property)
                    {
                        flag = true;
                        var pt = MyShader.GetPropertyType(i);
                        if (pt == ShaderPropertyType.Color)
                        {
                            if ((md.Value.Length == 6 || md.Value.Length == 8) && uint.TryParse(md.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r))
                                v = (Color)new Color32((byte)((r >> 16) & 0xFF), (byte)((r >> 8) & 0xFF), (byte)(r & 0xFF), md.Value.Length == 6 ? (byte)((r >> 24) & 0xFF) : (byte)255);
                            else
                                Main.logger.LogWarning($"Custom Skin material property value is not a valid color (must be hex) [target={md.Target},property={md.Property},value={md.Value}]");

                        }
                        else if (pt == ShaderPropertyType.Float || pt == ShaderPropertyType.Range)
                        {
                            if (float.TryParse(md.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                                v = r;
                            else
                                Main.logger.LogWarning($"Custom Skin material property value is not a valid floating point number [target={md.Target},property={md.Property},value={md.Value}]");
                        }
                        else if (pt == ShaderPropertyType.Int)
                        {
                            if (int.TryParse(md.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                                v = r;
                            else
                                Main.logger.LogWarning($"Custom Skin material property value is not a valid integer [target={md.Target},property={md.Property},value={md.Value}]");
                        }
                        else if (pt == ShaderPropertyType.Vector)
                        {
                            try
                            {
                                var a = Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(md.Value, new Newtonsoft.Json.JsonSerializerSettings() { Culture = CultureInfo.InvariantCulture });
                                v = new Vector4(a.Length > 0 ? a[0] : 0, a.Length > 1 ? a[1] : 0, a.Length > 2 ? a[2] : 0, a.Length > 3 ? a[3] : 0);
                            }
                            catch
                            {
                                Main.logger.LogWarning($"Custom Skin material property value is not a valid vector (must be a set of floating point numbers, example: \"[0, 1.3, 2]\") [target={md.Target},property={md.Property},value={md.Value}]");
                            }
                        }
                        else if (pt == ShaderPropertyType.Texture)
                        {
                            var k = new ResouceKey(md.Value);
                            if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Texture>(out var tex))
                                v = tex;
                            else
                                Main.logger.LogWarning($"Custom Skin material property requested texture [bundle={k.bundle},resource={k.resource}] but no texture was loaded [target={md.Target},property={md.Property},value={md.Value}]");
                        }
                        else
                            Main.logger.LogWarning($"Custom Skin material property is of an unsupported property type [target={md.Target},property={md.Property},value={md.Value}]");
                        break;
                    }
                if (v == null)
                {
                    if (!flag)
                        Main.logger.LogWarning($"Custom Skin material does not have that property [target={md.Target},property={md.Property},value={md.Value}]");
                    continue;
                }
                foreach (var m in ind == -1 ? ms : new[] { ms[ind] })
                    if (v is Color c)
                        m.SetColor(md.Property, c);
                    else if (v is float f)
                        m.SetFloat(md.Property, f);
                    else if (v is int i)
                        m.SetInt(md.Property, i);
                    else if (v is Vector4 d)
                        m.SetVector(md.Property, d);
                    else if (v is Texture t)
                        m.SetTexture(md.Property, t);
                    else
                        Main.logger.LogWarning($"Custom Skin YOU SHOULD NEVER SEE THIS [target={md.Target},property={md.Property},value={md.Value}]");
            }
        }
        public override void Destroy()
        {
            base.Destroy();
            skin._BabyMaterials.DestroyAll();
            skin._TeenMaterials.DestroyAll();
            skin._Materials.DestroyAll();
            skin._TitanMaterials.DestroyAll();
            Object.Destroy(skin.gameObject);
            if (hwskin)
            {
                hwskin._BabyMaterials.DestroyAll();
                hwskin._TeenMaterials.DestroyAll();
                hwskin._Materials.DestroyAll();
                hwskin._TitanMaterials.DestroyAll();
                Object.Destroy(hwskin.gameObject);
            }
            customAssets.Remove(item.AssetName.After('/'));
        }
        protected override GameObject GetAsset(string key)
        {
            var flag = key.StartsWith("HW");
            if (Main.logging)
                Main.logger.LogInfo($"Found requested asset {(flag ? hwskin : skin)}");
            return (flag ? hwskin : skin)?.gameObject;
        }
        protected override void Setup()
        {
            item.AssetName = $"{Main.CustomBundleName}/DragonSkin_{ItemID}";
            item.Category = new[] { new ItemDataCategory() { CategoryId = Category.DragonSkin } };
            skin = new GameObject(item.AssetName.After('/')).AddComponent<DragonSkin>();
            Object.DontDestroyOnLoad(skin.gameObject);
            if (MaterialData != null)
            {
                if (MaterialData.Any(x => x.Target.StartsWith("Baby")))
                    skin._BabyMaterials = new[]
                    {
                    CreateFromTemplate("BabyBody"),
                    CreateFromTemplate("BabyEyes")
                };
                if (MaterialData.Any(x => x.Target.StartsWith("Teen")))
                    skin._TeenLODMaterials = skin._TeenMaterials = new[]
                    {
                    CreateFromTemplate("TeenBody"),
                    CreateFromTemplate("TeenEyes")
                };
                if (MaterialData.Any(x => x.Target.StartsWith("Adult")))
                    skin._LODMaterials = skin._Materials = new[]
                    {
                    CreateFromTemplate("Body"),
                    CreateFromTemplate("Eyes")
                };
                if (MaterialData.Any(x => x.Target.StartsWith("Titan")))
                    skin._TitanLODMaterials = skin._TitanMaterials = new[]
                    {
                    CreateFromTemplate("TitanBody"),
                    CreateFromTemplate("TitanEyes")
                };
            }
            skin._RenderersToChange = TargetRenderers;
            if (HWMaterialData != null)
            {
                if (HWMaterialData.Length == 0)
                    hwskin = skin;
                else
                {
                    hwskin = Object.Instantiate(skin);
                    Object.DontDestroyOnLoad(hwskin.gameObject);
                    hwskin.name = "HW" + skin.name;
                    hwskin._BabyMaterials.InstatiateAll((n, o) => n.name = "HW" + o.name);
                    hwskin._TeenMaterials.InstatiateAll((n, o) => n.name = "HW" + o.name);
                    hwskin._TeenLODMaterials = hwskin._TeenMaterials;
                    hwskin._Materials.InstatiateAll((n, o) => n.name = "HW" + o.name);
                    hwskin._LODMaterials = hwskin._Materials;
                    hwskin._TitanMaterials.InstatiateAll((n, o) => n.name = "HW" + o.name);
                    hwskin._TitanLODMaterials = hwskin._TitanMaterials;
                }
            }
            customAssets[item.AssetName.After('/')] = this;
            if (Main.logging)
                Main.logger.LogInfo($"Created skin {Name} as asset {item.AssetName.After('/')}");
        }
        static Material _t;
        static Shader _s;
        static Shader MyShader => _s ? _s : _s = Shader.Find("JS Games/Dragon Bumped Spec");
        static Material CreateFromTemplate(string name)
        {
            if (!_t)
            {
                _t = new Material(MyShader);
                var cm = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                cm.name = "NoColorMask";
                cm.SetPixel(0, 0, Color.red);
                cm.Apply();
                _t.SetTexture("_ColorMask", cm);
            }
            var n = Object.Instantiate(_t);
            n.name = name;
            return n;
        }
    }
    [Serializable]
    public class MaterialProperty
    {
        public string Property;
        public string Value;
        public string Target;
    }
    [Serializable]
    public class MeshOverrides
    {
        [OptionalField]
        public string Baby;
        [OptionalField]
        public string Teen;
        [OptionalField]
        public string Adult;
        [OptionalField]
        public string Titan;
    }
}
