using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// The upgrade tree for the currently equipped weapon: three paths across, five tiers down.
    /// <para>
    /// The important thing it communicates is <em>why</em> a tier cannot be bought. "Locked by
    /// the 5-3-0 rule" and "you cannot afford it yet" are completely different messages - one
    /// is a permanent consequence of an earlier choice, the other is a matter of waiting. A UI
    /// that greys both identically makes the rule feel arbitrary rather than strategic.
    /// </para>
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] GameObject root;
        [SerializeField] WeaponLoadout loadout;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;

        [Header("Header")]
        [SerializeField] Text weaponLabel;
        [SerializeField] Text ruleLabel;
        [SerializeField] Text[] pathTitles = new Text[1];

        [Header("Tiers, path-major: path0 t1-5, path1 t1-5, path2 t1-5")]
        [SerializeField] Button[] tierButtons = new Button[5];
        [SerializeField] Text[] tierLabels = new Text[5];

        /// <summary>
        /// How many columns this panel was built with. Read from the wiring rather than
        /// fixed, so going back to multiple paths per weapon is a builder change and nothing
        /// here.
        /// </summary>
        int Paths => pathTitles != null ? pathTitles.Length : 0;
        const int TiersPerPath = 5;

        static readonly Color Purchased = new(0.16f, 0.42f, 0.38f, 0.95f);
        static readonly Color Available = new(0.70f, 0.38f, 0.06f, 0.95f);
        static readonly Color TooExpensive = new(0.30f, 0.26f, 0.22f, 0.9f);
        static readonly Color RuleLocked = new(0.34f, 0.16f, 0.16f, 0.9f);
        static readonly Color Future = new(0.18f, 0.19f, 0.22f, 0.85f);

        /// <summary>
        /// Whether <see cref="Subscribe"/> actually attached. Tracked rather than assumed,
        /// because the failure it guards against is silent: a missed subscription leaves a
        /// panel that draws once, never updates, and still takes clicks.
        /// </summary>
        bool subscribed;

        void Awake()
        {
            for (int i = 0; i < tierButtons.Length; i++)
            {
                if (tierButtons[i] == null) continue;

                int path = i / TiersPerPath;   // captured per button, not per frame
                tierButtons[i].onClick.AddListener(() => Buy(path));
            }

            if (openButton != null) openButton.onClick.AddListener(() => Show(true));
            if (closeButton != null) closeButton.onClick.AddListener(() => Show(false));

            if (root != null) root.SetActive(false);
        }

        // Start, not OnEnable. This component lives on ArmoryUI's root, which is never
        // toggled, so OnEnable fires exactly once during scene load - and OnEnable is not
        // ordered against other objects' Awake, where both singletons are assigned. When it
        // lost that race the null checks skipped the subscription and nothing said so: the
        // tree bought tiers and never redrew, so tier one stayed the only live button while
        // every purchase behind it landed. Start is the earliest hook guaranteed to run
        // after every Awake in the scene, which is why ArmoryUI - same GameObject, same
        // managers, working - has always used it.
        void Start() => Subscribe();

        void OnDestroy()
        {
            if (!subscribed) return;

            if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed -= Refresh;
            if (GameManager.Instance != null) GameManager.Instance.GoldChanged -= OnGoldChanged;
            subscribed = false;
        }

        /// <summary>
        /// Idempotent, so <see cref="Show"/> can retry it. If the managers genuinely are not
        /// there yet, opening the panel gets another attempt rather than leaving a dead UI.
        /// </summary>
        void Subscribe()
        {
            if (subscribed) return;

            var manager = UpgradeManager.Instance;
            var game = GameManager.Instance;

            if (manager == null || game == null)
            {
                Debug.LogError(
                    "UpgradePanel: no " +
                    (manager == null ? "UpgradeManager" : "GameManager") +
                    " in the scene, so the tree cannot redraw after a purchase. Buying will " +
                    "still work and the panel will look frozen - re-run the arena builder.",
                    this);
                return;
            }

            manager.Changed += Refresh;
            game.GoldChanged += OnGoldChanged;
            subscribed = true;
        }

        void OnGoldChanged(int _) => Refresh();

        public void Show(bool visible)
        {
            if (root == null) return;

            root.SetActive(visible);

            if (visible)
            {
                Subscribe();
                Refresh();
            }
        }

        void Buy(int path)
        {
            var weapon = loadout != null ? loadout.CurrentDefinition : null;
            if (weapon == null) return;

            UpgradeManager.Instance?.TryBuyNextTier(weapon, path);
        }

        void Refresh()
        {
            if (root == null || !root.activeSelf) return;

            var manager = UpgradeManager.Instance;
            var weapon = loadout != null ? loadout.CurrentDefinition : null;
            var tree = manager != null ? manager.TreeFor(weapon) : null;

            if (weaponLabel != null)
            {
                weaponLabel.text = weapon != null
                    ? weapon.DisplayName.ToUpperInvariant()
                    : "NO WEAPON";
            }

            if (ruleLabel != null)
            {
                // Describes what is actually enforced. With one path per weapon the 5-3-0
                // rule can never trigger, and printing it would be telling the player about
                // a restriction they will never meet.
                ruleLabel.text = tree == null
                    ? "This weapon has no upgrade path yet."
                    : "Five tiers  ·  a run has gold to max exactly one weapon";
            }

            for (int path = 0; path < Paths; path++)
            {
                if (pathTitles != null && path < pathTitles.Length && pathTitles[path] != null)
                {
                    var definition = tree != null ? tree.PathAt(path) : null;
                    pathTitles[path].text = definition != null
                        ? definition.title.ToUpperInvariant()
                        : "—";
                }
            }

            int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;

            for (int i = 0; i < tierButtons.Length; i++)
            {
                int path = i / TiersPerPath;
                int tierIndex = i % TiersPerPath;
                RefreshTier(i, path, tierIndex, tree, weapon, manager, gold);
            }
        }

        void RefreshTier(int i, int path, int tierIndex, WeaponUpgradeTree tree,
                         WeaponDefinition weapon, UpgradeManager manager, int gold)
        {
            var button = tierButtons[i];
            var label = i < tierLabels.Length ? tierLabels[i] : null;
            if (button == null) return;

            var image = button.GetComponent<Image>();
            var tier = tree != null ? tree.TierAt(path, tierIndex) : null;

            if (tier == null)
            {
                button.interactable = false;
                Paint(button, image, Future);
                if (label != null) label.text = string.Empty;
                return;
            }

            int owned = manager != null ? manager.TierIn(weapon, path) : 0;
            bool purchased = tierIndex < owned;
            bool isNext = tierIndex == owned;
            bool allowed = isNext && manager != null && manager.IsNextTierAllowed(weapon, path);
            bool affordable = gold >= tier.cost;

            button.interactable = allowed && affordable;

            Color colour;
            string text;

            if (purchased)
            {
                colour = Purchased;
                text = $"✓ {tier.title}";
            }
            else if (isNext && !allowed)
            {
                // The distinction that makes the rule read as strategy rather than arbitrary.
                colour = RuleLocked;
                text = $"{tier.title}\n<LOCKED BY 5/3/0>";
            }
            else if (isNext && !affordable)
            {
                colour = TooExpensive;
                text = $"{tier.title}\n${tier.cost}";
            }
            else if (isNext)
            {
                colour = Available;
                text = $"{tier.title}\n${tier.cost}";
            }
            else
            {
                colour = Future;
                text = $"{tier.title}\n${tier.cost}";
            }

            Paint(button, image, colour);
            if (label != null) label.text = text;
        }

        /// <summary>
        /// Applies a tier's state colour through the Button's own ColorBlock as well as the
        /// Image.
        /// <para>
        /// Setting <c>image.color</c> alone does not survive: Selectable drives its target
        /// graphic from <see cref="Selectable.colors"/> on every state change, so a
        /// non-interactable button snapped back to one disabled grey. That flattened
        /// purchased, rule-locked, unaffordable and not-yet-reachable into a single swatch -
        /// erasing exactly the distinction this panel exists to draw - and left the live
        /// button showing the grey baked in at build time rather than the "available" amber.
        /// </para>
        /// </summary>
        static void Paint(Button button, Image image, Color colour)
        {
            if (image != null) image.color = colour;

            var colors = button.colors;
            colors.normalColor = colour;
            colors.highlightedColor = colour * 1.25f;
            colors.pressedColor = colour * 0.75f;
            colors.selectedColor = colour;
            // The state colour already says why a tier cannot be bought, so the disabled
            // tint must not overwrite it with grey.
            colors.disabledColor = colour;
            button.colors = colors;
        }
    }
}
