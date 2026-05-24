using AppConfigurations.Repository;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Configs;
using MaktabDataContracts.Responses.Configs;
using System.Data.Common;

namespace AppConfigurations.Repository.Implementation
{
    public class AppConfigRepository : DbRepository, IAppConfigRepository
    {
        public AppConfigRepository(IDatabase database) : base(database)
        {
        }

        public async Task<AppConfigResponse> AddAppConfig(AddAppConfigRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            var appConfigId = Guid.NewGuid();
            var createdAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;
            var updatedOn = request.UpdatedOn == default ? createdAt : request.UpdatedOn;

            cmd.CommandText = @"
                INSERT INTO app_config
                (Id, Content, ConfigurationType, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@Id, @Content, @ConfigurationType, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@Id", appConfigId.ToByteArray());
            cmd.AddParameter("@Content", request.Content);
            cmd.AddParameter("@ConfigurationType", (int)request.ConfigurationType);
            cmd.AddParameter("@IsActive", request.IsActive);
            cmd.AddParameter("@CreatedAt", createdAt);
            cmd.AddParameter("@UpdatedOn", updatedOn);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return await GetAppConfig(appConfigId).ConfigureAwait(false);
        }

        public async Task<AppConfigResponse> GetAppConfig(Guid appConfigId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM app_config WHERE Id = @Id";
            cmd.AddParameter("@Id", appConfigId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapToResponse(reader);
        }

        public async Task<IEnumerable<AppConfigResponse>> GetAllAppConfigs(bool onlyActive = true)
        {
            var responses = new List<AppConfigResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM app_config";

            if (onlyActive)
            {
                cmd.CommandText += " WHERE IsActive = @IsActive";
                cmd.AddParameter("@IsActive", true);
            }

            cmd.CommandText += " ORDER BY CreatedAt DESC";

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                responses.Add(MapToResponse(reader));
            }

            return responses;
        }

        public async Task<AppConfigResponse> GetLatestAppConfigByType(ConfigurationType configurationType, bool onlyActive = true)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT *
                FROM app_config
                WHERE ConfigurationType = @ConfigurationType";

            cmd.AddParameter("@ConfigurationType", (int)configurationType);

            if (onlyActive)
            {
                cmd.CommandText += " AND IsActive = @IsActive";
                cmd.AddParameter("@IsActive", true);
            }

            cmd.CommandText += " ORDER BY UpdatedOn DESC, CreatedAt DESC LIMIT 1";

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapToResponse(reader);
        }

        public async Task<bool> UpdateAppConfig(Guid appConfigId, UpdateAppConfigRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE app_config
                SET Content = @Content,
                    ConfigurationType = @ConfigurationType,
                    IsActive = @IsActive,
                    UpdatedOn = @UpdatedOn
                WHERE Id = @Id";

            cmd.AddParameter("@Id", appConfigId.ToByteArray());
            cmd.AddParameter("@Content", request.Content);
            cmd.AddParameter("@ConfigurationType", (int)request.ConfigurationType);
            cmd.AddParameter("@IsActive", request.IsActive);
            cmd.AddParameter("@UpdatedOn", request.UpdatedOn == default ? DateTime.UtcNow : request.UpdatedOn);

            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        public async Task<bool> DeleteAppConfig(Guid appConfigId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = @"DELETE FROM app_config WHERE Id = @Id";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE app_config
                    SET IsActive = FALSE,
                        UpdatedOn = @UpdatedOn
                    WHERE Id = @Id";
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@Id", appConfigId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        private static AppConfigResponse MapToResponse(DbDataReader reader)
        {
            return new AppConfigResponse
            {
                Id = reader.GetGuidFromByteArray("Id"),
                Content = reader.GetStringOrDefault("Content"),
                ConfigurationType = (MaktabDataContracts.Enums.ConfigurationType)(reader.GetNullableInt("ConfigurationType") ?? 0),
                IsActive = reader.GetBooleanOrDefault("IsActive"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
            };
        }
    }
}
