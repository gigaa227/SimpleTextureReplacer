using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SimpleResourceReplacer
{
    // Prevents the skins from appearing twice in the customization UI
    [HarmonyPatch(typeof(KAUISelectDragonMenu))]
    static class Patch_InventoryOpen
    {
        static ConditionalWeakTable<KAUISelectDragonMenu, HashSet<int>> added = new ConditionalWeakTable<KAUISelectDragonMenu, HashSet<int>>();
        [HarmonyPatch("AddInvMenuItem")]
        static bool Prefix(KAUISelectDragonMenu __instance, UserItemData userItem)
        {
            if (!added.GetOrCreateValue(__instance).Add(userItem.ItemID))
                return false;
            if (Main.logging)
                Main.logger.LogInfo("Trying to add item to dragon customization UI: " + Newtonsoft.Json.JsonConvert.SerializeObject(userItem));
            return true;
        }
        [HarmonyPatch("FinishMenuItems")]
        static void Prefix(KAUISelectDragonMenu __instance) => added.GetOrCreateValue(__instance).Clear();
    }
}
