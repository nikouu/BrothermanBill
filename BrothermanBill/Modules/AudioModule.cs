﻿using BrothermanBill.Services;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
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

    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly AudioService _audioService;
        private readonly EmbedHandler _embedHandler;
        private readonly ILogger _logger;
        private readonly MemeService _memeService;

        private LavaPlayer Player => _lavaNode.GetPlayer(Context.Guild);

        public AudioModule(LavaNode lavaNode, AudioService audioService, MemeService memeService, EmbedHandler embedHandler, ILogger<AudioModule> logger)
        {
            _lavaNode = lavaNode;
            _audioService = audioService;
            _memeService = memeService;
            _embedHandler = embedHandler;
            _logger = logger;
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
            }
            catch (Exception exception)
            {
                await ReplyAsync(exception.Message);
            }
        }

        [Command("Play")]
        public async Task PlayAsync([Remainder] string searchQuery)
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

            if (Player?.Track?.IsStream == true)
            {
                await PlayNowAsync(searchQuery);
                return;
            }

            var searchResponse = await LavaLinkSearch(searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                //await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
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
                Player.Queue.Enqueue(track);

                var art = await track.FetchArtworkAsync();
                var embed = await _embedHandler.CreatePlayEmbed(track?.Title, track?.Author, track?.Url, art);
                await ReplyAsync(message: "Queued:", embed: embed);
            }

            if (Player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                return;
            }

            Player.Queue.TryDequeue(out var lavaTrack);
            await Player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.ShouldPause = false;
            });
        }

        [Command("PlayNow")]
        public async Task PlayNowAsync([Remainder] string searchQuery)
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

            var searchResponse = await LavaLinkSearch(searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                await ReplyAsync($"Cannot Play Now a playlist");
                return;
            }
            else
            {
                var track = searchResponse.Tracks.FirstOrDefault();
                await AddToFront(track);

                if (Player.PlayerState is PlayerState.Playing)
                {
                    var (oldTrack, currentTrack) = await Player.SkipAsync();
                }

                _logger.LogInformation($"Playing now:{track?.Title}");
            }

            if (Player.PlayerState is PlayerState.Playing or PlayerState.Paused)
            {
                return;
            }

            Player.Queue.TryDequeue(out var lavaTrack);
            await Player.PlayAsync(x =>
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
                _logger.LogInformation($"Resumed: {player.Track.Title}");
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
                _logger.LogInformation("Attempted stop on already stopped PlayerState");
                return;
            }

            try
            {
                await player.StopAsync();
                _logger.LogInformation("Queue finished.");
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
                    // fix bug when something is playing, like a meme then skipping fails
                    var (oldTrack, currentTrack) = await player.SkipAsync();

                    var art = await currentTrack.FetchArtworkAsync();
                    var embed = await _embedHandler.CreatePlayEmbed(currentTrack?.Title, currentTrack?.Author, currentTrack?.Url, art);

                    await ReplyAsync(message: "Now Playing:", embed: embed);
                    _logger.LogInformation($"Skipped: {oldTrack.Title}\nNow Playing: {player.Track.Title}");
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

            var track = Player.Track;
            var art = await track.FetchArtworkAsync();
            var duration = CreateDurationString(track);
            var embed = await _embedHandler.CreateNowPlayingEmbed(track?.Title, track?.Author, track?.Url, art, duration);

            await ReplyAsync(message: "Now playing:", embed: embed);
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
            Player.Queue.Clear();
            await ReplyAsync("Queue cleared.");
        }

        [Command("Meme"), Alias("m")]
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
            return searchResponse;
        }

        private Task AddToFront(IEnumerable<LavaTrack> tracks)
        {
            var currentTrack = Player.Track;
            var trackList = Player.Queue.ToList();

            trackList = trackList.Prepend(currentTrack).ToList();

            trackList.InsertRange(0, tracks);

            Player.Queue.Clear();
            Player.Queue.Enqueue(trackList);

            return Task.CompletedTask;
        }

        private Task AddToFront(LavaTrack track)
        {
            var currentTrack = Player.Track;
            var trackList = Player.Queue.ToList();

            trackList = trackList.Prepend(currentTrack).ToList();
            trackList = trackList.Prepend(track).ToList();

            Player.Queue.Clear();
            Player.Queue.Enqueue(trackList);

            return Task.CompletedTask;
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
    }
}
