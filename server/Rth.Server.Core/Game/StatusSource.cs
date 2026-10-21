using System.Collections.Generic;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// A system that puts players under statuses (the buff and debuff bars). The world asks every
    /// source each tick, so a status lasts exactly as long as its source keeps listing it.
    /// </summary>
    public interface IStatusSource
    {
        void AddStatuses(Player player, List<StatusState> statuses);
    }
}
