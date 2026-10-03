using System;
using UnityEngine;
using UnityEngine.UI;

namespace WitchTower.Home
{
    /// <summary>
    /// One registered drawing with articulated arms. The body pixels never
    /// change, and each joint pose is calculated directly from presentation time.
    /// </summary>
    public sealed class FusionIonaRig
    {
        public const string ResourcePath = "UI/FusionPage/Cinematic/IonaRig/";

        // Small rotations preserve the authored sleeves and joint overlaps.
        // Opposite signs mirror the gesture without scaling or distorting art.
        public const float ShoulderDegrees = 3f;
        public const float ElbowDegrees = -26f;

        [Serializable]
        private sealed class Point
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class LayoutData
        {
            public float canvasWidth;
            public float canvasHeight;
            public Point leftShoulder;
            public Point rightShoulder;
            public Point leftElbow;
            public Point rightElbow;
            public Point leftPalm;
            public Point rightPalm;
        }

        private readonly RectTransform stage;
        private readonly RectTransform root;
        private readonly RectTransform leftElbowJoint, rightElbowJoint;
        private readonly Image leftUpperArm, rightUpperArm, leftForearm, rightForearm;
        private readonly Image[] images;
        private readonly LayoutData layout;
        private readonly Vector2 leftPalmFromElbow, rightPalmFromElbow;

        public Image Body { get; }
        public Image Foreground { get; }
        public RectTransform Root => root;

        public FusionIonaRig(RectTransform stage)
        {
            this.stage = stage != null ? stage : throw new ArgumentNullException(nameof(stage));
            var json = Resources.Load<TextAsset>(ResourcePath + "layout");
            if (json == null) throw new InvalidOperationException("Fusion Iona rig layout is missing.");
            layout = JsonUtility.FromJson<LayoutData>(json.text);
            ValidateLayout(layout);

            root = Rect("FusionIonaRig", stage);
            root.sizeDelta = new Vector2(layout.canvasWidth, layout.canvasHeight);
            // The original drawing is partitioned, without duplicated pixels:
            // rear hair behind the arms; torso and front locks above the arms.
            // Keeping all hair on either side of the arms breaks their depth.
            Body = Picture("FusionIona", "Body", root);
            leftUpperArm = UpperArm("FusionIonaLeftUpperArm", "LeftUpperArm", layout.leftShoulder);
            rightUpperArm = UpperArm("FusionIonaRightUpperArm", "RightUpperArm", layout.rightShoulder);
            leftElbowJoint = Elbow("FusionIonaLeftElbowJoint", leftUpperArm.rectTransform,
                layout.leftShoulder, layout.leftElbow);
            rightElbowJoint = Elbow("FusionIonaRightElbowJoint", rightUpperArm.rectTransform,
                layout.rightShoulder, layout.rightElbow);

            // Forearms overlap the lower end of each upper sleeve at its
            // elbow. Keep draw order separate from the joint hierarchy.
            leftForearm = Forearm("FusionIonaLeftForearm", "LeftForearm", layout.leftElbow);
            rightForearm = Forearm("FusionIonaRightForearm", "RightForearm", layout.rightElbow);
            Foreground = Picture("FusionIonaForeground", "Foreground", root);
            images = new[] { Body, leftUpperArm, rightUpperArm, leftForearm, rightForearm, Foreground };
            leftPalmFromElbow = LocalPoint(layout.leftPalm) - LocalPoint(layout.leftElbow);
            rightPalmFromElbow = LocalPoint(layout.rightPalm) - LocalPoint(layout.rightElbow);
            Render(0f, 0f, 1200f);
        }

