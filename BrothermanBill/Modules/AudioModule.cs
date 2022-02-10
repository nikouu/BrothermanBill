using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace BrothermanBill.Modules
{
    // https://www.oracle.com/java/technologies/downloads/#jdk17-windows
    // https://github.com/Yucked/Victoria/wiki
    // https://raw.githubusercontent.com/freyacodes/Lavalink/master/LavalinkServer/application.yml.example
    // perhaps have a play now, that just injects a new track immediately, hten goes back to the old one

    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly EmbedHandler _embedHandler;
        private readonly ILogger _logger;
        private readonly MemeService _memeService;
        private readonly StatusService _statusService;

        private LavaPlayer Player
            => _lavaNode.GetPlayer(Context.Guild);

        public AudioModule(LavaNode lavaNode, AudioService audioService, MemeService memeService, EmbedHandler embedHandler, ILogger<AudioModule> logger, StatusService statusService)
        {
            _lavaNode = lavaNode;
            _memeService = memeService;
            _embedHandler = embedHandler;
            _logger = logger;
            _statusService = statusService;
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
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                _logger.LogInformation($"Joined {voiceState.VoiceChannel.Name}!");

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
                await ReplyAsync(":(");

            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Play")]
        public async Task PlayAsync([Remainder] string searchQuery)
            => await HandlePlay(searchQuery, false);

        [Command("PlayNow")]
        public async Task PlayNowAsync([Remainder] string searchQuery)
            => await HandlePlay(searchQuery, true);

        // todo: add an embed of the next track coming up

        [Command("MoveToBack")]
        public async Task MoveToBack()
            => await MoveTrackToBack();


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
                _logger.LogInformation("I cannot pause when I'm not playing anything!");
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
                _logger.LogInformation("I cannot resume when I'm not playing anything!");
                return;
            }

            try
            {
                await player.ResumeAsync();
                _logger.LogInformation($"Resumed: {player.Track.Title}");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
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
                _logger.LogInformation("Attempted stop on already stopped PlayerState");
                return;
            }

            try
            {
                await player.StopAsync();
                await ClearQueue();
                _logger.LogInformation("Queue finished.");
                await _statusService.SetStatus(null);
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
                    // fix bug when something is playing, like a meme then skipping fails
                    var (oldTrack, currentTrack) = await player.SkipAsync();

                    _logger.LogInformation($"Skipped: {oldTrack.Title}");
                    await HandleNextTrackComment(currentTrack);
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
                await ReplyAsync("Playing nothing.");
                return;
            }

            var track = Player.Track;
            var art = await track.FetchArtworkAsync();
            var duration = track.IsStream ? "" : CreateDurationString(track);
            var embed = await _embedHandler.CreateNowPlayingEmbed(track?.Title, track?.Author, track?.Url, art, duration);

            await ReplyAsync(message: "Now playing:", embed: embed);
        }

        [Command("Queue")]
        public async Task QueueAsync([Remainder] string command = "")
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }           

            var nowPlaying = Player?.Track is null ? null : $"{Player?.Track?.Title}";
            var queue = player.Queue.Select(x => $"{x.Title}");
            var displayFullQueue = command.ToLower() == "full";
            var embed = await _embedHandler.CreateQueueEmbed(nowPlaying, queue, displayFullQueue);

            await ReplyAsync(player.PlayerState != PlayerState.Playing
                ? "Nothing is playing."
                : "Queue:", embed: embed);
        }

        [Command("ClearQueue")]
        public async Task ClearQueue()
        {
            Player.Queue.Clear();
            await ReplyAsync("Queue cleared.");
        }

        [Command("Meme")]
        [Alias("m", "meem", "mmee", "emme")]
        public async Task RandomMeme([Remainder] string meme = "")
        {
            var url = string.IsNullOrWhiteSpace(meme)
                ? await _memeService.GetRandomMeme()
                : await _memeService.GetMeme(meme);

            if (!string.IsNullOrWhiteSpace(url))
            {
                await PlayNowAsync(url);
                return;
            }

            _logger.LogInformation($"No meme sound clip for {meme}.");
            return;
        }

        private async Task<SearchResponse> LavaLinkSearch(string searchQuery)
        {
            var fullQuery = searchQuery;
            var isValidUrl = Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri);

            if (!isValidUrl)
            {
                fullQuery = "ytsearch: " + searchQuery;
            }

            var searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, fullQuery);

            // set position of track on the track object
            return searchResponse;
        }

        private Task AddToFront(LavaTrack track)
        {
            var currentTrack = Player.Track;
            var trackList = Player.Queue.ToList();

            if (currentTrack is not null)
            {
                trackList = trackList.Prepend(currentTrack).ToList();
            }

            trackList = trackList.Prepend(track).ToList();

            Player.Queue.Clear();
            Player.Queue.Enqueue(trackList);

            return Task.CompletedTask;
        }

        private async Task MoveTrackToBack()
        {
            var currentTrack = Player.Track;
            var trackList = Player.Queue.ToList();

            if (currentTrack is not null)
            {
                trackList = trackList.Append(currentTrack).ToList();
            }

            Player.Queue.Clear();
            Player.Queue.Enqueue(trackList);

            await Player.SkipAsync();

            await HandleNextTrackComment(Player.Track);

            return;
        }

        private string CreateDurationString(LavaTrack track)
        {
            // https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings
            // The reason there is a slash is because they're string literals to the formatter
            var durationString = "";
            var durationStringFormat = @"mm\:ss";

            if (track.Duration.TotalHours >= 1)
            {
                durationStringFormat = @"hh\:mm\:ss";
            }

            durationString = $"{track.Position.ToString(durationStringFormat)}/{track.Duration.ToString(durationStringFormat)}";

            return durationString;
        }

        private async Task HandlePlay(string searchQuery, bool playImmediately)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await ReplyAsync("Please provide search terms.");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await JoinAsync();
            }

            var seekTime = GetUrlParameterTime(searchQuery);
            var searchResponse = await LavaLinkSearch(searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                _logger.LogInformation($"I wasn't able to find anything for `{searchQuery}`.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                Player.Queue.Enqueue(searchResponse.Tracks);
                await ReplyAsync($"Enqueued {searchResponse.Tracks.Count} songs.");
            }
            else
            {
                var track = searchResponse.Tracks.FirstOrDefault();

                if (seekTime != TimeSpan.Zero)
                {
                    track = new LavaTrack(track.Hash, track.Id, track.Title, track.Author, track.Url, seekTime, (long)track.Duration.TotalMilliseconds, track.CanSeek, track.IsStream, track.Source);
                }

                if (Player?.Track?.IsStream == true || playImmediately)
                {
                    await PlayTrackImmediately(track);
                }
                else
                {
                    await EnqueueTrack(track);
                }
            }

            if (Player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                return;
            }

            Player.Queue.TryDequeue(out var lavaTrack);
            await Player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.StartTime = seekTime;
            });
        }

        private async Task EnqueueTrack(LavaTrack track)
        {
            Player.Queue.Enqueue(track);

            var art = await track.FetchArtworkAsync();
            var embed = await _embedHandler.CreatePlayEmbed(track?.Title, track?.Author, track?.Url, art);
            await ReplyAsync(message: "Queued:", embed: embed);
        }

        private async Task PlayTrackImmediately(LavaTrack track)
        {
            if (Player.PlayerState is PlayerState.Paused)
            {
                await ResumeAsync();
            }

            await AddToFront(track);

            if (Player.PlayerState is PlayerState.Playing)
            {
                await Player.SkipAsync();
            }

            await HandleNextTrackComment(track);
        }

        private TimeSpan GetUrlParameterTime(string searchQuery)
        {
            var isValidUrl = Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri);

            if (!isValidUrl)
            {
                return new TimeSpan(0, 0, 0);
            }

            // todo: check if the queryparameter key contains t, not just if any of the string contains t
            if (!uri.Query.Contains("&t="))
            {
                return new TimeSpan(0, 0, 0);
            }

            var queryString = HttpUtility.ParseQueryString(uri.Query);
            var seconds = int.Parse(queryString.Get("t"));

            var timeSpan = TimeSpan.FromSeconds(seconds);
            return timeSpan;
        }

        private async Task HandleNextTrackComment(LavaTrack track)
        {
            var art = await track.FetchArtworkAsync();
            var embed = await _embedHandler.CreatePlayEmbed(track?.Title, track?.Author, track?.Url, art);

            _logger.LogInformation($"Playing now:{track?.Title}");
            await ReplyAsync(message: "Playing now:", embed: embed);
        }
    }
}
