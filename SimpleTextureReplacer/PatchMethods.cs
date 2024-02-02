using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleResourceReplacer
{
    static class PatchMethods
    {
        static FieldInfo[] skinMaterials = typeof(DragonSkin).GetFields(~BindingFlags.Default).Where(x => x.Name.Contains("Materials")).ToArray();
        public static void TryChange(string bundle,string resource, GameObject target)
        {
            if (Main.logging)
                Main.logger.LogInfo($"TryChange  bundle={bundle},resource={resource},target={(target ? target.name : "Null")}");
            if (Main.GameObjects.TryGetValue((bundle, resource),out var d))
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
                                if (((ot && d.TryGetValue(ot.name, out var r)) || d.TryGetValue("MatProp:" + mat.shader.GetPropertyName(i), out r)) && r.TryReplace<Texture>(out var t))
                                        mat.SetTexture(mat.shader.GetPropertyName(i), t);
                            }
                }
                bool TryEditMats(Material[] mats)
                {
                    var success = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i])
                        {
                            if (d.TryGetValue(mats[i].name, out var r) && r.TryReplace<Material>(out var rm))
                            {
                                mats[i] = rm;
                                success = true;
                            }
                            TryEditMat(mats[i]);
                        }
                    return success;
                }
                foreach (var rend in target.GetComponentsInChildren<Renderer>(true))
                {
                    var m = rend.sharedMaterials;
                    if (TryEditMats(m))
                        rend.sharedMaterials = m;
                    if (rend is SkinnedMeshRenderer skin && ((skin.sharedMesh && d.TryGetValue(skin.sharedMesh.name, out var r) && r.TryReplace<Mesh>(out var nm)) || (d.TryGetValue("SkinnedMesh:" + skin.name, out r) && r.TryReplace<Mesh>(out nm))))
                        skin.sharedMesh = nm;
                }
                foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
                    if ((filter.sharedMesh && d.TryGetValue(filter.sharedMesh.name, out var r) && r.TryReplace<Mesh>(out var nm)) || (d.TryGetValue("Mesh:" + filter.name, out r) && r.TryReplace<Mesh>(out nm)))
                        filter.sharedMesh = nm;
                foreach (var skin in target.GetComponentsInChildren<DragonSkin>(true))
                {
                    foreach (var f in skinMaterials)
                        if (f.GetValue(skin) is Material[] a)
                            TryEditMats(a);
                    if (d.TryGetValue("DragonSkinMesh:Baby", out var r) && r.TryReplace<Mesh>(out var nm))
                        skin._BabyMesh = nm;
                    if (d.TryGetValue("DragonSkinMesh:Teen", out r) && r.TryReplace(out nm))
                        skin._TeenMesh = nm;
                    if (d.TryGetValue("DragonSkinMesh:Adult", out r) && r.TryReplace(out nm))
                        skin._Mesh = nm;
                    if (d.TryGetValue("DragonSkinMesh:Titan", out r) && r.TryReplace(out nm))
                        skin._TitanMesh = nm;
                }
                foreach (var p in d)
                    if (p.Key.StartsWith("Field:"))
                    {
                        if (Main.logging)
                            Main.logger.LogInfo($"Trying to parse {p.Key}");
                        var split = p.Key.Remove(0, 6).Split(',');
                        if ((split.Length != 3 && split.Length != 2) || split[0].StartsWith("Unity") || split[0].StartsWith("System"))
                            continue;
                        if (Main.logging)
                            Main.logger.LogInfo($"Split into [Type={split[0]},Field={split[1]}]");
                        var type = AccessTools.TypeByName(split[0]);
                        if (type == null || !typeof(Component).IsAssignableFrom(type))
                            continue;
                        if (Main.logging)
                            Main.logger.LogInfo($"Got type {type.FullName}");
                        var field = type.GetField(split[1], ~BindingFlags.Default);
                        if (field == null || field.IsStatic || !typeof(Object).IsAssignableFrom(field.FieldType) || typeof(Component).IsAssignableFrom(field.FieldType) || field.FieldType == typeof(GameObject))
                            continue;
                        if (Main.logging)
                            Main.logger.LogInfo($"Got field {field}");
                        foreach (var comp in target.GetComponentsInChildren(type, true))
                            if ((split.Length == 2 || split[2] == comp.name) && p.Value.TryReplace(field.FieldType, out var nv))
                            {
                                if (Main.logging)
                                    Main.logger.LogInfo($"Setting field on {comp}");
                                field.SetValue(comp, nv);
                            }
                    }
                foreach (var widget in target.GetComponentsInChildren<UITexture>(true))
                    if (widget && widget.mainTexture && d.TryGetValue(widget.mainTexture.name, out var r) && r.TryReplace<Texture>(out var n))
                            widget.mainTexture = n;
            }
        }
    }
}