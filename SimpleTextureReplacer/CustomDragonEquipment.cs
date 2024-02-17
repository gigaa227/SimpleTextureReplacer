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
    public abstract class CustomDragonEquipment
    {
        public string Name;
        public int ItemID;
        public string SkinIcon;
        public int PetType;
        [OptionalField]
        [NonSerialized]
        public UserItemData userItem;
        [OptionalField]
        [NonSerialized]
        public ItemData item;

        public virtual void Destroy()
        {
            if (Patch_InventoryCreation.hasRun)
            {
                foreach (var l in CommonInventoryData.pInstance.GetInventory().Values)
                    l.RemoveAll(x => x.ItemID == item.ItemID);
                Main.ItemData_RemoveFromCache(item.ItemID);
            }
        }
        protected static Dictionary<string, CustomDragonEquipment> customAssets = new Dictionary<string, CustomDragonEquipment>();
        public static GameObject GetCustomAsset(string key)
        {
            if (Main.logging)
                Main.logger.LogInfo($"Requested custom bundle asset {key}");
            var flag = key.StartsWith("HW");
            if (!customAssets.TryGetValue(flag ? key.Remove(0, 2) : key, out var s))
                return null;
            s.Reapply();
            return s.GetAsset(key);
        }
        protected abstract void Reapply();
        protected abstract GameObject GetAsset(string key);
        static int uiid = -1;
        public void Init()
        {
            userItem = new UserItemData()
            {
                ItemTier = null,
                CreatedDate = new DateTime(638428661429519390L, DateTimeKind.Utc),
                Item = item = new ItemData()
                {
                    AllowStacking = true,
                    AssetName = null,
                    Attribute = new[]
                    {
                        new ItemAttribute()
                        {
                            Key = "PetTypeID",
                            Value = PetType.ToString()
                        }
                    },
                    Availability = null,
                    BluePrint = null,
                    CashCost = 0,
                    Category = new ItemDataCategory[0],
                    Cost = 0,
                    CreativePoints = 0,
                    Description = "A custom skin",
                    Geometry2 = null,
                    IconName = SkinIcon,
                    InventoryMax = 1,
                    IsNew = false,
                    ItemID = ItemID,
                    ItemName = Name,
                    ItemNamePlural = null,
                    ItemRarity = null,
                    ItemSaleConfigs = null,
                    ItemStates = new List<ItemState>(),
                    ItemStatsMap = null,
                    Locked = false,
                    MemberSaleList = null,
                    Points = null,
                    PopularRank = -1,
                    PossibleStatsMap = null,
                    RankId = null,
                    Relationship = null,
                    RewardTypeID = 0,
                    Rollover = null,
                    SaleFactor = 0,
                    SaleList = null,
                    Stackable = true,
                    Texture = null,
                    Uses = -1
                },
                ItemID = item.ItemID,
                ItemStats = null,
                ModifiedDate = new DateTime(638428661429519390L, DateTimeKind.Utc),
                Quantity = 1,
                UserItemAttributes = null,
                UserInventoryID = uiid--,
                Uses = item.Uses
            };
            Setup();
            if (Patch_InventoryCreation.hasRun)
            {
                foreach (var l in CommonInventoryData.pInstance.GetInventory().Values)
                    l.RemoveAll(x => x.ItemID == item.ItemID);
                CommonInventoryData.pInstance.AddToCategories(userItem);
                ItemData.AddToCache(item);
            }
        }
        protected abstract void Setup();
    }
}
