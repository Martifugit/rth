using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Rth.Shared.Net;

namespace Rth.Server.Net
{
    internal sealed class TcpConnection : IConnection
    {
        /// <summary>A client this far behind on reading is treated as gone.</summary>
        private const int MaxQueuedFrames = 1000;
        /// <summary>How long a closing connection gets to flush queued messages before it is cut.</summary>
        private const int CloseFlushMillis = 2000;

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Action<string> _log;
        private readonly ConcurrentQueue<Message> _inbox = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<byte[]> _outbox = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _outboxSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private int _closing;
        private int _aborted;

        public TcpConnection(TcpClient client, Action<string> log)
        {
            _client = client;
            _client.NoDelay = true;
            _stream = client.GetStream();
            _log = log;
            Description = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        }

        /// <summary>False once either side has started closing.</summary>
        public bool IsOpen => Volatile.Read(ref _closing) == 0;
        public string Description { get; }

        public void Start()
        {
            Task.Run(ReceiveLoop);
            Task.Run(SendLoop);
        }

        public bool TryReceive(out Message message) => _inbox.TryDequeue(out message);

        public void Send(Message message)
        {
            if (!IsOpen) return;
            if (_outbox.Count >= MaxQueuedFrames)
            {
                _log(Description + ": send queue full, dropping connection.");
                Abort();
                return;
            }
            _outbox.Enqueue(MessageCodec.EncodeFrame(message));
            _outboxSignal.Release();
        }

        /// <summary>Stops accepting new messages, sends what is already queued, then closes the socket.</summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closing, 1) != 0) return;
            _outboxSignal.Release();
            Task.Delay(CloseFlushMillis).ContinueWith(_ => Abort());
        }

        private void Abort()
        {
            Interlocked.Exchange(ref _closing, 1);
            if (Interlocked.Exchange(ref _aborted, 1) != 0) return;
            _cancel.Cancel();
            _client.Close();
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (Volatile.Read(ref _aborted) == 0)
                {
                    Message? message = await MessageCodec.ReadFrameAsync(_stream, Protocol.MaxClientFrame, _cancel.Token).ConfigureAwait(false);
                    if (message == null) break;
                    _inbox.Enqueue(message);
                }
            }
            catch (Exception e) when (IsExpectedNetworkError(e))
            {
                if (e is InvalidDataException) _log(Description + ": bad data, closing: " + e.Message);
            }
            Abort();
        }

        private async Task SendLoop()
        {
            try
            {
                while (true)
                {
                    await _outboxSignal.WaitAsync(_cancel.Token).ConfigureAwait(false);
                    while (_outbox.TryDequeue(out byte[] frame))
                        await _stream.WriteAsync(frame, 0, frame.Length, _cancel.Token).ConfigureAwait(false);
                    if (!IsOpen) break;
                }
            }
            catch (Exception e) when (IsExpectedNetworkError(e)) { }
            Abort();
        }

        private static bool IsExpectedNetworkError(Exception e) =>
            e is IOException || e is SocketException || e is ObjectDisposedException ||
            e is OperationCanceledException || e is InvalidDataException;
    }
}
