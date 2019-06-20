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
        private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

        //TODO: Get from config file.
        private readonly static string _musicStorage = @"D:/youtubemusic/";
        private readonly static string _musicPlayListStorage = @"D:/youtubeMusicPlayList/";
        private readonly DiscordSocketClient _client;
        private bool Pause
        {
            get => _internalPause;
            set
            {
                new Thread(() => _tcs.TrySetResult(value)).Start();
                _internalPause = value;
            }
        }
        private bool _internalPause;


        private static bool _skip = false;

        private int? _skipToSong = null;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;
        private static bool QueueIsRunning = false;
        private readonly object locker = new object();
        public bool PopulateSystemPlayList = false;
        private static Thread QueueThread;

        public ConcurrentDictionary<int, SongInQueue> Queue { get; } = new ConcurrentDictionary<int, SongInQueue>();

        public event EventHandler<string> OnQueueEmpty;

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
                if(PopulateSystemPlayList)
                    GetSongsFromPlayList();

                return Task.CompletedTask;
            };
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
                    Queue.TryAdd(Queue.Count() +1, songToQueue);
                }
                LogMessage("Finished populating playlist.");
            }
        }

        private async Task StartQueueThread(ICommandContext context, int? underNumber = null)
        {
            if (ConnectedChannels.IsEmpty)
            {
                LogMessage("Add client to the chanel, then start queue!");
                return;
            }
                
            QueueIsRunning = true;

            LogMessage($"Starting queue! with queue items: { Queue.Count }");

            while (!Queue.IsEmpty && QueueIsRunning)
            {
                try
                {
                    //GetFirstSong
                    if (TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong))
                    {
                        SongInQueue song = kvSong.Value;

                        LogMessage($"Song found in the queue { song.ToString() }");

                        song.IsPlaying = true;
                        await SendAudio(context.Guild, song);
                        song.IsPlaying = false;

                        LogMessage($"Finished playing song: { song.ToString() }");

                        if (Queue.TryRemove(kvSong.Key, out SongInQueue removedSong))
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
                    QueueIsRunning = false;
                    throw ex;
                }
            }

            QueueIsRunning = false;
            LogMessage("Queue is empty, stopping queue...");

            bool TryGetSongToPlay(out KeyValuePair<int, SongInQueue> kvSong)
            {
                kvSong = Queue.FirstOrDefault();
                SongInQueue song = kvSong.Value;

                if (_skipToSong.HasValue && Queue.TryGetValue(_skipToSong.Value, out SongInQueue songToSkipTo))
                    song = songToSkipTo;

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

        public SongInQueue AddToQueue(string link, IUser user, bool persist = false)
        {

            try
            {
                var songInQueue = PrepareSong(link);
                songInQueue.QueueBy = user;
                songInQueue.PersistInQueue = persist;
                Queue.TryAdd(Queue.Count + 1, songInQueue);
                LogMessage($"Added { songInQueue.Name } to queue.");

                return songInQueue;
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                return null;
            }
        }

        //private async Task SendAudio(IGuild guild, SongInQueue song)
        //{

        //    if (ConnectedChannels.TryGetValue(guild.Id, out IAudioClient _audio))
        //    {
        //        using (Process ffmpeg = GetFfmpeg(song.FilePath))
        //        using (Stream output = ffmpeg.StandardOutput.BaseStream)
        //        using (AudioOutStream AudioOutStream = _audio.CreatePCMStream(AudioApplication.Music))
        //        {
        //            //Adjust?
        //            int bufferSize = 4096;
        //            bool fail = false;
        //            bool exit = false;
        //            byte[] buffer = new byte[bufferSize];
                    
        //            while (!Skip && !fail && !_disposeToken.IsCancellationRequested && !exit)
        //            {
        //                try
        //                {
        //                    int read = await output.ReadAsync(buffer, 0, bufferSize, _disposeToken.Token);
        //                    if (read == 0)
        //                    {
        //                        //No more data available
        //                        exit = true;
        //                        break;
        //                    }

        //                    await AudioOutStream.WriteAsync(buffer, 0, read, _disposeToken.Token);

        //                    if (Pause)
        //                    {
        //                        bool pauseAgain;

        //                        do
        //                        {
        //                            pauseAgain = await _tcs.Task;
        //                            _tcs = new TaskCompletionSource<bool>();
        //                        } while (pauseAgain);
        //                    }
        //                }
        //                catch (TaskCanceledException)
        //                {
        //                    exit = true;
        //                }
        //                catch
        //                {
        //                    fail = true;
        //                }
        //            }
        //        }
        //    }
        //}

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

                    Thread.Sleep(2 * 1000); // let all buffered data go out.
                    _skip = false;
                }
            }
        }
        
        public async Task StartQueue(ICommandContext context, int? underNumber = null)
        {
            
            if (QueueThread == null)
            {
                LogMessage("QueueThread is null, create new one...");
                QueueThread = new Thread(async () => await StartQueueThread(context, underNumber));
                QueueThread.Start();
            }
            else if (!QueueThread.IsAlive)
            {
                LogMessage("QueuThread is no longer alive, create new one and start it again...");
                QueueThread = new Thread(async () => await StartQueueThread(context, underNumber));
                QueueThread.Start();
            }
            else
            {
                LogMessage("Queue thread is already running!");
            }
        }

        public async Task StopQueue()
        {
            //QueueIsRunning = false;
            _skip = true;
        }

        
        public (SongInQueue song, bool IsSuccess) TrySkip(IGuild guild, IVoiceChannel target, int? underNumber = null)
        {
            _skipToSong = underNumber;

            KeyValuePair<int, SongInQueue> skippedKvSong = Queue.FirstOrDefault(it => it.Value.IsPlaying);
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
            QueueIsRunning = false;

            if (ConnectedChannels.TryRemove(guild.Id, out IAudioClient client))
            {
                await client.StopAsync();
            }
        }

        public void PauseAudio()
        {
            Pause = !Pause;
        }

    }
}
