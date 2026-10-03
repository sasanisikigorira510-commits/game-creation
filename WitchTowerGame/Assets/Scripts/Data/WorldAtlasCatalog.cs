using UnityEngine;

namespace WitchTower.Data
{
    public sealed class WorldAtlasRegion
    {
        public readonly string Id, Name, Description, AccessNote;
        // Coordinates use the map artwork's top-left origin.
        public readonly Vector2 MapPosition;
        public readonly bool IsHomeland;
        public WorldAtlasRegion(string id, string name, Vector2 position, string description, bool homeland = false, string accessNote = null)
        { Id=id; Name=name; MapPosition=position; Description=description; IsHomeland=homeland; AccessNote=accessNote; }
    }

    // Geographic destinations only. Uncharted regions do not create floors,
    // unlock battles, change saves or imply a playable dungeon already exists.
    public static class WorldAtlasCatalog
    {
        public const string MapPath = "UI/DungeonSelect/WorldMap/VastWorldAtlasImage2";
        public static readonly WorldAtlasRegion[] Regions =
        {
            new WorldAtlasRegion("homeland", "故郷の地方", new Vector2(.20f,.77f),
                "見習いの五門洞・獣影の廃工廠\n古契約の地下書庫・紅蓮竜道\n星鉱の巨殿・深淵魔導回廊", true),
            new WorldAtlasRegion("harbor", "空船の港", new Vector2(.46f,.56f),
                "岩ごと空に浮かぶ港。帆を広げた空船が行き交う。\n海を渡った先には、空を旅する人々がいた。"),
            new WorldAtlasRegion("ascending_sea", "天へ昇る海", new Vector2(.49f,.32f),
                "滝が空へ向かって流れ、雲の上に海を作っている。\n頭上の水面には、見たこともない生き物の影が泳ぐ。"),
            new WorldAtlasRegion("shell_sea", "大貝の内海", new Vector2(.84f,.43f),
                "巨大な貝の内側に、淡く光る海と小さな街がある。\n貝が閉じる夜には、真珠の明かりが道しるべになる。"),
            new WorldAtlasRegion("inverted_isles", "逆さの大地", new Vector2(.82f,.17f),
                "島も建物も逆さまに浮かんでいる。\n空を見上げると、誰かがこちらを見上げていた。"),
            new WorldAtlasRegion("living_land", "旅する巨獣", new Vector2(.78f,.77f),
                "島だと思った大地が、ゆっくりと海を渡っていく。\nその背では街が育ち、人々が巨獣とともに旅を続ける。"),
            new WorldAtlasRegion("cloud_isles", "浮遊群島", new Vector2(.21f,.18f),
                "雲より高く浮かぶ島々。足元には青空が広がる。\n風に揺れる橋の向こうにも、まだ知らない島が続く。"),
            new WorldAtlasRegion("time_gate", "時の門", new Vector2(.50f,.10f),
                "止まった時計の輪に、見知らぬ昔の街が映る。\n新章の6話を終えた先で、過去の世界へ続く門。",
                accessNote: "過去への入口・今はまだ閉ざされている")
        };

        public static bool IsAvailable(PlayerProfile profile) => profile != null &&
            profile.HasCompletedTutorial && profile.HighestFloor >= 60 &&
            StoryTutorialService.HasSeenStory(profile, StoryTutorialService.StoryFirstArcComplete);
    }
}
