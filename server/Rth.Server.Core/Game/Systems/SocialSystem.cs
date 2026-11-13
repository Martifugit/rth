using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Persistence;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// Friends, ignores, private messages and groups: the social side of a world for a group of
    /// friends. Everything is typed in chat, so any client can use it:
    /// <list type="bullet">
    /// <item><c>/w name message</c> (or <c>/tell</c>, <c>/msg</c>) a private message; <c>/r message</c> replies to the last one.</item>
    /// <item><c>/friend name</c>, <c>/unfriend name</c>, <c>/friends</c>; friends are told when you come and go.</item>
    /// <item><c>/ignore name</c>, <c>/unignore name</c>: nothing they say reaches you.</item>
    /// <item><c>/invite name</c>, <c>/accept</c>, <c>/decline</c>, <c>/leave</c>, <c>/kick name</c>, <c>/group</c>;
    /// <c>/g message</c> (or <c>//message</c>) talks to the group.</item>
    /// </list>
    /// Friends and ignores are saved ("social" section); a group lasts while its members are in
    /// the world. The "social" state (<see cref="SocialState"/>) carries all of it to the client.
    /// </summary>
    public sealed class SocialSystem : GameSystem
    {
        public const string SaveKey = "social";
        public const int MaxFriends = 200;
        public const int MaxGroupSize = 8;
        /// <summary>A group invitation lapses after this many ticks (one minute).</summary>
        public const int InviteTicks = 100;
        public const string Prefix = "/";

        private sealed class Saved
        {
            public List<string> Friends = new List<string>();
            public List<string> Ignored = new List<string>();
        }

        /// <summary>One player's social state.</summary>
        public sealed class PlayerSocial
        {
            /// <summary>Display names, in the order added.</summary>
            public List<string> Friends = new List<string>();
            public List<string> Ignored = new List<string>();
            public string? ReplyTo;
            public Group? Group;
            public Group? Invite;
            public string InvitedBy = "";
            public uint InviteUntil;
            public bool Dirty = true;
            /// <summary>The last state sent, to send again only when it changes.</summary>
            public string LastSent = "";
        }

        public sealed class Group
        {
            public readonly List<Player> Members = new List<Player>();
            public Player Leader => Members[0];
        }

        private readonly Dictionary<string, Action<Player, string>> _commands = new Dictionary<string, Action<Player, string>>();

        protected override void OnAttach()
        {
            Register(Tell, "w", "tell", "msg", "pm");
            Register(Reply, "r", "reply");
            Register((p, a) => AddFriend(p, a), "friend", "addfriend");
            Register((p, a) => RemoveFriend(p, a), "unfriend", "removefriend", "delfriend");
            Register((p, _) => ListFriends(p), "friends");
            Register((p, a) => Ignore(p, a), "ignore");
            Register((p, a) => Unignore(p, a), "unignore");
            Register((p, a) => Invite(p, a), "invite");
            Register((p, _) => Accept(p), "accept", "join");
            Register((p, _) => Decline(p), "decline");
            Register((p, _) => Leave(p), "leave");
            Register((p, a) => Kick(p, a), "kick");
            Register((p, _) => ListGroup(p), "group", "party");
            Register(GroupSay, "g", "p");
            Register((p, _) => Help(p), "help", "commands");
        }

        private void Register(Action<Player, string> run, params string[] words)
        {
            foreach (string word in words) _commands[word] = run;
        }

        public PlayerSocial Of(Player player) => player.Component<PlayerSocial>();

        // ------------------------------------------------------------------ commands

        /// <summary>
        /// Runs a chat line that starts with "/" as a social command. Returns false if it isn't
        /// one (an unknown word), so the chat system can say it out loud instead.
        /// </summary>
        public bool TryCommand(Player player, string text)
        {
            if (!text.StartsWith(Prefix, StringComparison.Ordinal) || text.StartsWith(DevSystem.Prefix, StringComparison.Ordinal)) return false;
            if (text.StartsWith("//", StringComparison.Ordinal))
            {
                GroupSay(player, text.Substring(2).Trim());
                return true;
            }
            string body = text.Substring(Prefix.Length);
            int space = body.IndexOf(' ');
            string word = (space < 0 ? body : body.Substring(0, space)).ToLowerInvariant();
            string rest = space < 0 ? "" : body.Substring(space + 1).Trim();
            if (!_commands.TryGetValue(word, out Action<Player, string>? run)) return false;
            run(player, rest);
            return true;
        }

        private void Help(Player player) => Notice(player,
            "Social: /w name message, /r message, /friend name, /unfriend name, /friends, /ignore name, /unignore name, " +
            "/invite name, /accept, /decline, /leave, /kick name, /group, /g message (or //message).");

        /// <summary>Splits "name message" where the name may hold spaces: the longest leading words that name a player online.</summary>
        private (Player? To, string Message) SplitTarget(string args)
        {
            string[] words = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int n = Math.Min(words.Length - 1, 4); n >= 1; n--)
            {
                Player? to = World.FindPlayer(string.Join(" ", words.Take(n)));
                if (to != null) return (to, string.Join(" ", words.Skip(n)));
            }
            return (null, "");
        }

        private void Tell(Player player, string args)
        {
            (Player? to, string message) = SplitTarget(args);
            if (to == null)
            {
                Warn(player, args.Length == 0 ? "Who to? /w name message" : "Nobody of that name is in the world.");
                return;
            }
            SendPrivate(player, to, message);
        }

        private void Reply(Player player, string message)
        {
            string? name = Of(player).ReplyTo;
            Player? to = name == null ? null : World.FindPlayer(name);
            if (to == null)
            {
                Warn(player, name == null ? "Nobody has messaged you yet." : name + " is not in the world.");
                return;
            }
            SendPrivate(player, to, message);
        }

        /// <summary>A private message: to the recipient (unless they ignore the sender), echoed back to the sender.</summary>
        public void SendPrivate(Player from, Player to, string text)
        {
            text = ChatSystem.Clean(text);
            if (text.Length == 0) return;
            if (to == from)
            {
                Warn(from, "Talking to yourself?");
                return;
            }
            from.Connection.Send(new ChatLineMessage { Channel = ChatChannel.PrivateOut, From = "To " + to.Name, Text = text });
            if (Ignores(to, from)) return; // they never know
            Of(to).ReplyTo = from.Name;
            to.Connection.Send(new ChatLineMessage { Channel = ChatChannel.PrivateIn, From = "From " + from.Name, Text = text });
        }

        // ------------------------------------------------------------------ friends and ignores

        public bool AddFriend(Player player, string name)
        {
            PlayerSocial social = Of(player);
            string? spelled = string.IsNullOrWhiteSpace(name) ? null : World.SavedName(name.Trim());
            if (spelled == null)
            {
                Warn(player, string.IsNullOrWhiteSpace(name) ? "Whose? /friend name" : "There's nobody called " + name.Trim() + ".");
                return false;
            }
            if (Same(spelled, player.Name)) return Warn(player, "You're already your own friend.");
            if (social.Friends.Any(f => Same(f, spelled))) return Warn(player, spelled + " is already a friend.");
            if (social.Friends.Count >= MaxFriends) return Warn(player, "Your friends list is full.");
            social.Ignored.RemoveAll(i => Same(i, spelled));
            social.Friends.Add(spelled);
            social.Dirty = true;
            Player? online = World.FindPlayer(spelled);
            Notice(player, spelled + " is now a friend" + (online != null ? " (in the world now)." : "."));
            return true;
        }

        public bool RemoveFriend(Player player, string name)
        {
            PlayerSocial social = Of(player);
            if (social.Friends.RemoveAll(f => Same(f, name.Trim())) == 0) return Warn(player, name.Trim() + " isn't on your friends list.");
            social.Dirty = true;
            Notice(player, name.Trim() + " is no longer a friend.");
            return true;
        }

        private void ListFriends(Player player)
        {
            List<string> friends = Of(player).Friends;
            if (friends.Count == 0)
            {
                Notice(player, "No friends yet: /friend name.");
                return;
            }
            Notice(player, "Friends: " + string.Join(", ", friends.Select(f => f + (World.FindPlayer(f) != null ? " (on)" : ""))) + ".");
        }

        public bool Ignore(Player player, string name)
        {
            PlayerSocial social = Of(player);
            string? spelled = string.IsNullOrWhiteSpace(name) ? null : World.SavedName(name.Trim());
            if (spelled == null) return Warn(player, "There's nobody called " + name.Trim() + ".");
            if (Same(spelled, player.Name)) return Warn(player, "You can't ignore yourself.");
            if (social.Ignored.Any(i => Same(i, spelled))) return Warn(player, spelled + " is already ignored.");
            social.Friends.RemoveAll(f => Same(f, spelled));
            social.Ignored.Add(spelled);
            social.Dirty = true;
            Notice(player, "Ignoring " + spelled + ".");
            return true;
        }

        public bool Unignore(Player player, string name)
        {
            PlayerSocial social = Of(player);
            if (social.Ignored.RemoveAll(i => Same(i, name.Trim())) == 0) return Warn(player, name.Trim() + " isn't ignored.");
            social.Dirty = true;
            Notice(player, "No longer ignoring " + name.Trim() + ".");
            return true;
        }

        /// <summary>Whether <paramref name="listener"/> ignores <paramref name="speaker"/> (the chat system asks before public chat too).</summary>
        public bool Ignores(Player listener, Player speaker) => Of(listener).Ignored.Any(i => Same(i, speaker.Name));

        private static bool Same(string a, string b) => PlayerNames.ToKey(a) == PlayerNames.ToKey(b);

        // ------------------------------------------------------------------ groups

        public Group? GroupOf(Player player) => Of(player).Group;

        public bool Invite(Player player, string name)
        {
            Player? to = string.IsNullOrWhiteSpace(name) ? null : World.FindPlayer(name.Trim());
            if (to == null) return Warn(player, "Nobody of that name is in the world.");
            if (to == player) return Warn(player, "You're always in your own company.");
            Group? group = Of(player).Group;
            if (group != null && group.Leader != player) return Warn(player, "Only " + group.Leader.Name + " can invite to the group.");
            if (group != null && group.Members.Count >= MaxGroupSize) return Warn(player, "The group is full (" + MaxGroupSize + ").");
            if (Of(to).Group != null) return Warn(player, to.Name + " is already in a group.");
            if (Ignores(to, player)) return Warn(player, to.Name + " isn't taking invitations.");
            if (group == null)
            {
                group = new Group();
                group.Members.Add(player);
                Of(player).Group = group;
            }
            PlayerSocial invited = Of(to);
            invited.Invite = group;
            invited.InvitedBy = player.Name;
            invited.InviteUntil = World.CurrentTick + InviteTicks;
            invited.Dirty = true;
            MarkGroup(group);
            Notice(player, "You invite " + to.Name + " to your group.");
            Notice(to, player.Name + " invites you to their group: /accept or /decline.");
            return true;
        }

        public bool Accept(Player player)
        {
            PlayerSocial social = Of(player);
            Group? group = social.Invite;
            if (group == null || World.CurrentTick >= social.InviteUntil || group.Members.Count == 0)
            {
                ClearInvite(social);
                return Warn(player, "No invitation is waiting.");
            }
            if (social.Group != null) Leave(player);
            ClearInvite(social);
            if (group.Members.Count >= MaxGroupSize) return Warn(player, "The group is full.");
            group.Members.Add(player);
            social.Group = group;
            foreach (Player member in group.Members)
                if (member != player) Notice(member, player.Name + " joins the group.");
            Notice(player, "You join " + group.Leader.Name + "'s group.");
            MarkGroup(group);
            return true;
        }

        private void Decline(Player player)
        {
            PlayerSocial social = Of(player);
            if (social.Invite == null)
            {
                Warn(player, "No invitation is waiting.");
                return;
            }
            Player? from = World.FindPlayer(social.InvitedBy);
            if (from != null) Notice(from, player.Name + " declines.");
            ClearInvite(social);
            Notice(player, "You decline.");
        }

        private static void ClearInvite(PlayerSocial social)
        {
            social.Invite = null;
            social.InvitedBy = "";
            social.InviteUntil = 0;
            social.Dirty = true;
        }

        public bool Leave(Player player, bool quietly = false)
        {
            PlayerSocial social = Of(player);
            Group? group = social.Group;
            if (group == null) return quietly || Warn(player, "You're not in a group.");
            bool wasLeader = group.Leader == player;
            group.Members.Remove(player);
            social.Group = null;
            social.Dirty = true;
            if (!quietly) Notice(player, "You leave the group.");
            if (group.Members.Count == 1)
            {
                // A group of one is no group.
                Player last = group.Members[0];
                Of(last).Group = null;
                Of(last).Dirty = true;
                group.Members.Clear();
                Notice(last, "The group breaks up.");
                return true;
            }
            foreach (Player member in group.Members) Notice(member, player.Name + " leaves the group.");
            if (wasLeader) Notice(group.Leader, "You lead the group now.");
            MarkGroup(group);
            return true;
        }

        private bool Kick(Player player, string name)
        {
            Group? group = Of(player).Group;
            if (group == null) return Warn(player, "You're not in a group.");
            if (group.Leader != player) return Warn(player, "Only " + group.Leader.Name + " can do that.");
            Player? member = group.Members.FirstOrDefault(m => Same(m.Name, name.Trim()));
            if (member == null || member == player) return Warn(player, "Nobody of that name is in your group.");
            Notice(member, "You're removed from the group.");
            Leave(member, quietly: true);
            return true;
        }

        private void ListGroup(Player player)
        {
            Group? group = Of(player).Group;
            if (group == null)
            {
                Notice(player, "You're not in a group: /invite name.");
                return;
            }
            Notice(player, "Group: " + string.Join(", ", group.Members.Select(m => m.Name + (m == group.Leader ? " (leads)" : ""))) + ".");
        }

        private void GroupSay(Player player, string text)
        {
            text = ChatSystem.Clean(text);
            if (text.Length == 0) return;
            Group? group = Of(player).Group;
            if (group == null)
            {
                Warn(player, "You're not in a group.");
                return;
            }
            var line = new ChatLineMessage { Channel = ChatChannel.Group, EntityId = player.Id, From = "[Group] " + player.Name, Text = text };
            foreach (Player member in group.Members)
                if (!Ignores(member, player)) member.Connection.Send(line);
        }

        private void MarkGroup(Group group)
        {
            foreach (Player member in group.Members) Of(member).Dirty = true;
        }

        // ------------------------------------------------------------------ lifecycle

        public override void OnLogin(Player player, PlayerSave? save)
        {
            PlayerSocial social = Of(player);
            Saved? saved = save?.Section<Saved>(SaveKey);
            if (saved != null)
            {
                social.Friends = saved.Friends.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                social.Ignored = saved.Ignored.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            social.Dirty = true;
            foreach (Player other in World.Players)
            {
                if (other == player) continue;
                if (Of(other).Friends.Any(f => Same(f, player.Name)))
                {
                    Of(other).Dirty = true;
                    if (!Ignores(other, player)) Notice(other, player.Name + " has come into the world.");
                }
            }
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            PlayerSocial social = Of(player);
            save.SetSection(SaveKey, new Saved { Friends = social.Friends.ToList(), Ignored = social.Ignored.ToList() });
        }

        public override void OnLogout(Player player)
        {
            Leave(player, quietly: true);
            foreach (Player other in World.Players)
            {
                PlayerSocial social = Of(other);
                if (social.Invite != null && social.Invite.Members.Count == 0) ClearInvite(social);
                if (social.Friends.Any(f => Same(f, player.Name)))
                {
                    social.Dirty = true;
                    if (!Ignores(other, player)) Notice(other, player.Name + " has left the world.");
                }
            }
        }

        public override void AfterPlayer(Player player)
        {
            PlayerSocial social = Of(player);
            if (social.Invite != null && World.CurrentTick >= social.InviteUntil) ClearInvite(social);
        }

        /// <summary>The social state, when it changed: friends' and group members' places and blood move, so it is rebuilt every few ticks.</summary>
        public override void SendState(Player player)
        {
            PlayerSocial social = Of(player);
            if (!social.Dirty && (social.Group == null || World.CurrentTick % 5 != 0) && World.CurrentTick % 25 != 0) return;
            social.Dirty = false;
            RegionSystem? regions = World.Find<RegionSystem>();
            var state = new SocialState { Ignored = social.Ignored.ToList(), InvitedBy = social.Invite != null ? social.InvitedBy : "" };
            foreach (string name in social.Friends)
            {
                Player? online = World.FindPlayer(name);
                bool shown = online != null && !Ignores(online, player);
                state.Friends.Add(new SocialFriend { Name = name, Online = shown, Region = shown ? regions?.RegionOf(online!)?.Title ?? "" : "" });
            }
            if (social.Group != null)
                foreach (Player member in social.Group.Members)
                    state.Group.Add(new SocialMember
                    {
                        Name = member.Name,
                        Leader = member == social.Group.Leader,
                        EntityId = member.Id,
                        BloodPercent = member.HealthPercent,
                        Region = regions?.RegionOf(member)?.Title ?? "",
                    });
            JsonStateMessage message = JsonStateMessage.Of(SocialState.Key, state);
            if (message.Json == social.LastSent) return;
            social.LastSent = message.Json;
            player.Connection.Send(message);
        }

        // ------------------------------------------------------------------ telling

        private void Notice(Player player, string text) =>
            player.Connection.Send(new ChatLineMessage { Channel = ChatChannel.Notice, Text = text });

        private bool Warn(Player player, string text)
        {
            World.Tell(player, TextKind.Warning, text);
            return false;
        }
    }
}