        public void Render(float progress, float alpha, float height)
        {
            float amount = Ease(float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress));
            root.anchoredPosition = Vector2.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * (Mathf.Clamp(height, 600f, 1600f) / layout.canvasHeight);
            leftUpperArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ShoulderDegrees * amount);
            rightUpperArm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ShoulderDegrees * amount);
            leftElbowJoint.localRotation = Quaternion.Euler(0f, 0f, ElbowDegrees * amount);
            rightElbowJoint.localRotation = Quaternion.Euler(0f, 0f, -ElbowDegrees * amount);
            FollowJoint(leftForearm.rectTransform, leftElbowJoint);
            FollowJoint(rightForearm.rectTransform, rightElbowJoint);
            float visibility = Mathf.Clamp01(alpha);
            foreach (var image in images)
            {
                image.color = new Color(1f, 1f, 1f, visibility);
                image.enabled = visibility > 0f;
            }
        }

        public Vector2 GetPalmInStage(bool left)
        {
            var arm = left ? leftForearm : rightForearm;
            Vector2 palm = left ? leftPalmFromElbow : rightPalmFromElbow;
            return stage.InverseTransformPoint(arm.rectTransform.TransformPoint(palm));
        }

        private Image UpperArm(string name, string asset, Point shoulder)
        {
            var image = Picture(name, asset, root);
            image.rectTransform.pivot = Pivot(shoulder);
            image.rectTransform.anchoredPosition = LocalPoint(shoulder);
            return image;
        }

        private RectTransform Elbow(string name, RectTransform upperArm, Point shoulder, Point elbow)
        {
            var joint = Rect(name, upperArm);
            joint.sizeDelta = Vector2.one;
            // Anchor at the shoulder pivot. At zero rotation every layer maps
            // to exactly the same canvas, including its transparent padding.
            joint.anchorMin = joint.anchorMax = upperArm.pivot;
            joint.anchoredPosition = LocalPoint(elbow) - LocalPoint(shoulder);
            return joint;
        }

        private Image Forearm(string name, string asset, Point elbow)
        {
            var image = Picture(name, asset, root);
            image.rectTransform.pivot = Pivot(elbow);
            return image;
        }

        private void FollowJoint(RectTransform image, RectTransform joint)
        {
            image.anchoredPosition = root.InverseTransformPoint(joint.position);
            image.localRotation = Quaternion.Inverse(root.rotation) * joint.rotation;
        }

        private Image Picture(string name, string asset, RectTransform parent)
        {
            var sprite = Resources.Load<Sprite>(ResourcePath + asset);
            if (sprite == null) throw new InvalidOperationException("Fusion Iona rig art is missing: " + asset);
            if (Mathf.Abs(sprite.rect.width - layout.canvasWidth) > .5f
                || Mathf.Abs(sprite.rect.height - layout.canvasHeight) > .5f)
                throw new InvalidOperationException("Fusion Iona rig art must share its registered canvas: " + asset);
            var rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(layout.canvasWidth, layout.canvasHeight);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Vector2 Pivot(Point point) => new Vector2(point.x / layout.canvasWidth, 1f - point.y / layout.canvasHeight);
        private Vector2 LocalPoint(Point point) => new Vector2(point.x - layout.canvasWidth * .5f, layout.canvasHeight * .5f - point.y);
        private static float Ease(float p) => p * p * p * (p * (p * 6f - 15f) + 10f);

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        private static void ValidateLayout(LayoutData data)
        {
            if (data == null || !(data.canvasWidth > 0f) || !(data.canvasHeight > 0f)
                || float.IsInfinity(data.canvasWidth) || float.IsInfinity(data.canvasHeight))
                throw new InvalidOperationException("Fusion Iona rig canvas is invalid.");
            foreach (var point in new[] { data.leftShoulder, data.rightShoulder, data.leftElbow,
                data.rightElbow, data.leftPalm, data.rightPalm })
            {
                if (point == null || float.IsNaN(point.x) || float.IsNaN(point.y)
                    || float.IsInfinity(point.x) || float.IsInfinity(point.y)
                    || point.x < 0f || point.x > data.canvasWidth || point.y < 0f || point.y > data.canvasHeight)
                    throw new InvalidOperationException("Fusion Iona rig joint is outside its canvas.");
            }
        }
    }
}
