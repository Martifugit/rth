using System.Collections.Generic;

namespace Rth.Shared.Net
{
    /// <summary>
    /// The "social" server state: the player's friends (and who of them is on), who they ignore,
    /// their group and any group invitation waiting for them. Sent when any of it changes.
    /// </summary>
    public sealed class SocialState
    {
        public const string Key = "social";

        public List<SocialFriend> Friends = new List<SocialFriend>();
        public List<string> Ignored = new List<string>();
        /// <summary>Their group's members (leader first), or empty when they're in none.</summary>
        public List<SocialMember> Group = new List<SocialMember>();
        /// <summary>Who invited them to a group they haven't answered, or "".</summary>
        public string InvitedBy = "";
    }

    public sealed class SocialFriend
    {
        public string Name = "";
        public bool Online;
        /// <summary>The region they are in, when online ("" otherwise).</summary>
        public string Region = "";
    }

    public sealed class SocialMember
    {
        public string Name = "";
        public bool Leader;
        public int EntityId;
        /// <summary>Their blood as a percentage, so a group can see who is in trouble.</summary>
        public int BloodPercent;
        public string Region = "";
    }
}
