using System.Collections.Generic;
using Rth.Shared.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Modals
{
    /// <summary>Settings: the game and interface, and the controls.</summary>
    public sealed class SettingsModal : UiModal
    {
        public override string Id => "settings";
        public override string Title => "Settings";
        public override string Icon => "settings";
        public override Vector2 Size => new Vector2(640f, 480f);

        protected override IEnumerable<ModalTab> CreateTabs() => new ModalTab[] { new GeneralSettingsTab(), new ControlsTab() };
    }

    public sealed class GeneralSettingsTab : ModalTab
    {
        public override string Id => "general";
        public override string Title => "General";
        public override string Icon => "settings";

        private UiKit.Check _roofs;
        private UiKit.Check _lock;
        private UiKit.Check _queueing;
        private readonly Dictionary<RevolutionMode, UiKit.Check> _revolution = new Dictionary<RevolutionMode, UiKit.Check>();
        private Label _scale;
        private Label _who;

        protected override void Build(VisualElement root)
        {
            ScrollView scroll = UiKit.Scroll(root);

            UiKit.Heading(scroll, "Display");
            VisualElement display = UiKit.El(scroll, "card");
            _roofs = new UiKit.Check(display, "Show roofs", () => RoofHider.ShowRoofs = !RoofHider.ShowRoofs);
            UiKit.Text(display, "Roofs always hide while you're indoors.", "muted", "small");

            UiKit.Heading(scroll, "Interface").style.marginTop = 12f;
            VisualElement ui = UiKit.El(scroll, "card");
            _lock = new UiKit.Check(ui, "Lock windows in place", () => Ui.Locked = !Ui.Locked);
            VisualElement scaleRow = UiKit.El(ui, "row");
            scaleRow.style.marginTop = 4f;
            UiKit.Text(scaleRow, "Interface scale");
            UiKit.El(scaleRow, "spacer");
            UiKit.Button(scaleRow, "-", () => Ui.Scale -= 0.125f);
            _scale = UiKit.Text(scaleRow, "", "value", "bold");
            _scale.style.width = 56f;
            _scale.style.unityTextAlign = TextAnchor.MiddleCenter;
            UiKit.Button(scaleRow, "+", () => Ui.Scale += 0.125f);
            VisualElement resetRow = UiKit.El(ui, "row");
            resetRow.style.marginTop = 4f;
            UiKit.Text(resetRow, "Put every window back where it started.", "muted", "small");
            UiKit.El(resetRow, "spacer");
            UiKit.Button(resetRow, "Reset layout", () => Ui.ResetLayout());

            // Combat options live in client state, so they follow the player; the server applies them.
            UiKit.Heading(scroll, "Combat").style.marginTop = 12f;
            VisualElement combat = UiKit.El(scroll, "card");
            _queueing = new UiKit.Check(combat, "Ability queueing", () =>
                Session.SetClientState(CombatOptions.QueueingKey, CombatOptions.QueueingValue(!Options().Queueing)));
            UiKit.Text(combat, "An ability pressed during the global cooldown waits and fires when it ends. Off: it is ignored.", "muted", "small");
            UiKit.Text(combat, "Revolution: abilities that fire on their own", "value").style.marginTop = 6f;
            foreach ((RevolutionMode mode, string label) in new[]
            {
                (RevolutionMode.Manual, "Off: every ability by hand"),
                (RevolutionMode.Builders, "Builders"),
                (RevolutionMode.All, "Builders, spenders and ultimates"),
            })
                _revolution[mode] = new UiKit.Check(combat, label, () => Session.SetClientState(CombatOptions.RevolutionKey, CombatOptions.RevolutionValue(mode)));
            UiKit.Text(combat, "When nothing is pressed, the leftmost ready ability on the main bar fires after each global cooldown.", "muted", "small");

            UiKit.Heading(scroll, "Account").style.marginTop = 12f;
            VisualElement account = UiKit.El(scroll, "card", "row");
            _who = UiKit.Text(account, "");
            UiKit.El(account, "spacer");
            UiKit.Button(account, "Log out", () =>
            {
                Ui.CloseModal();
                Session.Logout();
            });
        }

        public override void Refresh()
        {
            _roofs.Set(RoofHider.ShowRoofs);
            _lock.Set(Ui.Locked);
            CombatOptions options = Options();
            _queueing.Set(options.Queueing);
            foreach (KeyValuePair<RevolutionMode, UiKit.Check> r in _revolution) r.Value.Set(options.Revolution == r.Key);
            _scale.text = Mathf.RoundToInt(Ui.Scale * 100f) + "%";
            EntityView local = Ui.world.Local;
            _who.text = "Logged in as " + UiKit.Colour(Session.PlayerName, UiKit.Value) + (local != null ? UiKit.Colour("   at " + local.Tile, UiKit.Muted) : "");
        }

        private CombatOptions Options() => new CombatOptions
        {
            Queueing = CombatOptions.QueueingFrom(Session.ClientState.TryGetValue(CombatOptions.QueueingKey, out string q) ? q : null),
            Revolution = CombatOptions.RevolutionFrom(Session.ClientState.TryGetValue(CombatOptions.RevolutionKey, out string r) ? r : null),
        };
    }

    /// <summary>Every key and click, in one place.</summary>
    public sealed class ControlsTab : ModalTab
    {
        public override string Id => "controls";
        public override string Title => "Controls";
        public override string Icon => "guide";

        protected override void Build(VisualElement root)
        {
            ScrollView scroll = UiKit.Scroll(root);
            Section(scroll, "Moving and fighting", new[]
            {
                ("Left-click", "Walk, attack, talk, take: the action shown top left"),
                ("Right-click", "Every option for what's under the pointer"),
                ("R", "Run on or off"),
                ("1 - 0, -, =", "Main action bar"),
                ("Z / X / C", "Deflect melee / ranged / occult"),
                ("Arrow keys, middle-drag", "Turn the camera"),
                ("Mouse wheel", "Zoom (over the world)"),
            });
            Section(scroll, "Windows", new[]
            {
                ("B / E / A / F / K / Q", "Pack, worn, abilities, focuses, skills, quests"),
                ("J / G / M", "Journal, guide, world map"),
                ("Enter", "Type in chat; Enter again sends"),
                ("Space, 1 - 9", "Continue a conversation, choose an answer"),
                ("Escape", "Close the open window or conversation"),
                ("Drag a title bar", "Move a window (edges snap)"),
                ("Drag a tab", "Pull a panel out, or onto another window"),
            });
        }

        private static void Section(VisualElement parent, string title, (string Key, string What)[] rows)
        {
            UiKit.Heading(parent, title).style.marginTop = 8f;
            VisualElement card = UiKit.El(parent, "card");
            foreach ((string key, string what) in rows)
            {
                VisualElement row = UiKit.El(card, "row");
                row.style.paddingTop = row.style.paddingBottom = 2f;
                Label k = UiKit.Text(row, key, "value", "bold");
                k.style.width = 190f;
                UiKit.Text(row, what);
            }
        }
    }
}
