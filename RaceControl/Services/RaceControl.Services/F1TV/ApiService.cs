﻿using NLog;
using RaceControl.Common.Enum;
using RaceControl.Common.Interfaces;
using RaceControl.Services.Interfaces.F1TV;
using RaceControl.Services.Interfaces.F1TV.Api;
using RaceControl.Services.Interfaces.Lark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RaceControl.Services.F1TV
{
    public class ApiService : IApiService
    {
        private const string SessionOccurrence = "session-occurrence";

        private readonly ILogger _logger;
        private readonly IF1TVClient _client;

        public ApiService(ILogger logger, IF1TVClient client)
        {
            _logger = logger;
            _client = client;
        }

        public async Task<List<Session>> GetLiveSessionsAsync()
        {
            _logger.Info("Querying live sessions...");

            var utcNow = DateTime.UtcNow;

            var request = _client
                .NewRequest(SessionOccurrence)
                .WithField(Session.UIDField)
                .WithField(Session.NameField)
                .WithField(Session.SessionNameField)
                .WithField(Session.StatusField)
                .WithField(Session.StartTimeField)
                .WithField(Session.EndTimeField)
                .WithField(Session.EventOccurrenceUrlField)
                .WithField(Session.SeriesUrlField)
                .WithFilter(Session.StartTimeField, LarkFilterType.LessThan, utcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss"))
                .WithFilter(Session.EndTimeField, LarkFilterType.GreaterThan, utcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss"))
                .OrderBy(Session.StartTimeField, LarkSortDirection.Descending)
                ;

            var sessions = (await _client.GetCollectionAsync<Session>(request)).Objects;
            _logger.Info($"Found {sessions.Count} live sessions.");

            return sessions;
        }

        public async Task<List<Season>> GetSeasonsAsync()
        {
            _logger.Info("Querying seasons...");

            var request = _client
                .NewRequest("race-season")
                .WithField(Season.UIDField)
                .WithField(Season.NameField)
                .WithField(Season.HasContentField)
                .WithField(Season.YearField)
                .WithFilter(Season.YearField, LarkFilterType.GreaterThan, 2017.ToString())
                .WithFilter(Season.HasContentField, LarkFilterType.Equals, bool.TrueString.ToLower())
                .OrderBy(Season.YearField, LarkSortDirection.Descending)
                ;

            var seasons = (await _client.GetCollectionAsync<Season>(request)).Objects;
            _logger.Info($"Found {seasons.Count} seasons.");

            return seasons;
        }

        public async Task<List<Series>> GetSeriesAsync()
        {
            _logger.Info("Querying series...");

            var request = _client
                .NewRequest("series")
                .WithField(Series.UIDField)
                .WithField(Series.SelfField)
                .WithField(Series.NameField)
                .WithField(Series.ContentUrlsField)
                .WithField(Series.SessionOccurrenceUrlsField)
                .OrderBy(Series.NameField, LarkSortDirection.Ascending)
                ;

            var series = (await _client.GetCollectionAsync<Series>(request)).Objects;
            _logger.Info($"Found {series.Count} series.");

            return series;
        }

        public async Task<List<VodType>> GetVodTypesAsync()
        {
            _logger.Info("Querying VOD types...");

            var request = _client
                .NewRequest("vod-type-tag")
                .WithField(VodType.UIDField)
                .WithField(VodType.NameField)
                .WithField(VodType.ContentUrlsField)
                ;

            var vodTypes = (await _client.GetCollectionAsync<VodType>(request)).Objects;
            _logger.Info($"Found {vodTypes.Count} VOD types.");

            return vodTypes;
        }

        public async Task<List<Event>> GetEventsForSeasonAsync(string seasonUID)
        {
            _logger.Info($"Querying events for season with UID '{seasonUID}'...");

            var request = _client
                .NewRequest("event-occurrence")
                .WithField(Event.UIDField)
                .WithField(Event.NameField)
                .WithField(Event.StartDateField)
                .WithField(Event.EndDateField)
                .WithField(Event.RaceSeasonUrlField)
                .WithFilter(Event.RaceSeasonUrlField, LarkFilterType.Equals, seasonUID)
                .OrderBy(Event.StartDateField, LarkSortDirection.Ascending)
                ;

            var events = (await _client.GetCollectionAsync<Event>(request)).Objects;
            _logger.Info($"Found {events.Count} events.");

            return events;
        }

        public async Task<List<Session>> GetSessionsForEventAsync(string eventUID)
        {
            _logger.Info($"Querying sessions for event with UID '{eventUID}'...");

            var request = _client
                .NewRequest(SessionOccurrence)
                .WithField(Session.UIDField)
                .WithField(Session.NameField)
                .WithField(Session.SessionNameField)
                .WithField(Session.StatusField)
                .WithField(Session.StartTimeField)
                .WithField(Session.EndTimeField)
                .WithField(Session.EventOccurrenceUrlField)
                .WithField(Session.SeriesUrlField)
                .WithField(Session.ImageUrlsField, true)
                .WithSubField(Session.ImageUrlsField, Image.UIDField)
                .WithSubField(Session.ImageUrlsField, Image.TitleField)
                .WithSubField(Session.ImageUrlsField, Image.ImageTypeField)
                .WithSubField(Session.ImageUrlsField, Image.UrlField)
                .WithFilter(Session.EventOccurrenceUrlField, LarkFilterType.Equals, eventUID)
                .OrderBy(Session.StartTimeField, LarkSortDirection.Ascending)
                ;

            var sessions = (await _client.GetCollectionAsync<Session>(request)).Objects;
            _logger.Info($"Found {sessions.Count} sessions.");

            return sessions;
        }

        public async Task<List<Channel>> GetChannelsForSessionAsync(string sessionUID)
        {
            _logger.Info($"Querying channels for session with UID '{sessionUID}'...");

            var request = _client
                .NewRequest(SessionOccurrence, sessionUID)
                .WithField(Session.ChannelUrlsField, true)
                .WithSubField(Session.ChannelUrlsField, Channel.UIDField)
                .WithSubField(Session.ChannelUrlsField, Channel.SelfField)
                .WithSubField(Session.ChannelUrlsField, Channel.NameField)
                .WithSubField(Session.ChannelUrlsField, Channel.ChannelTypeField)
                .WithSubField(Session.ChannelUrlsField, Channel.DriverOccurrenceUrlsField)
                ;

            var channels = (await _client.GetItemAsync<Session>(request)).ChannelUrls;
            _logger.Info($"Found {channels.Count} channels.");

            return channels;
        }

        public async Task<List<Episode>> GetEpisodesForSessionAsync(string sessionUID)
        {
            _logger.Info($"Querying episodes for session with UID '{sessionUID}'...");

            var request = _client
                .NewRequest(SessionOccurrence, sessionUID)
                .WithField(Session.ContentUrlsField, true)
                .WithSubField(Session.ContentUrlsField, Episode.UIDField)
                .WithSubField(Session.ContentUrlsField, Episode.TitleField)
                .WithSubField(Session.ContentUrlsField, Episode.ItemsField)
                .WithSubField(Session.ContentUrlsField, Episode.ImageUrlsField, true)
                .WithSubSubField(Session.ContentUrlsField, Episode.ImageUrlsField, Image.UIDField)
                .WithSubSubField(Session.ContentUrlsField, Episode.ImageUrlsField, Image.TitleField)
                .WithSubSubField(Session.ContentUrlsField, Episode.ImageUrlsField, Image.ImageTypeField)
                .WithSubSubField(Session.ContentUrlsField, Episode.ImageUrlsField, Image.UrlField)
                ;

            var episodes = (await _client.GetItemAsync<Session>(request)).ContentUrls;
            _logger.Info($"Found {episodes.Count} episodes.");

            return episodes;
        }

        public async Task<Episode> GetEpisodeAsync(string episodeUID)
        {
            _logger.Info($"Querying episode with UID '{episodeUID}'...");

            var request = _client
                .NewRequest("episodes", episodeUID)
                .WithField(Episode.UIDField)
                .WithField(Episode.TitleField)
                .WithField(Episode.ItemsField)
                .WithField(Episode.ImageUrlsField, true)
                .WithSubField(Episode.ImageUrlsField, Image.UIDField)
                .WithSubField(Episode.ImageUrlsField, Image.TitleField)
                .WithSubField(Episode.ImageUrlsField, Image.ImageTypeField)
                .WithSubField(Episode.ImageUrlsField, Image.UrlField)
                ;

            return await _client.GetItemAsync<Episode>(request);
        }

        public async Task<Driver> GetDriverAsync(string driverUID)
        {
            _logger.Info($"Querying driver with UID '{driverUID}'...");

            var request = _client
                .NewRequest("driver-occurrence", driverUID)
                .WithField(Driver.UIDField)
                .WithField(Driver.NameField)
                .WithField(Driver.ImageUrlsField, true)
                .WithSubField(Driver.ImageUrlsField, Image.UIDField)
                .WithSubField(Driver.ImageUrlsField, Image.TitleField)
                .WithSubField(Driver.ImageUrlsField, Image.ImageTypeField)
                .WithSubField(Driver.ImageUrlsField, Image.UrlField)
                ;

            return await _client.GetItemAsync<Driver>(request);
        }

        public async Task<string> GetTokenisedUrlAsync(string token, IPlayableContent playableContent)
        {
            _logger.Info($"Getting tokenised URL for content-type '{playableContent.ContentType}' and content-URL '{playableContent.ContentUrl}' using token '{token}'...");

            var url = playableContent.ContentType switch
            {
                ContentType.Channel => (await _client.GetTokenisedUrlForChannelAsync(token, playableContent.ContentUrl)).Url,
                ContentType.Asset => (await _client.GetTokenisedUrlForAssetAsync(token, playableContent.ContentUrl)).Objects.First().TokenisedUrl.Url,
                ContentType.Backup => (await _client.GetBackupStream()).StreamManifest,
                _ => throw new ArgumentException($"Could not generate tokenised URL for unsupported content-type '{playableContent.ContentType}'.", nameof(playableContent))
            };

            _logger.Info($"Got tokenised URL '{url}'.");

            return url;
        }
    }
}