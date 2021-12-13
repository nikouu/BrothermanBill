using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;

namespace BrothermanBill.Modules
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
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

                await ListenUserAsync(user);
            }
        }

        public async Task ListenUserAsync(IGuildUser user)
        {
            var socketUser = (user as SocketGuildUser);
            var userAduioStream = (InputStream)socketUser.AudioStream;

            var recordingTimeInSeconds = 3;
            var startListeningTime = DateTime.Now;

            //using (var ffmpeg = CreateFfmpegOut())
            //using (var ffmpegOutStdinStream = ffmpeg.StandardInput.BaseStream)
            using (var memoryStream = new MemoryStream())
            {
                try
                {
                    var buffer = new byte[4096];
                    // this will wait until there is audio in order to pop the recording time limit. i.e. if its silent before the recording time in seconds pops, it will wait until the next sound to break from the while
                    while (await userAduioStream.ReadAsync(buffer, 0, buffer.Length) > 0 && (DateTime.Now - startListeningTime).TotalSeconds < recordingTimeInSeconds)
                    {
                        await memoryStream.WriteAsync(buffer, 0, buffer.Length);
                        //Console.WriteLine(ffmpegOutStdinStream.Position);
                       // await ffmpegOutStdinStream.FlushAsync();
                    }
                }
                finally
                {
                    await memoryStream.FlushAsync();                    
                    //ffmpegOutStdinStream.Close();
                    //ffmpeg.Close();
                }

                Console.WriteLine(memoryStream.Length);
                memoryStream.Position = 0; //THIS WAS IT

                var currentTicks = DateTime.Now.Ticks;

                using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks}.bin", FileMode.Create))
                {
                    memoryStream.CopyTo(fileStream);
                }

                using (var process = CreateFfmpegOut(@$"C:\lmao2\{currentTicks}.bin"))
                using (var fileStream = new FileStream(@$"C:\lmao2\{currentTicks}.wav", FileMode.Create))
                {
                    process.StandardOutput.BaseStream.CopyTo(fileStream);
                }

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
                Arguments = $"-hide_banner -loglevel panic -ac 2 -f s16le -ar 48000 -i {filePath} -acodec pcm_u8 -ar 22050 -f wav -",
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
