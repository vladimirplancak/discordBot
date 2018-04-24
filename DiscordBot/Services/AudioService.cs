using DiscordBot.Models;
using MediaToolkit;
using MediaToolkit.Model;
using System;
using System.Collections.Generic;
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
        private List<QueueItem> _queue = new List<QueueItem>();
        private bool IsQueueRunning = false;
        //TODO: Get from config file.
        private readonly static string _musicStorage = @"E:/youtubeMusic/";

        public List<QueueItem> Queue
        {
            get { return _queue }
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
            return vid.FullName.Replace("YouTube" + vid.FileExtension, "");
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

        public QueueItem AddToQueue(string link)
        {

            try
            {
                var queueItem = PrepareFile(link);
                _queue.Add(queueItem);
                Console.WriteLine($"Added { queueItem.Name } to queue.");

                return queueItem;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public void StartPlaying()
        {
            IsQueueRunning = true;

            while (IsQueueRunning)
            {
                var queueItem = _queue.FirstOrDefault();

                if(queueItem == null)
                {
                    Console.WriteLine("Queue is empty!, stopped playing.");
                    IsQueueRunning = false;
                    return;
                }

                Console.WriteLine($"Started playing { queueItem.Name }");
                Thread.Sleep(5000);
                _queue.Remove(queueItem);
                Console.WriteLine($"Removeing from queue { queueItem.Name }");
            }
        }
    }
}
