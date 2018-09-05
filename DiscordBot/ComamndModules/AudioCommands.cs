using Discord;
using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    public class AudioCommands : ModuleBase
    {
        private bool OnQueueEmtpySubscribed = false;
        private readonly AudioService _audioService;
        

        public AudioCommands(AudioService service)
        {
            _audioService = service;
            if (!OnQueueEmtpySubscribed)
            {
                //_audioService.OnQueueEmpty += _audioService_OnQueueEmpty;
                OnQueueEmtpySubscribed = true;
            }
        }

        private void Replay(string msg)
        {
             ReplyAsync($"```{msg}```");
        }

        #region EventHandlers
        private void _audioService_OnQueueEmpty(object sender, string e)
        {
            Replay("Queue is empty!");
        }

        private void _audioService_OnFinishConverting(object sender, string e)
        {
            Replay("Finished converting");
        }

        private void _audioService_OnStartConverting(object sender, string e)
        {
            Replay("Started converting");
        }

        private void _audioService_OnFinishSavingToDisc(object sender, string e)
        {
            Replay("Finished saving to disc");
        }

        private void _audioService_OnStartSavingToDisc(object sender, string e)
        {
            Replay("Started saving to disc");
        }

        private void _audioService_OnStartDownloading(object sender, string e)
        {
            Replay("Started downloading");
        }

        private void _audioService_OnFinishDownloading(object sender, string e)
        {
            Replay("Finished downloading");
        }
        #endregion

        // You *MUST* mark these commands with 'RunMode.Async'
        // otherwise the bot will not respond until the Task times out.
        [Command("join", RunMode = RunMode.Async), Summary("Joins channel in which author of the command is currently in. Or channel by passed id")]
        public async Task JoinCmd([Summary("Id of the voice channel.")] ulong? channelId = null)
        {
            var hasJoined = false;
            var errorMessage = "```Voice channel not found. Please user correct id for voice channel, or join one of the channels that bot has privileges to join and execute command again.```";

            if (channelId.HasValue)
            {
                //Try to get voice by id.
                var channel = await Context.Guild.GetVoiceChannelAsync((ulong)channelId);

                if(channel != null)
                {
                    try
                    {
                        hasJoined = await _audioService.JoinAudio(Context.Guild, channel);
                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine(ex);
                    }
                    
                }
            }

            //Try to join channel that user is currently in.
            if (!hasJoined)
            {
                try
                {
                    hasJoined = await _audioService.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex);
                }
                
            }

            //Still not joined, replay with error.
            if (!hasJoined)
            {
                Replay(errorMessage);
            }
            
        }

        // Remember to add preconditions to your commands,
        // this is merely the minimal amount necessary.
        // Adding more commands of your own is also encouraged.
        [Command("leave", RunMode = RunMode.Async), Summary("Removes bot from any voice channel")]
        public async Task LeaveCmd()
        {
            await _audioService.LeaveAudio(Context.Guild);
        }

        [Command("play", RunMode = RunMode.Async), Summary("Starts playing songs from the queue")]
        public async Task PlayCmd()
        {
            await _audioService.StartQueue(Context);
        }


        [Command("add", RunMode = RunMode.Async), Summary("Queues new song.")]
        public async Task Queue(
            [Summary("Link of the youtube song!")] string link, 
            [Summary("should this song be looped in queue")] bool persist = false
            )
        {
            var addedItem = _audioService.AddToQueue(link, Context.User, persist);

            if (addedItem != null)
            {
                Replay("{ addedItem.Name } - Added to queue!");
            }
            else
            {
                Replay("{ link } - was not able to be processed!");
            }
        }

        [Command("list", RunMode = RunMode.Async), Summary("Gets queue list.")]
        public async Task QueueList()
        {
            var retVal = "\n";

            var i = 1;
            foreach(var song in _audioService.Queue)
            {
                string isPlaying = song.IsPlaying ? " - playing! " : "";
               
                retVal += i + ". " + song.Name + " " + isPlaying + "\n";
                i++;
            }

            Replay(retVal);
        }

        [Command("next", RunMode = RunMode.Async), Summary("Skip current song!")]
        public async Task Next()
        {
            var skippedSong = _audioService.Next(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
            await ReplyAsync($"{  Context.User.Mention } - skipped { skippedSong.Name }!" );
        }

        [Command("pause", RunMode = RunMode.Async), Summary("Pause current song!")]
        public async Task Pause() => _audioService.PauseAudio();

        [Command("playlist", RunMode = RunMode.Async), Summary("Adds whole playlist to the queue!")]
        public async Task CreatePlayList() => _audioService.GetSongsFromPlayList();
    }
}
