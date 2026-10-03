using System.Collections.Generic;

namespace WitchTower.Save
{
    // These are investigation signals, not proof of cheating. Keep the original values.
    public static class SaveDataDiagnostics
    {
        public static List<string> Inspect(PlayerSaveData data)
        {
            var issues = new List<string>();
            if (data.Gold < 0 || data.FreeGachaStones < 0 || data.PaidGachaStones < 0)
                issues.Add("negative_currency");
            var ids = new HashSet<string>();
            foreach (var monster in data.OwnedMonsters)
            {
                if (monster == null || string.IsNullOrEmpty(monster.InstanceId) || !ids.Add(monster.InstanceId))
                    issues.Add("invalid_monster_instance");
                else if (monster.Level < 1 || monster.Exp < 0)
                    issues.Add("invalid_monster_progress");
            }
            ids.Clear();
            foreach (var equipment in data.OwnedEquipments)
                if (equipment == null || string.IsNullOrEmpty(equipment.InstanceId) || !ids.Add(equipment.InstanceId))
                    issues.Add("invalid_equipment_instance");
            return issues;
        }
    }
}
