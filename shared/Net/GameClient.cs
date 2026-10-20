using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Rth.Shared.Net
{
    /// <summary>
    /// The client end of a connection. Messages are received on a background thread and queued;
    /// the owner (the Unity main thread, or a test) drains them with <see cref="TryReceive"/>.
    /// </summary>
    public sealed class GameClient : IDisposable
    {
        private readonly ConcurrentQueue<Message> _incoming = new ConcurrentQueue<Message>();
        private readonly object _sendLock = new object();
        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private Thread? _receiveThread;
        private volatile bool _connected;

        public bool IsConnected => _connected;

        /// <summary>Why the connection ended, if it ended on its own.</summary>
        public string? DisconnectReason { get; private set; }

        public void Connect(string host, int port, int timeoutMillis = 5000)
        {
            if (_tcp != null) throw new InvalidOperationException("Already connected.");
            var tcp = new TcpClient { NoDelay = true };
            try
            {
                if (!tcp.ConnectAsync(host, port).Wait(timeoutMillis))
                    throw new TimeoutException("Timed out connecting to " + host + ":" + port + ".");
            }
            catch (AggregateException e) when (e.InnerException != null)
            {
                tcp.Dispose();
                throw e.InnerException;
            }
            catch
            {
                tcp.Dispose();
                throw;
            }

            _tcp = tcp;
            _stream = tcp.GetStream();
            DisconnectReason = null;
            _connected = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Rth client receive" };
            _receiveThread.Start();
        }

        public bool TryReceive(out Message message) => _incoming.TryDequeue(out message);

        public void Send(Message message)
        {
            NetworkStream? stream = _stream;
            if (!_connected || stream == null) return;
            byte[] frame = MessageCodec.EncodeFrame(message);
            try
            {
                lock (_sendLock) stream.Write(frame, 0, frame.Length);
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException)
            {
                Close("Send failed: " + e.Message);
            }
        }

        private void ReceiveLoop()
        {
            string reason = "Server closed the connection.";
            try
            {
                while (_connected && _stream != null)
                {
                    Message? message = MessageCodec.ReadFrame(_stream, Protocol.MaxServerFrame);
                    if (message == null) break;
                    _incoming.Enqueue(message);
                }
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException || e is InvalidDataException)
            {
                reason = e.Message;
            }
            Close(reason);
        }

        private void Close(string reason)
        {
            if (!_connected) return;
            _connected = false;
            DisconnectReason = reason;
            _tcp?.Close();
        }

        /// <summary>Closes the connection. Send a <see cref="LogoutMessage"/> first for a clean logout.</summary>
        public void Disconnect() => Close("Disconnected by client.");

        public void Dispose() => Disconnect();
    }
}
