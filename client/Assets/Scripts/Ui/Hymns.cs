using System.Collections.Generic;
using Rth.Shared.Net;
using Rth.Shared.Religion;
using Rth.Shared.Skills;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Hymns as the interface knows them (from content): the church's prayers, sung from the religion
    /// tab or from an action bar slot ("h:id"), and draining devotion while sung. The server
    /// decides what is sung; these just look things up, toggle and describe.
    /// </summary>
    public static class Hymns
    {
        /// <summary>Salt-white with a little gold: the tint every hymn icon gets.</summary>
        public static readonly Color Tint = new Color(0.96f, 0.9f, 0.72f);

        public static HymnDef Get(GameSession session, string id)
        {
            List<HymnDef> hymns = session.Content?.Hymns;
            if (hymns == null || id == null) return null;
            foreach (HymnDef h in hymns)
                if (h.Id == id) return h;
            return null;
        }

        public static bool IsSung(PlayerStateMessage state, string id) => state != null && state.Hymns.Contains(id);

        public static void Toggle(GameSession session, string id) => session.SetHymn(id, !IsSung(session.PlayerState, id));

        /// <summary>The level the hymn needs (its highest requirement), for the locked badge.</summary>
        public static int LevelNeeded(HymnDef hymn)
        {
            int level = 1;
            foreach (SkillRequirement r in hymn.Requirements) level = Mathf.Max(level, r.Level);
            return level;
        }

        /// <summary>Why it can't be sung now (a level), or null.</summary>
        public static string Problem(GameSession session, HymnDef hymn) =>
            SkillRequirement.Check(hymn.Requirements, session.Level, session.SkillName, "sing " + hymn.Name);

        /// <summary>A tooltip card (rich text).</summary>
        public static string Describe(GameSession session, HymnDef hymn)
        {
            var lines = new List<string>
            {
                UiKit.Title(hymn.Name),
                UiKit.Colour(hymn.Overhead ? "Overhead hymn: only one at a time" : "Hymn", UiKit.Muted),
                hymn.Description,
                "",
                "Drains " + UiKit.Colour(hymn.Drain.ToString("0.##") + " devotion", UiKit.Value) + " a tick",
            };
            string problem = Problem(session, hymn);
            if (problem != null) lines.Add("\n" + UiKit.Colour(problem, UiKit.Bad));
            else lines.Add(UiKit.Colour(IsSung(session.PlayerState, hymn.Id) ? "Click to stop singing it." : "Click to sing it.", UiKit.Muted));
            return string.Join("\n", lines);
        }
    }
}
