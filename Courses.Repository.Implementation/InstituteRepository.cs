using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class InstituteRepository : DbRepository, IInstituteRepository
    {
        public InstituteRepository(IDatabase database) : base(database) { }

        public async Task<InstituteResponse> AddInstitute(AddInstitute institute)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO institutes (InstituteId, Name, AddressId, IsActive, CreatedAt, UpdatedOn)
                            VALUES (@InstituteId, @Name, @AddressId, @IsActive, @CreatedAt, @UpdatedOn)";
            var instituteId = Guid.NewGuid();
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@Name", institute.Name);
            cmd.AddParameter("@AddressId", institute.AddressId.ToByteArray());
            cmd.AddParameter("@IsActive", true);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
            return await GetInstitute(instituteId);
        }

        public async Task<InstituteResponse> GetInstitute(Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM institutes WHERE InstituteId = @InstituteId";
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapToInstituteResponse(reader);
        }

        public async Task<IEnumerable<InstituteResponse>> GetAllInstitutes(bool onlyActive = true)
        {
            var results = new List<InstituteResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM institutes";
            if (onlyActive) cmd.CommandText += " WHERE IsActive = TRUE";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToInstituteResponse(reader));
            }
            return results;
        }

        public async Task<bool> UpdateInstitute(Guid instituteId, AddInstitute institute)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE institutes SET Name = @Name, AddressId = @AddressId, UpdatedOn = @UpdatedOn WHERE InstituteId = @InstituteId";
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@Name", institute.Name);
            cmd.AddParameter("@AddressId", institute.AddressId.ToByteArray());
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteInstitute(Guid instituteId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            if (hardDelete)
                cmd.CommandText = @"DELETE FROM institutes WHERE InstituteId = @InstituteId";
            else
                cmd.CommandText = @"UPDATE institutes SET IsActive = FALSE WHERE InstituteId = @InstituteId";
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private InstituteResponse MapToInstituteResponse(DbDataReader reader)
        {
            return new InstituteResponse
            {
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                Name = reader.GetString("Name"),
                AddressId = reader.GetGuidFromByteArray("AddressId"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }
}
