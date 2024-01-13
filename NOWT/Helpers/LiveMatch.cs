﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using RestSharp;
using RestSharp.Serializers.Json;
using NOWT.Objects;
using NOWT.Properties;
using static NOWT.Helpers.Login;

namespace NOWT.Helpers;

public class LiveMatch
{
    public delegate void UpdateProgress(int percentage);

    public MatchDetails MatchInfo { get; } = new();
    private static Guid Matchid { get; set; }
    private static Guid Partyid { get; set; }
    private static string Stage { get; set; }
    public string QueueId { get; set; }
    public string Status { get; set; }

    private static async Task<bool> CheckAndSetLiveMatchIdAsync()
    {
        var client = new RestClient(
            $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/core-game/v1/players/{Constants.Ppuuid}"
        );
        var request = new RestRequest();
        request.AddHeader("X-Riot-Entitlements-JWT", Constants.EntitlementToken);
        request.AddHeader("Authorization", $"Bearer {Constants.AccessToken}");
        var response = await client.ExecuteGetAsync<MatchIDResponse>(request).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            Matchid = response.Data.MatchId;
            Stage = "core";
            return true;
        }

        client = new RestClient(
            $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/pregame/v1/players/{Constants.Ppuuid}"
        );
        response = await client.ExecuteGetAsync<MatchIDResponse>(request).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            Matchid = response.Data.MatchId;
            Stage = "pre";
            return true;
        }

        Constants.Log.Error(
            "CheckAndSetLiveMatchIdAsync() failed. Response: {Response}",
            response.ErrorException
        );
        return false;
    }

    public async Task<bool> CheckAndSetPartyIdAsync()
    {
        var client = new RestClient(
            $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/parties/v1/players/{Constants.Ppuuid}"
        );
        var request = new RestRequest();
        request.AddHeader("X-Riot-Entitlements-JWT", Constants.EntitlementToken);
        request.AddHeader("Authorization", $"Bearer {Constants.AccessToken}");
        request.AddHeader("X-Riot-ClientVersion", Constants.Version);
        var response = await client.ExecuteGetAsync<PartyIdResponse>(request).ConfigureAwait(false);
        if (!response.IsSuccessful)
            return false;
        Partyid = response.Data.CurrentPartyId;
        return true;
    }

    public static async Task<bool> LiveMatchChecksAsync()
    {
        if (await Checks.CheckLoginAsync().ConfigureAwait(false))
        {
            await LocalRegionAsync().ConfigureAwait(false);
            return await CheckAndSetLiveMatchIdAsync().ConfigureAwait(false);
        }

        if (!await Checks.CheckLocalAsync().ConfigureAwait(false))
            return false;
        await LocalLoginAsync().ConfigureAwait(false);
        await Checks.CheckLoginAsync().ConfigureAwait(false);
        await LocalRegionAsync().ConfigureAwait(false);

        return await CheckAndSetLiveMatchIdAsync().ConfigureAwait(false);
    }

    private static async Task<LiveMatchResponse> GetLiveMatchDetailsAsync()
    {
        RestClient client =
            new(
                $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/core-game/v1/matches/{Matchid}"
            );
        RestRequest request = new();
        request.AddHeader("X-Riot-Entitlements-JWT", Constants.EntitlementToken);
        request.AddHeader("Authorization", $"Bearer {Constants.AccessToken}");
        var response = await client
            .ExecuteGetAsync<LiveMatchResponse>(request)
            .ConfigureAwait(false);
        if (response.IsSuccessful)
            return response.Data;
        Constants.Log.Error(
            "GetLiveMatchDetailsAsync() failed. Response: {Response}",
            response.ErrorException
        );
        return null;
    }

    private static async Task<PreMatchResponse> GetPreMatchDetailsAsync()
    {
        RestClient client =
            new(
                $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/pregame/v1/matches/{Matchid}"
            );
        RestRequest request = new();
        request.AddHeader("X-Riot-Entitlements-JWT", Constants.EntitlementToken);
        request.AddHeader("Authorization", $"Bearer {Constants.AccessToken}");
        var response = await client
            .ExecuteGetAsync<PreMatchResponse>(request)
            .ConfigureAwait(false);
        if (response.IsSuccessful)
            return response.Data;
        Constants.Log.Error(
            "GetPreMatchDetailsAsync() failed. Response: {Response}",
            response.ErrorException
        );
        return null;
    }

    private static async Task<PartyResponse> GetPartyDetailsAsync()
    {
        RestClient client =
            new(
                $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/parties/v1/parties/{Partyid}"
            );
        RestRequest request = new();
        request.AddHeader("X-Riot-Entitlements-JWT", Constants.EntitlementToken);
        request.AddHeader("Authorization", $"Bearer {Constants.AccessToken}");
        var response = await client.ExecuteGetAsync<PartyResponse>(request).ConfigureAwait(false);
        if (response.IsSuccessful)
            return response.Data;
        Constants.Log.Error(
            "GetPreMatchDetailsAsync() failed. Response: {Response}",
            response.ErrorException
        );
        return null;
    }

    private async Task<Player> GetPrePlayerInfo(
        RiotPrePlayer riotPlayer,
        sbyte index,
        Guid[] seasonData,
        PresencesResponse presencesResponse
    )
    {
        Player player = new();
        try
        {
            var cardTask = GetCardAsync(riotPlayer.PlayerIdentity.PlayerCardId, index);
            var historyTask = GetPlayerHistoryAsync(riotPlayer.Subject, seasonData);
            var presenceTask = GetPresenceInfoAsync(riotPlayer.Subject, presencesResponse);

            await Task.WhenAll(cardTask, historyTask, presenceTask).ConfigureAwait(false);

            player.IdentityData = cardTask.Result;
            player.RankData = historyTask.Result;
            player.PlayerUiData = presenceTask.Result;
            player.IgnData = await GetIgcUsernameAsync(
                    riotPlayer.Subject,
                    riotPlayer.PlayerIdentity.Incognito,
                    false
                )
                .ConfigureAwait(false);
            player.AccountLevel = !riotPlayer.PlayerIdentity.HideAccountLevel
                ? riotPlayer.PlayerIdentity.AccountLevel.ToString()
                : "-";
            player.TeamId = "Blue";
            player.Active = Visibility.Visible;
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetPlayerInfo() (PRE) failed for player {index}: {e}", index, e);
        }

        return player;
    }

    private async Task<Player> GetLivePlayerInfo(
        RiotLivePlayer riotPlayer,
        sbyte index,
        Guid[] seasonData,
        PresencesResponse presencesResponse
    )
    {
        Player player = new();
        try
        {
            var agentTask = GetAgentInfoAsync(riotPlayer.CharacterId);
            var playerTask = GetPlayerHistoryAsync(riotPlayer.Subject, seasonData);
            var skinTask = GetMatchSkinInfoAsync(index, riotPlayer.PlayerIdentity.PlayerCardId);
            var presenceTask = GetPresenceInfoAsync(riotPlayer.Subject, presencesResponse);

            await Task.WhenAll(agentTask, playerTask, skinTask, presenceTask).ConfigureAwait(false);

            player.IdentityData = agentTask.Result;
            player.RankData = playerTask.Result;
            player.SkinData = skinTask.Result;
            player.PlayerUiData = presenceTask.Result;
            player.IgnData = await GetIgcUsernameAsync(
                    riotPlayer.Subject,
                    riotPlayer.PlayerIdentity.Incognito,
                    false
                )
                .ConfigureAwait(false);
            player.AccountLevel = !riotPlayer.PlayerIdentity.HideAccountLevel
                ? riotPlayer.PlayerIdentity.AccountLevel.ToString()
                : "-";
            player.TeamId = riotPlayer.TeamId;
            player.Active = Visibility.Visible;
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetPlayerInfo() (LIVE) failed for player {index}: {e}", index, e);
        }

        return player;
    }

    private async Task GetPrePlayers(
        List<Task<Player>> playerTasks,
        PreMatchResponse matchIdInfo,
        Guid[] seasonData,
        PresencesResponse presencesResponse
    )
    {
        Task sTask = Task.Run(
            async () => seasonData = await GetSeasonsAsync().ConfigureAwait(false)
        );
        Task pTask = Task.Run(
            async () => presencesResponse = await GetPresencesAsync().ConfigureAwait(false)
        );
        await Task.WhenAll(sTask, pTask).ConfigureAwait(false);
        sbyte index = 0;

        foreach (var riotPlayer in matchIdInfo.AllyTeam.Players)
        {
            playerTasks.Add(GetPrePlayerInfo(riotPlayer, index, seasonData, presencesResponse));
            index++;
        }
    }

    private async Task GetLivePlayers(
        List<Task<Player>> playerTasks,
        LiveMatchResponse matchIdInfo,
        Guid[] seasonData,
        PresencesResponse presencesResponse
    )
    {
        Task sTask = Task.Run(
            async () => seasonData = await GetSeasonsAsync().ConfigureAwait(false)
        );
        Task pTask = Task.Run(
            async () => presencesResponse = await GetPresencesAsync().ConfigureAwait(false)
        );
        await Task.WhenAll(sTask, pTask).ConfigureAwait(false);
        sbyte index = 0;

        foreach (var riotPlayer in matchIdInfo.Players)
        {
            if (riotPlayer.IsCoach)
                continue;

            playerTasks.Add(GetLivePlayerInfo(riotPlayer, index, seasonData, presencesResponse));

            index++;
        }
    }

    private async Task<dynamic> GetMatchResponse()
    {
        if (Stage == "pre")
        {
            return await GetPreMatchDetailsAsync().ConfigureAwait(false);
        }
        return await GetLiveMatchDetailsAsync().ConfigureAwait(false);
    }

    private void SetServer(dynamic matchIdInfo)
    {
        var gamePodId = matchIdInfo.GamePodId;
        var pods = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Constants.LocalAppDataPath + "\\ValAPI\\gamepods.json")
        );
        var nameAvailable = pods.TryGetValue(gamePodId, out string serverName);
        if (!nameAvailable)
            return;
        MatchInfo.Server = "🌍 " + serverName;
    }

    private async Task GetPlayers(
        UpdateProgress updateProgress,
        List<Task<Player>> playerTasks,
        Guid[] seasonData,
        PresencesResponse presencesResponse
    )
    {
        var matchIdInfo = await GetMatchResponse();
        updateProgress(10);

        if (matchIdInfo == null)
            return;

        SetServer(matchIdInfo);

        if (Stage == "pre")
        {
            await GetPrePlayers(playerTasks, matchIdInfo, seasonData, presencesResponse);
            return;
        }
        await GetLivePlayers(playerTasks, matchIdInfo, seasonData, presencesResponse);
    }

    private void AddPlayerParties(List<Player> playerList)
    {
        var playerPartyColors = new List<string>
        {
            "Red",
            "#32e2b2",
            "DarkOrange",
            "White",
            "DeepSkyBlue",
            "MediumPurple",
            "SaddleBrown"
        };
        List<string> newArray = new();
        newArray.AddRange(Enumerable.Repeat("Transparent", playerList.Count));

        for (var i = 0; i < playerList.Count; i++)
        {
            if (playerList[i].PlayerUiData is null)
                continue;

            var colourused = false;
            var id = playerList[i].PlayerUiData.PartyUuid;
            for (var j = i; j < playerList.Count; j++)
            {
                if (
                    newArray[i] != "Transparent"
                    || playerList[i] == playerList[j]
                    || playerList[j].PlayerUiData?.PartyUuid != id
                    || id == Guid.Empty
                )
                    continue;
                newArray[j] = playerPartyColors[0];
                colourused = true;
            }

            if (!colourused)
                continue;
            newArray[i] = playerPartyColors[0];
            playerPartyColors.RemoveAt(0);
        }
        for (var i = 0; i < playerList.Count; i++)
            playerList[i].PlayerUiData.PartyColour = newArray[i];
    }

    public async Task<List<Player>> LiveMatchOutputAsync(UpdateProgress updateProgress)
    {
        var playerList = new List<Player>();
        var playerTasks = new List<Task<Player>>();
        var seasonData = new Guid[4];
        var presencesResponse = new PresencesResponse();

        await GetPlayers(updateProgress, playerTasks, seasonData, presencesResponse);

        playerList.AddRange(await Task.WhenAll(playerTasks).ConfigureAwait(false));
        updateProgress(75);

        try
        {
            AddPlayerParties(playerList);
            updateProgress(100);
        }
        catch (Exception e)
        {
            Constants.Log.Error("LiveMatchOutputAsync() party colour failed: {e}", e);
        }

        return playerList;
    }

    private async Task<Player> GetPartyPlayerInfo(Member riotPlayer, sbyte index, Guid[] seasonData)
    {
        Player player = new();

        var cardTask = GetCardAsync(riotPlayer.PlayerIdentity.PlayerCardId, index);
        var historyTask = GetMatchHistoryAsync(riotPlayer.Subject);
        var playerTask = GetPlayerHistoryAsync(riotPlayer.Subject, seasonData);

        await Task.WhenAll(cardTask, historyTask, playerTask).ConfigureAwait(false);

        player.IdentityData = cardTask.Result;
        player.MatchHistoryData = historyTask.Result;
        player.RankData = playerTask.Result;
        player.PlayerUiData = new PlayerUIData
        {
            BackgroundColour = "#252A40",
            PartyUuid = Partyid,
            PartyColour = "Transparent",
            Puuid = riotPlayer.PlayerIdentity.Subject
        };
        player.IgnData = await GetIgcUsernameAsync(riotPlayer.Subject, false, true)
            .ConfigureAwait(false);
        player.AccountLevel = !riotPlayer.PlayerIdentity.HideAccountLevel
            ? riotPlayer.PlayerIdentity.AccountLevel.ToString()
            : "-";
        player.TeamId = "Blue";
        player.Active = Visibility.Visible;
        return player;
    }

    private async Task GetPartyPlayers(PartyResponse partyInfo, List<Task<Player>> playerTasks)
    {
        if (partyInfo == null)
            return;

        var seasonData = await GetSeasonsAsync().ConfigureAwait(false);
        sbyte index = 0;

        foreach (var riotPlayer in partyInfo.Members)
        {
            playerTasks.Add(GetPartyPlayerInfo(riotPlayer, index, seasonData));
            index++;
        }
    }

    public async Task<List<Player>> PartyOutputAsync()
    {
        var playerList = new List<Player>();
        var playerTasks = new List<Task<Player>>();
        var partyInfo = await GetPartyDetailsAsync().ConfigureAwait(false);

        await GetPartyPlayers(partyInfo, playerTasks);

        playerList.AddRange(await Task.WhenAll(playerTasks).ConfigureAwait(false));

        return playerList;
    }

    private static async Task<IgnData> GetIgcUsernameAsync(
        Guid puuid,
        bool isIncognito,
        bool inParty
    )
    {
        IgnData ignData = new();
        ignData.TrackerEnabled = Visibility.Hidden;
        ignData.TrackerDisabled = Visibility.Visible;

        if (isIncognito && !inParty)
        {
            ignData.Username = "----";
            return ignData;
        }

        ignData.Username = await GetNameServiceGetUsernameAsync(puuid).ConfigureAwait(false);

        var trackerUri = await TrackerAsync(ignData.Username).ConfigureAwait(false);

        if (trackerUri != null)
        {
            ignData.TrackerEnabled = Visibility.Visible;
            ignData.TrackerDisabled = Visibility.Collapsed;
            ignData.TrackerUri = trackerUri;
            ignData.Username = ignData.Username + " 🔗";
        }

        return ignData;
    }

    private static async Task<IdentityData> GetAgentInfoAsync(Guid agentid)
    {
        IdentityData identityData = new();

        if (agentid == Guid.Empty)
        {
            Constants.Log.Error("GetAgentInfoAsync Failed: AgentID is empty");
            identityData.Image = null;
            identityData.Name = "";
            return identityData;
        }

        identityData.Image = new Uri(
            Constants.LocalAppDataPath + $"\\ValAPI\\agentsimg\\{agentid}.png"
        );
        var agents = JsonSerializer.Deserialize<Dictionary<Guid, string>>(
            await File.ReadAllTextAsync(Constants.LocalAppDataPath + "\\ValAPI\\agents.json")
                .ConfigureAwait(false)
        );
        agents.TryGetValue(agentid, out var agentName);
        identityData.Name = agentName;
        return identityData;
    }

    private static async Task<IdentityData> GetCardAsync(Guid cardid, sbyte index)
    {
        if (cardid != Guid.Empty)
        {
            var cards = JsonSerializer.Deserialize<Dictionary<Guid, ValNameImage>>(
                await File.ReadAllTextAsync(Constants.LocalAppDataPath + "\\ValAPI\\cards.json")
                    .ConfigureAwait(false)
            );
            cards.TryGetValue(cardid, out var card);
            return new IdentityData
            {
                Image = card.Image,
                Name = Resources.Player + " " + (index + 1)
            };
        }

        Constants.Log.Error("GetCardAsync Failed: CardID is empty");
        return new IdentityData();
    }

    private static async Task<SkinData> GetMatchSkinInfoAsync(sbyte playerno, Guid cardid)
    {
        var response = await DoCachedRequestAsync(
                Method.Get,
                $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/core-game/v1/matches/{Matchid}/loadouts",
                true
            )
            .ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            var content = JsonSerializer.Deserialize<MatchLoadoutsResponse>(response.Content);
            return await GetSkinInfoAsync(content.Loadouts[playerno].Loadout, cardid)
                .ConfigureAwait(false);
        }

        Constants.Log.Error("GetMatchSkinInfoAsync Failed: {e}", response.ErrorException);
        return new SkinData();
    }

    private static async Task<SkinData> GetPreSkinInfoAsync(sbyte playerno, Guid cardid)
    {
        var response = await DoCachedRequestAsync(
                Method.Get,
                $"https://glz-{Constants.Shard}-1.{Constants.Region}.a.pvp.net/pregame/v1/matches/{Matchid}/loadouts",
                true
            )
            .ConfigureAwait(false);
        if (response.IsSuccessful)
            try
            {
                var content = JsonSerializer.Deserialize<PreMatchLoadoutsResponse>(
                    response.Content
                );
                return await GetSkinInfoAsync(content.Loadouts[playerno], cardid);
            }
            catch
            {
                // ignored
            }

        Constants.Log.Error("GetPreSkinInfoAsync Failed: {e}", response.ErrorException);
        return new SkinData();
    }

    private static async Task<SkinData> GetSkinInfoAsync(LoadoutLoadout loadout, Guid cardid)
    {
        Dictionary<Guid, ValCard> cards = null;
        Dictionary<Guid, ValNameImage> sprays = null;
        Dictionary<Guid, ValNameImage> skins = null;
        try
        {
            skins = JsonSerializer.Deserialize<Dictionary<Guid, ValNameImage>>(
                await File.ReadAllTextAsync(
                        Constants.LocalAppDataPath + "\\ValAPI\\skinchromas.json"
                    )
                    .ConfigureAwait(false)
            );
            cards = JsonSerializer.Deserialize<Dictionary<Guid, ValCard>>(
                await File.ReadAllTextAsync(Constants.LocalAppDataPath + "\\ValAPI\\cards.json")
                    .ConfigureAwait(false)
            );
            sprays = JsonSerializer.Deserialize<Dictionary<Guid, ValNameImage>>(
                await File.ReadAllTextAsync(Constants.LocalAppDataPath + "\\ValAPI\\sprays.json")
                    .ConfigureAwait(false)
            );
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetSkinInfoAsync failed: {e}", e);
        }

        var skinData = new SkinData
        {
            CardImage = cards[cardid].Image,
            LargeCardImage = cards[cardid].FullImage,
            CardName = cards[cardid].Name,
            Spray1Image = sprays[loadout.Sprays.SpraySelections[0].SprayId].Image,
            Spray1Name = sprays[loadout.Sprays.SpraySelections[0].SprayId].Name,
            Spray2Image = sprays[loadout.Sprays.SpraySelections[1].SprayId].Image,
            Spray2Name = sprays[loadout.Sprays.SpraySelections[1].SprayId].Name,
            Spray3Image = sprays[loadout.Sprays.SpraySelections[2].SprayId].Image,
            Spray3Name = sprays[loadout.Sprays.SpraySelections[2].SprayId].Name,
            ClassicImage = skins[
                loadout.Items["29a0cfab-485b-f5d5-779a-b59f85e204a8"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            ClassicName = skins[
                loadout.Items["29a0cfab-485b-f5d5-779a-b59f85e204a8"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            ShortyImage = skins[
                loadout.Items["42da8ccc-40d5-affc-beec-15aa47b42eda"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            ShortyName = skins[
                loadout.Items["42da8ccc-40d5-affc-beec-15aa47b42eda"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            FrenzyImage = skins[
                loadout.Items["44d4e95c-4157-0037-81b2-17841bf2e8e3"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            FrenzyName = skins[
                loadout.Items["44d4e95c-4157-0037-81b2-17841bf2e8e3"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            GhostImage = skins[
                loadout.Items["1baa85b4-4c70-1284-64bb-6481dfc3bb4e"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            GhostName = skins[
                loadout.Items["1baa85b4-4c70-1284-64bb-6481dfc3bb4e"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            SheriffImage = skins[
                loadout.Items["e336c6b8-418d-9340-d77f-7a9e4cfe0702"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            SheriffName = skins[
                loadout.Items["e336c6b8-418d-9340-d77f-7a9e4cfe0702"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            StingerImage = skins[
                loadout.Items["f7e1b454-4ad4-1063-ec0a-159e56b58941"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            StingerName = skins[
                loadout.Items["f7e1b454-4ad4-1063-ec0a-159e56b58941"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            SpectreImage = skins[
                loadout.Items["462080d1-4035-2937-7c09-27aa2a5c27a7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            SpectreName = skins[
                loadout.Items["462080d1-4035-2937-7c09-27aa2a5c27a7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            BuckyImage = skins[
                loadout.Items["910be174-449b-c412-ab22-d0873436b21b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            BuckyName = skins[
                loadout.Items["910be174-449b-c412-ab22-d0873436b21b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            JudgeImage = skins[
                loadout.Items["ec845bf4-4f79-ddda-a3da-0db3774b2794"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            JudgeName = skins[
                loadout.Items["ec845bf4-4f79-ddda-a3da-0db3774b2794"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            BulldogImage = skins[
                loadout.Items["ae3de142-4d85-2547-dd26-4e90bed35cf7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            BulldogName = skins[
                loadout.Items["ae3de142-4d85-2547-dd26-4e90bed35cf7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            GuardianImage = skins[
                loadout.Items["4ade7faa-4cf1-8376-95ef-39884480959b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            GuardianName = skins[
                loadout.Items["4ade7faa-4cf1-8376-95ef-39884480959b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            PhantomImage = skins[
                loadout.Items["ee8e8d15-496b-07ac-e5f6-8fae5d4c7b1a"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            PhantomName = skins[
                loadout.Items["ee8e8d15-496b-07ac-e5f6-8fae5d4c7b1a"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            VandalImage = skins[
                loadout.Items["9c82e19d-4575-0200-1a81-3eacf00cf872"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            VandalName = skins[
                loadout.Items["9c82e19d-4575-0200-1a81-3eacf00cf872"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            MarshalImage = skins[
                loadout.Items["c4883e50-4494-202c-3ec3-6b8a9284f00b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            MarshalName = skins[
                loadout.Items["c4883e50-4494-202c-3ec3-6b8a9284f00b"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            OutlawImage = skins[
                loadout.Items["5f0aaf7a-4289-3998-d5ff-eb9a5cf7ef5c"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            OutlawName = skins[
                loadout.Items["5f0aaf7a-4289-3998-d5ff-eb9a5cf7ef5c"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            OperatorImage = skins[
                loadout.Items["a03b24d3-4319-996d-0f8c-94bbfba1dfc7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            OperatorName = skins[
                loadout.Items["a03b24d3-4319-996d-0f8c-94bbfba1dfc7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            AresImage = skins[
                loadout.Items["55d8a0f4-4274-ca67-fe2c-06ab45efdf58"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            AresName = skins[
                loadout.Items["55d8a0f4-4274-ca67-fe2c-06ab45efdf58"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            OdinImage = skins[
                loadout.Items["63e6c2b6-4a8e-869c-3d4c-e38355226584"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            OdinName = skins[
                loadout.Items["63e6c2b6-4a8e-869c-3d4c-e38355226584"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name,
            MeleeImage = skins[
                loadout.Items["2f59173c-4bed-b6c3-2191-dea9b58be9c7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Image,
            MeleeName = skins[
                loadout.Items["2f59173c-4bed-b6c3-2191-dea9b58be9c7"].Sockets[
                    "3ad1b2b2-acdb-4524-852f-954a76ddae0a"
                ]
                    .Item
                    .Id
            ].Name
        };
        if (skinData == null)
            Constants.Log.Error("GetSkinInfoAsync failed: skinData is null");

        return skinData;
    }

    public static async Task<MatchHistoryData> GetMatchHistoryAsync(Guid puuid)
    {
        MatchHistoryData history =
            new()
            {
                PreviousGameColours = new string[3] { "#7f7f7f", "#7f7f7f", "#7f7f7f" },
                PreviousGames = new int[3]
            };

        try
        {
            if (puuid == Guid.Empty)
            {
                Constants.Log.Error("GetMatchHistoryAsync: Puuid is null");
                return history;
            }
            var response = await DoCachedRequestAsync(
                    Method.Get,
                    $"https://pd.{Constants.Region}.a.pvp.net/mmr/v1/players/{puuid}/competitiveupdates?queue=competitive",
                    true
                )
                .ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                Constants.Log.Error(
                    "GetMatchHistoryAsync request failed: {e}",
                    response.ErrorException
                );
                return history;
            }

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            };
            var content = JsonSerializer.Deserialize<CompetitiveUpdatesResponse>(
                response.Content,
                options
            );

            if (content?.Matches.Length == 0)
            {
                return history;
            }

            history.RankProgress = content.Matches[0].RankedRatingAfterUpdate;

            for (int i = 0; i < 3; i++)
            {
                if (i > content.Matches.Length)
                    break;
                var match = content.Matches[i].RankedRatingEarned;
                history.PreviousGames[i] = Math.Abs(match);
                history.PreviousGameColours[i] = match switch
                {
                    > 0 => "#32e2b2",
                    < 0 => "#ff4654",
                    _ => "#7f7f7f"
                };
            }
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetMatchHistoryAsync failed: {e}", e);
        }

        return history;
    }

    private static async Task<RankData> GetPlayerHistoryAsync(Guid puuid, Guid[] seasonData)
    {
        var rankData = new RankData();
        var ranks = new int[4];

        rankData.RankImages = new Uri[ranks.Length];
        rankData.RankNames = new string[ranks.Length];
        Array.Fill(
            rankData.RankImages,
            new Uri(Constants.LocalAppDataPath + $"\\ValAPI\\ranksimg\\0.png")
        );
        Array.Fill(rankData.RankNames, "UNRATED");

        if (puuid == Guid.Empty)
        {
            Constants.Log.Error("GetPlayerHistoryAsync Failed: PUUID is empty");
            return rankData;
        }
        var response = await DoCachedRequestAsync(
                Method.Get,
                $"https://pd.{Constants.Region}.a.pvp.net/mmr/v1/players/{puuid}",
                true
            )
            .ConfigureAwait(false);

        if (!response.IsSuccessful && response.Content != null)
        {
            Constants.Log.Error("GetPlayerHistoryAsync Failed: {e}", response.ErrorException);
            return rankData;
        }

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        var content = JsonSerializer.Deserialize<MmrResponse>(response.Content, options);

        if (content.QueueSkills.Competitive.SeasonalInfoBySeasonId is null)
        {
            Constants.Log.Error("GetPlayerHistoryAsync Failed; seasonInfoById is null");
            return rankData;
        }

        var SeasonInfo = content.QueueSkills.Competitive.SeasonalInfoBySeasonId.Act;

        for (int i = 0; i < ranks.Length; i++)
        {
            if (!SeasonInfo.TryGetValue(seasonData[i].ToString(), out var currentActJsonElement))
                continue;

            var act = currentActJsonElement.Deserialize<ActInfo>();
            var rank = act.CompetitiveTier;

            if (rank is 1 or 2)
                rank = 0;
            if (Constants.BeforeAscendantSeasons.Contains(seasonData[i]))
                rank += 3;

            ranks[i] = rank;
        }

        if (ranks[0] >= 24)
        {
            var leaderboardResponse = await DoCachedRequestAsync(
                    Method.Get,
                    $"https://pd.{Constants.Shard}.a.pvp.net/mmr/v1/leaderboards/affinity/{Constants.Region}/queue/competitive/season/{seasonData[0]}?startIndex=0&size=0",
                    true
                )
                .ConfigureAwait(false);
            if (leaderboardResponse.Content != null && leaderboardResponse.IsSuccessful)
            {
                var leaderboardcontent = JsonSerializer.Deserialize<LeaderboardsResponse>(
                    leaderboardResponse.Content
                );
                try
                {
                    rankData.MaxRr = leaderboardcontent.TierDetails[
                        ranks[0].ToString()
                    ].RankedRatingThreshold;
                }
                catch (Exception e)
                {
                    Constants.Log.Error(
                        "GetPlayerHistoryAsync Failed; leaderboardcontent error: {e}",
                        e
                    );
                }
            }
            else
            {
                Constants.Log.Error(
                    "GetPlayerHistoryAsync Failed; leaderboardResponse error: {e}",
                    leaderboardResponse.ErrorException
                );
            }
        }

        try
        {
            var rankNames = JsonSerializer.Deserialize<Dictionary<int, string>>(
                await File.ReadAllTextAsync(
                        Constants.LocalAppDataPath + "\\ValAPI\\competitivetiers.json"
                    )
                    .ConfigureAwait(false)
            );

            for (int i = 0; i < ranks.Length; i++)
            {
                rankNames.TryGetValue(ranks[i], out var rank);
                rankData.RankImages[i] = new Uri(
                    Constants.LocalAppDataPath + $"\\ValAPI\\ranksimg\\{ranks[i]}.png"
                );
                rankData.RankNames[i] = rank;
            }
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetPlayerHistoryAsync Failed; rank dictionary error: {e}", e);
        }

        return rankData;
    }

    private static async Task<Guid[]> GetSeasonsAsync()
    {
        var seasonData = new Guid[4];
        try
        {
            var response = await DoCachedRequestAsync(
                Method.Get,
                $"https://shared.{Constants.Region}.a.pvp.net/content-service/v3/content",
                true
            );

            if (!response.IsSuccessful)
            {
                Constants.Log.Error("GetSeasonsAsync Failed: {e}", response.ErrorException);
                return seasonData;
            }

            var data = JsonSerializer.Deserialize<ContentResponse>(response.Content);

            sbyte index = 0;
            var seasons = data.Seasons.Reverse();
            var acts = seasons.Where(season => season.Type == "act");

            foreach (var act in acts)
            {
                if (index >= seasonData.Length)
                    break;
                if (index > 0)
                {
                    seasonData[index] = act.Id;
                    index++;
                }
                if (index == 0 & act.IsActive)
                {
                    seasonData[0] = act.Id;
                    index++;
                }
            }
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetSeasonsAsync Failed: {Exception}", e);
        }

        return seasonData;
    }

    private static async Task<Uri> TrackerAsync(string username)
    {
        if (string.IsNullOrEmpty(username))
            return null;
        try
        {
            var encodedUsername = Uri.EscapeDataString(username);
            var url = new Uri("https://tracker.gg/valorant/profile/riot/" + encodedUsername);
            var response = await DoCachedRequestAsync(
                    Method.Get,
                    url.ToString(),
                    false,
                    false,
                    false
                )
                .ConfigureAwait(false);
            var numericStatusCode = (short)response.StatusCode;
            if (numericStatusCode == 200)
                return url;
        }
        catch (Exception e)
        {
            Constants.Log.Error("TrackerAsync Failed: {Exception}", e);
        }
        return null;
    }

    private static async Task<PresencesResponse> GetPresencesAsync()
    {
        var options = new RestClientOptions($"https://127.0.0.1:{Constants.Port}/chat/v4/presences")
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                true
        };
        var client = new RestClient(options);
        var base64String = "";
        try
        {
            base64String = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"riot:{Constants.LPassword}")
            );
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetPresencesAsync Failed; To Base 64 failed: {Exception}", e);
            return null;
        }

        var request = new RestRequest()
            .AddHeader("Authorization", $"Basic {base64String}")
            .AddHeader("X-Riot-ClientPlatform", Constants.Platform)
            .AddHeader("X-Riot-ClientVersion", Constants.Version);
        client.UseSystemTextJson(
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            }
        );
        var response = await client
            .ExecuteGetAsync<PresencesResponse>(request)
            .ConfigureAwait(false);
        if (response.IsSuccessful)
            return response.Data;
        Constants.Log.Error(
            "GetPresencesAsync Failed: {e}. Presences: {presences}",
            response.ErrorException,
            response.Data
        );
        return null;
    }

    private async Task<PlayerUIData> GetPresenceInfoAsync(Guid puuid, PresencesResponse presences)
    {
        PlayerUIData playerUiData = new() { BackgroundColour = "#252A40", Puuid = puuid };

        try
        {
            var friend = presences.Presences.First(friend => friend.Puuid == puuid);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(friend.Private));
            var content = JsonSerializer.Deserialize<PresencesPrivate>(json);
            if (content == null)
                return playerUiData;

            playerUiData.PartyUuid = content.PartyId;

            if (puuid != Constants.Ppuuid)
                return playerUiData;

            var maps = JsonSerializer.Deserialize<Dictionary<string, ValMap>>(
                await File.ReadAllTextAsync(Constants.LocalAppDataPath + "\\ValAPI\\maps.json")
                    .ConfigureAwait(false)
            );

            maps.TryGetValue(content.MatchMap, out var map);
            MatchInfo.Map = map.Name;
            MatchInfo.MapImage = new Uri(
                Constants.LocalAppDataPath + $"\\ValAPI\\mapsimg\\{map.UUID}.png"
            );
            playerUiData.BackgroundColour = "#181E34";
            Constants.PPartyId = content.PartyId;

            if (content?.ProvisioningFlow == "CustomGame")
            {
                MatchInfo.GameMode = "Custom";
                MatchInfo.GameModeImage = new Uri(
                    Constants.LocalAppDataPath
                        + "\\ValAPI\\gamemodeimg\\96bd3920-4f36-d026-2b28-c683eb0bcac5.png"
                );
                return playerUiData;
            }
            var textInfo = new CultureInfo("en-US", false).TextInfo;

            var gameModeName = "";
            var gameModeId = Guid.Parse("96bd3920-4f36-d026-2b28-c683eb0bcac5");
            QueueId = content?.QueueId;
            Status = content?.SessionLoopState;

            switch (content?.QueueId)
            {
                case "competitive":
                    gameModeName = "Competitive";
                    break;
                case "unrated":
                    gameModeName = "Unrated";
                    break;
                case "deathmatch":
                    gameModeId = Guid.Parse("a8790ec5-4237-f2f0-e93b-08a8e89865b2");
                    break;
                case "spikerush":
                    gameModeId = Guid.Parse("e921d1e6-416b-c31f-1291-74930c330b7b");
                    break;
                case "ggteam":
                    gameModeId = Guid.Parse("a4ed6518-4741-6dcb-35bd-f884aecdc859");
                    break;
                case "newmap":
                    gameModeName = "New Map";
                    break;
                case "onefa":
                    gameModeId = Guid.Parse("96bd3920-4f36-d026-2b28-c683eb0bcac5");
                    break;
                case "snowball":
                    gameModeId = Guid.Parse("57038d6d-49b1-3a74-c5ef-3395d9f23a97");
                    break;
                default:
                    gameModeName = textInfo.ToTitleCase(content.QueueId);
                    break;
            }

            MatchInfo.GameMode = gameModeName;

            if (string.IsNullOrEmpty(gameModeName))
            {
                var gamemodes = JsonSerializer.Deserialize<Dictionary<Guid, string>>(
                    await File.ReadAllTextAsync(
                            Constants.LocalAppDataPath + "\\ValAPI\\gamemode.json"
                        )
                        .ConfigureAwait(false)
                );
                gamemodes.TryGetValue(gameModeId, out var gamemode);
                MatchInfo.GameMode = gamemode;
            }

            MatchInfo.GameModeImage = new Uri(
                Constants.LocalAppDataPath + $"\\ValAPI\\gamemodeimg\\{gameModeId}.png"
            );
        }
        catch (InvalidOperationException)
        {
            return playerUiData;
        }
        catch (Exception e)
        {
            Constants.Log.Error("GetPresenceInfoAsync Failed; To Base 64 failed: {Exception}", e);
        }

        return playerUiData;
    }
}
