using System.Collections.Generic;
using System.Linq;
using WitchTower.Data;
using WitchTower.Managers;
using WitchTower.MasterData;
using WitchTower.Save;
using UnityEngine;

namespace WitchTower.Battle
{
    public enum BattleVisualPose
    {
        Idle = 0,
        Move = 1,
        Attack = 2
    }

    public static class BattleVisualResolver
    {
        private static readonly string[] FallbackPartySpritePaths =
        {
            "FamilyMonsters/Dragon/dragon_whelp",
            "FamilyMonsters/Robot/chibi_gear",
            "FamilyMonsters/Golem/rock_golem",
            "FamilyMonsters/Swordsman/apprentice_swordsman",
            "FamilyMonsters/Mage/apprentice_mage"
        };

        private static readonly Dictionary<string, string> EnemyPortraitPaths = new Dictionary<string, string>
        {
            { "enemy_slime", "FamilyMonsters/Slime/Slime1" },
            { "enemy_guard", "FormationMonsters/VaultGuard" },
            { "enemy_harpy", "FormationMonsters/Bat" },
            { "enemy_knight", "FormationMonsters/HellKnight" },
            { "enemy_wraith", "FormationMonsters/Wraith" }
        };

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, List<Sprite>> SpriteFramesCache = new Dictionary<string, List<Sprite>>();

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

            if (PrototypePartyBootstrapService.EnsureParty(profile, maxCount))
            {
                SaveManager.Instance?.SaveCurrentGame();
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
            return ResolveMonsterIdleSprite(monsterData);
        }

        public static List<Sprite> ResolvePartySprites(PlayerProfile profile, int maxCount = 5)
        {
            MasterDataManager.Instance?.Initialize();

            var result = new List<Sprite>();
            List<OwnedMonsterData> ownedMonsters = ResolvePartyOwnedMonsters(profile, maxCount);
            foreach (OwnedMonsterData ownedMonster in ownedMonsters)
            {
                MonsterDataSO monsterData = MasterDataManager.Instance?.GetMonsterData(ownedMonster.MonsterId);
                result.Add(ResolveMonsterIdleSprite(monsterData));
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
            return ResolveEnemyIdleSprite(enemyData);
        }

        public static Sprite ResolveEnemyIdleSprite(EnemyDataSO enemyData)
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

        public static Sprite ResolveEnemyMoveSprite(EnemyDataSO enemyData)
        {
            return ResolveEnemyIdleSprite(enemyData);
        }

        public static Sprite ResolveEnemyAttackSprite(EnemyDataSO enemyData)
        {
            return ResolveEnemyIdleSprite(enemyData);
        }

        public static Sprite ResolveMonsterSprite(MonsterDataSO monsterData)
        {
            return ResolveMonsterIdleSprite(monsterData);
        }

        public static Sprite ResolveMonsterIdleSprite(MonsterDataSO monsterData)
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

        public static Sprite ResolveMonsterMoveSprite(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(monsterData.battleMoveResourcePath))
            {
                return LoadSprite(monsterData.battleMoveResourcePath);
            }

            return ResolveMonsterIdleSprite(monsterData);
        }

        public static Sprite ResolveMonsterAttackSprite(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(monsterData.battleAttackResourcePath))
            {
                return LoadSprite(monsterData.battleAttackResourcePath);
            }

            return ResolveMonsterIdleSprite(monsterData);
        }

        public static List<Sprite> ResolveEnemyIdleSprites(EnemyDataSO enemyData)
        {
            return BuildSingleSpriteList(ResolveEnemyIdleSprite(enemyData));
        }

        public static List<Sprite> ResolveEnemyMoveSprites(EnemyDataSO enemyData)
        {
            return BuildSingleSpriteList(ResolveEnemyMoveSprite(enemyData));
        }

        public static List<Sprite> ResolveEnemyAttackSprites(EnemyDataSO enemyData)
        {
            return BuildSingleSpriteList(ResolveEnemyAttackSprite(enemyData));
        }

        public static BattleFacingDirection ResolveMonsterFacing(MonsterDataSO monsterData, BattleVisualPose pose)
        {
            if (monsterData == null)
            {
                return BattleFacingDirection.Left;
            }

            switch (pose)
            {
                case BattleVisualPose.Move:
                    return monsterData.battleMoveFacing;
                case BattleVisualPose.Attack:
                    return monsterData.battleAttackFacing;
                case BattleVisualPose.Idle:
                default:
                    return monsterData.battleIdleFacing;
            }
        }

        public static BattleFacingDirection ResolveEnemyFacing(EnemyDataSO enemyData, BattleVisualPose pose)
        {
            if (enemyData == null)
            {
                return BattleFacingDirection.Left;
            }

            switch (pose)
            {
                case BattleVisualPose.Move:
                    return enemyData.battleMoveFacing;
                case BattleVisualPose.Attack:
                    return enemyData.battleAttackFacing;
                case BattleVisualPose.Idle:
                default:
                    return enemyData.battleIdleFacing;
            }
        }

        public static List<Sprite> ResolveMonsterIdleSprites(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return new List<Sprite>();
            }

            if (!string.IsNullOrEmpty(monsterData.battleIdleResourcePath))
            {
                return LoadSpriteFrames(monsterData.battleIdleResourcePath);
            }

            return BuildSingleSpriteList(ResolveMonsterIdleSprite(monsterData));
        }

        public static List<Sprite> ResolveMonsterMoveSprites(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return new List<Sprite>();
            }

            if (!string.IsNullOrEmpty(monsterData.battleMoveResourcePath))
            {
                List<Sprite> frames = LoadSpriteFrames(monsterData.battleMoveResourcePath);
                if (frames.Count > 0)
                {
                    return frames;
                }
            }

            return ResolveMonsterIdleSprites(monsterData);
        }

        public static List<Sprite> ResolveMonsterAttackSprites(MonsterDataSO monsterData)
        {
            if (monsterData == null)
            {
                return new List<Sprite>();
            }

            if (!string.IsNullOrEmpty(monsterData.battleAttackResourcePath))
            {
                List<Sprite> frames = LoadSpriteFrames(monsterData.battleAttackResourcePath);
                if (frames.Count > 0)
                {
                    return frames;
                }
            }

            return ResolveMonsterIdleSprites(monsterData);
        }

        public static List<Sprite> ResolveSpriteFramesFromResourcePath(string resourcePath)
        {
            return LoadSpriteFrames(resourcePath);
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

        public static List<Sprite> LoadSpriteFrames(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return new List<Sprite>();
            }

            if (SpriteFramesCache.TryGetValue(resourcePath, out List<Sprite> cachedFrames))
            {
                return cachedFrames;
            }

            var frames = new List<Sprite>();
            for (int i = 0; i < 16; i += 1)
            {
                Sprite frame = LoadSprite($"{resourcePath}_{i}");
                if (frame == null)
                {
                    break;
                }

                frames.Add(frame);
            }

            if (frames.Count == 0)
            {
                Sprite singleSprite = LoadSprite(resourcePath);
                if (singleSprite != null)
                {
                    frames.Add(singleSprite);
                }
            }

            SpriteFramesCache[resourcePath] = frames;
            return frames;
        }

        private static List<Sprite> BuildSingleSpriteList(Sprite sprite)
        {
            if (sprite == null)
            {
                return new List<Sprite>();
            }

            return new List<Sprite> { sprite };
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
