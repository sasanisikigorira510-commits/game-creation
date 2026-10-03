using UnityEditor;
using WitchTower.UI;

public static class WorldAtlasPreview
{
    [MenuItem("Tools/WitchTower/世界地図 プレビュー")]
    public static void Show()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("世界地図プレビュー", "Playモードで実行してください。進行度にかかわらず表示でき、セーブは変更しません。", "閉じる");
            return;
        }
        WorldAtlasController.ShowEditorPreview();
    }
}
