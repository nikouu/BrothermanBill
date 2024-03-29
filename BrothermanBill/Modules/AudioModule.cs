﻿using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace BrothermanBill.Modules
{
    [Name("Audio Module")]
    [Summary("Provides audio capabilities.")]
    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly EmbedHandler _embedHandler;
        private readonly ILogger _logger;
        private readonly MemeService _memeService;
        private readonly StatusService _statusService;
        private readonly Dictionary<ulong, Task> _healthReminders = new Dictionary<ulong, Task>();

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
        [Summary("Adds Brotherman Bill to the calling user's audio channel.")]
        public async Task JoinAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("You must be connected to a voice channel!");
                return;
            }

            try
            {
                if (_lavaNode.HasPlayer(Context.Guild))
                {
                    await LeaveAsync();
                }

                await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                _logger.LogInformation($"Joined {voiceState.VoiceChannel.Name}!");

            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Leave")]
        [Summary("Disconnects Brotherman Bill to the calling user's audio channel.")]
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

        [Command("Play", RunMode = RunMode.Async)]
        [Summary("Adds a YouTube search query or a YouTube video or playlist URL to the queue.")]
        public async Task PlayAsync([Remainder] string searchQuery)
            => await HandlePlay(searchQuery, false);

        [Command("PlayNow", RunMode = RunMode.Async)]
        [Summary("Immediately plays a YouTube search query or a YouTube video or playlist URL.")]
        public async Task PlayNowAsync([Remainder] string searchQuery)
            => await HandlePlay(searchQuery, true);

        // todo: add an embed of the next track coming up

        [Command("MoveToBack")]
        [Summary("Moves the currently playing track to the back of the queue.")]
        public async Task MoveToBack()
            => await MoveTrackToBack();


        [Command("Pause")]
        [Summary("Pauses the current track.")]
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

        // drink water and posture check shoutout

        [Command("Seek")]
        [Summary("Seeks with a given time. Formats include \"ss\", \"mm:ss\", \"h:mm:ss\". Can be negative.")]
        public async Task Seek(string timeSpanString)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                _logger.LogInformation("I cannot seek when I'm not playing anything.");
                return;
            }

            var isNegative = timeSpanString.StartsWith("-");

            var formats = new[] {
                @"s",
                @"ss",
                @"m\:ss",
                @"mm\:ss",
                @"h\:mm\:ss"
            };

            if (!TimeSpan.TryParseExact(timeSpanString.Replace("-", ""), formats, CultureInfo.CurrentCulture, out TimeSpan duration))
            {
                return;
            }

            if (isNegative)
            {
                duration = -duration;
            }

            try
            {
                await player.SeekAsync(player.Track.Position + duration);
                _logger.LogInformation($"Seeked `{player.Track.Title}` to {player.Track.Position + duration}.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
            }
        }


        [Command("SeekTo")]
        [Summary("Seeks to a given time. Formats include \"ss\", \"mm:ss\", \"h:mm:ss\".")]
        public async Task SeekTo(string timeSpanString)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                _logger.LogInformation("I cannot seekTo when I'm not playing anything.");
                return;
            }

            // might be overkill
            var formats = new[] {
                @"s",
                @"ss",
                @"m\:ss",
                @"mm\:ss",
                @"h\:mm\:ss"
            };

            if (!TimeSpan.TryParseExact(timeSpanString, formats, CultureInfo.CurrentCulture, out TimeSpan duration))
            {
                return;
            }

            try
            {
                await player.SeekAsync(duration);
                await ReplyAsync($"I've seeked `{player.Track.Title}` to {duration}.");
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Resume")]
        [Summary("Resumes the current track.")]
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
        [Summary("Stops playing the current track and clears the queue.")]
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
        [Summary("Skips the currently playing track.")]
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

        [Command("NowPlaying")]
        [Alias("Np")]
        [Summary("Displays information about the currently playing track.")]
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
            var duration = track.IsStream ? "Live stream" : CreateDurationString(track);
            var embed = await _embedHandler.CreateNowPlayingEmbed(track?.Title, track?.Author, track?.Url, art, duration);

            await ReplyAsync(message: "Now playing:", embed: embed);
        }

        [Command("Queue")]
        [Summary("Displays the current queue. Use \"full\" after the command for the entire queue.")]
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
        [Summary("Clears the queue.")]
        public async Task ClearQueue()
        {
            Player.Queue.Clear();
            await ReplyAsync("Queue cleared.");
        }

        [Command("Meme")]
        [Alias("m", "meem", "mmee", "emme")]
        [Summary("Calls random meme soundbyte. Add a search query afterwards to search.")]
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

        private async Task StartHealthTimer()
        {

        }
    }
}
