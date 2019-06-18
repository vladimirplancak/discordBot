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
        private static readonly PerformanceCounter _ramMemoryCounter;
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


        private bool _skip = false;
        private bool Skip
        {
            get
            {
                return _skip;
            }
            set
            {
                LogMessage($"Setting skip value to { value }");
                _skip = value;
            }
        }

        private int? _skipToSong = null;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;
        private bool QueueIsRunning = false;
        private DateTime OnQueueEmptyCalled;
        private readonly object locker = new object();
        public bool PopulateSystemPlayList = false;

        public ConcurrentDictionary<string, SongInQueue> Queue { get; } = new ConcurrentDictionary<string, SongInQueue>();

        public event EventHandler<string> OnQueueEmpty;

        static AudioService()
        {
            DeleteOldFiles();
            ///_ramMemoryCounter = new PerformanceCounter("Memory", "Available Mbayts", true);
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
                    Queue.TryAdd(Guid.NewGuid().ToString(), songToQueue);
                }
                LogMessage("Finished populating playlist.");
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
            var outputFile = new MediaFile (fullFilePathWithExtension);

            result.FilePath = fullFilePathWithExtension;

            var convertSW = new Stopwatch();
            using (var convertEngine = new Engine())
            {
                convertEngine.GetMetadata(inputFile);
                convertSW.Start();
                convertEngine.Convert(inputFile, outputFile);
                convertSW.Stop();
                //convertEngine.ConvertProgressEvent += ConvertEngine_ConvertProgressEvent;
                
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

        private void ConvertEngine_ConvertProgressEvent(object sender, ConvertProgressEventArgs e)
        {
            //LogMessage($"Current memory usage: { _ramMemoryCounter.NextValue().ToString() } MB");
        }

        private static Process GetFfmpeg(string path)
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
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {message}");
        }

        public SongInQueue AddToQueue(string link, IUser user, bool persist = false)
        {

            try
            {
                var songInQueue = PrepareSong(link);
                songInQueue.QueueBy = user;
                songInQueue.PersistInQueue = persist;
                Queue.TryAdd(Guid.NewGuid().ToString(), songInQueue);
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
                using (Process ffmpeg = GetFfmpeg(song.FilePath))
                using (Stream output = ffmpeg.StandardOutput.BaseStream)
                using (AudioOutStream AudioOutStream = _audio.CreatePCMStream(AudioApplication.Music))
                {
                    //Adjust?
                    int bufferSize = 4096;
                    bool fail = false;
                    bool exit = false;
                    byte[] buffer = new byte[bufferSize];

                    while (!Skip && !fail && !_disposeToken.IsCancellationRequested && !exit)
                    {
                        try
                        {
                            int read = await output.ReadAsync(buffer, 0, bufferSize, _disposeToken.Token);
                            if (read == 0)
                            {
                                //No more data available
                                exit = true;
                                break;
                            }

                            await AudioOutStream.WriteAsync(buffer, 0, read, _disposeToken.Token);

                            //if (Pause)
                            //{
                            //    bool pauseAgain;

                            //    do
                            //    {
                            //        pauseAgain = await _tcs.Task;
                            //        _tcs = new TaskCompletionSource<bool>();
                            //    } while (pauseAgain);
                            //}
                        }
                        catch (TaskCanceledException)
                        {
                            exit = true;
                        }
                        catch
                        {
                            fail = true;
                        }

                        var tsc = new TaskCompletionSource<bool>();

                        
                    }
                    Thread.Sleep(2 * 1000); // let all buffered data go out.
                }
            }
        }

        //public async Task StartQueue(ICommandContext context, int? underNumber = null)
        //{

        //    if (IsPlaying)
        //    {
        //        log.Warn("Cant start playing because playing is already in process!");
        //        return;
        //    }
        //    IsPlaying = true;


        //    bool next = true;


        //    while (true)
        //    {
        //        LogMessage("Starting queue pool (entering while loop)");

        //        bool pause = false;
        //        //Next song if current is over
        //        if (!next)
        //        {
        //            IsPlaying = false;
        //            pause = await _tcs.Task;
        //            _tcs = new TaskCompletionSource<bool>();
        //        }
        //        else
        //        {
        //            next = false;
        //        }

        //        try
        //        {
        //            if (_queue.Count == 0)
        //            {
        //                LogMessage("Queue empty - ended");
        //                //Event is fiering twice. 
        //                OnQueueEmpty?.Invoke(this, null);
        //                break;
        //            }
        //            else
        //            {
        //                if (!pause)
        //                {
        //                    //Get Song
        //                    SongInQueue song;

        //                    if (underNumber.HasValue)
        //                    {
        //                        //Since C# lists are zero based, we have to decrement by one.
        //                        song = _queue.ElementAtOrDefault(underNumber.Value - 1);
        //                    }
        //                    else if(_skipToSong.HasValue)
        //                    {
        //                        //Since C# lists are zero based, we have to decrement by one.
        //                        song = _queue.ElementAtOrDefault(_skipToSong.Value - 1);
        //                        if (song == null)
        //                            song = _queue.FirstOrDefault();
        //                    }
        //                    else
        //                    {
        //                        song = _queue.FirstOrDefault();
        //                    }

        //                    //Send audio (Long Async blocking, Read/Write stream)
        //                    song.IsPlaying = true;
        //                    await SendAudio(context.Guild, song);
        //                    song.IsPlaying = false;

        //                    try
        //                    {
        //                        //Check if song should be persistant.
        //                        if (song.PersistInQueue)
        //                        {
        //                            //Persist song at the end of the queue
        //                            _queue.Add(song);
        //                        }
        //                        else
        //                        {
        //                            //otherwise delete item.
        //                            _queue.Remove(song);

        //                            if (!song.IsPlayList)
        //                                File.Delete(song.FilePath);
        //                        }
        //                    }
        //                    catch
        //                    {
        //                        // ignored
        //                    }
        //                    next = true;
        //                }
        //            }
        //        }
        //        catch
        //        {
        //            //audio can't be played
        //        }
        //    }
        //}

        public async Task StartQueue(ICommandContext context, int? underNumber = null)
        {
            QueueIsRunning = true;
            LogMessage($"Starting queue! with queue items: { Queue.Count }");

            while (!Queue.IsEmpty && QueueIsRunning)
            {
                try
                {
                    //GetFirstSong
                    KeyValuePair<string, SongInQueue> queueSong = Queue.FirstOrDefault();
                    SongInQueue song = queueSong.Value;


                    if (song == null || ConnectedChannels.IsEmpty)
                        break;

                    LogMessage($"Song found in the queue { queueSong.Value.ToString() }");

                    song.IsPlaying = true;
                    await SendAudio(context.Guild, queueSong.Value);
                    song.IsPlaying = false;
                    LogMessage($"Finished playing song: { song.ToString() }");

                    if (Queue.TryRemove(queueSong.Key, out SongInQueue removedSong))
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
                catch (Exception ex)
                {
                    QueueIsRunning = false;
                    throw ex;
                }
            }

            LogMessage("Queue is empty!");
        }

        public async Task StopQueue()
        {
            QueueIsRunning = false;
            Skip = true;
        }

        public SongInQueue Next(IGuild guild, IVoiceChannel target, int? underNumber = null)
        {
            _skipToSong = underNumber; 
            Skip = true;
            Pause = false;
            var song = Queue.FirstOrDefault(it => it.Value.IsPlaying).Value;

            if (!song.IsPlayList && File.Exists(song.FilePath))
                File.Delete(song.FilePath);

            _tcs = new TaskCompletionSource<bool>();

            return song;
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
