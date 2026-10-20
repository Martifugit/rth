using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Rth.Server.Net
{
    /// <summary>Accepts TCP clients and hands each one to the world as an <see cref="IConnection"/>.</summary>
    public sealed class NetworkServer
    {
        private readonly TcpListener _listener;
        private readonly Action<IConnection> _onAccepted;
        private readonly Action<string> _log;
        private volatile bool _running;

        public NetworkServer(int port, Action<IConnection> onAccepted, Action<string> log)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _onAccepted = onAccepted;
            _log = log;
        }

        /// <summary>The port actually bound (useful when 0 was requested).</summary>
        public int Port { get; private set; }

        public void Start()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            Task.Run(AcceptLoop);
        }

        public void Stop()
        {
            _running = false;
            _listener.Stop();
        }

        private async Task AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException || e is InvalidOperationException)
                {
                    if (_running) _log("Accept failed: " + e.Message);
                    continue;
                }

                var connection = new TcpConnection(client, _log);
                _log("Connection from " + connection.Description + ".");
                connection.Start();
                _onAccepted(connection);
            }
        }
    }
}
