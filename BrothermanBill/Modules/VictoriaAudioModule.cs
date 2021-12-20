using BrothermanBill.Services;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;
using System.Net;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace BrothermanBill.Modules
{
    // https://www.oracle.com/java/technologies/downloads/#jdk17-windows
    // https://github.com/Yucked/Victoria/wiki
    // https://raw.githubusercontent.com/freyacodes/Lavalink/master/LavalinkServer/application.yml.example
    // perhaps have a play now, that just injects a new track immediately, hten goes back to the old one

    public class VictoriaAudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly VictoriaAudioService _audioService;

        public VictoriaAudioModule(LavaNode lavaNode, VictoriaAudioService audioService)
        {
            _lavaNode = lavaNode;
            _audioService = audioService;
        }

        [Command("testaudio", RunMode = RunMode.Async)]
        public async Task Test()
        {
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel;
            var users = await voiceChannel.GetUsersAsync().ToListAsync();

            var socketUsers = users[0].Select(x => x as SocketGuildUser).ToList();

            var aa = socketUsers[0];
            var audioStreams = Context.Guild?.AudioClient?.GetStreams();
        }

        [Command("TestSendAudio", RunMode = RunMode.Async)]
        public async Task TestSendAudio()
        {
            
        }

        [Command("Join", RunMode = RunMode.Async)]
        public async Task JoinAsync()
        {
            if (_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync("I'm already connected to a voice channel!");
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("You must be connected to a voice channel!");
                return;
            }

            try
            {
                var f = await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                var joinAudioFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "joinSound.mp3");

                await PlayAsync(@"C:\temp\joinSound.mp3");
                await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");

            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Leave")]
        public async Task LeaveAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to any voice channels!");
                return;
            }

            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null)
            {
                await ReplyAsync("Not sure which voice channel to disconnect from.");
                return;
            }

            try
            {
                await _lavaNode.LeaveAsync(voiceChannel);
                //await ReplyAsync($"I've left {voiceChannel.Name}!");
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Play")]
        public async Task PlayAsync([Remainder] string searchQuery)
        {
            var fullQuery = searchQuery;

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await ReplyAsync("Please provide search terms.");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await JoinAsync();
            }

            var player = _lavaNode.GetPlayer(Context.Guild);

            if (player?.Track?.IsStream == true)
            {
                await PlayNowAsync(searchQuery);
                return;
            }

            // is there a better way to do this pattern?
            // do the yt search by default here
            var isValidUrl = Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri);

            if (!isValidUrl)
            {
                // if there are no spaces before the colon, im badly going to assume its a query that specifies a search type for lavalink
                if (!searchQuery.Split(":")[0].Contains(" "))
                {
                   
                }
                else
                {
                    // default to youtube
                    fullQuery = "ytsearch: " + searchQuery;
                }

            }

            var searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, fullQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                //await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
                return;
            }

            //var player = _lavaNode.GetPlayer(Context.Guild);
            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                player.Queue.Enqueue(searchResponse.Tracks);
                await ReplyAsync($"Enqueued {searchResponse.Tracks.Count} songs.");
            }
            else
            {
                var track = searchResponse.Tracks.FirstOrDefault();
                player.Queue.Enqueue(track);

                await ReplyAsync($"Enqueued {track?.Title}");
            }

            if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                return;
            }

            player.Queue.TryDequeue(out var lavaTrack);
            await player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.ShouldPause = false;
            });
        }

        [Command("PlayNow")]
        public async Task PlayNowAsync([Remainder] string searchQuery)
        {
            var fullQuery = searchQuery;

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await ReplyAsync("Please provide search terms.");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await JoinAsync();
            }

            // is there a better way to do this pattern?
            // do the yt search by default here
            var isValidUrl = Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri);

            if (!isValidUrl)
            {
                fullQuery = "ytsearch: " + searchQuery;
            }

            var searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, fullQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);

            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                await ReplyAsync($"Cannot Play Now a playlist");
                return;
            }
            else
            {
                var track = searchResponse.Tracks.FirstOrDefault();
                await AddToFront(track);
                var (oldTrack, currenTrack) = await player.SkipAsync();
                await ReplyAsync($"Playing now:{track?.Title}");
            }

            if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                return;
            }

            player.Queue.TryDequeue(out var lavaTrack);
            await player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.ShouldPause = false;
            });
        }


        [Command("Pause")]
        public async Task PauseAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync("I cannot pause when I'm not playing anything!");
                return;
            }

            try
            {
                await player.PauseAsync();
                await ReplyAsync($"Paused: {player.Track.Title}");
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Resume")]
        public async Task ResumeAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Paused)
            {
                await ReplyAsync("I cannot resume when I'm not playing anything!");
                return;
            }

            try
            {
                await player.ResumeAsync();
                await ReplyAsync($"Resumed: {player.Track.Title}");
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Stop")]
        public async Task StopAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("Woaaah there, I can't stop the stopped forced.");
                return;
            }

            try
            {
                await player.StopAsync();
                await ReplyAsync("Queue finished.");
                await _audioService.UpdateStatusWithTrackName(null);
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Skip")]
        public async Task SkipAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync("Woaaah there, I can't skip when nothing is playing.");
                return;
            }

            try
            {
                if (!player.Queue.Any())
                {
                    await StopAsync();
                    return;
                }
                else
                {
                    var (oldTrack, currenTrack) = await player.SkipAsync();
                    await ReplyAsync($"Skipped: {oldTrack.Title}\nNow Playing: {player.Track.Title}");
                }

            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("NowPlaying"), Alias("Np")]
        public async Task NowPlayingAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync("Woaaah there, I'm not playing any tracks.");
                return;
            }

            var track = player.Track;
            var artwork = await track.FetchArtworkAsync();

            var embed = new EmbedBuilder()
                .WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
                .WithTitle($"Now Playing: {track.Title}")
                .WithImageUrl(artwork)
                .WithFooter($"{track.Position}/{track.Duration}");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("Queue")]
        public async Task QueueAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.Queue.Count == 0)
            {
                await ReplyAsync("Queue is empty.");
                return;
            }

            await ReplyAsync(player.PlayerState != PlayerState.Playing
                ? "Nothing is playing."
                : string.Join(Environment.NewLine, player.Queue.Select(x => x.Title)));
        }

        [Command("ClearQueue")]
        public async Task ClearQueue()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);
            player.Queue.Clear();
            await ReplyAsync("Queue cleared");
        }

        public Task AddToFront(LavaTrack track)
        {
            var player = _lavaNode.GetPlayer(Context.Guild);
            var currentTrack = player.Track;
            var trackList = player.Queue.ToList();

            trackList = trackList.Prepend(currentTrack).ToList();
            trackList = trackList.Prepend(track).ToList();

            player.Queue.Clear();
            player.Queue.Enqueue(trackList);

            return Task.CompletedTask;
        }
    }
}
