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
    public class CustomSaddle : CustomDragonEquipment
    {
        [OptionalField]
        public bool CustomMesh;
        public string Mesh;
        public string Texture;
        [OptionalField]
        [NonSerialized]
        public SkinnedMeshRenderer skin;

        protected override void Setup()
        {
            item.Category = new[] { new ItemDataCategory() { CategoryId = Category.DragonSaddles } };
            item.Texture = new[] { new ItemDataTexture() { TextureName = Texture, TextureTypeName = "Style" } };
            if (CustomMesh)
            {
                item.AssetName = $"{Main.CustomBundleName}/DragonSaddle_{ItemID}";
                skin = new GameObject(item.AssetName.After('/')).AddComponent<SkinnedMeshRenderer>();
                skin.gameObject.SetActive(false);
                Object.DontDestroyOnLoad(skin.gameObject);
                customAssets[item.AssetName.After('/')] = this;
            }
            else
                item.AssetName = Mesh;
            if (Main.logging)
                Main.logger.LogInfo($"Created skin {Name} as asset {item.AssetName.After('/')}");
        }
        protected override GameObject GetAsset(string key) => skin.gameObject;
        protected override void Reapply()
        {
            var k = new ResouceKey(Mesh);
            if (Main.SingleAssets.TryGetValue(k, out var r) && r.TryReplace<Mesh>(out var m))
                skin.sharedMesh = m;
        }
        public override void Destroy()
        {
            Object.Destroy(skin.gameObject);
            base.Destroy();
        }
    }
}
