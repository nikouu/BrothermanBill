using BrothermanBill.Models;
using BrothermanBill.Players;
using BrothermanBill.Services;
using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Web;

namespace BrothermanBill.Modules
{
    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IAudioService _audioService;
        private readonly EmbedHandler _embedHandler;
        private readonly ILogger _logger;
        private readonly MemeService _memeService;
        private readonly StatusService _statusService;

        // "%s", not "s": a single-character format string is read as a *standard*
        // specifier, and "s" is not one, so a bare "s" rejects every single-digit
        // input ("/seek 5"). The "%" marks it as a custom specifier.
        private static readonly string[] TimeFormats =
        {
            @"%s",
            @"ss",
            @"m\:ss",
            @"mm\:ss",
            @"h\:mm\:ss"
        };

        public AudioModule(IAudioService audioService, MemeService memeService, EmbedHandler embedHandler, ILogger<AudioModule> logger, StatusService statusService)
        {
            _audioService = audioService;
            _memeService = memeService;
            _embedHandler = embedHandler;
            _logger = logger;
            _statusService = statusService;
        }

        private async ValueTask<ResumableQueuedPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
        {
            var retrieveOptions = new PlayerRetrieveOptions(
                ChannelBehavior: connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None);

            var voiceState = Context.User as IVoiceState;

            var playerFactory = PlayerFactory.Create<ResumableQueuedPlayer, QueuedLavalinkPlayerOptions>(
                properties => new ResumableQueuedPlayer(properties));

            PlayerResult<ResumableQueuedPlayer> result;
            try
            {
                using var cts = new CancellationTokenSource(millisecondsDelay: 30000);
                await _audioService.WaitForReadyAsync(cts.Token).ConfigureAwait(false);

                result = await _audioService.Players
                    .RetrieveAsync(Context.Guild.Id, voiceState?.VoiceChannel?.Id, playerFactory, Options.Create(new QueuedLavalinkPlayerOptions()), retrieveOptions);
            }
            catch (OperationCanceledException exception)
            {
                // WaitForReadyAsync timed out: the Lavalink node isn't reachable/ready.
                _logger.LogError(exception, "Timed out waiting for the audio service to be ready.");
                await FollowupAsync("The audio service isn't responding right now. Try again in a moment.");
                return null;
            }
            catch (Exception exception)
            {
                // Any other failure (e.g. voice connection wedged) would otherwise leave
                // the deferred interaction hanging with no response at all.
                _logger.LogError(exception, "Failed to retrieve the audio player.");
                await FollowupAsync("Something went wrong connecting to voice. Try again in a moment.");
                return null;
            }

            if (!result.IsSuccess)
            {
                var errorMessage = result.Status switch
                {
                    PlayerRetrieveStatus.UserNotInVoiceChannel => "You must be connected to a voice channel!",
                    PlayerRetrieveStatus.BotNotConnected => "I'm not connected to a voice channel.",
                    _ => "Unknown error.",
                };
                await FollowupAsync(errorMessage);
                return null;
            }

            return result.Player;
        }

