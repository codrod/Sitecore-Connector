﻿using Brightcove.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Brightcove.Core.Extensions;
using System.Net.Http.Headers;
using Brightcove.Core.Exceptions;
using System.Net;
using Brightcove.MediaFramework.Brightcove.Entities;

namespace Brightcove.Core.Services
{
    public class BrightcoveService
    {
        readonly static HttpClient client = BrightcoveHttpClient.Instance;

        readonly string cmsBaseUrl = "https://cms.api.brightcove.com/v1/accounts";
        readonly string ingestBaseUrl = "https://ingest.api.brightcove.com/v1/accounts";
        readonly string playersBaseUrl = "https://players.api.brightcove.com/v1/accounts";
        readonly string experienceBaseUrl = "https://experiences.api.brightcove.com/v1/accounts";
        readonly string accountId;
        readonly BrightcoveAuthenticationService authenticationService;

        public BrightcoveService(string accountId, string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("argument must not be null or empty", nameof(accountId));
            }

            this.accountId = accountId;

            authenticationService = new BrightcoveAuthenticationService(clientId, clientSecret);
        }

        public string IngestVideo(string videoId, string url)
        {
            IngestVideo video = new IngestVideo();
            video.IngestMaster = new IngestMaster();
            video.IngestMaster.Url = url;

            HttpRequestMessage request = new HttpRequestMessage();
            request.Content = new StringContent(JsonConvert.SerializeObject(video), Encoding.UTF8, "application/json");
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"{ingestBaseUrl}/{accountId}/videos/{videoId}/ingest-requests");

            HttpResponseMessage response = SendRequest(request);
            IngestJobId ingestJobId = JsonConvert.DeserializeObject<IngestJobId>(response.Content.ReadAsString());

