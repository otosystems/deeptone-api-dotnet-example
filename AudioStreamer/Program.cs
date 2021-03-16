using NAudio.Wave;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace AudioStreamer
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var exitEvent = new ManualResetEvent(false);
            var url = new Uri("wss://api.oto.ai/stream?models=gender&volume_threshold=0.001");

            await SendMic(exitEvent, url);
        }

        private static async Task SendMic(ManualResetEvent exitEvent, Uri url)
        {
            var waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            var factory = new Func<ClientWebSocket>(() =>
            {
                var clientWebsocket = new ClientWebSocket();
                clientWebsocket.Options.SetRequestHeader("X-Api-Key", Environment.GetEnvironmentVariable("API_KEY"));
                return clientWebsocket;
                
            });

            using (var client = new WebsocketClient(url, factory))
            {

                client.DisconnectionHappened.Subscribe(info =>
                {
                    Console.WriteLine("disconnected" + info?.Exception?.Message);
                });

                client.ReconnectTimeout = TimeSpan.FromSeconds(5);
                client.ReconnectionHappened.Subscribe(info =>
                {
                    Console.WriteLine($"Reconnection happened, type: {info.Type}");
                });
                //{"message_type":"results","results":{"channels":{"0":{"results":{"speech-rt":{"confidence":1.0,"result":"silence"}},"timestamp":22528.0}}}}
                client.MessageReceived.Subscribe(msg =>
                {
                    var parsed = JObject.Parse(msg.Text);
                    if (parsed["message_type"].ToString() == "results")
                    {
                        var type = parsed["results"]["channels"]["0"]["results"]["gender"]["result"];
                        Console.WriteLine($"{type}");
                    }
                });

                await client.StartOrFail();
                waveIn.StartRecording();
                waveIn.DataAvailable += (o, e) =>
                {
                    Console.WriteLine($"Sendin some buffer: {e.Buffer.Length}");
                    // or fire-and-forget
                    // Task.Run(() => client.SendInstant(e.Buffer).GetAwaiter().GetResult());
                    client.SendInstant(e.Buffer).GetAwaiter().GetResult();
                };

                exitEvent.WaitOne();
            }
        }

        private static void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
