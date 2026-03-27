using System.Collections.Generic;
using System.Linq;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using UnityEngine;

namespace WitchTower.Battle
{
    public static class BattleVisualResolver
    {
        private static readonly string[] FallbackPartySpritePaths =
        {
            "MonsterPortraits/mon_rock_golem_portrait",
            "FormationMonsters/Goblin",
            "FormationMonsters/Wraith",
            "FormationMonsters/Centaur",
            "FormationMonsters/HellKnight"
        };

        private static readonly Dictionary<string, string> EnemyPortraitPaths = new Dictionary<string, string>
        {
            { "enemy_slime", "FormationMonsters/Worm" },
            { "enemy_guard", "FormationMonsters/VaultGuard" },
            { "enemy_harpy", "FormationMonsters/Bat" },
            { "enemy_knight", "FormationMonsters/HellKnight" },
            { "enemy_wraith", "FormationMonsters/Wraith" }
        };

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static OwnedMonsterData ResolveLeadOwnedMonster(PlayerProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            if (profile.PartyMonsterInstanceIds != null)
            {
                foreach (string instanceId in profile.PartyMonsterInstanceIds)
                {
                    OwnedMonsterData partyMonster = profile.GetOwnedMonster(instanceId);
                    if (partyMonster != null)
                    {
                        return partyMonster;
                    }
                }
            }

            return profile.OwnedMonsters != null && profile.OwnedMonsters.Count > 0
                ? profile.OwnedMonsters[0]
                : null;
        }

        public static List<OwnedMonsterData> ResolvePartyOwnedMonsters(PlayerProfile profile, int maxCount = 5)
        {
            var result = new List<OwnedMonsterData>();
            if (profile == null || maxCount <= 0)
            {
                return result;
            }

            var seenInstanceIds = new HashSet<string>();

            if (profile.PartyMonsterInstanceIds != null)
            {
                foreach (string instanceId in profile.PartyMonsterInstanceIds)
                {
                    OwnedMonsterData ownedMonster = profile.GetOwnedMonster(instanceId);
                    if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                    {
                        continue;
                    }

                    result.Add(ownedMonster);
                    if (result.Count >= maxCount)
                    {
                        return result;
                    }
                }
            }

            if (profile.OwnedMonsters == null)
            {
                return result;
            }

            foreach (OwnedMonsterData ownedMonster in profile.OwnedMonsters)
            {
                if (ownedMonster == null || string.IsNullOrEmpty(ownedMonster.InstanceId) || !seenInstanceIds.Add(ownedMonster.InstanceId))
                {
                    continue;
                }

                result.Add(ownedMonster);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }

            return result;
        }

        public static MonsterDataSO ResolvePlayerMonsterData(PlayerProfile profile)
        {
            MasterDataManager.Instance?.Initialize();

            OwnedMonsterData ownedMonster = ResolveLeadOwnedMonster(profile);
            if (ownedMonster != null)
            {
                MonsterDataSO ownedMonsterData = MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId);
                if (ownedMonsterData != null)
                {
                    return ownedMonsterData;
                }
            }

            return MasterDataManager.Instance?.GetMonsterData("monster_rock_golem");
        }

        public static Sprite ResolvePlayerSprite(PlayerProfile profile)
        {
            MonsterDataSO monsterData = ResolvePlayerMonsterData(profile);
            return ResolveMonsterSprite(monsterData);
        }

        public static List<Sprite> ResolvePartySprites(PlayerProfile profile, int maxCount = 5)
        {
            MasterDataManager.Instance?.Initialize();

            var result = new List<Sprite>();
            List<OwnedMonsterData> ownedMonsters = ResolvePartyOwnedMonsters(profile, maxCount);
            foreach (OwnedMonsterData ownedMonster in ownedMonsters)
            {
                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId);
                result.Add(ResolveMonsterSprite(monsterData));
            }

            int fallbackIndex = 0;
            while (result.Count < maxCount)
            {
                Sprite fallbackSprite = fallbackIndex < FallbackPartySpritePaths.Length
                    ? LoadSprite(FallbackPartySpritePaths[fallbackIndex])
                    : null;
                result.Add(fallbackSprite);
                fallbackIndex += 1;
            }

            while (result.Count < maxCount)
            {
                result.Add(null);
            }

            return result;
        }

        public static Sprite ResolveEnemySprite(EnemyDataSO enemyData)
        {
            if (enemyData == null)
            {
                return LoadSprite("FormationMonsters/HellKnight");
            }

            if (EnemyPortraitPaths.TryGetValue(enemyData.enemyId, out string resourcePath))
            {
                return LoadSprite(resourcePath);
            }

            return LoadSprite("FormationMonsters/HellKnight");
        }

        public static Sprite ResolveMonsterSprite(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(monsterData.battleIdleResourcePath))
            {
                return LoadSprite(monsterData.battleIdleResourcePath);
            }

            if (!string.IsNullOrEmpty(monsterData.portraitResourcePath))
            {
                return LoadSprite(monsterData.portraitResourcePath);
            }

            return monsterData.portraitSprite != null ? monsterData.portraitSprite : monsterData.illustrationSprite;
        }

        public static string BuildMonsterRoleText(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return "未設定";
            }

            return $"{ToElementLabel(monsterData.element)} / {ToRangeLabel(monsterData.rangeType)} / {ToDamageLabel(monsterData.damageType)}";
        }

        public static string BuildEnemyRoleText(EnemyDataSO enemyData)
        {
            if (enemyData == null)
            {
                return "敵データなし";
            }

            return ToEnemyTraitLabel(enemyData.enemyTrait);
        }

        public static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            if (SpriteCache.TryGetValue(resourcePath, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite == null)
            {
                Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath);
                if (loadedSprites != null && loadedSprites.Length > 0)
                {
                    loadedSprite = loadedSprites[0];
                }
            }

            if (loadedSprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    loadedSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            if (loadedSprite != null)
            {
                SpriteCache[resourcePath] = loadedSprite;
            }

            return loadedSprite;
        }

        private static string ToElementLabel(MonsterElement element)
        {
            return element switch
            {
                MonsterElement.Wood => "木",
                MonsterElement.Water => "水",
                MonsterElement.Fire => "火",
                MonsterElement.Light => "光",
                MonsterElement.Dark => "闇",
                _ => "無"
            };
        }

        private static string ToRangeLabel(MonsterRangeType rangeType)
        {
            return rangeType == MonsterRangeType.Ranged ? "遠距離" : "近距離";
        }

        private static string ToDamageLabel(MonsterDamageType damageType)
        {
            return damageType == MonsterDamageType.Magic ? "魔法" : "物理";
        }

        private static string ToEnemyTraitLabel(EnemyTrait trait)
        {
            return trait switch
            {
                EnemyTrait.HighDefense => "堅守型",
                EnemyTrait.FastAttack => "速攻型",
                EnemyTrait.Drain => "吸収型",
                EnemyTrait.Critical => "会心型",
                _ => "標準型"
            };
        }
    }
}
