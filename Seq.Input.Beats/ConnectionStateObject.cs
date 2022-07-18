using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seq.Input.Beats
{
    public partial class BeatsInput
    {
        public class ConnectionStateObject : IDisposable
        {
            public int writePosition = 0;
            public int readPosition = 0;

            // Receive buffer.  
            public byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);

            // Client socket.
            public Socket workSocket = null!;

            private uint batchSize = 0;

            public Func<ConnectionStateObject, Message, Task> ProcessMessage = (state, message) => Task.CompletedTask;

            public async Task TryAckBatch(CancellationToken cancellationToken)
            {
                try
                {
                    if (this.batchSize == 0)
                    {
                        await AckMessage(MaxSequence, cancellationToken);

                        MaxSequence = 0;
                    }
                }
                catch (Exception ex)
                {
                    // write clef error out when this happend???
                    // error??
                }
            }

            public async Task AckMessage(uint sequance, CancellationToken cancellationToken)
            {
                static Memory<byte> SetPayload(byte[] buffer, uint sequance)
                {
                    buffer[0] = (byte)'2';
                    buffer[1] = (byte)'A';
                    var span = buffer.AsSpan(2, 4);
                    if (!BitConverter.TryWriteBytes(span, sequance))
                    {
                        throw new Exception();
                    }
                    span.Reverse();

                    return buffer.AsMemory(0, 6);
                }

                try
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(6);

                    try
                    {
                        if (workSocket != null)
                        {
                            await workSocket.SendAsync(SetPayload(buffer, sequance), SocketFlags.None, cancellationToken);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                catch (Exception ex)
                {
                    // write clef error out when this happend???
                    // error??
                }
            }

            public async Task MessageRecieved(Message message, CancellationToken cancellationToken)
            {
                if (this.MaxSequence < message.Sequence)
                {
                    this.MaxSequence = message.Sequence;
                }

                this.batchSize--;

                if (batchSize == 0)
                {
                    _sequence = 0;
                }

                // handle batch size of zero!!
                if (ProcessMessage != null)
                {
                    await ProcessMessage.Invoke(this, message);
                }

                await AckMessage(message.Sequence, cancellationToken);
            }

            private States _state = States.READ_HEADER;
            private int _requiredBytes = 1;
            private uint _sequence = 0;
            public uint MaxSequence = 0;

            public States state
            {
                get => _state;

                set
                {
                    _state = value;
                    _requiredBytes = state switch
                    {
                        States.READ_HEADER => 1,
                        States.READ_FRAME_TYPE => 1,
                        States.READ_WINDOW_SIZE => 4,
                        States.READ_JSON_HEADER => 8,
                        States.READ_COMPRESSED_FRAME_HEADER => 4,
                        States.READ_COMPRESSED_FRAME => -1,
                        States.READ_JSON => -1,
                        States.READ_DATA_FIELDS => -1,
                    };
                }
            }


            private byte ReadByte()
            {
                var res = this.buffer[this.readPosition];
                this.readPosition++;
                return res;
            }

            private uint ReadUInt()
            {
                var span = this.buffer.AsSpan(this.readPosition, 4);
                span.Reverse();
                var res = BitConverter.ToUInt32(span);
                this.readPosition += 4;
                return res;
            }

            private Span<byte> ReadBytes(int length)
            {
                var res = this.buffer.AsSpan(this.readPosition, length);
                this.readPosition += length;
                return res;
            }

            private string ReadString(int length)
            {
                return Encoding.UTF8.GetString(ReadBytes(length));
            }

            public async Task<bool> TryDecode(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested && await TryDecodeInner(cancellationToken)) ;
                }
                catch (Exception ex)
                {
                    throw;
                }

                var availible = (writePosition - readPosition);

                if (availible == 0)
                {
                    this.readPosition = 0;
                    this.writePosition = 0;
                }

                if (readPosition > writePosition / 2)
                {
                    var pooled = ArrayPool<byte>.Shared.Rent(this.buffer.Length);

                    // copy the boffer to the start and so prevent reallocation
                    Array.Copy(this.buffer, readPosition, pooled, 0, availible);
                    var oldBuffer = this.buffer;
                    this.buffer = pooled;
                    ArrayPool<byte>.Shared.Return(oldBuffer);

                    this.readPosition = 0;
                    this.writePosition = availible;
                }
                return false;
            }

            public async Task<bool> TryDecodeInner(CancellationToken cancellationToken)
            {
                int availibleBytes = writePosition - readPosition;
                if (availibleBytes < _requiredBytes)
                {
                    return false;
                }

                switch (state)
                {
                    case States.READ_HEADER:
                        {
                            int version = Protocol.Version(ReadByte());
                            this.state = States.READ_FRAME_TYPE;
                            break;
                        }
                    case States.READ_FRAME_TYPE:
                        {
                            byte frameType = ReadByte();

                            switch (frameType)
                            {
                                case Protocol.CODE_WINDOW_SIZE:
                                    {
                                        this.state = States.READ_WINDOW_SIZE;
                                        break;
                                    }
                                case Protocol.CODE_JSON_FRAME:
                                    {
                                        // Reading Sequence + size of the payload
                                        this.state = States.READ_JSON_HEADER;
                                        break;
                                    }
                                case Protocol.CODE_COMPRESSED_FRAME:
                                    {
                                        this.state = States.READ_COMPRESSED_FRAME_HEADER;
                                        break;
                                    }
                                case Protocol.CODE_FRAME:
                                    {
                                        this.state = States.READ_DATA_FIELDS;
                                        break;
                                    }
                                default:
                                    {
                                        throw new InvalidFrameProtocolException("Invalid Frame Type, received: " + frameType);
                                    }
                            }
                            break;
                        }
                    case States.READ_WINDOW_SIZE:
                        {
                            //logger.trace("Running: READ_WINDOW_SIZE");
                            batchSize = ReadUInt();

                            //        // This is unlikely to happen but I have no way to known when a frame is
                            //        // actually completely done other than checking the windows and the sequence number,
                            //        // If the FSM read a new window and I have still
                            //        // events buffered I should send the current batch down to the next handler.
                            //        if (!batch.isEmpty())
                            //        {
                            //            logger.warn("New window size received but the current batch was not complete, sending the current batch");
                            //out.add(batch);
                            //            batchComplete();
                            //        }
                            this.state = States.READ_HEADER;
                            break;
                        }
                    case States.READ_DATA_FIELDS:
                        {
                            // Lumberjack version 1 protocol, which use the Key:Value format.
                            //logger.trace("Running: READ_DATA_FIELDS");

                            var sequence = ReadUInt();
                            //sequence = (int) in.readUnsignedInt();
                            int fieldsCount = (int)ReadUInt();
                            int count = 0;

                            if (fieldsCount <= 0)
                            {
                                throw new InvalidFrameProtocolException("Invalid number of fields, received: " + fieldsCount);
                            }

                            var message = new Message(sequence, fieldsCount);

                            while (count < fieldsCount)
                            {
                                var fieldLength = (int)ReadUInt();
                                string field = ReadString(fieldLength);
                                var dataLength = (int)ReadUInt();
                                string data = ReadString(dataLength);

                                message.Fields.Add(field, data);

                                count++;
                            }

                            this.state = States.READ_HEADER;
                            await this.MessageRecieved(message, cancellationToken);
                            break;
                        }
                    case States.READ_JSON_HEADER:
                        {
                            // logger.trace("Running: READ_JSON_HEADER");

                            _sequence = ReadUInt();
                            var jsonPayloadSize = (int)ReadUInt();

                            if (jsonPayloadSize <= 0)
                            {
                                throw new InvalidFrameProtocolException("Invalid json length, received: " + jsonPayloadSize);
                            }

                            this.state = States.READ_JSON;
                            _requiredBytes = jsonPayloadSize;
                            break;
                        }
                    case States.READ_COMPRESSED_FRAME_HEADER:
                        {
                            this.state = States.READ_COMPRESSED_FRAME;
                            _requiredBytes = (int)ReadUInt();

                            break;
                        }

                    case States.READ_COMPRESSED_FRAME:
                        {
                            using var ms = new MemoryStream(this.buffer, readPosition, _requiredBytes);
                            using var stream = new ZlibInflateStream(ms);
                            readPosition += _requiredBytes;

                            var state = new ConnectionStateObject()
                            {
                                ProcessMessage = async (s, m) =>
                                {
                                    await this.MessageRecieved(m, cancellationToken);
                                }
                            };

                            while (true)
                            {
                                state.Expand();

                                try
                                {
                                    var read = stream.Read(state.buffer, state.writePosition, state.buffer.Length - state.writePosition);
                                    state.writePosition += read;
                                    await state.TryDecode(cancellationToken);
                                    if (read == 0)
                                    {
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw;
                                }

                            }
                            this.state = States.READ_HEADER;
                            break;
                        }
                    case States.READ_JSON:
                        {
                            var json = ReadString(_requiredBytes);

                            var fields = JsonHelper.DeserializeAndFlatten(json);

                            var msg = new Message(_sequence, fields);
                            await MessageRecieved(msg, cancellationToken);

                            this.state = States.READ_HEADER;
                            break;
                        }
                }

                return true;
            }

            public void Dispose()
            {
                if (this.buffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(this.buffer);
                    this.buffer = Array.Empty<byte>();
                }
            }

            internal void Expand()
            {
                if (writePosition == buffer.Length)
                {
                    // can trim off the start of the buffer first
                    var bufferlength = buffer.Length * 2;
                    if (bufferlength == 0)
                    {
                        bufferlength = 1024;
                    }

                    var newbuffer = ArrayPool<byte>.Shared.Rent(bufferlength);
                    Array.Copy(buffer, readPosition, newbuffer, 0, writePosition - readPosition);

                    if (buffer.Length > 0)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }

                    buffer = newbuffer;

                    writePosition -= readPosition;
                    readPosition = 0;
                }
            }
        }
    }
}
