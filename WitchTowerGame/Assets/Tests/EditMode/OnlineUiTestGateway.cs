using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WitchTower.Tests
{
    // Only a presentation-test fixture. Authoritative amounts, rejection and retry
    // behavior are covered separately against the real local Python server.
    internal static class OnlineUiTestGateway
    {
        private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType("WitchTower." + n, true);
        private static object P(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o, string n) => o.GetType().GetField(n).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n).SetValue(o, v);
        private static object Manager(string n) => T("Managers." + n).GetProperty("Instance").GetValue(null);
        public static void Install() => T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, (Action<string, Action<string, string>>)Deliver);
        public static void Clear() => T("Save.OnlinePlayerData").GetField("EditorExecuteOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        private static void Deliver(string json, Action<string, string> done)
        {
            object request = JsonUtility.FromJson(json, T("Save.OnlineRequest"));
            var game = Manager("GameManager");
            object profile = P(game, "PlayerProfile");
            object save = profile.GetType().GetMethod("ToSaveData").Invoke(profile, new[] { P(game, "CurrentFloor") });
            object op = Activator.CreateInstance(T("Save.OnlineOperation"));
            Set(op, "RequestId", Guid.NewGuid().ToString("N"));
            Set(op, "Revision", (long)P(profile, "EconomyRevision") + 1);
            Set(op, "Kind", F(request, "Kind")); Set(op, "Target", F(request, "Target"));
            int free = (int)P(profile, "FreeGachaStones"), paid = (int)P(profile, "PaidGachaStones");
            if ((string)F(request, "Kind") == "upgrade")
            {
                string target = (string)F(request, "Target");
                paid -= target.Contains("storage") ? 1500 : 1200;
                Set(op, "UpgradeField", target == "auto_repeat" ? "HasAutoRepeatFloorUpgrade" : target == "auto_sell" ? "HasAutoSellEquipmentUpgrade" :
                    target == "auto_release" ? "HasAutoReleaseMonsterUpgrade" : target == "monster_storage" ? "MonsterStorageLimit" : "EquipmentStorageLimit");
            }
            else
            {
                int count = (int)F(request, "Count"); bool isPaid = (bool)F(request, "Paid");
                if (isPaid) paid -= count * 300; else free -= count * 300;
                bool tutorial = !(bool)P(profile, "HasCompletedTutorial") && !isPaid;
                int pulls = (int)P(profile, "InitialTutorialSummonCount");
                var pool = (Array)Manager("MasterDataManager").GetType().GetMethod("GetAllMonsterData").Invoke(Manager("MasterDataManager"), null);
                var monsters = Array.CreateInstance(T("Save.OwnedMonsterData"), count);
                for (int i = 0; i < count; i++)
                {
                    string mid = tutorial ? new[] { "monster_dragon_whelp", "monster_rock_golem", "monster_apprentice_mage" }[Math.Min(2, pulls)] :
                        (string)F(pool.Cast<object>().First(m => (int)F(m, "classRank") == 3 && !(bool)F(m, "fusionExclusive")), "monsterId");
                    object monster = Activator.CreateInstance(T("Save.OwnedMonsterData"));
                    Set(monster, "InstanceId", Guid.NewGuid().ToString("N")); Set(monster, "MonsterId", mid); Set(monster, "Level", 1); Set(monster, "HasIndividualValues", true);
                    foreach (var field in new[] { "Hp", "Attack", "Wisdom", "Defense", "MagicDefense", "AttackSpeed" }) Set(monster, "Individual" + field, 50);
                    monsters.SetValue(monster, i);
                }
                Set(op, "Monsters", monsters); Set(op, "TutorialPulls", tutorial ? pulls + count : pulls);
            }
            if (free < 0 || paid < 0) { done(null, "有償宝晶が不足しています。"); return; }
            Set(op, "Free", free); Set(op, "Paid", paid);
            object staged = T("Save.OnlineGrantApplier").GetMethod("Stage").Invoke(null, new[] { save, op });
            var saves = Manager("SaveManager");
            if (saves != null)
            {
                var args = new object[] { staged, null };
                if (!(bool)saves.GetType().GetMethod("TrySave").Invoke(saves, args)) { done(null, (string)args[1]); return; }
            }
            object updated = Activator.CreateInstance(T("Data.PlayerProfile"), staged);
            // Preserve fixture-held object references, matching the pre-network UI tests.
            foreach (var property in profile.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanWrite) property.SetValue(profile, property.GetValue(updated));
                else if (property.GetValue(profile) is IList oldList && property.GetValue(updated) is IList newList && !oldList.IsReadOnly)
                { oldList.Clear(); foreach (var item in newList) oldList.Add(item); }
            }
            done(JsonUtility.ToJson(op), null);
        }
    }
}
