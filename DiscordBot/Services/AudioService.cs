using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Models;
using MediaToolkit;
using MediaToolkit.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoLibrary;
using DiscordBot.Extensions;
using log4net;
using System.Reflection;
using DiscordBot.YoutubeDownlaoder;

namespace DiscordBot.Services
{
    public class CustomConcurrentDictionary<T1, T2> : ConcurrentDictionary<T1, T2>
    {
        public override string ToString()
        {
            string loggingString = $"Current queue is: { Environment.NewLine }";

            foreach (var key in Keys)
            {
                loggingString += $"[{key}]. { this[key].ToString() }{ Environment.NewLine }";
                
            }

            return loggingString;
        }

    }

    public class AudioService
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(AudioService));

        #region private fields used for settings
        //TODO: Get from config file.
        private readonly static string _musicStorage = @"D:/youtubemusic/";
        private readonly static string _musicPlayListStorage = @"D:/youtubeMusicPlayList/";
        public bool PopulateSystemPlayList = false;
        #endregion

        private readonly DiscordSocketClient _client;
        private readonly IYoutubeDownloaderClient _youtubeDownloaderClient;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;
        private static ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(true);

        private ConcurrentDictionary<int, SongInQueue> _queue = new CustomConcurrentDictionary<int, SongInQueue>();
        private ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

        #region #Private fields used by queue
        private static Task _queueTask;
        private int? _skipToSong = null;
        private static bool _skip = false;
        private static bool _queueIsRunning = false;
        private int keyOfPreviousSong = 0;

        #endregion

        public IEnumerable<KeyValuePair<int, SongInQueue>> QueueItems
        {
            get
            {
                return _queue.AsEnumerable();
            }
        }

        #region ctor
        static AudioService()
        {
            DeleteOldFiles();
            _log.Info("Static constructor started...");
        }

        public AudioService(DiscordSocketClient client, IYoutubeDownloaderClient youtubeDownloaderClient)
        {
            _client = client;
            _youtubeDownloaderClient = youtubeDownloaderClient;
            _tcs = new TaskCompletionSource<bool>();
            _disposeToken = new CancellationTokenSource();

            _client.Ready += () =>
            {
                if (PopulateSystemPlayList)
                    GetSongsFromPlayList();

                return Task.CompletedTask;
            };

            client.LatencyUpdated += Client_LatencyUpdated;

            _client.Disconnected += Client_Disconnected;
        }

        private Task Client_LatencyUpdated(int arg1, int arg2)
        {
            _log.Info($"Latency update. { arg1 } -> { arg2 }");

            return Task.CompletedTask;
        }

        private Task Client_Disconnected(Exception arg)
        {
            _log.Warn("Discord client disconnected, reset connected channels");

            foreach (var connectedChannel in ConnectedChannels)
            {
                connectedChannel.Value.StopAsync().Wait();
            }

            ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

            return Task.CompletedTask;
        }

        #endregion ctor

        private void StartQueueThread(ICommandContext context, int? underNumber = null)
        {
            if (ConnectedChannels.IsEmpty)
            {
                _log.Info("Add client to the chanel, then start queue!");
                return;
            }

            _queueIsRunning = true;

            _log.Info($"Starting queue! with queue items: { _queue.Count }");

            while (!_queue.IsEmpty && _queueIsRunning)
            {
                try
                {
                    //GetFirstSong
                    if (TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong))
                    {
                        SongInQueue song = kvSong.Value;
                        keyOfPreviousSong = kvSong.Key;

                        _log.Info($"Playing { song.ToString() } found in the queue...");

                        song.IsPlaying = true;
                        SendAudio(context.Guild, song).Wait();
                        song.IsPlaying = false;

                        _log.Info($"Finished playing song: { song.ToString() }.");
                        RemoveSongFromQueue(kvSong.Key);
                    }
                }
                catch (Exception ex)
                {
                    _queueIsRunning = false;
                    throw ex;
                }
            }

            _queueIsRunning = false;
            _log.Info("Queue is empty, stopping queue...");

            bool TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong)
            {
                _log.Info(_queue.ToString());

                kvSong = _queue.FirstOrDefault();
                SongInQueue song = kvSong.Value;

                if (_skipToSong.HasValue && _queue.TryGetValue(_skipToSong.Value, out SongInQueue songToSkipTo))
                {
                    song = songToSkipTo;
                    kvSong = new KeyValuePair<int, SongInQueue>(_skipToSong.Value, songToSkipTo);
                    _skipToSong = null;
                }
                else if(underNumber.HasValue && _queue.TryGetValue(underNumber.Value, out SongInQueue songUnderIndex))
                {
                    song = songUnderIndex;
                    kvSong = new KeyValuePair<int, SongInQueue>(underNumber.Value, songUnderIndex);
                }

                return song != null;
            }
        }

        private void RemoveSongFromQueue(int key)
        {
            _log.Info($"Trying to remove song from queue key: { key } curretQueue: { _queue.ToString() }");
            if (_queue.TryRemove(key, out SongInQueue removedSong))
            {
                _log.Info($"Successfully removed song from the queue key: { key } curretQueue: { _queue.ToString() }");

                if (!removedSong.IsPlayList && File.Exists(removedSong.FilePath))
                {
                    File.Delete(removedSong.FilePath);
                }
            }
            else
            {
                _log.Info($"Failed to removed song from the queue key: { key } curretQueue: { _queue.ToString() }");
            }

            //Update old list 
            var oldList = new List<KeyValuePair<int, SongInQueue>>();

            int i = 1;
            foreach (var item in _queue)
            {
                oldList.Add(new KeyValuePair<int, SongInQueue>(i, item.Value));
                i++;
            }

            _log.Info($"Queue after update: { _queue.ToString() }");

            _queue = new ConcurrentDictionary<int, SongInQueue>(oldList);
        }

        private static void DeleteOldFiles()
        {
            var files = Directory.GetFiles(_musicStorage).ToList();
            files.ForEach(it =>
            {
                if (File.Exists(it))
                {
                    File.Delete(it);
                }
                else
                {
                    throw new NotImplementedException();
                }
            });
        }

        private SongInQueue PrepareSong(string link)
        {
            return _youtubeDownloaderClient.DownloadSong(link);
        }

        private static Process GetFfmpegProcess(string path)
        {
            ProcessStartInfo ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -xerror -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,    //TODO: true or false?
                RedirectStandardOutput = true
            };
            return Process.Start(ffmpeg);
        }

        private async Task SendAudio(IGuild guild, SongInQueue song)
        {

            if (ConnectedChannels.TryGetValue(guild.Id, out IAudioClient _audio))
            {
                using (Process ffmpeg = GetFfmpegProcess(song.FilePath))
                using (Stream output = ffmpeg.StandardOutput.BaseStream)
                using (AudioOutStream AudioOutStream = _audio.CreatePCMStream(AudioApplication.Music))
                {
                    //Adjust?
                    int bufferSize = 4096;
                    bool fail = false;
                    bool exit = false;
                    byte[] buffer = new byte[bufferSize];

                    while (!_skip && !fail && !_disposeToken.IsCancellationRequested && !exit)
                    {
                        try
                        {
                            _manualResetEventSlim.Wait();
                            int read = await output.ReadAsync(buffer, 0, bufferSize, _disposeToken.Token);

                            if (read == 0)
                                break;

                            await AudioOutStream.WriteAsync(buffer, 0, read, _disposeToken.Token);

                        }
                        catch (TaskCanceledException tce)
                        {
                            _log.Info($"Task Canceled exception { tce.ToString() }");
                            exit = true;
                        }
                        catch
                        {
                            fail = true;
                        }
                    }

                    _skip = false;
                }

                Thread.CurrentThread.Join(TimeSpan.FromSeconds(5));
            }
        }

        public void GetSongsFromPlayList()
        {
            if (!Directory.Exists(_musicPlayListStorage))
            {
                Directory.CreateDirectory(_musicPlayListStorage);
            }
            else
            {
                _log.Info("Started populating playlist.");
                var files = Directory.GetFiles(_musicPlayListStorage);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var songToQueue = new SongInQueue()
                    {
                        FilePath = file,
                        Name = fileName,
                        PersistInQueue = false,
                        IsPlayList = true,
                        QueueBy = _client.CurrentUser
                    };

                    _log.Info($"Adding song { songToQueue.Name }");
                    _queue.TryAdd(_queue.Count() + 1, songToQueue);
                }
                _log.Info("Finished populating playlist.");
            }
        }

        public SongInQueue AddToQueue(string link, IUser user, bool persist = false)
        {

            try
            {
                var songInQueue = PrepareSong(link);
                songInQueue.QueueBy = user;
                songInQueue.PersistInQueue = persist;
                _queue.TryAdd(_queue.Count + 1, songInQueue);
                _log.Info($"Added { songInQueue.Name } to queue.");

                return songInQueue;
            }
            catch (Exception ex)
            {
                _log.Info(ex.ToString());
                return null;
            }
        }

        public async Task StartQueue(ICommandContext context, int? underNumber = null)
        {
            if (_queueTask == null)
            {
                _log.Info("Queue task is null, create new one...");
                _queueTask = Task.Factory.StartNew(() => StartQueueThread(context, underNumber));
                
            }
            else if (_queueTask.IsCompleted)
            {
                _log.Info($"Queue task is no longer alive, create new one and start it again... {_queueTask.Status }");
                _queueTask = Task.Factory.StartNew(() => StartQueueThread(context, underNumber));
            }
            else
            {
                _log.Info("Queue task is already running!");
            }
        }

        public async Task StopQueue()
        {
            _queueIsRunning = false;
            _skip = true;
        }

        public (SongInQueue song, bool IsSuccess) TrySkip(IGuild guild, IVoiceChannel target, int? underNumber = null)
        {
            _log.Info($"Skip requested... (under number: { underNumber }).");
            _skipToSong = underNumber;

            //To match adjusted queue after refreshing indexes
            if (_skipToSong > 1 && _skipToSong > keyOfPreviousSong)
            {
                _skipToSong--;
            }

            _log.Info($"Skip requested after modification... (under number: { underNumber }).");


            KeyValuePair<int, SongInQueue> skippedKvSong = _queue.FirstOrDefault(it => it.Value.IsPlaying);
            SongInQueue skippedSong = skippedKvSong.Value;
            


            if (skippedSong == null)
                throw new ArgumentNullException(nameof(skippedSong));


            if (underNumber.HasValue && skippedKvSong.Key == underNumber.Value)
                return (skippedSong, IsSuccess: false);

            _skip = true;

            return (skippedSong, true);
        }

        public async Task<bool> JoinAudio(IGuild guild, IVoiceChannel voiceChannel)
        {
            var retVal = false;

            if (ConnectedChannels.TryGetValue(guild.Id, out IAudioClient client))
            {
                retVal = false;
            }
            if (voiceChannel.Guild.Id != guild.Id)
            {
                retVal = false;
            }

            try
            {
                var audioClient = await voiceChannel.ConnectAsync();


                if (ConnectedChannels.TryAdd(guild.Id, audioClient))
                {
                    _log.Info($"Connected to voice on {guild.Name}.");
                    retVal = true;
                }
                else
                {
                    _log.Info($"Faild to connected to voice on {guild.Name}.");
                    retVal = false;
                }

                return retVal;
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }

        public async Task LeaveAudio(IGuild guild)
        {
            _queueIsRunning = false;

            if (ConnectedChannels.TryRemove(guild.Id, out IAudioClient client))
            {
                await client.StopAsync();
            }
        }

        public void PauseAudio()
        {
            if (_manualResetEventSlim.IsSet)
            {
                _log.Info("Pausing current song streaming...");
                _manualResetEventSlim.Reset();
            }
            else
            {
                _log.Info("Unpause current song streaming...");
                _manualResetEventSlim.Set();
            }
        }

    }
}
