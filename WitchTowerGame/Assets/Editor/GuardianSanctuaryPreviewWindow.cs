using System;
using UnityEditor;
using UnityEngine;
using WitchTower.Data;
using WitchTower.Home;
using WitchTower.Save;

public sealed class GuardianSanctuaryPreviewWindow : EditorWindow
{
    private static readonly string[] Ids = { "seiryu", "suzaku", "byakko", "genbu" };
    private static readonly string[] Names = { "青龍", "朱雀", "白虎", "玄武" };
    private static readonly string[] States = { "未解放", "試練前", "神核から誕生", "契約済み・Lv.1", "契約型解放・Lv.10", "契約の紋章・Lv.20", "誓約達成・Lv.30", "個別育成・Lv.30／20／10／1" };
    [SerializeField] private int guardian;
    [SerializeField] private int state = 4;
    private string message;

    [MenuItem("Tools/WitchTower/神獣の聖域 プレビュー")]
    public static void Open()
    {
        var window = GetWindow<GuardianSanctuaryPreviewWindow>("神獣の聖域");
        window.minSize = new Vector2(390, 300);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("四神の新しい聖域画面と契約演出を確認できます。独立した確認用データを使い、通常のセーブ・石・仲間は変更しません。", MessageType.Info);
        guardian = EditorGUILayout.Popup("表示する神獣", guardian, Names);
        state = EditorGUILayout.Popup("確認する状態", state, States);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorApplication.isPaused))
        {
            if (GUILayout.Button("Game ビューで聖域を開く", GUILayout.Height(38))) Show(false);
            if (GUILayout.Button("4秒の契約誕生演出を再生", GUILayout.Height(38))) Show(true);
        }
        if (!EditorApplication.isPlaying) EditorGUILayout.HelpBox("Play モードにしてから再生してください。画面はスマートフォンの縦表示を想定しています。", MessageType.None);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("確認できる操作", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("四神の切り替え・神技のタイプ選択・編成・誕生演出のスキップ。試練への移動はプレビューから行いません。", EditorStyles.wordWrappedLabel);
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
    }

    private void Show(bool birth)
    {
        var view = GuardianSanctuaryController.ShowPreview(CreateProfile(state, Ids[guardian]), Ids[guardian]);
        if (birth) view.PreviewBirth();
        message = "Game ビューに表示しました。通常の保存データは変更していません。";
        Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameView != null) GetWindow(gameView).Show();
    }

    public static PlayerProfile CreateProfile(int visualState, string selectedId)
    {
        var save = PlayerSaveData.CreateDefault();
        save.HighestFloor = visualState == 0 ? 20 : 60;
        save.CurrentFloor = save.HighestFloor + 1;
        save.HasCompletedTutorial = true;
        save.TutorialStepId = "Complete";
        int level = visualState == 6 ? 30 : visualState == 5 ? 20 : visualState == 4 ? 10 : 1;
        save.SeenStoryEventIds.Add(StoryTutorialService.StoryFirstArcComplete);
        save.SeenTutorialHintIds.Add(GuardianService.IntroductionSeen);
        if (visualState == 2) save.GuardianCoreIds.Add(selectedId);
        if (visualState >= 3)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                int guardianLevel = visualState == 7 ? new[] { 30, 20, 10, 1 }[i] : level;
                int guardianExp = visualState == 7 ? new[] { 0, 35, 90, 40 }[i] : 0;
                save.OwnedGuardians.Add(new OwnedGuardianData { Id = Ids[i], Level = guardianLevel, Exp = guardianExp, ContractId = "basic" });
            }
            save.EquippedGuardianId = selectedId;
        }
        if (visualState == 6) foreach (string id in Ids) save.GuardianOathIds.Add(id);
        return new PlayerProfile(save);
    }
}