            return ingestJobId.JobId;
        }

        public TemporaryIngestUrls GetTemporaryIngestUrls(string videoId)
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{ingestBaseUrl}/{accountId}/videos/{videoId}/upload-urls/{videoId}");

            HttpResponseMessage response = SendRequest(request);
            TemporaryIngestUrls urls = JsonConvert.DeserializeObject<TemporaryIngestUrls>(response.Content.ReadAsString());

            return urls;
        }

        public Video CreateVideo(string name)
        {
            Video video = new Video();
            video.Name = name;

            HttpRequestMessage request = new HttpRequestMessage();
            request.Content = new StringContent(JsonConvert.SerializeObject(video), Encoding.UTF8, "application/json");
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/videos");

            HttpResponseMessage response = SendRequest(request);
            video = JsonConvert.DeserializeObject<Video>(response.Content.ReadAsString());

            return video;
        }

        public PlayList CreatePlaylist(string name)
        {
            PlayList playlist = new PlayList();
            playlist.Name = name;
            playlist.PlaylistType = "EXPLICIT";

            HttpRequestMessage request = new HttpRequestMessage();
            request.Content = new StringContent(JsonConvert.SerializeObject(playlist), Encoding.UTF8, "application/json");
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/playlists");

            HttpResponseMessage response = SendRequest(request);
            playlist = JsonConvert.DeserializeObject<PlayList>(response.Content.ReadAsString());

            return playlist;
        }

        public void DeletePlaylist(string playlistId)
        {
            if (string.IsNullOrWhiteSpace(playlistId))
            {
                return;
            }

            if (playlistId.Contains(","))
            {
                throw new ArgumentException("the playlist ID must not contain any commas", nameof(playlistId));
            }

            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Delete;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/playlists/{playlistId}");

            SendRequest(request);

            return;
        }

        public void DeleteVideo(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return;
            }

            if (videoId.Contains(","))
            {
                throw new ArgumentException("the video ID must not contain any commas", nameof(videoId));
            }

            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Delete;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/videos/{videoId}");

            SendRequest(request);

            return;
        }

        public Video UpdateVideo(Video video)
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = new HttpMethod("PATCH");
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/videos/{video.Id}");

            //Some properties will cause an invalid response because they are not updateable
            //Setting the property to null should remove it from the serialized request
            //Use shallowcopy to avoid side-effects caused by mutating the reference
            Video newVideo = video.ShallowCopy();
            newVideo.Id = null;
            newVideo.Images = null;

            request.Content = new StringContent(JsonConvert.SerializeObject(newVideo), Encoding.UTF8, "application/json");

            HttpResponseMessage response = SendRequest(request);

            return JsonConvert.DeserializeObject<Video>(response.Content.ReadAsString());
        }

        public Player CreatePlayer(string name, string description)
        {
            var playerRequest = new { name = name, description = description };

            HttpRequestMessage request = new HttpRequestMessage();
            request.Content = new StringContent(JsonConvert.SerializeObject(playerRequest), Encoding.UTF8, "application/json");
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"{playersBaseUrl}/{accountId}/players");

            HttpResponseMessage response = SendRequest(request);
            return JsonConvert.DeserializeObject<Player>(response.Content.ReadAsString());
        }

        public Player UpdatePlayer(Player player)
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = new HttpMethod("PATCH");
            request.RequestUri = new Uri($"{playersBaseUrl}/{accountId}/players/{player.Id}");

            var newPlayer = new { name = player.Name, description = player.ShortDescription };

            request.Content = new StringContent(JsonConvert.SerializeObject(newPlayer), Encoding.UTF8, "application/json");
            HttpResponseMessage response = SendRequest(request);
            return JsonConvert.DeserializeObject<Player>(response.Content.ReadAsString());
        }

        public void DeletePlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            if (playerId.Contains(","))
                throw new ArgumentException("the player ID must not contain any commas", nameof(playerId));

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Delete;
            request.RequestUri = new Uri($"{playersBaseUrl}/{accountId}/players/{playerId}");
            SendRequest(request);
        }

        public PlayList UpdatePlaylist(PlayList playlist)
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = new HttpMethod("PATCH");
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/playlists/{playlist.Id}");

            //Some properties will cause an invalid response because they are not updateable
            //Setting the property to null should remove it from the serialized request
            //Use shallowcopy to avoid side-effects caused by mutating the reference
            PlayList newPlaylist = playlist.ShallowCopy();
            newPlaylist.Id = null;
            newPlaylist.Images = null;
            newPlaylist.CreationDate = null;
            newPlaylist.LastModifiedDate = null;

            request.Content = new StringContent(JsonConvert.SerializeObject(newPlaylist), Encoding.UTF8, "application/json");

            HttpResponseMessage response = SendRequest(request);

            return JsonConvert.DeserializeObject<PlayList>(response.Content.ReadAsString());
        }

        public IEnumerable<PlayList> GetPlayLists(int offset = 0, int limit = 20, string sort = "", string query = "")
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/playlists?offset={offset}&limit={limit}&sort={sort}&q={query}");

            HttpResponseMessage response = SendRequest(request);

            return JsonConvert.DeserializeObject<List<PlayList>>(response.Content.ReadAsString());
        }

        public IEnumerable<Video> GetVideos(int offset = 0, int limit = 20, string sort = "", string query = "")
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/videos?offset={offset}&limit={limit}&sort={sort}&query={query}");

            HttpResponseMessage response = SendRequest(request);

            return JsonConvert.DeserializeObject<List<Video>>(response.Content.ReadAsString());
        }

        public PlayerList GetPlayers()
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{playersBaseUrl}/{accountId}/players");

            HttpResponseMessage response = SendRequest(request);

            PlayerList players = JsonConvert.DeserializeObject<PlayerList>(response.Content.ReadAsString());
            foreach (var p in players.Items)
                p.LastSyncTime = DateTime.UtcNow;

            return players;
        }

        public ExperienceList GetExperiences()
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{experienceBaseUrl}/{accountId}/experiences");

            HttpResponseMessage response = SendRequest(request);

            return JsonConvert.DeserializeObject<ExperienceList>(response.Content.ReadAsString());
        }

        public bool TryGetPlayer(string playerId, out Player player)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                player = null;
                return false;
            }

            if (playerId.Contains(","))
            {
                throw new ArgumentException("the video ID must not contain any commas", nameof(playerId));
            }

            HttpRequestMessage request = new HttpRequestMessage();
            HttpResponseMessage response;

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{playersBaseUrl}/{accountId}/players/{playerId}");

            try
            {
                response = SendRequest(request);
            }
            catch (HttpStatusException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    player = null;
                    return false;
                }

                throw ex;
            }

            player = JsonConvert.DeserializeObject<Player>(response.Content.ReadAsString());
            return true;
        }

        public bool TryGetVideo(string videoId, out Video video)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                video = null;
                return false;
            }

            if (videoId.Contains(","))
            {
                throw new ArgumentException("the video ID must not contain any commas", nameof(videoId));
            }

            HttpRequestMessage request = new HttpRequestMessage();
            HttpResponseMessage response;

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/videos/{videoId}");

            try
            {
                response = SendRequest(request);
            }
            catch (HttpStatusException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    video = null;
                    return false;
                }

                throw ex;
            }

            video = JsonConvert.DeserializeObject<Video>(response.Content.ReadAsString());
            return true;
        }

        public bool TryGetPlaylist(string playlistId, out PlayList playlist)
        {
            if (string.IsNullOrWhiteSpace(playlistId))
            {
                playlist = null;
                return false;
            }

            if (playlistId.Contains(","))
            {
                throw new ArgumentException("the playlist ID must not contain any commas", nameof(playlistId));
            }

            HttpRequestMessage request = new HttpRequestMessage();
            HttpResponseMessage response;

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/playlists/{playlistId}");

            try
            {
                response = SendRequest(request);
            }
            catch (HttpStatusException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    playlist = null;
                    return false;
                }

                throw ex;
            }

            playlist = JsonConvert.DeserializeObject<PlayList>(response.Content.ReadAsString());
            return true;
        }

        public int VideosCount()
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/counts/videos");

            HttpResponseMessage response = SendRequest(request);
            Count count = JsonConvert.DeserializeObject<Count>(response.Content.ReadAsString());

            return count.Value;
        }

        public int PlayListsCount()
        {
            HttpRequestMessage request = new HttpRequestMessage();

            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{cmsBaseUrl}/{accountId}/counts/playlists");

            HttpResponseMessage response = SendRequest(request);
            Count count = JsonConvert.DeserializeObject<Count>(response.Content.ReadAsString());

            return count.Value;
        }

        private HttpResponseMessage SendRequest(HttpRequestMessage request)
        {
            request.Headers.Authorization = authenticationService.CreateAuthenticationHeader();

            HttpResponseMessage response = client.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpStatusException(request, response);
            }

            return response;
        }
    }
}
