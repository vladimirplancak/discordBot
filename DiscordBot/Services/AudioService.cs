using Discord;
using Discord.Audio;
using Discord.Commands;
using DiscordBot.Models;
using MediaToolkit;
using MediaToolkit.Model;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoLibrary;

namespace DiscordBot.Services
{
    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

        private Queue<QueueItem> _queue = new Queue<QueueItem>();
        //TODO: Get from config file.
        private readonly static string _musicStorage = @"E:/youtubeMusic/";
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
        private bool _internalSkip;
        private TaskCompletionSource<bool> _tcs;
        private CancellationTokenSource _disposeToken;

        public Queue<QueueItem> Queue
        {
            get { return _queue; }
        }

        public event EventHandler<string> OnStartDownloading;
        public event EventHandler<string> OnFinishDownloading;
        public event EventHandler<string> OnStartSavingToDisc;
        public event EventHandler<string> OnFinishSavingToDisc;
        public event EventHandler<string> OnStartConverting;
        public event EventHandler<string> OnFinishConverting;

        static AudioService()
        {
            DeleteOldFiles();
        }

        public AudioService()
        {
            _tcs = new TaskCompletionSource<bool>();
            _disposeToken = new CancellationTokenSource();
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

        private QueueItem PrepareFile(string link)
        {
            Console.WriteLine("Started processing file " + link);
            var guid = Guid.NewGuid().ToString();
            var result = new QueueItem();
            result.Id = _queue.Count;

            YouTube youtube = YouTube.Default;
            var fullFilePath = _musicStorage + guid;
            OnStartDownloading?.Invoke(this, link);
            Video vid = youtube.GetVideo(link);
            OnFinishDownloading?.Invoke(this, link);
            result.Name = GetPropperName(vid);

            OnStartSavingToDisc?.Invoke(this, link);
            File.WriteAllBytes(fullFilePath, vid.GetBytes());
            OnFinishSavingToDisc?.Invoke(this, link);

            var inputFile = new MediaFile { Filename = fullFilePath };
            var fullFilePathWithExtension = $"{fullFilePath}.mp3";
            var outputFile = new MediaFile { Filename = fullFilePathWithExtension };

            result.FilePath = fullFilePathWithExtension;

            OnStartConverting?.Invoke(this, link);
            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);

                engine.Convert(inputFile, outputFile);

            }
            OnFinishConverting?.Invoke(this, link);

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

        public QueueItem AddToQueue(string link)
        {

            try
            {
                var queueItem = PrepareFile(link);
                _queue.Enqueue(queueItem);
                Console.WriteLine($"Added { queueItem.Name } to queue.");

                return queueItem;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        private async Task SendAudio(IGuild guild, QueueItem song)
        {
            Process ffmpeg = GetFfmpeg(song.FilePath);
            IAudioClient _audio;
            if (ConnectedChannels.TryGetValue(guild.Id, out _audio))
            {

                using (Stream output = ffmpeg.StandardOutput.BaseStream)
                using (AudioOutStream discord = _audio.CreatePCMStream(AudioApplication.Mixed))
                {
                    //Adjust?
                    int bufferSize = 2048;
                    int bytesSent = 0;
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

                            await discord.WriteAsync(buffer, 0, read, _disposeToken.Token);

                            if (Pause)
                            {
                                bool pauseAgain;

                                do
                                {
                                    pauseAgain = await _tcs.Task;
                                    _tcs = new TaskCompletionSource<bool>();
                                } while (pauseAgain);
                            }

                            bytesSent += read;
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
                    await discord.FlushAsync();
                    discord.Dispose();
                    await output.FlushAsync();
                }
            }
        }

        public async Task StartQueue(ICommandContext context)
        {
            
            bool next = true;


            while (true)
            {
                bool pause = false;
                //Next song if current is over
                if (!next)
                {
                    pause = await _tcs.Task;
                    _tcs = new TaskCompletionSource<bool>();
                }
                else
                {
                    next = false;
                }

                try
                {
                    if (_queue.Count == 0)
                    {
                        Console.WriteLine("Queue empty - ended");
                        await context.Channel.SendMessageAsync("```Queue is empty!```");

                    }
                    else
                    {
                        if (!pause)
                        {
                            //Get Song
                            var song = _queue.Peek();

                            //Send audio (Long Async blocking, Read/Write stream)
                            await SendAudio(context.Guild, song);

                            try
                            {
                                File.Delete(song.FilePath);
                            }
                            catch
                            {
                                // ignored
                            }
                            finally
                            {
                                //Finally remove song from playlist
                                _queue.Dequeue();
                            }
                            next = true;
                        }
                    }
                }
                catch
                {
                    //audio can't be played
                }
            }
        }


        public QueueItem Next(IGuild guild, IVoiceChannel target)
        {

            Skip = true;
            Pause = false;

            return _queue.Peek();
        }



        public async Task<bool> JoinAudio(IGuild guild, IVoiceChannel target)
        {
            var retVal = false;

            IAudioClient client;
            if (ConnectedChannels.TryGetValue(guild.Id, out client))
            {
                retVal = false;
            }
            if (target.Guild.Id != guild.Id)
            {
                retVal = false;
            }

            var audioClient = await target.ConnectAsync();

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

        public async Task LeaveAudio(IGuild guild)
        {
            IAudioClient client;
            if (ConnectedChannels.TryRemove(guild.Id, out client))
            {
                await client.StopAsync();
            }
        }

    }
}
