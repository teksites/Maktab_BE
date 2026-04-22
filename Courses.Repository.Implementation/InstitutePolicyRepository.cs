using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;
using MaktabDataContracts.Enums;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class InstitutePolicyRepository : DbRepository, IInstitutePolicyRepository
    {
        public InstitutePolicyRepository(IDatabase database) : base(database) { }

        public async Task<InstitutePolicyResponse> AddPolicy(AddInstitutePolicy policy)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            var policyId = Guid.NewGuid();

            cmd.CommandText = @"INSERT INTO institute_policy 
                                (InstitutePolicyId, InstituteId, Details, InstutePolicyType, IsActive, CreatedAt, UpdatedOn) 
                                VALUES 
                                (@InstitutePolicyId, @InstituteId, @Details, @InstutePolicyType, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@InstitutePolicyId", policyId.ToByteArray());
            cmd.AddParameter("@InstituteId", policy.InstituteId.ToByteArray());
            cmd.AddParameter("@Details", policy.Details);
            cmd.AddParameter("@InstutePolicyType", (byte)policy.InstutePolicy);
            cmd.AddParameter("@IsActive", policy.IsActive);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            return await GetPolicy(policyId);
        }

        public async Task<InstitutePolicyResponse> GetPolicy(Guid policyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM institute_policy WHERE InstitutePolicyId = @InstitutePolicyId";
            cmd.AddParameter("@InstitutePolicyId", policyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return MapToPolicyResponse(reader);
        }

        /// <summary>
        /// Returns all policies for an institute. If isActiveFilter is not null, it will filter on IsActive.
        /// </summary>
        public async Task<IEnumerable<InstitutePolicyResponse>> GetAllPolicies(Guid instituteId, bool? isActiveFilter = null)
        {
            var results = new List<InstitutePolicyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM institute_policy WHERE InstituteId = @InstituteId";
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            if (isActiveFilter.HasValue)
            {
                cmd.CommandText += " AND IsActive = @IsActive";
                cmd.AddParameter("@IsActive", isActiveFilter.Value);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToPolicyResponse(reader));
            }
            return results;
        }

        public async Task<bool> UpdatePolicy(UpdateInstitutePolicy policy)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE institute_policy 
                                SET Details = @Details, 
                                    InstutePolicyType = @InstutePolicyType, 
                                    IsActive = @IsActive, 
                                    UpdatedOn = @UpdatedOn 
                                WHERE InstitutePolicyId = @InstitutePolicyId";

            cmd.AddParameter("@InstitutePolicyId", policy.InstitutePolicyId.ToByteArray());
            cmd.AddParameter("@Details", policy.Details);
            cmd.AddParameter("@InstutePolicyType", (byte)policy.InstutePolicy);
            cmd.AddParameter("@IsActive", policy.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeletePolicy(Guid policyId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM institute_policy WHERE InstitutePolicyId = @InstitutePolicyId";
            else
                cmd.CommandText = @"UPDATE institute_policy SET IsActive = FALSE WHERE InstitutePolicyId = @InstitutePolicyId";

            cmd.AddParameter("@InstitutePolicyId", policyId.ToByteArray());

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private InstitutePolicyResponse MapToPolicyResponse(DbDataReader reader)
        {
            return new InstitutePolicyResponse
            {
                InstitutePolicyId = reader.GetGuidFromByteArray("InstitutePolicyId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                PolicyId = reader.GetGuidFromByteArray("InstitutePolicyId"), // Assuming PolicyId maps to InstitutePolicyId
                Details = reader.GetString("Details"),
                InstutePolicy = (PolicyType)(reader.GetNullableInt("InstutePolicyType") ?? 0),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }
}
