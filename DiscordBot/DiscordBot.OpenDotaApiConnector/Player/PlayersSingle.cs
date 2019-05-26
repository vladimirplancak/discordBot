using Newtonsoft.Json.Linq;
using System;

namespace DiscordBot.OpenDotaApiConnector.Player
{
    public class PlayersSingle
    {
        #region Properties
        public string TrackedUntil { get; set; }
        public long AccountId { get; set; }
        public string PersonName { get; set; }
        public string Name { get; set; }
        public string SteamId { get; set; }
        public string Avatar { get; set; }
        public string AvatarMedium { get; set; }
        public string AvatarFull { get; set; }
        public string ProfileUrl { get; set; }
        public DateTime LastLogin { get; set; }
        public string LocCountryCode { get; set; }
        public bool IsContributor { get; set; }
        public int? MmrEstimate { get; set; }
        public int? SoloCompetitiveRank { get; set; }
        public int? RankTier { get; set; }
        public int? LeaderBoardRank  { get; set; }
        public int? CompetitiveRank { get; set; }
        #endregion

        public PlayersSingle(string json)
        {
            JObject obj = JObject.Parse(json);

            TrackedUntil = obj["tracked_until"].Value<string>();
            JToken prof = obj["profile"];
            AccountId = prof["account_id"].Value<int>();
            PersonName = prof["personaname"].Value<string>();
            Name = prof["name"].Value<string>();
            SteamId = prof["steamid"].Value<string>();
            Avatar = prof["avatar"].Value<string>();
            AvatarMedium = prof["avatarmedium"].Value<string>();
            AvatarFull = prof["avatarfull"].Value<string>();
            ProfileUrl = prof["profileurl"].Value<string>();
            LastLogin = prof["last_login"].Value<DateTime>();
            LocCountryCode = prof["loccountrycode"].Value<string>();
            IsContributor = prof["is_contributor"].Value<bool>();
            MmrEstimate = obj["mmr_estimate"]["estimate"].Value<int?>();
            SoloCompetitiveRank = obj["solo_competitive_rank"].Value<int?>();
            RankTier = obj["rank_tier"].Value<int?>();
            LeaderBoardRank = obj["leaderboard_rank"].Value<int?>();
            CompetitiveRank = obj["competitive_rank"].Value<int?>();
        }

        
    }
}
