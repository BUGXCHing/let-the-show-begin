using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static HaoxiKaiyan.StagePalette;

namespace HaoxiKaiyan
{
    // UI construction lives here; lifecycle and combat coordination remain in Core.
    public sealed partial class HaoxiGameDirector
    {
        private void BuildInterface()
        {
            circleSprite = BuildCircleSprite(192);
            Texture2D solid = Texture2D.whiteTexture;
            healthSprite = Sprite.Create(solid, new Rect(0, 0, solid.width, solid.height), new Vector2(.5f, .5f));
            var canvasObject = new GameObject("Game HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            controls = new GameObject("Dual control input").AddComponent<DualStickInput>();

            CreateHealthGroup(canvas.transform, "木偶", new Vector2(26, -24), new Vector2(305, 52),
                new Color(0.38f, 0.25f, 0.12f), out playerHealthFill, out playerHealthText);
            CreateHealthGroup(canvas.transform, "对手", new Vector2(-26, -24), new Vector2(305, 52),
                Vermilion, out enemyHealthFill, out enemyHealthText, true);

            statusText = CreateText(canvas.transform, "Opening title",
                "好 戏 开 演    ·    牵 线 为 刃", 24, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -35), new Vector2(440, 54));
            CreateText(canvas.transform, "Control hint",
                "左轮盘：走位 / 闪避        右轮盘：牵引武器 · 双点拾取 / 换武器", 18,
                new Color(0.17f, 0.18f, 0.17f, 0.92f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(780, 42));
            CreateText(canvas.transform, "Keyboard hint", "键盘：WASD 移动 · 方向键牵引 · R 重开 · Esc 暂停", 14,
                new Color(0.24f, 0.24f, 0.22f, 0.72f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(760, 30));

            VirtualStick move = CreateStick(canvas.transform, "身体摇杆", new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(152, 152), new Color(0.43f, 0.25f, 0.13f, 0.42f));
            VirtualStick weapon = CreateStick(canvas.transform, "武器摇杆", new Vector2(1, 0),
                new Vector2(0, 0), new Vector2(152, 152), new Color(0.67f, 0.34f, 0.13f, 0.40f));
            weaponStick = weapon;
            SetAnchoredPosition(move.transform as RectTransform, new Vector2(136, 126), new Vector2(0, 0));
            SetAnchoredPosition(weapon.transform as RectTransform, new Vector2(-136, 126), new Vector2(1, 0));
            controls.Configure(move, weapon, player.transform, stageCamera);

            CreateButton(canvas.transform, "Pause button", "暂停", new Vector2(1, 1), new Vector2(-98, -112),
                new Vector2(108, 46), () => TogglePause());
            CreateButton(canvas.transform, "Restart button", "重开", new Vector2(1, 1), new Vector2(-218, -112),
                new Vector2(108, 46), () => Restart());
            CreateButton(canvas.transform, "Add opponent button", "添加敌人", new Vector2(1, 1), new Vector2(-346, -112),
                new Vector2(120, 46), () => AddEnemyFromButton());

            resultPanel = CreateResultPanel(canvas.transform);
            resultPanel.SetActive(false);
        }

        private void UpdateHud()
        {
            if (playerHealthFill != null) playerHealthFill.fillAmount = player.Health01;
            MarionetteController focus = null;
            float nearest = float.MaxValue;
            int alive = 0;
            foreach (EnemyBrain brain in enemies)
            {
                if (brain.actor == null || brain.actor.IsDefeated) continue;
                alive++;
                float gap = (brain.actor.transform.position - player.transform.position).sqrMagnitude;
                if (gap < nearest) { nearest = gap; focus = brain.actor; }
            }
            if (enemyHealthFill != null) enemyHealthFill.fillAmount = focus != null ? focus.Health01 : 0f;
            if (playerHealthText != null) playerHealthText.text = Mathf.CeilToInt(player.Health).ToString();
            if (enemyHealthText != null) enemyHealthText.text = focus != null ?
                Mathf.CeilToInt(focus.Health) + " · " + alive + "敌" : "0";
        }

        private GameObject CreateResultPanel(Transform parent)
        {
            RectTransform rect = CreateRect("Result overlay", parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 300));
            Image backdrop = rect.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.92f, 0.89f, 0.81f, 0.96f);
            CreateText(rect, "Result", "好戏落幕", 42, Vermilion, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(460, 68));
            CreateText(rect, "Caption", "", 20, Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(470, 52));
            CreateButton(rect, "Again", "再演一场", new Vector2(0.5f, 0.2f), Vector2.zero,
                new Vector2(172, 52), () => Restart());
            return rect.gameObject;
        }

