using Jint;
using Newtonsoft.Json;
using Seq.Apps;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Seq.Input.Beats
{
    [SeqApp("Beats Check Input",
        Description = "Endpoint excepting input in the format beats exposes")]
    public partial class BeatsInput : SeqApp, IPublishJson, IDisposable
    {
        private TextWriter textWriter;
        private CancellationTokenSource? tokenSource;
        private Task? task;

        [SeqAppSetting(
            DisplayName = "Listening Port",
            InputType = SettingInputType.Integer,
            IsOptional = true)]
        public int Port { get; set; } = 5044;

        [SeqAppSetting(
            DisplayName = "Transform Script",
            InputType = SettingInputType.LongText,
            IsOptional = true)]
        public string TransformScript { get; set; }

        public string TransformScriptPath { get; set; }

        private bool RunTransform => !string.IsNullOrWhiteSpace(TransformScript) || RunTransformDebugMode;

        private bool RunTransformDebugMode => !string.IsNullOrWhiteSpace(TransformScriptPath) && File.Exists(TransformScriptPath);

        private Jint.Parser.Ast.Program parseTransform;
        public void Start(TextWriter inputWriter)
        {
            if (RunTransformDebugMode)
            {
                TransformScript = File.ReadAllText(TransformScriptPath);
            }
            if (RunTransform)
            {
                parseTransform = new Jint.Parser.JavaScriptParser().Parse(TransformScript, new Jint.Parser.ParserOptions
                {
                    Source = TransformScriptPath,
                });
            }

            this.textWriter = inputWriter;
            this.tokenSource = new CancellationTokenSource();
            this.task = Task.Run(async () =>
            {
                IPAddress ipAddress = IPAddress.Any;
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, Port);
                var listener = new TcpListener(localEndPoint);
                listener.Start(100);

                var token = tokenSource.Token;

                try
                {

                    while (!token.IsCancellationRequested)
                    {
                        var socket = await listener.AcceptSocketAsync();

                        var task = HandleConnection(socket, token);
                    }

                    listener.Stop();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }, tokenSource.Token);
        }

        public async Task HandleConnection(Socket handler, CancellationToken cancellationToken)
        {
            // Create the state object.  
            ConnectionStateObject state = new ConnectionStateObject()
            {
                ProcessMessage = HandleMessages,
                workSocket = handler,
                buffer = ArrayPool<byte>.Shared.Rent(1024)
            };

            try
            {
                var read = int.MaxValue;

                while (handler.Connected && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        state.Expand();
                        var target = state.buffer.AsMemory(state.writePosition);
                        read = await handler.ReceiveAsync(target, 0, cancellationToken);

                        state.writePosition += read;

                        await state.TryDecode(cancellationToken);

                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(state.buffer);
            }

            handler.Close();
        }

        public Task HandleMessages(ConnectionStateObject state, Message message)
        {
            var result = message.AsResult();
            // add transforms handling here

            if (RunTransform)
            {
                var jsEngine = new Engine(options =>
                {
                    // Set a timeout to 4 seconds.
                    options.TimeoutInterval(TimeSpan.FromSeconds(1));

                    options.LimitRecursion(100);

                    options.DebugMode(RunTransformDebugMode);
                });

                jsEngine.SetValue("message", result);

                jsEngine.Execute(parseTransform);
            }

            lock (textWriter)
            {
                result.Write(textWriter);
                textWriter.WriteLine();
            }

            return Task.CompletedTask;
        }

        public void Stop()
        {
            textWriter?.Flush();
            textWriter = null;

            tokenSource?.Cancel();

            task?.Wait(250);
            task = null;

            tokenSource?.Dispose();
            tokenSource = null;
        }

        public void Dispose()
        {
            this.Stop();
        }
    }
}
