using System.Collections.Generic;
using Rth.Server.Persistence;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// A self-contained piece of game logic (combat, NPCs, items, a skill...) that plugs into the
    /// world. Systems hook the tick phases they need, register handlers for client messages with
    /// <see cref="World.OnMessage{T}"/>, subscribe to <see cref="World.Events"/>, and keep their own
    /// section of the player save. Systems run in the order they were added, within each phase.
    /// Everything here runs on the tick thread.
    /// </summary>
    public abstract class GameSystem
    {
        public World World { get; private set; } = null!;

        internal void Attach(World world)
        {
            World = world;
            OnAttach();
        }

        /// <summary>Register message handlers and event subscriptions here.</summary>
        protected virtual void OnAttach() { }

        /// <summary>Phase 1: world and global timers.</summary>
        public virtual void OnTimers() { }

        /// <summary>Phase 3: NPC processing.</summary>
        public virtual void OnNpcs() { }

        /// <summary>Phase 4, per player, before their interaction or movement runs.</summary>
        public virtual void BeforePlayer(Player player) { }

        /// <summary>Phase 4, per player, after their interaction or movement ran.</summary>
        public virtual void AfterPlayer(Player player) { }

        /// <summary>Phase 6: zone and ground-item updates.</summary>
        public virtual void OnZones() { }

        /// <summary>Phase 7, per viewer: add anything this system shows to the world update.</summary>
        public virtual void WriteUpdate(Player viewer, WorldUpdateMessage update, ISet<int> visibleEntityIds) { }

        /// <summary>Phase 7, per viewer, after the world update: send this system's own messages.</summary>
        public virtual void SendState(Player viewer) { }

        /// <summary>Phase 8: per-tick cleanup.</summary>
        public virtual void OnCleanup() { }

        /// <summary>A player has just logged in. <paramref name="save"/> is null for a new character.</summary>
        public virtual void OnLogin(Player player, PlayerSave? save) { }

        /// <summary>Write this system's part of the player into the save (logout, autosave, shutdown).</summary>
        public virtual void OnSave(Player player, PlayerSave save) { }

        /// <summary>A player is about to leave the world (logout, dropped connection, shutdown), before their final save: settle anything that must be saved.</summary>
        public virtual void OnLeaving(Player player) { }

        /// <summary>A player is leaving the world (after their final save).</summary>
        public virtual void OnLogout(Player player) { }

        /// <summary>Hot-reloaded content has just been swapped in.</summary>
        public virtual void OnContentChanged() { }
    }
}
