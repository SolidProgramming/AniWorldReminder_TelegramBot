﻿using AniWorldModels = AniWorldReminder_TelegramBot.Models.AniWorld;
using DBModels = AniWorldReminder_TelegramBot.Models.DB;
using Dapper;
using MySql.Data.MySqlClient;
using AniWorldReminder_TelegramBot.Misc;
using AniWorldReminder_TelegramBot.Models.DB;
using AniWorldReminder_TelegramBot.Enums;

namespace AniWorldReminder_TelegramBot.Services
{
    public class DBService(ILogger<DBService> logger) : IDBService
    {
        private string? DBConnectionString;

        public async Task<bool> Init()
        {
            DatabaseSettingsModel? settings = SettingsHelper.ReadSettings<DatabaseSettingsModel>();

            if (settings is null)
            {
                logger.LogError(ErrorMessage.ReadSettings);
                throw new Exception(ErrorMessage.ReadSettings);
            }

            DBConnectionString = $"server={settings.Ip};port=3306;database={settings.Database};user={settings.Username};password={settings.Password};";

            if (!await TestDBConnection())
                return false;

            logger.LogInformation($"{DateTime.Now} | DB Service initialized");

            return true;
        }
        private async Task<bool> TestDBConnection()
        {
            try
            {
                using (MySqlConnection connection = new(DBConnectionString))
                {
                    await connection.OpenAsync();
                }

                logger.LogInformation($"{DateTime.Now} | Database reachablility ensured");

                return true;
            }
            catch (MySqlException ex)
            {
                logger.LogError($"{DateTime.Now} | DB connection could not be established. Error: " + ex.ToString());
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"{DateTime.Now} | {ex}");
                return false;
            }
        }
        public async Task<DBModels.UsersModel?> GetUserAsync(string telegramChatId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@TelegramChatId", telegramChatId }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT * FROM users WHERE TelegramChatId = @TelegramChatId";

            return await connection.QueryFirstOrDefaultAsync<DBModels.UsersModel>(query, parameters);
        }
        public async Task<DBModels.SeriesModel> GetSeriesAsync(string seriesName)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@Name", seriesName }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT * FROM series WHERE series.Name = @Name";

