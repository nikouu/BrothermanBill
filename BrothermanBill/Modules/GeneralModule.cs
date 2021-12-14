using BrothermanBill.Services;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.Commands;
using Discord.WebSocket;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BrothermanBill.Modules
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        private SpeechService _speechService;
        public GeneralModule(SpeechService speechService)
        {
            _speechService = speechService;
        }

        [Command("ping")]
        public Task PingAsync()
            => ReplyAsync($"Current Ping {Context.Client.Latency}ms");

        [Command("userinfo")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user ??= Context.User;
            await ReplyAsync(user.ToString());
        }

        [Command("setgame")]
        public async Task GameAsync([Remainder] string setgame)  // [Remainder] takes all arguments as one
        {
            await Context.Client.SetGameAsync(setgame);
            await ReplyAsync("Set game succeeded");
        }

        [Command("listenSimple")]
        public async Task ListenSimple()  // [Remainder] takes all arguments as one
        {
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel;
            var users = await voiceChannel.GetUsersAsync().ToListAsync();
            foreach (var user in users[0])
            {
                if (user.IsBot)
                    continue;

                await ListenUserAsync2(user);
            }
        }

        public async Task<byte[]> BufferIncomingStream(AudioInStream e, int time = 3)
        {
            ConcurrentQueue<byte> voiceInQueue = new ConcurrentQueue<byte>();
            SemaphoreSlim queueLock = new SemaphoreSlim(1, 1);
            return await Task.Run(async () =>
            {
                DateTime nowTime = DateTime.Now;
                while (DateTime.Now.Subtract(nowTime).TotalSeconds <= time)
                {
                    if (e.AvailableFrames > 0)
                    {
                        queueLock.Wait();
                        RTPFrame frame = await e.ReadFrameAsync(CancellationToken.None);
                        for (int i = 0; i < frame.Payload.Length; i++)
                        {
                            voiceInQueue.Enqueue(frame.Payload[i]);
                        }
                        queueLock.Release();
                    }
                }
                return voiceInQueue.ToArray();
            });
        }

        // https://github.com/jfantonopoulos/Misaka/blob/002975680ce75500c1b72ddaf2289674c9e17e55/Misaka/Services/AudioService.cs
        public async Task ListenUserAsync2(IGuildUser user)
        {
            var socketUser = (user as SocketGuildUser);
            var userAduioStream = (InputStream)socketUser.AudioStream;
            var currentTicks = DateTime.Now;

            var memoryStream = new MemoryStream(await BufferIncomingStream(userAduioStream, 3));

            using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks.Ticks}.bin", FileMode.Create))
            {

                memoryStream.CopyTo(fileStream);
            }

            memoryStream.Position = 0;
            using (var process = CreateFfmpegOut(@$"C:\lmao2\{currentTicks.Ticks}.bin"))
            using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks.Ticks}.wav", FileMode.Create))
            {
                process.StandardOutput.BaseStream.CopyTo(fileStream);
                //outputMemoryStream.CopyTo(fileStream);
            }

            _speechService.ParseStream(memoryStream);
        }


        public async Task ListenUserAsync(IGuildUser user)

        {
            var socketUser = (user as SocketGuildUser);
            var userAduioStream = (InputStream)socketUser.AudioStream;

            var recordingTimeInSeconds = 4;
            var startListeningTime = DateTime.Now;

            //using (var ffmpeg = CreateFfmpegOut())
            //using (var ffmpegOutStdinStream = ffmpeg.StandardInput.BaseStream)
            //using (var ffmpegStdinStream = ffmpeg.StandardOutput.BaseStream)
            using (var inputMemoryStream = new MemoryStream())
            using (var outputMemoryStream = new MemoryStream())
            {
                try
                {
                    var buffer = new byte[4096];
                    // this will wait until there is audio in order to pop the recording time limit. i.e. if its silent before the recording time in seconds pops, it will wait until the next sound to break from the while
                    while (await userAduioStream.ReadAsync(buffer, 0, buffer.Length) > 0)
                    {
                        await inputMemoryStream.WriteAsync(buffer, 0, buffer.Length);
                        await inputMemoryStream.FlushAsync();
                    }
                }
                finally
                {
                    await inputMemoryStream.FlushAsync();
                    //await ffmpegStdinStream.FlushAsync();
                    //ffmpegStdinStream.Close();
                    //ffmpeg.Close();
                }

                Console.WriteLine(inputMemoryStream.Length);
                inputMemoryStream.Position = 0; //THIS WAS IT

                var currentTicks = DateTime.Now;



                //await FFMpegArguments.FromPipeInput(new StreamPipeSource(inputMemoryStream), options => options
                //    .WithCustomArgument("-ac 2")
                //    .WithCustomArgument("-f s16le")
                //    //.WithCustomArgument("-ar 48000")
                //    )
                //    .OutputToPipe(new StreamPipeSink(outputMemoryStream), options => options
                //    .WithCustomArgument("-acodec pcm_u8")
                //    .WithCustomArgument("-ar 48000")
                //    .WithCustomArgument("-f wav")
                //    ).ProcessAsynchronously();


                outputMemoryStream.Position = 0;


                using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks.Ticks}.bin", FileMode.Create))
                {

                    inputMemoryStream.CopyTo(fileStream);
                }

                outputMemoryStream.Position = 0;
                using (var process = CreateFfmpegOut(@$"C:\lmao2\{currentTicks.Ticks}.bin"))
                using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks.Ticks}.wav", FileMode.Create))
                {
                    process.StandardOutput.BaseStream.CopyTo(fileStream);
                    //outputMemoryStream.CopyTo(fileStream);
                }

                var differenceInMs = (DateTime.Now - currentTicks).TotalMilliseconds;
                Console.WriteLine(differenceInMs);

                _speechService.ParseStream(inputMemoryStream);

                //var analysis = FFProbe.Analyse(memoryStream);

                //FFMpegArguments.FromPipeInput(new StreamPipeSource(memoryStream))
                //    .OutputToFile(@$"C:\lmao2\{DateTime.Now.Ticks}.wav")
                //    .ProcessSynchronously();
            }
        }
        public static Process CreateFfmpegOut(string filePath)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -ac 2 -f s16le -ar 48000 -i {filePath} -ac 2 -ar 44100 -f wav -",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            });
        }

        public static Process CreateFfmpegOut()
        {        
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -ac 2 -f s16le -ar 48000 -i pipe:0 -acodec pcm_u8 -ar 22050 -f wav -",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            });
        }


        [Command("hd")]
        public async Task HitDebug()
        {
            var guild = Context.Guild;
            var voiceState = Context.User as IVoiceState;
            var channel = Context.Channel;
            var iTextChannel = Context.Channel as ITextChannel;
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel;



            var audioClient = guild.AudioClient;

            audioClient.StreamCreated += AudioClient_StreamCreated;
            var streams = audioClient.GetStreams();
            var inputStream = streams.FirstOrDefault().Value;
        }

        private async Task AudioClient_StreamCreated(ulong userid, AudioInStream stream)
        {
            Console.WriteLine($"ReadFrameAsync: {userid}");
            var frame = await stream.ReadFrameAsync(System.Threading.CancellationToken.None);
        }
    }
}
