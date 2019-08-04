using System;
using System.Diagnostics;
using System.IO;
using DiscordBot.Models;
using log4net;
using MediaToolkit;
using MediaToolkit.Model;
using VideoLibrary;

namespace DiscordBot.YoutubeDownlaoder
{
    public class YoutubeVideoLiberaryClient : IYoutubeDownloaderClient
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(YoutubeVideoLiberaryClient));
        private readonly static string _musicStorage = @"D:/youtubemusic/";

        public SongInQueue DownloadSong(string link)
        {
            try
            {
                _log.Info("Started processing file " + link);
                string guid = Guid.NewGuid().ToString();
                SongInQueue result = new SongInQueue();

                YouTube youtube = YouTube.Default;
                string fullFilePath = _musicStorage + guid;
                Video vid = youtube.GetVideo(link);
                _log.Info("Finished downloading file " + link);
                result.Name = GetPropperName(vid);

                File.WriteAllBytes(fullFilePath, vid.GetBytes());
                _log.Info("Finished saving file to the disc.");

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

                _log.Info($"Finished convering. Time: { convertSW.Elapsed.ToString() }");

                if (File.Exists(fullFilePath))
                {
                    File.Delete(fullFilePath);
                }
                else
                {
                    throw new NotImplementedException();
                }

                _log.Info("Finished processing file " + link);
                return result;
            }
            catch (Exception ex)
            {
                _log.Info($"Failed to prepare file:  { ex }");
            }

            return null;
        }

        private string GetPropperName(Video vid)
        {
            return vid.FullName.Replace(" - YouTube" + vid.FileExtension, "");
        }
    }
}
