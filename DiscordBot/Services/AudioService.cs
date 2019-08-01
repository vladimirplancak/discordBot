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

namespace DiscordBot.Services
{
    public class AudioService
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region private fields used for settings
        //TODO: Get from config file.
        private readonly static string _musicStorage = @"D:/youtubemusic/";
        private readonly static string _musicPlayListStorage = @"D:/youtubeMusicPlayList/";
        public bool PopulateSystemPlayList = false;
        #endregion

        private readonly DiscordSocketClient _client;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;
        private static ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(true);

        private readonly ConcurrentDictionary<int, SongInQueue> _queue = new ConcurrentDictionary<int, SongInQueue>();
        private  ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

        #region #Private fields used by queue
        private static Task _queueTask;
        private int? _skipToSong = null;
        private static bool _skip = false;
        private static bool _queueIsRunning = false;
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

        }

        public AudioService(DiscordSocketClient client)
        {
            _client = client;
            _tcs = new TaskCompletionSource<bool>();
            _disposeToken = new CancellationTokenSource();

            _client.Ready += () =>
            {
                if (PopulateSystemPlayList)
                    GetSongsFromPlayList();

                return Task.CompletedTask;
            };

            _client.Disconnected += _client_Disconnected;
        }

        private Task _client_Disconnected(Exception arg)
        {
            LogMessage("Discord client disconnected, reset connected channels");
            ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

            return Task.CompletedTask;

        }

        #endregion ctor

        private void StartQueueThread(ICommandContext context, int? underNumber = null)
        {
            if (ConnectedChannels.IsEmpty)
            {
                LogMessage("Add client to the chanel, then start queue!");
                return;
            }

            _queueIsRunning = true;

            LogMessage($"Starting queue! with queue items: { _queue.Count }");

            while (!_queue.IsEmpty && _queueIsRunning)
            {
                try
                {
                    //GetFirstSong
                    if (TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong))
                    {
                        SongInQueue song = kvSong.Value;

                        LogMessage($"Song found in the queue { song.ToString() }");

                        song.IsPlaying = true;
                        SendAudio(context.Guild, song).Wait();
                        song.IsPlaying = false;

                        LogMessage($"Finished playing song: { song.ToString() }");

                        if (_queue.TryRemove(kvSong.Key, out SongInQueue removedSong))
                        {
                            LogMessage($"Song successfully removed from queue: { removedSong.ToString() }");
                            if (!song.IsPlayList && File.Exists(song.FilePath))
                            {
                                File.Delete(song.FilePath);
                            }
                        }
                        else
                        {
                            LogMessage($"Failed to remove song from the queue: { removedSong.ToString() }");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _queueIsRunning = false;
                    throw ex;
                }
            }

            _queueIsRunning = false;
            LogMessage("Queue is empty, stopping queue...");

            bool TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong)
            {
                string loggingString = $"Current queue is: { Environment.NewLine }";
                _queue.ToList().ForEach(it => {
                    loggingString += $"[{it.Key}]. { it.Value.Name }{ Environment.NewLine }";
                });
                LogMessage(loggingString);

                kvSong = _queue.FirstOrDefault();
                SongInQueue song = kvSong.Value;

                if (_skipToSong.HasValue && _queue.TryGetValue(_skipToSong.Value, out SongInQueue songToSkipTo))
                {
                    song = songToSkipTo;
                    kvSong = new KeyValuePair<int, SongInQueue>(_skipToSong.Value, songToSkipTo);
                    _skipToSong = null;
                }

                return song != null;
            }
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

        private string GetPropperName(Video vid)
        {
            return vid.FullName.Replace(" - YouTube" + vid.FileExtension, "");
        }

        private SongInQueue PrepareSong(string link)
        {
            try
            {
                LogMessage("Started processing file " + link);
                string guid = Guid.NewGuid().ToString();
                SongInQueue result = new SongInQueue();

                YouTube youtube = YouTube.Default;
                string fullFilePath = _musicStorage + guid;
                Video vid = youtube.GetVideo(link);
                LogMessage("Finished downloading file " + link);
                result.Name = GetPropperName(vid);

                File.WriteAllBytes(fullFilePath, vid.GetBytes());
                LogMessage("Finished saving file to the disc.");

                var inputFile = new MediaFile(fullFilePath);
                var fullFilePathWithExtension = $"{fullFilePath}.mp3";
                var outputFile = new MediaFile(fullFilePathWithExtension);

                result.FilePath = fullFilePathWithExtension;

                var convertSW = new Stopwatch();
                using (Engine convertEngine = new Engine())
                {
                    convertEngine.GetMetadata(inputFile);
                    convertSW.Start();
                    convertEngine.Convert(inputFile, outputFile);
                    convertSW.Stop();
                }

                LogMessage($"Finished convering. Time: { convertSW.Elapsed.ToString() }");

                if (File.Exists(fullFilePath))
                {
                    File.Delete(fullFilePath);
                }
                else
                {
                    throw new NotImplementedException();
                }

                LogMessage("Finished processing file " + link);
                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to prepare file:  { ex }");
            }

            return null;
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

        private void LogMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("hh:mm:ss")}] - [{Thread.CurrentThread.ManagedThreadId}] {message}");
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
                            LogMessage($"Task Canceled exception { tce.ToString() }");
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
                LogMessage("Started populating playlist.");
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

                    LogMessage($"Adding song { songToQueue.Name }");
                    _queue.TryAdd(_queue.Count() + 1, songToQueue);
                }
                LogMessage("Finished populating playlist.");
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
                LogMessage($"Added { songInQueue.Name } to queue.");

                return songInQueue;
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                return null;
            }
        }

        public async Task StartQueue(ICommandContext context, int? underNumber = null)
        {
            if (_queueTask == null)
            {
                LogMessage("Queue task is null, create new one...");
                _queueTask = Task.Factory.StartNew(() => StartQueueThread(context, underNumber));
                
            }
            else if (_queueTask.IsCompleted)
            {
                LogMessage($"Queue task is no longer alive, create new one and start it again... {_queueTask.Status }");
                _queueTask = Task.Factory.StartNew(() => StartQueueThread(context, underNumber));
            }
            else
            {
                LogMessage("Queue task is already running!");
            }
        }

        public async Task StopQueue()
        {
            _queueIsRunning = false;
            _skip = true;
        }

        public (SongInQueue song, bool IsSuccess) TrySkip(IGuild guild, IVoiceChannel target, int? underNumber = null)
        {
            LogMessage($"Skip requested... (under number: { underNumber }.");
            _skipToSong = underNumber;

            KeyValuePair<int, SongInQueue> skippedKvSong = _queue.FirstOrDefault(it => it.Value.IsPlaying);
            SongInQueue skippedSong = skippedKvSong.Value;

            if (skippedSong == null)
                throw new ArgumentNullException(nameof(skippedSong));


            if (underNumber.HasValue && skippedKvSong.Key == underNumber.Value)
                return (skippedSong, false);

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
                    LogMessage($"Connected to voice on {guild.Name}.");
                    retVal = true;
                }
                else
                {
                    LogMessage($"Faild to connected to voice on {guild.Name}.");
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
                LogMessage("Pausing current song streaming...");
                _manualResetEventSlim.Reset();
            }
            else
            {
                LogMessage("Unpause current song streaming...");
                _manualResetEventSlim.Set();
            }
        }

    }
}
