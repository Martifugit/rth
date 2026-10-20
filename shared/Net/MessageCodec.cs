using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rth.Shared.Net
{
    /// <summary>
    /// Frames are a little-endian int32 length, then that many bytes: one type byte and the body.
    /// </summary>
    public static class MessageCodec
    {
        public static byte[] EncodeFrame(Message message)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(0); // length placeholder
                w.Write((byte)message.Type);
                message.WriteBody(w);
                w.Flush();
                byte[] frame = ms.ToArray();
                int length = frame.Length - 4;
                frame[0] = (byte)length;
                frame[1] = (byte)(length >> 8);
                frame[2] = (byte)(length >> 16);
                frame[3] = (byte)(length >> 24);
                return frame;
            }
        }

        public static Message Decode(byte[] buffer, int count)
        {
            using (var ms = new MemoryStream(buffer, 0, count))
            using (var r = new BinaryReader(ms, Encoding.UTF8))
            {
                var type = (MessageType)r.ReadByte();
                Message message = type switch
                {
                    MessageType.Login => LoginMessage.Read(r),
                    MessageType.WalkTo => WalkToMessage.Read(r),
                    MessageType.Logout => new LogoutMessage(),
                    MessageType.Attack => AttackMessage.Read(r),
                    MessageType.UseAbility => UseAbilityMessage.Read(r),
                    MessageType.SetDeflect => SetDeflectMessage.Read(r),
                    MessageType.ItemAction => ItemActionMessage.Read(r),
                    MessageType.MoveItem => MoveItemMessage.Read(r),
                    MessageType.Pickup => PickupMessage.Read(r),
                    MessageType.ObjectAction => ObjectActionMessage.Read(r),
                    MessageType.NpcAction => NpcActionMessage.Read(r),
                    MessageType.DialogueChoice => DialogueChoiceMessage.Read(r),
                    MessageType.CloseInterface => new CloseInterfaceMessage(),
                    MessageType.BankDeposit => BankDepositMessage.Read(r),
                    MessageType.BankWithdraw => BankWithdrawMessage.Read(r),
                    MessageType.ShopBuy => ShopBuyMessage.Read(r),
                    MessageType.ShopSell => ShopSellMessage.Read(r),
                    MessageType.LoginOk => LoginOkMessage.Read(r),
                    MessageType.LoginRejected => LoginRejectedMessage.Read(r),
                    MessageType.WorldUpdate => WorldUpdateMessage.Read(r),
                    MessageType.PlayerState => PlayerStateMessage.Read(r),
                    MessageType.ContentInfo => ContentInfoMessage.Read(r),
                    MessageType.GameText => GameTextMessage.Read(r),
                    MessageType.Container => ContainerMessage.Read(r),
                    MessageType.Stats => StatsMessage.Read(r),
                    MessageType.Skills => SkillsMessage.Read(r),
                    MessageType.Dialogue => DialogueMessage.Read(r),
                    MessageType.Interface => InterfaceMessage.Read(r),
                    MessageType.Shop => ShopMessage.Read(r),
                    MessageType.Task => TaskMessage.Read(r),
                    MessageType.Quests => QuestsMessage.Read(r),
                    MessageType.Regions => RegionsMessage.Read(r),
                    MessageType.Chat => ChatMessage.Read(r),
                    MessageType.ChatLine => ChatLineMessage.Read(r),
                    MessageType.JsonState => JsonStateMessage.Read(r),
                    MessageType.SetClientState => SetClientStateMessage.Read(r),
                    _ => throw new InvalidDataException("Unknown message type " + (int)type + "."),
                };
                if (ms.Position != count) throw new InvalidDataException("Trailing bytes after " + type + " message.");
                return message;
            }
        }

        /// <summary>Blocks until a whole frame arrives. Returns null if the stream ended cleanly between frames.</summary>
        public static Message? ReadFrame(Stream stream, int maxFrameLength)
        {
            var header = new byte[4];
            if (!ReadExactly(stream, header, 4, allowEndOfStream: true)) return null;
            int length = ReadLength(header, maxFrameLength);
            var body = new byte[length];
            ReadExactly(stream, body, length, allowEndOfStream: false);
            return Decode(body, length);
        }

        public static async Task<Message?> ReadFrameAsync(Stream stream, int maxFrameLength, CancellationToken cancellation)
        {
            var header = new byte[4];
            if (!await ReadExactlyAsync(stream, header, 4, true, cancellation).ConfigureAwait(false)) return null;
            int length = ReadLength(header, maxFrameLength);
            var body = new byte[length];
            await ReadExactlyAsync(stream, body, length, false, cancellation).ConfigureAwait(false);
            return Decode(body, length);
        }

        private static int ReadLength(byte[] header, int maxFrameLength)
        {
            int length = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
            if (length < 1 || length > maxFrameLength) throw new InvalidDataException("Bad frame length " + length + ".");
            return length;
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count, bool allowEndOfStream)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n == 0)
                {
                    if (read == 0 && allowEndOfStream) return false;
                    throw new EndOfStreamException("Connection closed mid-frame.");
                }
                read += n;
            }
            return true;
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, int count, bool allowEndOfStream, CancellationToken cancellation)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, read, count - read, cancellation).ConfigureAwait(false);
                if (n == 0)
                {
                    if (read == 0 && allowEndOfStream) return false;
                    throw new EndOfStreamException("Connection closed mid-frame.");
                }
                read += n;
            }
            return true;
        }
    }
}
