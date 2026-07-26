using System.Collections.Generic;

namespace WitchTower.Save
{
    public static class PlayerSaveDataMigration
    {
        public static bool TryMigrate(PlayerSaveData saveData, out string error)
        {
            error = string.Empty;
            if (saveData == null)
            {
                error = "Save data was null.";
                return false;
            }

            if (saveData.SchemaVersion < 0 || saveData.SchemaVersion > PlayerSaveData.CurrentSchemaVersion)
            {
                error = $"Unsupported save schema version: {saveData.SchemaVersion}.";
                return false;
            }

            // Schema 0 is every save written before explicit versioning was added.
            // Newly introduced fields intentionally retain their serialized defaults.
            saveData.DailyClaimedQuestIds ??= new List<string>();
            saveData.MissionProgressList ??= new List<MissionProgressData>();
            saveData.OwnedMaterials ??= new List<OwnedMaterialData>();
            saveData.OwnedEquipments ??= new List<OwnedEquipmentData>();
            saveData.OwnedEnhancementRelics ??= new List<OwnedEnhancementRelicData>();
            saveData.OwnedMonsters ??= new List<OwnedMonsterData>();
            saveData.MonsterDexEntries ??= new List<MonsterDexEntryData>();
            saveData.PartyMonsterInstanceIds ??= new List<string>();
            saveData.SkillLevels ??= new List<SkillLevelData>();
            saveData.RebirthSkillLevels ??= new List<RebirthSkillLevelData>();
            saveData.SeenStoryEventIds ??= new List<string>();
            saveData.SeenTutorialHintIds ??= new List<string>();
            saveData.SchemaVersion = PlayerSaveData.CurrentSchemaVersion;
            return true;
        }
    }
}
