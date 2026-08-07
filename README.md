<div align="center">
	<img src="readme/top.png" />
	<p>Music and meme bot for Discord<p>
</div>


---

## 🤔About
Brotherman Bill (aka BrothermanBill) came from the need to fill Discord rooms with a little somethin' somethin'. Turns out, Discord bots aren't too bad to write yourself and BrothermanBill was born.

✨ Read all about this in my post: [Writing a .NET Music Discord Bot for a Raspberry Pi Zero 2 W: Brotherman Bill](https://www.nikouusitalo.com/blog/writing-a-net-music-discord-bot-for-a-raspberry-pi-zero-2-w-brotherman-bill/) ✨

## 📝Features
- 🎧Audio
	- `/play <query>` — queue a song (YouTube, SoundCloud, Vimeo, Twitch)
	- `/playnow <query>` — play immediately, then resume where the previous song left off
	- `/pause` / `/resume` — pause and resume playback
	- `/stop` — stop playback
	- `/skip` — skip to the next song in the queue
	- `/seek <time>` — seek by a relative offset (e.g. `300`, `-1:00`, `1:30:00`)
	- `/seekto <time>` — seek to an absolute position (e.g. `300`, `1:00`, `1:30:00`)
		- Both accept a plain number of seconds, `mm:ss`, or `h:mm:ss`. Only `/seek` accepts a negative value.
	- `/nowplaying` (`/np`) — show currently playing track with artwork
	- `/queue [full]` — show the queue (10 items by default; set `full` to show every track)
	- `/clearqueue` — clear the queue
	- `/join` / `/leave` — join or leave a voice channel
	- Auto-disconnects when the last user leaves the voice channel
- 😀Memes
	- `/meme [query]` — play a random or searched meme soundbyte from [MyInstants](https://www.myinstants.com/)
- 🧰Admin
	- `/ping` — latency to Discord servers
	- `/uptime` — bot uptime
	- `/setgame <status>` — set the bot's Discord status
	- `/kkona` — posts the KKona emote
	- `/cum` — health check
	- `/pick <words>` — randomly pick one word from a space-separated list

## Setup

### .NET
Requires [.NET 10 or higher](https://dotnet.microsoft.com/en-us/download).

### Build
```
dotnet build
```

### Deploying to a Raspberry Pi 4

1. **Install Java on the Pi:**
   ```
   sudo apt update
   sudo apt install openjdk-21-jre-headless
   ```

2. **On your dev PC, publish the bot:**
   ```
   dotnet publish -c Release -r linux-arm64 --self-contained
   ```

3. **Copy the published output to the Pi** (e.g. via VNC) into `/home/pi/BrothermanBill/`.

4. **Fix Windows line endings and make scripts executable:**
   ```
   cd /home/pi/BrothermanBill
   sed -i 's/\r$//' install.sh start.sh stop.sh restart.sh uninstall.sh brothermanbill.service
   chmod +x install.sh start.sh stop.sh restart.sh uninstall.sh BrothermanBill
   ```

5. **Update the Discord bot token** in `/home/pi/BrothermanBill/appsettings.json`.

6. **Monitor with:**
   ```
   journalctl -u brothermanbill -n 200 --no-pager
   ```

### Managing the service
- `./start.sh` — start the bot
- `./stop.sh` — stop the bot
- `./restart.sh` — restart the bot
- `./install.sh` — install as a service (runs on boot)
- `./uninstall.sh` — remove the service

### Deploying to Ubuntu with Docker

Runs as two containers (the bot + official Lavalink) via Docker Compose, the main dev machine with a single script over SSH. Debugging with Visual Studio continues to spawn the child Lavalink process.

**One-time server setup:**
```bash
sudo mkdir -p /opt/brothermanbill
sudo chown "$USER:$USER" /opt/brothermanbill
printf 'DiscordBotToken=YOUR_TOKEN\n' > /opt/brothermanbill/.env
chmod 600 /opt/brothermanbill/.env
sudo usermod -aG docker "$USER"
```
The Discord bot token lives only in `.env` on the server

**Deploy (from the dev machine, repeatable):**
```
./deploy.ps1
```
It prompts for the server host and SSH user (remote path defaults to `/opt/brothermanbill`), then: builds the image, `scp`s the tarball + `docker-compose.yml` + `application.yml` to the server, `docker load`s and `docker compose up -d`s over SSH, and removes the tarball locally and remotely.

Note: Assumes key-based SSH.

**Manage (on the server):**
```
docker compose logs -f bot     # follow logs
docker compose down            # stop and remove both containers
```

To upgrade Lavalink later, bump the `ghcr.io/lavalink-devs/lavalink` tag in `docker-compose.yml` (keep `application.yml`'s `youtube-plugin` version in step), then redeploy.

## 🧱Dependencies
- [Discord.Net](https://github.com/discord-net/Discord.Net)
- [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET)
- [Lavalink](https://github.com/lavalink-devs/Lavalink)

## 🎙️Namesake
[The classic TerribleTim song, *Brotherman Bill*](https://www.youtube.com/watch?v=qkUVToIfrKg)


*Yes that is the KKona emote, not Brotherman Bill*
