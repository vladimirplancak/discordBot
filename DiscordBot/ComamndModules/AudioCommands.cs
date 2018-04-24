using Discord;
using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    public class AudioCommands : ModuleBase
    {
        private readonly AudioService _audioService;
        public AudioCommands(AudioService service)
        {
            _audioService = service;
            //_audioService.OnFinishDownloading += _audioService_OnFinishDownloading;
            //_audioService.OnStartDownloading += _audioService_OnStartDownloading;
            //_audioService.OnStartSavingToDisc += _audioService_OnStartSavingToDisc;
            //_audioService.OnFinishSavingToDisc += _audioService_OnFinishSavingToDisc;
            //_audioService.OnStartConverting += _audioService_OnStartConverting;
            //_audioService.OnFinishConverting += _audioService_OnFinishConverting;
        }

        private void _audioService_OnFinishConverting(object sender, string e)
        {
            ReplyAsync($"```Finished converting```");
        }

        private void _audioService_OnStartConverting(object sender, string e)
        {
            ReplyAsync($"```Started converting```");
        }

        private void _audioService_OnFinishSavingToDisc(object sender, string e)
        {
            ReplyAsync($"```Finished saving to disc```");
        }

        private void _audioService_OnStartSavingToDisc(object sender, string e)
        {
            ReplyAsync($"```Started saving to disc```");
        }

        private void _audioService_OnStartDownloading(object sender, string e)
        {
            ReplyAsync($"```Started downloading```");
        }

        private void _audioService_OnFinishDownloading(object sender, string e)
        {
            ReplyAsync($"```Finished downloading```");
        }


        // You *MUST* mark these commands with 'RunMode.Async'
        // otherwise the bot will not respond until the Task times out.
        [Command("join", RunMode = RunMode.Async), Summary("Joins channel in which author of the command is currently in.")]
        public async Task JoinCmd()
        {
            //await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        }

        // Remember to add preconditions to your commands,
        // this is merely the minimal amount necessary.
        // Adding more commands of your own is also encouraged.
        [Command("leave", RunMode = RunMode.Async), Summary("Removes bot from any voice channel")]
        public async Task LeaveCmd()
        {
            //await _service.LeaveAudio(Context.Guild);
        }

        [Command("play", RunMode = RunMode.Async), Summary("Starts playing songs from the queue")]
        public async Task PlayCmd()
        {
            _audioService.StartPlaying();
        }


        [Command("queue", RunMode = RunMode.Async), Summary("Queues new song.")]
        public async Task Queue([Remainder] string link)
        {
            var addedItem = _audioService.AddToQueue(link);

            if (addedItem != null)
            {
                await ReplyAsync($"```{ addedItem.Name } - Added to queue!```");
            }
            else
            {
                await ReplyAsync($"```{ link } - was not able to be processed!```");
            }
        }

        [Command("queuelist", RunMode = RunMode.Async), Summary("Gets queue list.")]
        public async Task QueueList([Remainder] string song)
        {

        }

    }
}
