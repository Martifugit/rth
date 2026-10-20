using Rth.Shared.Net;

namespace Rth.Server.Net
{
    /// <summary>
    /// One client's connection as the world sees it. Thread-safe: messages arrive on network
    /// threads and are only read by the tick thread.
    /// </summary>
    public interface IConnection
    {
        bool IsOpen { get; }
        string Description { get; }
        bool TryReceive(out Message message);
        void Send(Message message);
        void Close();
    }
}