        private void CreateHealthGroup(Transform parent, string title, Vector2 position, Vector2 size,
            Color fillColor, out Image fill, out Text number, bool right = false)
        {
            RectTransform root = CreateRect(title + " health", parent,
                right ? new Vector2(1, 1) : new Vector2(0, 1), right ? new Vector2(1, 1) : new Vector2(0, 1),
                position, size);
            CreateText(root, "Label", title, 15, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(160, 24));
            RectTransform bar = CreateRect("Health track", root, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(0, 6), new Vector2(size.x, 19));
            Image background = bar.gameObject.AddComponent<Image>();
            background.sprite = healthSprite;
            background.color = new Color(0.16f, 0.16f, 0.15f, 0.68f);
            RectTransform inner = CreateRect("Health fill", bar, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(2, 2), new Vector2(size.x - 4, 15));
            fill = inner.gameObject.AddComponent<Image>();
            fill.sprite = healthSprite;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            number = CreateText(root, "Remaining health", "100", 16, Ink, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-5, 0), new Vector2(66, 24));
        }

        private VirtualStick CreateStick(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Color padColor)
        {
            RectTransform pad = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, size);
            Image padImage = pad.gameObject.AddComponent<Image>();
            padImage.sprite = circleSprite;
            padImage.color = padColor;
            padImage.raycastTarget = true;

            RectTransform ring = CreateRect("Inner ring", pad, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size * 0.64f);
            Image ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.sprite = circleSprite;
            ringImage.color = new Color(0.95f, 0.91f, 0.82f, 0.22f);
            ringImage.raycastTarget = false;

            RectTransform knob = CreateRect("Knob", pad, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size * 0.34f);
            Image knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = circleSprite;
            knobImage.color = new Color(0.78f, 0.29f, 0.17f, 0.94f);
            knobImage.raycastTarget = false;
            VirtualStick stick = pad.gameObject.AddComponent<VirtualStick>();
            stick.Configure(knob, size.x * 0.36f);
            if (name == "武器摇杆")
            {
                RectTransform finger = CreateRect("Finger target", pad, new Vector2(.5f, .5f),
                    new Vector2(.5f, .5f), Vector2.zero, size * .13f);
                Image ghost = finger.gameObject.AddComponent<Image>();
                ghost.sprite = circleSprite;
                ghost.color = new Color(1f, .87f, .58f, .58f);
                ghost.raycastTarget = false;
                stick.UsePhysicalFeedback(finger);
                CreateText(pad, "Weapon control hint", "方向   ·   拉力", 10,
                    new Color(.19f, .16f, .13f, .64f), TextAnchor.MiddleCenter,
                    new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, 12), new Vector2(130, 19));
            }
            CreateText(pad, "Stick label", name, 13, new Color(0.18f, 0.16f, 0.13f, 0.85f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -19),
                new Vector2(120, 24));
            return stick;
        }

        private void CreateButton(Transform parent, string name, string label, Vector2 anchor,
            Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.30f, 0.23f, 0.17f, 0.86f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText(rect, "Button text", label, 17, Paper, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Color color,
            TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.Load<Font>("NotoSansSC-UI");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == 1f ? 1f : anchorMin.x == 0f ? 0f : 0.5f,
                anchorMin.y == 1f ? 1f : anchorMin.y == 0f ? 0f : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void SetAnchoredPosition(RectTransform rect, Vector2 position, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
        }

        private static Sprite BuildCircleSprite(int resolution)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime soft circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[resolution * resolution];
            float center = (resolution - 1) * 0.5f;
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01((1f - r) * resolution * 0.22f);
                    pixels[y * resolution + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        }

    }
}
