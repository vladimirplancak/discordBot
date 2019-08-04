using System;
using System.Linq;
using DiscordBot.Models;
using log4net;
using MediaToolkit.Model;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace DiscordBot.YoutubeDownlaoder
{
    public class YoutubeExplodeClient : IYoutubeDownloaderClient
    {
        private readonly static string _musicStorage = @"D:/youtubemusic/";
        private static readonly ILog _log = LogManager.GetLogger(typeof(YoutubeExplodeClient));

        public SongInQueue DownloadSong(string link)
        {
            string guid = Guid.NewGuid().ToString();
            SongInQueue result = new SongInQueue();
            string fullFilePath = _musicStorage + guid;

            YoutubeClient client = new YoutubeClient();

            try
            {
                _log.Debug($"Started processing { link }");
                string parsedYoutubeId = YoutubeClient.ParseVideoId(link);
                MediaStreamInfoSet streamInfoSet = client.GetVideoMediaStreamInfosAsync(parsedYoutubeId).Result;

                YoutubeExplode.Models.Video video = client.GetVideoAsync(parsedYoutubeId).Result;
                result.Name = video.Title;
                AudioStreamInfo streamInfo = streamInfoSet.Audio.First();

                string ext = streamInfo.Container.GetFileExtension();
                fullFilePath += $".{ ext }";

                IProgress<double> progress = new YoutubeExtractorClientProgress($"{result.Name} - { guid }");

                
                client.DownloadMediaStreamAsync(streamInfo, fullFilePath, progress).Wait();
                var inputFile = new MediaFile(fullFilePath);

                result.FilePath = fullFilePath;

                _log.Debug("Finished processing file " + link);
                return result;
            }
            catch(Exception ex)
            {
                _log.Error($"Error while downloading youtube song", ex);
                throw ex;
            }
        }
    }

    public class YoutubeExtractorClientProgress : IProgress<double>
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(YoutubeExtractorClientProgress));
        public int lastReport = 0;
        private readonly string _songName;

        public YoutubeExtractorClientProgress(string songName)
        {
            _songName = songName;
        }

        public void Report(double value)
        {
            value *= 100;
            int intValue = (int)value;
            if(intValue % 5 == 0 && intValue > lastReport)
            {
                lastReport = intValue;
                _log.Info($"Downloaded precent { intValue }% - [{ _songName }]");
            }
        }
    }
}
