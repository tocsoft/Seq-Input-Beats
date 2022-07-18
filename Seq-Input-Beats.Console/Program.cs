using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Seq.Input.Beats
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args)
                .Build()
                .Run();

            //// read values from config/args

            //// run the input reading clef
            //var input = new BeatsInput();

            //input.TransformScriptPath = "transform.js";

            //input.Start(new EventedTextWriter());

            //Console.ReadLine();

            //input.Stop();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
           Host.CreateDefaultBuilder(args)
            .ConfigureServices((builder, services) =>
            {
                services.AddHttpClient<BeatsProxy>((HttpClient c) =>
                {
                    c.BaseAddress = new Uri(builder.Configuration.GetValue("Seq:Url", "http://localhost:5341"));
                    var apiKey = builder.Configuration.GetValue<string>("Seq:ApiKey");
                    if (apiKey != null)
                    {
                        c.DefaultRequestHeaders.Add("X-Seq-ApiKey", apiKey);
                    }
                });
                services.AddHostedService<BeatsProxy>();
            });
    }

    public class BeatsProxy : BackgroundService
    {
        private readonly IHttpClientFactory httpClientFactory;

        public BeatsProxy(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }
        private async Task Send(string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/vnd.serilog.clef");

            using var client = httpClientFactory.CreateClient(typeof(BeatsProxy).Name);
            var result = await client.PostAsync("api/events/raw", content);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var input = new BeatsInput();

            input.TransformScriptPath = "transform.js";

            input.Start(new EventedTextWriter()
            {
                MessageRecieved = (json) =>
                {
                    // batch up the messages and send over to API
                    _ = Send(json);
                }
            });

            var tcs = new TaskCompletionSource<object>();
            stoppingToken.Register(() =>
                {
                    input.Stop();
                    tcs.SetResult(null);
                });

            return tcs.Task;
        }
    }

    public class EventedTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        private StringBuilder sb = new StringBuilder();

        private void ProcessEventString(string json)
        {
            MessageRecieved?.Invoke(json);
        }

        public Action<string> MessageRecieved { get; set; } = (json) => { };

        public override void Write(char value)
        {
            if (value == '\n')
            {
                ProcessEventString(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(value);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            // buffer the data untill we see a newline '\n'
            // take all char prior to newline and process them as a json blob.
            // repeat untill all messages are processed stashing a buffer on partial writes
            // sb will never have any newlines!!!
            for (var i = index; i < count && i < buffer.Length; i++)
            {
                if (buffer[i] == '\n')
                {
                    var processedCounter = i - index;
                    count--;
                    if (i >= 1)
                    {
                        if (buffer[i - 1] == '\r')
                        {
                            processedCounter--;
                            count--;
                        }
                    }

                    if (processedCounter > 0)
                    {
                        // append the json blob to the buffer
                        sb.Append(buffer, index, processedCounter);
                    }
                    if (sb.Length > 0)
                    {
                        ProcessEventString(sb.ToString());
                        sb.Clear();
                    }
                    count -= processedCounter;
                    index = i + 1;
                    break;
                }
            }

            if (count > 0)
            {
                sb.Append(buffer, index, count);
            }
            // grabe the chars and push into StringBuilder
        }
    }
}
