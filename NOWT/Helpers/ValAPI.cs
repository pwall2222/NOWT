using NOWT.Objects;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Settings = NOWT.Properties.Settings;

namespace NOWT.Helpers;

public static class ValApi
{
    private static readonly RestClient Client;
    private static readonly RestClient MediaClient;

    private static Urls _mapsInfo;
    private static Urls _agentsInfo;
    private static Urls _ranksInfo;
    private static Urls _versionInfo;
    private static Urls _skinsInfo;
    private static Urls _cardsInfo;
    private static Urls _spraysInfo;
    private static Urls _gamemodeInfo;
    private static Urls _podsInfo;
    private static List<Urls> _allInfo;

    private static readonly Dictionary<string, string> ValApiLanguages =
        new()
        {
            { "ar", "ar-AE" },
            { "de", "de-DE" },
            { "en", "en-US" },
            { "es", "es-ES" },
            { "fr", "fr-FR" },
            { "id", "id-ID" },
            { "it", "it-IT" },
            { "ja", "ja-JP" },
            { "ko", "ko-KR" },
            { "pl", "pl-PL" },
            { "pt", "pt-BR" },
            { "ru", "ru-RU" },
            { "th", "th-TH" },
            { "tr", "tr-TR" },
            { "vi", "vi-VN" },
            { "zh", "zh-CN" }
        };

    static ValApi()
    {
        Client = new RestClient("https://valorant-api.com/v1");
        MediaClient = new RestClient();
    }

    private static async Task<RestResponse<T>> Fetch<T>(string url)
    {
        var request = new RestRequest(url);
        return await Client.ExecuteGetAsync<T>(request).ConfigureAwait(false);
    }

    private static async Task<string> GetValApiVersionAsync()
    {
        var response = await Fetch<VapiVersionResponse>("/version");
        return !response.IsSuccessful ? null : response.Data.Data.BuildDate;
    }

    private static async Task<string> GetLocalValApiVersionAsync()
    {
        if (!File.Exists(Constants.LocalAppDataPath + "\\ValAPI\\version.json"))
            return null;
        try
        {
            var lines = await File.ReadAllLinesAsync(
                    Constants.LocalAppDataPath + "\\ValAPI\\version.json"
                )
                .ConfigureAwait(false);
            return lines[1];
        }
        catch
        {
            return "";
        }
    }

    private static Task GetUrlsAsync()
    {
        var language = ValApiLanguages.GetValueOrDefault(Settings.Default.Language, "en-US");
        _mapsInfo = new Urls
        {
            Name = "Maps",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\maps.json",
            Url = $"/maps?language={language}"
        };
        _agentsInfo = new Urls
        {
            Name = "Agents",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\agents.json",
            Url = $"/agents?language={language}"
        };
        _skinsInfo = new Urls
        {
            Name = "Skins",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\skinchromas.json",
            Url = $"/weapons/skinchromas?language={language}"
        };
        _cardsInfo = new Urls
        {
            Name = "Cards",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\cards.json",
            Url = $"/playercards?language={language}"
        };
        _spraysInfo = new Urls
        {
            Name = "Sprays",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\sprays.json",
            Url = $"/sprays?language={language}"
        };
        _ranksInfo = new Urls
        {
            Name = "Ranks",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\competitivetiers.json",
            Url = $"/competitivetiers?language={language}"
        };
        _versionInfo = new Urls
        {
            Name = "Version",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\version.json",
            Url = "/version"
        };
        _gamemodeInfo = new Urls
        {
            Name = "Gamemode",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\gamemode.json",
            Url = $"/gamemodes?language={language}"
        };
        _podsInfo = new Urls
        {
            Name = "Gamepods",
            Filepath = Constants.LocalAppDataPath + "\\ValAPI\\gamepods.json",
            Url = $"../internal/locres/{language}"
        };
        _allInfo = new List<Urls>
        {
            _mapsInfo,
            _agentsInfo,
            _ranksInfo,
            _versionInfo,
            _skinsInfo,
            _cardsInfo,
            _spraysInfo,
            _gamemodeInfo,
            _podsInfo
        };
        return Task.CompletedTask;
    }