            return await connection.QueryFirstOrDefaultAsync<DBModels.SeriesModel>(query, parameters);
        }
        public async Task<UsersModel> InsertUserAsync(string telegramChatId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "INSERT INTO users (TelegramChatId, StateId) VALUES (@TelegramChatId, 0); " +
                "select LAST_INSERT_ID()";

            DBModels.UsersModel user = new()
            {
                TelegramChatId = telegramChatId
            };

            int id = await connection.ExecuteScalarAsync<int>(query, user);

            return new UsersModel()
            {
                Id = id,
                TelegramChatId = telegramChatId
            };
        }
        public async Task<int> InsertSeriesAsync(AniWorldModels.SeriesInfoModel seriesInfo, StreamingPortal streamingPortal)
        {
            if (string.IsNullOrEmpty(seriesInfo.Name))
                return -1;

            using MySqlConnection connection = new(DBConnectionString);

            string streamingPortalIdQuery = "SELECT id FROM streamingportals WHERE streamingportals.Name = @Name";

            string? streamingPortalName = StreamingPortalHelper.GetStreamingPortalName(streamingPortal);

            if (string.IsNullOrEmpty(streamingPortalName))
                return -1;

            Dictionary<string, object> dictionaryPortalId = new()
            {
                { "@Name", streamingPortalName },
            };

            DynamicParameters parametersPortalId = new(dictionaryPortalId);

            int streamingPortalId = await connection.QueryFirstOrDefaultAsync<int>(streamingPortalIdQuery, parametersPortalId);

            if (streamingPortalId < 1)
                return -1;

            string query = "INSERT INTO series (StreamingPortalId, Name, SeasonCount, EpisodeCount, CoverArtUrl) VALUES (@StreamingPortalId, @Name, @SeasonCount, @EpisodeCount, @CoverArtUrl); " +
                "select LAST_INSERT_ID()";

            Dictionary<string, object> dictionary = new()
            {
                { "@StreamingPortalId", streamingPortalId },
                { "@Name", seriesInfo.Name },
                { "@SeasonCount", seriesInfo.SeasonCount },
                { "@EpisodeCount", seriesInfo.Seasons.Last().EpisodeCount },
                { "@CoverArtUrl", seriesInfo.CoverArtUrl },
            };

            DynamicParameters parameters = new(dictionary);

            return await connection.ExecuteScalarAsync<int>(query, parameters);
        }
        public async Task<DBModels.UsersSeriesModel?> GetUsersSeriesAsync(string telegramChatId, string seriesName)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@TelegramChatId", telegramChatId },
                { "@seriesName", seriesName }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT users.*, series.*, users_series.* FROM users " +
                           "JOIN users_series ON users.id = users_series.UserId " +
                           "JOIN series ON users_series.SeriesId = series.id " +
                           "WHERE TelegramChatId = @TelegramChatId AND series.Name = @seriesName";

            IEnumerable<DBModels.UsersSeriesModel> users_series =
                await connection.QueryAsync<DBModels.UsersModel, DBModels.SeriesModel, DBModels.UsersSeriesModel, DBModels.UsersSeriesModel>
                (query, (users, series, users_series) =>
                {
                    return new DBModels.UsersSeriesModel()
                    {
                        Id = users_series.Id,
                        Users = users,
                        Series = series
                    };
                }, parameters);

            return users_series.FirstOrDefault();
        }
        public async Task<List<DBModels.UsersSeriesModel>?> GetUsersSeriesAsync()
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "SELECT users.*, series.*, users_series.* FROM users " +
                           "JOIN users_series ON users.id = users_series.UserId " +
                           "JOIN series ON users_series.SeriesId = series.id";

            IEnumerable<DBModels.UsersSeriesModel> users_series =
                await connection.QueryAsync<DBModels.UsersModel, DBModels.SeriesModel, DBModels.UsersSeriesModel, DBModels.UsersSeriesModel>
                (query, (users, series, users_series) =>
                {
                    return new DBModels.UsersSeriesModel()
                    {
                        Id = users_series.Id,
                        Users = users,
                        Series = series
                    };
                });

            return users_series.ToList();
        }
        public async Task<List<DBModels.UsersSeriesModel>?> GetUsersSeriesAsync(string telegramChatId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@TelegramChatId", telegramChatId }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT users.*, series.*, users_series.* FROM users " +
                           "JOIN users_series ON users.id = users_series.UserId " +
                           "JOIN series ON users_series.SeriesId = series.id " +
                           "WHERE TelegramChatId = @TelegramChatId";

            IEnumerable<DBModels.UsersSeriesModel> users_series =
                await connection.QueryAsync<DBModels.UsersModel, DBModels.SeriesModel, DBModels.UsersSeriesModel, DBModels.UsersSeriesModel>
                (query, (users, series, users_series) =>
                {
                    return new DBModels.UsersSeriesModel()
                    {
                        Id = users_series.Id,
                        Users = users,
                        Series = series
                    };
                }, parameters);

            return users_series.ToList();
        }
        public async Task InsertUsersSeriesAsync(DBModels.UsersSeriesModel usersSeries)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "INSERT INTO users_series (UserId, SeriesId) VALUES (@UserId, @SeriesId)";

            if (usersSeries.Users is null || usersSeries.Series is null)
                return;

            Dictionary<string, object> dictionary = new()
            {
                { "@UserId", usersSeries.Users.Id },
                { "@SeriesId", usersSeries.Series.Id }
            };

            DynamicParameters parameters = new(dictionary);

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task DeleteUsersSeriesAsync(DBModels.UsersSeriesModel usersSeries)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@id", usersSeries.Id }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "DELETE FROM users_series WHERE id = @id";

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task UpdateSeriesInfoAsync(int seriesId, AniWorldModels.SeriesInfoModel seriesInfo)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "UPDATE series " +
                            "SET series.SeasonCount = @SeasonCount, series.EpisodeCount = @EpisodeCount " +
                            "WHERE series.id = @id ";

            Dictionary<string, object> dictionary = new()
            {
                { "@id", seriesId },
                { "@SeasonCount", seriesInfo.SeasonCount },
                { "@EpisodeCount", seriesInfo.Seasons.Last().EpisodeCount },
            };

            DynamicParameters parameters = new(dictionary);

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task UpdateSeriesPathAsync(int seriesId, AniWorldModels.SeriesInfoModel seriesInfo)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "UPDATE series " +
                            "SET series.Path = @SeriesPath " +
                            "WHERE series.id = @id ";

            Dictionary<string, object> dictionary = new()
            {
                { "@id", seriesId },
                { "@SeriesPath", seriesInfo.Path },
            };

            DynamicParameters parameters = new(dictionary);

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task<List<DBModels.SeriesReminderModel>?> GetUsersReminderSeriesAsync()
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "SELECT DISTINCT users.*, series.*, streamingportals.*, users_series.* " +
                           "FROM users AS users " +
                           "JOIN users_series ON users.id = users_series.UserId " +
                           "JOIN series ON users_series.SeriesId = series.id " +
                           "JOIN streamingportals ON series.StreamingPortalId = streamingportals.id";

            IEnumerable<DBModels.SeriesReminderModel> reminderSeries =
           await connection.QueryAsync<DBModels.UsersModel, DBModels.SeriesModel, DBModels.StreamingPortalModel, UsersSeriesModel, DBModels.SeriesReminderModel>
           (query, (users, series, streamingportals, users_series) =>
           {
               DBModels.SeriesReminderModel reminderSerie = new()
               {
                   Series = series,
                   User = users,
                   Language = users_series.LanguageFlag
               };

               reminderSerie.Series.StreamingPortal = streamingportals;

               return reminderSerie;
           });

            if (reminderSeries is null)
                return null;

            return reminderSeries.ToList();
        }
        public async Task<(int seasonCount, int episodeCount)> GetSeriesSeasonEpisodeCountAsync(int seriesId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@id", seriesId }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT SeasonCount, EpisodeCount FROM series " +
                "WHERE id = @id";

            return await connection.QuerySingleOrDefaultAsync<(int, int)>(query, parameters);
        }
        public async Task<List<EpisodeModel>?> GetSeriesSeasonEpisodesAsync(int seriesId, int season)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@seriesId",  seriesId},
                { "@Season",  season}
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT series.Name, episodes.* FROM episodes " +
                           "JOIN series ON episodes.SeriesId = series.id " +
                           "WHERE series.id = @seriesId AND episodes.Season = @Season";

            IEnumerable<EpisodeModel>? episodes = await connection.QueryAsync<EpisodeModel>(query, parameters);

            return episodes.ToList();
        }
        public async Task<List<EpisodeModel>?> GetSeriesEpisodesAsync(int seriesId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@seriesId",  seriesId}
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT episodes.* FROM episodes " +
                           "JOIN series ON episodes.SeriesId = series.id " +
                           "WHERE series.id = @seriesId";

            IEnumerable<EpisodeModel>? episodes = await connection.QueryAsync<EpisodeModel>(query, parameters);

            return episodes.ToList();
        }
        public async Task InsertEpisodesAsync(int seriesId, List<EpisodeModel> episodes)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "INSERT INTO episodes (SeriesId, Season, Episode, Name, LanguageFlag) VALUES (@SeriesId, @Season, @Episode, @Name, @LanguageFlag)";

            Dictionary<string, object> dictionary;

            foreach (EpisodeModel episode in episodes)
            {
                if (string.IsNullOrEmpty(episode.Name))
                    continue;

                dictionary = new()
                {
                    { "@SeriesId",  seriesId},
                    { "@Season",  episode.Season},
                    { "@Episode",  episode.Episode},
                    { "@Name",  episode.Name},
                    { "@LanguageFlag", episode.LanguageFlag }
                };

                DynamicParameters parameters = new(dictionary);

                await connection.ExecuteAsync(query, parameters);
            }
        }
        public async Task<UserState> GetUserStateAsync(string telegramChatId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "SELECT states.id FROM states " +
                "INNER JOIN users ON states.id = users.StateId " +
                "WHERE users.TelegramChatId = @TelegramChatId";

            Dictionary<string, object> dictionary;

            dictionary = new()
            {
                { "@TelegramChatId",  telegramChatId}
            };

            DynamicParameters parameters = new(dictionary);

            return await connection.QuerySingleOrDefaultAsync<UserState>(query, parameters);
        }
        public async Task UpdateUserStateAsync(string telegramChatId, UserState userState)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "UPDATE users " +
                           "INNER JOIN states ON users.StateId = states.id " +
                           "SET users.StateId = @UserState " +
                           "WHERE users.TelegramChatId = @TelegramChatId";

            Dictionary<string, object> dictionary = new()
            {
                { "@UserState", (int)userState },
                { "@TelegramChatId", telegramChatId }
            };

            DynamicParameters parameters = new(dictionary);

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task UpdateVerifyTokenAsync(string telegramChatId, string token)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "UPDATE users " +
                           "SET users.VerifyToken = @VerifyToken " +
                           "WHERE users.TelegramChatId = @TelegramChatId";

            Dictionary<string, object> dictionary = new()
            {
                { "@VerifyToken", token },
                { "@TelegramChatId", telegramChatId }
            };

            DynamicParameters parameters = new(dictionary);

            await connection.ExecuteAsync(query, parameters);
        }
        public async Task InsertDownloadAsync(int seriesId, int usersId, List<EpisodeModel> episodes)
        {
            using MySqlConnection connection = new(DBConnectionString);

            string query = "INSERT INTO download (SeriesId, UsersId, Season, Episode, LanguageFlag) VALUES (@SeriesId, @UsersId, @Season, @Episode, @LanguageFlag)";

            Dictionary<string, object> dictionary;

            foreach (EpisodeModel episode in episodes)
            {
                dictionary = new()
                {
                    { "@SeriesId",  seriesId},
                    { "@UsersId",  usersId},
                    { "@Season",  episode.Season},
                    { "@Episode",  episode.Episode},
                    { "@LanguageFlag",  episode.LanguageFlag}
                };

                DynamicParameters parameters = new(dictionary);

                await connection.ExecuteAsync(query, parameters);
            }
        }
        public async Task UpdateEpisodesAsync(int seriesId, List<EpisodeModel> episodes)
        {
            using MySqlConnection connection = new(DBConnectionString);

            foreach (EpisodeModel episode in episodes)
            {
                string query = "UPDATE episodes " +
                           "SET episodes.LanguageFlag = @LanguageFlag, episodes.Name = @EpisodeName " +
                           "WHERE episodes.Id = @EpisodeId";

                Dictionary<string, object> dictionary = new()
                {
                    { "@LanguageFlag", episode.LanguageFlag },
                    { "@EpisodeId",  episode.Id},
                    { "@EpisodeName",  episode.Name}
                };

                DynamicParameters parameters = new(dictionary);

                await connection.ExecuteAsync(query, parameters);
            }
        }
        public async Task<UserWebsiteSettings?> GetUserWebsiteSettings(string telegramChatId)
        {
            using MySqlConnection connection = new(DBConnectionString);

            Dictionary<string, object> dictionary = new()
            {
                { "@TelegramChatId", telegramChatId }
            };

            DynamicParameters parameters = new(dictionary);

            string query = "SELECT users_settings.* FROM users_settings " +
                "LEFT JOIN users ON users_settings.UserId = users.id " +
                "WHERE users.TelegramChatId = @TelegramChatId";

            return await connection.QueryFirstOrDefaultAsync<UserWebsiteSettings>(query, parameters);
        }
    }
}
