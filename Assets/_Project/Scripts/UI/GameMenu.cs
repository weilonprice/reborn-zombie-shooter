using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ZombieShooter
{
    [DefaultExecutionOrder(-10000)]
    public class GameMenu : MonoBehaviour
    {
        public static GameMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.overlay != null && Instance.overlay.activeSelf;
        int resumedFrame = -1;
        public static bool BlocksGameplayInput => IsOpen || (Instance != null && Time.frameCount <= Instance.resumedFrame);
        public bool AtStart { get; private set; } = true;
        GameObject overlay;
        Text title, subtitle;
        Button primary, returnButton;
        ArmoryUI armory;
        Font font;

        void Awake()
        {
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Game Menus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            overlay = new GameObject("Menu Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.04f, 1f);
            Label("MILITARY BASE  /  SURVIVAL", 18, 190, new Color(0.7f, 0.76f, 0.49f));
            title = Label("REBORN", 68, 115, Color.white);
            subtitle = Label("Hold the base. Survive the horde.", 22, 48, new Color(0.76f, 0.8f, 0.75f));
            primary = MakeButton("START GAME", -35, BeginOrResume);
            returnButton = MakeButton("MAIN MENU", -105, () => GameManager.Instance.Restart());
            returnButton.gameObject.SetActive(false);
            Label("WASD  Move     •     Mouse  Aim / Fire     •     R  Reload\nB  Armory     •     ESC  Pause / Resume", 18, -215, new Color(0.7f, 0.74f, 0.7f));
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        void Start()
        {
            armory = FindAnyObjectByType<ArmoryUI>();
            SelectPrimary();
        }

        void Update()
        {
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (AtStart) return;
            if (!escape && !start) return;
            if (GameManager.Instance != null && GameManager.Instance.IsRunOver) return;
            if (IsOpen) BeginOrResume();
            else
            {
                overlay.SetActive(true);
                title.text = "PAUSED";
                subtitle.text = "Take a breath. The base can wait.";
                primary.GetComponentInChildren<Text>().text = "RESUME";
                returnButton.gameObject.SetActive(true);
                Time.timeScale = 0f;
                AudioListener.pause = true;
                SelectPrimary();
            }
        }

        public void BeginOrResume()
        {
            resumedFrame = Time.frameCount;
            AtStart = false;
            overlay.SetActive(false);
            Time.timeScale = armory != null && armory.IsOpen ? 0f : 1f;
            AudioListener.pause = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void SelectPrimary()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }

        Text Label(string value, int size, float y, Color color)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(overlay.transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(900, 90); rect.anchoredPosition = new Vector2(0, y);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.text = value; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        Button MakeButton(string label, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(overlay.transform, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(320, 54); rect.anchoredPosition = new Vector2(0, y);
            go.GetComponent<Image>().color = new Color(0.3f, 0.38f, 0.2f);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(action);
            var text = Label(label, 21, 0, Color.white);
            text.transform.SetParent(go.transform, false);
            ((RectTransform)text.transform).sizeDelta = rect.sizeDelta;
            return button;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null; Time.timeScale = 1f; AudioListener.pause = false;
        }
    }
}