        [SlashCommand("join", "Adds Brotherman Bill to the calling user's audio channel.")]
        public async Task JoinAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: true);
            if (player is not null)
            {
                _logger.LogInformation("Joined voice channel!");
                await FollowupAsync("Joined!");
            }
        }

        [SlashCommand("leave", "Disconnects Brotherman Bill from the voice channel.")]
        public async Task LeaveAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(connectToVoiceChannel: false);
            if (player is null) return;

            await player.DisconnectAsync();
            await _statusService.SetReady();
            await FollowupAsync(":(");
        }

        [SlashCommand("play", "Adds a YouTube search query or a YouTube video or playlist URL to the queue.")]
        public async Task PlayAsync([Summary(description: "Search query or URL")] string query)
        {
            await DeferAsync();
            await HandlePlay(query, false);
        }

        [SlashCommand("playnow", "Immediately plays a YouTube search query or a YouTube video or playlist URL.")]
        public async Task PlayNowAsync([Summary(description: "Search query or URL")] string query)
        {
            await DeferAsync();
            await HandlePlay(query, true);
        }

        [SlashCommand("movetoback", "Moves the currently playing track to the back of the queue.")]
        public async Task MoveToBack()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            var currentTrack = player.CurrentTrack;
            if (currentTrack is null)
            {
                await FollowupAsync("Nothing is playing.");
                return;
            }

            // Add current track to end of queue, then skip to next
            await player.PlayAsync(currentTrack);
            await player.SkipAsync();

            if (player.CurrentTrack is not null)
            {
                await HandleNextTrackComment(player.CurrentTrack);
            }
            else
            {
                await FollowupAsync($"Moved `{currentTrack.Title}` to the back of the queue.");
            }
        }

        [SlashCommand("pause", "Pauses the current track.")]
        public async Task PauseAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.State != PlayerState.Playing)
            {
                _logger.LogInformation("Cannot pause when not playing!");
                await FollowupAsync("Nothing is playing to pause.");
                return;
            }

            await player.PauseAsync();
            await FollowupAsync($"Paused: {player.CurrentTrack?.Title}");
        }

        [SlashCommand("seek", "Seeks with a given time. Formats: \"ss\", \"mm:ss\", \"h:mm:ss\". Can be negative.")]
        public async Task Seek([Summary(description: "Time to seek by (e.g. 30, -1:00, 1:30:00)")] string time)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.CurrentTrack is null)
            {
                _logger.LogInformation("Cannot seek when not playing.");
                await FollowupAsync("Nothing is playing.");
                return;
            }

            var isNegative = time.StartsWith("-");

            if (!TimeSpan.TryParseExact(time.Replace("-", ""), TimeFormats, CultureInfo.InvariantCulture, out TimeSpan duration))
            {
                await FollowupAsync("Invalid time format.");
                return;
            }

            if (isNegative)
            {
                duration = -duration;
            }

            try
            {
                var currentPosition = player.Position?.Position ?? TimeSpan.Zero;
                await player.SeekAsync(currentPosition + duration);
                _logger.LogInformation("Seeked {Title} to {Position}.", player.CurrentTrack.Title, currentPosition + duration);
                await FollowupAsync($"Seeked `{player.CurrentTrack.Title}` by {time}.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Seek failed.");
                await FollowupAsync("Seek failed.");
            }
        }

        [SlashCommand("seekto", "Seeks to a given time. Formats: \"ss\", \"mm:ss\", \"h:mm:ss\".")]
        public async Task SeekTo([Summary(description: "Time to seek to (e.g. 30, 1:00, 1:30:00)")] string time)
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.CurrentTrack is null)
            {
                _logger.LogInformation("Cannot seekTo when not playing.");
                await FollowupAsync("Nothing is playing.");
                return;
            }

            if (!TimeSpan.TryParseExact(time, TimeFormats, CultureInfo.InvariantCulture, out TimeSpan duration))
            {
                await FollowupAsync("Invalid time format.");
                return;
            }

            try
            {
                await player.SeekAsync(duration);
                await FollowupAsync($"Seeked `{player.CurrentTrack.Title}` to {duration}.");
            }
            catch (Exception exception)
            {
                await FollowupAsync(exception.Message);
            }
        }

        [SlashCommand("resume", "Resumes the current track.")]
        public async Task ResumeAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.State != PlayerState.Paused)
            {
                _logger.LogInformation("Cannot resume when not paused!");
                await FollowupAsync("Nothing is paused.");
                return;
            }

            await player.ResumeAsync();
            _logger.LogInformation("Resumed: {Title}", player.CurrentTrack?.Title);
            await FollowupAsync($"Resumed: {player.CurrentTrack?.Title}");
        }

        [SlashCommand("stop", "Stops playing the current track and clears the queue.")]
        public async Task StopAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.CurrentTrack is null)
            {
                _logger.LogInformation("Attempted stop with nothing playing.");
                await FollowupAsync("Nothing is playing.");
                return;
            }

            await player.Queue.ClearAsync();
            await player.StopAsync();
            _logger.LogInformation("Stopped and cleared queue.");
            await _statusService.SetReady();
            await FollowupAsync("Stopped and cleared queue.");
        }

        [SlashCommand("skip", "Skips the currently playing track.")]
        public async Task SkipAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            if (player.CurrentTrack is null)
            {
                await FollowupAsync("Nothing is playing.");
                return;
            }

            if (player.Queue.Count == 0)
            {
                await player.Queue.ClearAsync();
                await player.StopAsync();
                _logger.LogInformation("Stopped and cleared queue.");
                await _statusService.SetReady();
                await FollowupAsync("Stopped and cleared queue.");
                return;
            }

            var oldTitle = player.CurrentTrack.Title;
            await player.SkipAsync();

            _logger.LogInformation("Skipped: {Title}", oldTitle);

            if (player.CurrentTrack is not null)
            {
                await HandleNextTrackComment(player.CurrentTrack);
            }
            else
            {
                await FollowupAsync($"Skipped `{oldTitle}`.");
            }
        }

        [SlashCommand("nowplaying", "Displays information about the currently playing track.")]
        public async Task NowPlayingAsync()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            var track = player.CurrentTrack;
            if (track is null)
            {
                await FollowupAsync("Playing nothing.");
                return;
            }

            var art = track.ArtworkUri?.ToString() ?? "";
            var duration = track.IsLiveStream ? "Live stream" : CreateDurationString(player);
            var embed = await _embedHandler.CreateNowPlayingEmbed(track.Title, track.Author, track.Uri?.ToString() ?? "", art, duration);

            await FollowupAsync(text: "Now playing:", embed: embed);
        }

        [SlashCommand("np", "Displays information about the currently playing track.")]
        public async Task NpAsync()
            => await NowPlayingAsync();

        [SlashCommand("queue", "Displays the current queue. Use \"full\" for the entire queue.")]
        public async Task QueueAsync([Summary(description: "Use \"full\" to see the entire queue")] string command = "")
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            var nowPlaying = player.CurrentTrack?.Title ?? "";
            var queue = player.Queue.Select(x => x.Track?.Title ?? "Unknown");
            var displayFullQueue = command.ToLower() == "full";
            var embed = await _embedHandler.CreateQueueEmbed(nowPlaying, queue, displayFullQueue);

            await FollowupAsync(text: player.CurrentTrack is null
                ? "Nothing is playing."
                : "Queue:", embed: embed);
        }

        [SlashCommand("clearqueue", "Clears the queue.")]
        public async Task ClearQueue()
        {
            await DeferAsync();
            var player = await GetPlayerAsync(false);
            if (player is null) return;

            await player.Queue.ClearAsync();
            await FollowupAsync("Queue cleared.");
        }

        [SlashCommand("meme", "Calls random meme soundbyte. Add a search query to search.")]
        public async Task RandomMeme([Summary(description: "Search query for a specific meme")] string meme = "")
        {
            await DeferAsync();

            var url = string.IsNullOrWhiteSpace(meme)
                ? await _memeService.GetRandomMeme()
                : await _memeService.GetMeme(meme);

            if (!string.IsNullOrWhiteSpace(url))
            {
                await HandlePlay(url, true);
                return;
            }

            _logger.LogInformation("No meme sound clip for {Meme}.", meme);
            await FollowupAsync($"No meme sound clip found for `{meme}`.");
        }

        private async Task HandlePlay(string searchQuery, bool playImmediately)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await FollowupAsync("Please provide search terms.");
                return;
            }

            var player = await GetPlayerAsync(connectToVoiceChannel: true);
            if (player is null) return;

            var seekTime = GetUrlParameterTime(searchQuery);
            var isUrl = Uri.TryCreate(searchQuery, UriKind.Absolute, out _);
            var searchMode = isUrl ? TrackSearchMode.None : TrackSearchMode.YouTube;

            // Handle playlists (URL only)
            if (isUrl)
            {
                var loadResult = await _audioService.Tracks.LoadTracksAsync(searchQuery, searchMode);

                if (loadResult.Playlist is not null && loadResult.Tracks.Length > 1)
                {
                    foreach (var t in loadResult.Tracks)
                    {
                        await player.PlayAsync(t);
                    }
                    await FollowupAsync($"Enqueued {loadResult.Tracks.Length} songs.");
                    return;
                }
            }

            var track = await _audioService.Tracks.LoadTrackAsync(searchQuery, searchMode);
            if (track is null)
            {
                _logger.LogInformation("Couldn't find anything for {Query}.", searchQuery);
                await FollowupAsync($"Couldn't find anything for `{searchQuery}`.");
                return;
            }

            var properties = seekTime != TimeSpan.Zero
                ? new TrackPlayProperties(StartPosition: seekTime)
                : default;

            var shouldPlayNow = playImmediately || (player.CurrentTrack?.IsLiveStream ?? false);

            if (shouldPlayNow && player.CurrentTrack is not null)
            {
                await PlayTrackImmediately(player, track, properties);
            }
            else
            {
                var position = await player.PlayAsync(track, properties: properties);
                if (position == 0)
                {
                    await HandleNextTrackComment(track);
                }
                else
                {
                    var art = track.ArtworkUri?.ToString() ?? "";
                    var embed = await _embedHandler.CreatePlayEmbed(track.Title, track.Author, track.Uri?.ToString() ?? "", art);
                    await FollowupAsync(text: "Queued:", embed: embed);
                }
            }
        }

        private async Task PlayTrackImmediately(ResumableQueuedPlayer player, LavalinkTrack track, TrackPlayProperties properties)
        {
            if (player.State == PlayerState.Paused)
            {
                await player.ResumeAsync();
            }

            // Save current track at front of queue so it resumes after the interruption
            if (player.CurrentTrack is not null && !player.CurrentTrack.IsLiveStream)
            {
                var resumePosition = player.Position?.Position ?? TimeSpan.Zero;
                await player.Queue.InsertAsync(0, new ResumableTrackQueueItem(player.CurrentTrack, resumePosition));
            }

            // Play new track immediately (replaces current track, bypasses queue)
            await player.PlayAsync(track, enqueue: false, properties: properties);

            await HandleNextTrackComment(track);
        }

        private string CreateDurationString(ResumableQueuedPlayer player)
        {
            var track = player.CurrentTrack;
            if (track is null) return "";

            var position = player.Position?.Position ?? TimeSpan.Zero;
            var duration = track.Duration;

            var durationStringFormat = duration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
            return $"{position.ToString(durationStringFormat)}/{duration.ToString(durationStringFormat)}";
        }

        private TimeSpan GetUrlParameterTime(string searchQuery)
        {
            if (!Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri))
                return TimeSpan.Zero;

            var queryString = HttpUtility.ParseQueryString(uri.Query);
            var tValue = queryString.Get("t");
            if (tValue is null || !int.TryParse(tValue, out var seconds))
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(seconds);
        }

        private async Task HandleNextTrackComment(LavalinkTrack track)
        {
            var art = track.ArtworkUri?.ToString() ?? "";
            var embed = await _embedHandler.CreatePlayEmbed(track.Title, track.Author, track.Uri?.ToString() ?? "", art);

            _logger.LogInformation("Playing now: {Title}", track.Title);
            await FollowupAsync(text: "Playing now:", embed: embed);
        }
    }
}
