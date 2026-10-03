using UnityEngine;

namespace WitchTower.UI
{
    // Scene UI must not adopt a runtime overlay canvas just because that
    // canvas has the first instance ID after a load or a return from battle.
    public static class SceneCanvasOwner
    {
        public static Canvas Find(Component owner, string canvasName)
        {
            if (owner == null || !owner.gameObject.scene.IsValid()) return null;
            foreach (var root in owner.gameObject.scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.name == canvasName && canvas.isRootCanvas) return canvas;
            return null;
        }
    }
}
