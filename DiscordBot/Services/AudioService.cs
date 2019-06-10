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

        private readonly ConcurrentDictionary<int, SongInQueue> _queue = new ConcurrentDictionary<int, SongInQueue>();

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
        private bool Skip
        {
            get
            {
                bool ret = _internalSkip;
                _internalSkip = false;
                return ret;
            }
            set => _internalSkip = value;
        }
        private int? _skipToSong = null;
        private bool _internalSkip;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;
        private bool IsPlaying = false;
        private DateTime OnQueueEmptyCalled;

        public ConcurrentDictionary<int, SongInQueue> Queue
        {
            get { return _queue; }
        }

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
                GetSongsFromPlayList();

                return Task.CompletedTask;
            };
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

        private SongInQueue PrepareSong(string link)
        {
            try
            {
                Console.WriteLine("Started processing file " + link);
                string guid = Guid.NewGuid().ToString();
                SongInQueue result = new SongInQueue();

                YouTube youtube = YouTube.Default;
                string fullFilePath = _musicStorage + guid;
                Video vid = youtube.GetVideo(link);
                Console.WriteLine("Finished downloading file " + link);
                result.Name = GetPropperName(vid);
                var bytes = vid.GetBytes();
                File.WriteAllBytes(fullFilePath, bytes);
                Console.WriteLine("Finished saving file to the disc.");

                var inputFile = new MediaFile(fullFilePath);
                var fullFilePathWithExtension = $"{fullFilePath}.mp3";
                var outputFile = new MediaFile(fullFilePathWithExtension);

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
                Console.WriteLine($"Finished convering. Time: { convertSW.Elapsed.ToString() }");

                if (File.Exists(fullFilePath))
                {
                    File.Delete(fullFilePath);
                }
                else
                {
                    throw new NotImplementedException();
                }

                Console.WriteLine("Finished processing file " + link);
                return result;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        private void ConvertEngine_ConvertProgressEvent(object sender, ConvertProgressEventArgs e)
        {
            //Console.WriteLine($"Current memory usage: { _ramMemoryCounter.NextValue().ToString() } MB");
        }

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

                            if (Pause)
                            {
                                bool pauseAgain;

                                do
                                {
                                    pauseAgain = await _tcs.Task;
                                    _tcs = new TaskCompletionSource<bool>();
                                } while (pauseAgain);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            exit = true;
                        }
                        catch
                        {
                            fail = true;
                        }
                    }
                }
            }
        }

        public SongInQueue AddToQueue(string link, IUser user, bool persist = false)
        {

            try
            {
                var songInQueue = PrepareSong(link);
                songInQueue.QueueBy = user;
                songInQueue.PersistInQueue = persist;

                if(_queue.TryAdd(_queue.Count, songInQueue))
                {
                    Console.WriteLine($"Added { songInQueue.Name } to queue.");
                }
                else
                {
                    Console.WriteLine($"Faild to add { songInQueue.Name } to queue.");
                }
                

                return songInQueue;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
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
                log.Info("Started populating playlist.");
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

                    
                    if (_queue.TryAdd(_queue.Count, songToQueue))
                    {
                        log.Info($"Adding song { songToQueue.Name }.");
                    }
                    else
                    {
                        log.Info($"Failed adding song { songToQueue.Name }.");
                    }
                }

                log.Info("Finished populating playlist.");
            }
        }

        //public async Task StartQueue_old(ICommandContext context, int? underNumber = null)
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
        //        log.Info("Starting queue pool (entering while loop)");

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
        //                log.Info("Queue empty - ended");
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
        //                        else if(_queue.TryTake(out SongInQueue songFromQueue) && songFromQueue.IsPlaying)
        //                        {
        //                            //otherwise delete item.
        //                            File.Delete(songFromQueue.FilePath);
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

        private SongInQueue GetSongFromTheQueue(int? underNumber = null, out int keyOfSong = )
        {
            SongInQueue retVal = null;

            if (underNumber.HasValue)
            {
                Queue.TryGetValue(underNumber.Value, out retVal);
            }
            else
            {
                SongInQueue anySong = Queue.Values.FirstOrDefault();
                retVal = anySong;
            }
               
            return retVal;
        }

        public async Task StartQueue(ICommandContext context, int? underNumber = null)
        {
            //Check if already playing
            if (IsPlaying)
            {
                log.Warn("Alreadying playing!");
            }

            IsPlaying = true;

            while(Queue.Count > 0)
            {
                //GetSongFromTheQueue
                SongInQueue song = GetSongFromTheQueue(underNumber);

                if (song != null)
                {
                    song.IsPlaying = true;
                    await SendAudio(context.Guild, song);
                    song.IsPlaying = false;
                    IsPlaying = false;
                    Queue.TryRemove()
                }
            }
        }

        public SongInQueue Next(IGuild guild, IVoiceChannel target, int? underNumber = null)
        {
            _skipToSong = underNumber; 
            Skip = true;
            Pause = false;
            var song = GetSongFromTheQueue(underNumber);

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
                    Console.WriteLine($"Connected to voice on {guild.Name}.");
                    retVal = true;
                }
                else
                {
                    Console.WriteLine($"Faild to connected to voice on {guild.Name}.");
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
            IsPlaying = false;

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
