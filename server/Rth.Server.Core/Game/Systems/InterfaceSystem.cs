using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>The window a player has open, and what it's anchored to.</summary>
    public sealed class OpenInterface
    {
        public InterfaceKind Kind;
        /// <summary>Which bank/shop/etc. (a shop id), if it matters.</summary>
        public string Id = "";
        public int Plane;
        public TileCoord Anchor;
    }

    /// <summary>
    /// Server-driven windows (bank, shops, later smithing menus or quest journals). Opening one is
    /// anchored to where it was opened; walking away, opening another, or dying closes it. Systems
    /// check <see cref="IsOpen"/> before acting on a window's messages.
    /// </summary>
    public sealed class InterfaceSystem : GameSystem
    {
        /// <summary>How far from the anchor the player can drift before the window closes.</summary>
        private const int Reach = 2;

        protected override void OnAttach() => World.OnMessage<CloseInterfaceMessage>((p, _) => Close(p));

        public OpenInterface State(Player player) => player.Component<OpenInterface>();

        public bool IsOpen(Player player, InterfaceKind kind, string? id = null)
        {
            OpenInterface open = State(player);
            return open.Kind == kind && (id == null || open.Id == id);
        }

        public void Open(Player player, InterfaceKind kind, string title, int plane, TileCoord anchor, string id = "")
        {
            World.Find<DialogueSystem>()?.End(player);
            OpenInterface open = State(player);
            open.Kind = kind;
            open.Id = id;
            open.Plane = plane;
            open.Anchor = anchor;
            player.Connection.Send(new InterfaceMessage { Kind = kind, Title = title });
        }

        public void Close(Player player)
        {
            OpenInterface open = State(player);
            if (open.Kind == InterfaceKind.None) return;
            open.Kind = InterfaceKind.None;
            open.Id = "";
            player.Connection.Send(new InterfaceMessage { Kind = InterfaceKind.None });
        }

        public override void AfterPlayer(Player player)
        {
            OpenInterface open = State(player);
            if (open.Kind == InterfaceKind.None) return;
            if (player.IsDead || player.Plane != open.Plane || player.Position.ChebyshevDistance(open.Anchor) > Reach) Close(player);
        }
    }
}