    public static async Task UpdateFilesAsync()
    {
        try
        {
            await GetUrlsAsync().ConfigureAwait(false);
            if (!Directory.Exists(Constants.LocalAppDataPath + "\\ValAPI"))
                Directory.CreateDirectory(Constants.LocalAppDataPath + "\\ValAPI");

            async Task UpdateVersion()
            {
                var versionRequest = new RestRequest(_versionInfo.Url);
                var versionResponse = await Client
                    .ExecuteGetAsync<VapiVersionResponse>(versionRequest)
                    .ConfigureAwait(false);
                if (!versionResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateVersion Failed, Response:{error}",
                        versionResponse.ErrorException
                    );
                    return;
                }
                string[] lines =
                {
                    versionResponse.Data?.Data.RiotClientVersion,
                    versionResponse.Data?.Data.BuildDate
                };
                await File.WriteAllLinesAsync(_versionInfo.Filepath, lines).ConfigureAwait(false);
            }

            async Task UpdateMapsDictionary()
            {
                var mapsResponse = await Fetch<ValApiMapsResponse>(_mapsInfo.Url);
                if (!mapsResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateMapsDictionary Failed, Response:{error}",
                        mapsResponse.ErrorException
                    );
                    return;
                }
                Dictionary<string, ValMap> mapsDictionary = new();
                if (!Directory.Exists(Constants.LocalAppDataPath + "\\ValAPI\\mapsimg"))
                    Directory.CreateDirectory(Constants.LocalAppDataPath + "\\ValAPI\\mapsimg");
                if (mapsResponse.Data?.Data != null)
                    foreach (var map in mapsResponse.Data.Data)
                    {
                        mapsDictionary.TryAdd(
                            map.MapUrl,
                            new ValMap { Name = map.DisplayName, UUID = map.Uuid }
                        );
                        var fileName =
                            Constants.LocalAppDataPath + $"\\ValAPI\\mapsimg\\{map.Uuid}.png";
                        var request = new RestRequest(map.ListViewIcon);
                        var response = await MediaClient
                            .DownloadDataAsync(request)
                            .ConfigureAwait(false);
                        if (response != null)
                            await File.WriteAllBytesAsync(fileName, response).ConfigureAwait(false);
                    }

                await File.WriteAllTextAsync(
                        _mapsInfo.Filepath,
                        JsonSerializer.Serialize(mapsDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateAgentsDictionary()
            {
                var agentsResponse = await Fetch<ValApiAgentsResponse>(_agentsInfo.Url);
                if (!agentsResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateAgentsDictionary Failed, Response:{error}",
                        agentsResponse.ErrorException
                    );
                    return;
                }
                Dictionary<Guid, string> agentsDictionary = new();
                if (!Directory.Exists(Constants.LocalAppDataPath + "\\ValAPI\\agentsimg"))
                    Directory.CreateDirectory(Constants.LocalAppDataPath + "\\ValAPI\\agentsimg");
                if (agentsResponse.Data != null)
                    foreach (var agent in agentsResponse.Data.Data)
                    {
                        agentsDictionary.TryAdd(agent.Uuid, agent.DisplayName);

                        var fileName =
                            Constants.LocalAppDataPath + $"\\ValAPI\\agentsimg\\{agent.Uuid}.png";
                        var request = new RestRequest(agent.DisplayIcon);
                        var response = await MediaClient
                            .DownloadDataAsync(request)
                            .ConfigureAwait(false);
                        if (response != null)
                            await File.WriteAllBytesAsync(fileName, response).ConfigureAwait(false);
                    }

                await File.WriteAllTextAsync(
                        _agentsInfo.Filepath,
                        JsonSerializer.Serialize(agentsDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateSkinsDictionary()
            {
                var skinsResponse = await Fetch<ValApiSkinsResponse>(_skinsInfo.Url);
                if (!skinsResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateSkinsDictionary Failed, Response:{error}",
                        skinsResponse.ErrorException
                    );
                    return;
                }
                Dictionary<Guid, ValNameImage> skinsDictionary = new();
                if (skinsResponse.Data != null)
                    foreach (var skin in skinsResponse.Data.Data)
                        skinsDictionary.TryAdd(
                            skin.Uuid,
                            new ValNameImage { Name = skin.DisplayName, Image = skin.FullRender }
                        );
                await File.WriteAllTextAsync(
                        _skinsInfo.Filepath,
                        JsonSerializer.Serialize(skinsDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateCardsDictionary()
            {
                var cardsResponse = await Fetch<ValApiCardsResponse>(_cardsInfo.Url);
                if (!cardsResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateCardsDictionary Failed, Response:{error}",
                        cardsResponse.ErrorException
                    );
                    return;
                }
                Dictionary<Guid, ValCard> cardsDictionary = new();
                if (cardsResponse.Data != null)
                    foreach (var card in cardsResponse.Data.Data)
                        cardsDictionary.TryAdd(
                            card.Uuid,
                            new ValCard
                            {
                                Name = card.DisplayName,
                                Image = card.DisplayIcon,
                                FullImage = card.LargeArt
                            }
                        );
                await File.WriteAllTextAsync(
                        _cardsInfo.Filepath,
                        JsonSerializer.Serialize(cardsDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateSpraysDictionary()
            {
                var spraysResponse = await Fetch<ValApiSpraysResponse>(_spraysInfo.Url);
                if (!spraysResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateSpraysDictionary Failed, Response:{error}",
                        spraysResponse.ErrorException
                    );
                    return;
                }
                Dictionary<Guid, ValNameImage> spraysDictionary = new();
                if (spraysResponse.Data != null)
                    foreach (var spray in spraysResponse.Data.Data)
                        spraysDictionary.TryAdd(
                            spray.Uuid,
                            new ValNameImage
                            {
                                Name = spray.DisplayName,
                                Image = spray.FullTransparentIcon
                            }
                        );
                await File.WriteAllTextAsync(
                        _spraysInfo.Filepath,
                        JsonSerializer.Serialize(spraysDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateRanksDictionary()
            {
                var ranksRequest = new RestRequest(_ranksInfo.Url);
                var ranksResponse = await Client
                    .ExecuteGetAsync<ValApiRanksResponse>(ranksRequest)
                    .ConfigureAwait(false);
                if (!ranksResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateRanksDictionary Failed, Response:{error}",
                        ranksResponse.ErrorException
                    );
                    return;
                }
                Dictionary<int, string> ranksDictionary = new();
                if (!Directory.Exists(Constants.LocalAppDataPath + "\\ValAPI\\ranksimg"))
                    Directory.CreateDirectory(Constants.LocalAppDataPath + "\\ValAPI\\ranksimg");
                if (ranksResponse.Data != null)
                    foreach (var rank in ranksResponse.Data.Data.Last().Tiers)
                    {
                        var tier = rank.TierTier;
                        ranksDictionary.TryAdd(tier, rank.TierName);

                        switch (tier)
                        {
                            case 1
                            or 2:
                                continue;
                            case 0:
                                {
                                    // File.Copy(Directory.GetCurrentDirectory() + "\\Assets\\0.png",
                                    //     Constants.LocalAppDataPath + "\\ValAPI\\ranksimg\\0.png", true);

                                    const string imagePath = "pack://application:,,,/Assets/0.png";
                                    var imageInfo = Application.GetResourceStream(new Uri(imagePath));
                                    using var ms = new MemoryStream();
                                    if (imageInfo != null)
                                    {
                                        await imageInfo.Stream.CopyToAsync(ms);
                                        var imageBytes = ms.ToArray();
                                        await File.WriteAllBytesAsync(
                                            Constants.LocalAppDataPath + "\\ValAPI\\ranksimg\\0.png",
                                            imageBytes
                                        );
                                    }

                                    continue;
                                }
                        }

                        var fileName =
                            Constants.LocalAppDataPath + $"\\ValAPI\\ranksimg\\{tier}.png";

                        var request = new RestRequest(rank.LargeIcon);
                        var response = await MediaClient
                            .DownloadDataAsync(request)
                            .ConfigureAwait(false);

                        // if (response.IsCompletedSuccessfully)
                        if (response != null)
                            await File.WriteAllBytesAsync(fileName, response).ConfigureAwait(false);
                    }

                await File.WriteAllTextAsync(
                        _ranksInfo.Filepath,
                        JsonSerializer.Serialize(ranksDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdateGamemodeDictionary()
            {
                var gameModeResponse = await Fetch<ValApiGamemodeResponse>(_gamemodeInfo.Url);
                if (!gameModeResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updateGamemodeDictionary Failed, Response:{error}",
                        gameModeResponse.ErrorException
                    );
                    return;
                }
                Dictionary<Guid, string> gamemodeDictionary = new();
                if (!Directory.Exists(Constants.LocalAppDataPath + "\\ValAPI\\gamemodeimg"))
                    Directory.CreateDirectory(Constants.LocalAppDataPath + "\\ValAPI\\gamemodeimg");
                if (gameModeResponse.Data != null)
                    foreach (var gamemode in gameModeResponse.Data.Data)
                    {
                        if (gamemode.DisplayIcon == null)
                            continue;
                        gamemodeDictionary.TryAdd(gamemode.Uuid, gamemode.DisplayName);

                        var fileName =
                            Constants.LocalAppDataPath
                            + $"\\ValAPI\\gamemodeimg\\{gamemode.Uuid}.png";
                        var request = new RestRequest(gamemode.DisplayIcon);
                        var response = await MediaClient
                            .DownloadDataAsync(request)
                            .ConfigureAwait(false);
                        if (response != null)
                            await File.WriteAllBytesAsync(fileName, response).ConfigureAwait(false);
                    }

                await File.WriteAllTextAsync(
                        _gamemodeInfo.Filepath,
                        JsonSerializer.Serialize(gamemodeDictionary)
                    )
                    .ConfigureAwait(false);
            }

            async Task UpdatePodsDictionary()
            {
                var podsResponse = await Fetch<ValApiLocresResponse>(_podsInfo.Url);
                if (!podsResponse.IsSuccessful)
                {
                    Constants.Log.Error(
                        "updatePodsDictionary Failed, Response:{error}",
                        podsResponse.ErrorException
                    );
                    return;
                }
                Dictionary<string, string> podsDictionary = new();
                if (
                    podsResponse.Data != null
                    && podsResponse.Data.Data.ContainsKey("UI_GamePodStrings")
                )
                {
                    podsDictionary = podsResponse.Data.Data["UI_GamePodStrings"];
                }
                if (Settings.Default.Language != "en")
                {
                    var locresEnglishREsponse = await Fetch<ValApiLocresResponse>(
                        _podsInfo.Url + "/../en-US"
                    );
                    if (
                        locresEnglishREsponse.Data != null
                        && locresEnglishREsponse.Data.Data.ContainsKey("UI_GamePodStrings")
                    )
                        locresEnglishREsponse.Data.Data["UI_GamePodStrings"]
                            .ToList()
                            .ForEach(x => podsDictionary.TryAdd(x.Key, x.Value));
                }

                await File.WriteAllTextAsync(
                        _podsInfo.Filepath,
                        JsonSerializer.Serialize(podsDictionary)
                    )
                    .ConfigureAwait(false);
            }

            try
            {
                await Task.WhenAll(
                        UpdateVersion(),
                        UpdateRanksDictionary(),
                        UpdateAgentsDictionary(),
                        UpdateMapsDictionary(),
                        UpdateSkinsDictionary(),
                        UpdateCardsDictionary(),
                        UpdateSpraysDictionary(),
                        UpdateGamemodeDictionary(),
                        UpdatePodsDictionary()
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Constants.Log.Error(
                    "updateGamemodeDictionary Parralel Tasks Failed, Response:{error}",
                    e
                );
            }
        }
        catch (Exception e)
        {
            Constants.Log.Error("UpdateFilesAsync Failed, Response:{error}", e);
        }
    }

    public static async Task CheckAndUpdateJsonAsync()
    {
        try
        {
            await GetUrlsAsync().ConfigureAwait(false);

            if (
                await GetValApiVersionAsync().ConfigureAwait(false)
                != await GetLocalValApiVersionAsync().ConfigureAwait(false)
            )
            {
                await UpdateFilesAsync().ConfigureAwait(false);
                return;
            }

            if (_allInfo.Any(url => !File.Exists(url.Filepath)))
                await UpdateFilesAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
