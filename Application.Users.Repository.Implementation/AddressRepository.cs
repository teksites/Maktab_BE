using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using Users.Repository;
using System;
using System.Data.Common;
using System.Data;

namespace Application.Users.Repository.Implementation
{
    public class AddressRepository : DbRepository, IAddressRepository
    {
        public AddressRepository(IDatabase database) : base(database)
        {
        }

        #region Add

        public async Task<AddAddressIntenal> AddAddress(AddAddressIntenal address)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO addresses 
                (AddressId, ConnectedId, AddressType, UnitNumber, ApartmentNumber, AddressLine1, AddressLine2,
                 City, Country, Province, PostalCode, IsActive, CreatedAt, UpdatedOn, HomeAddress)
                VALUES
                (@addressId, @connectedId, @addressType, @unitNumber, @apartmentNumber, @addressLine1, @addressLine2,
                 @city, @country, @province, @postalCode, @isActive, @createdAt, @updatedOn, @homeAddress)";

            cmd.AddParameter("@addressId", address.AddressId.ToByteArray());
            cmd.AddParameter("@connectedId", address.ConnectedId.ToByteArray());
            cmd.AddParameter("@addressType", Convert.ToInt32(address.AddressType));
            cmd.AddParameter("@unitNumber", address.UnitNo);
            cmd.AddParameter("@apartmentNumber", address.ApartmentNo);
            cmd.AddParameter("@addressLine1", address.AddressLine1);
            cmd.AddParameter("@addressLine2", address.AddressLine2);
            cmd.AddParameter("@city", address.City);
            cmd.AddParameter("@country", address.Country);
            cmd.AddParameter("@province", address.Province);
            cmd.AddParameter("@postalCode", address.PostalCode);
            cmd.AddParameter("@isActive", address.IsActive);
            cmd.AddParameter("@homeAddress", address.HomeAddress);
            cmd.AddParameter("@createdAt", DateTime.UtcNow);
            cmd.AddParameter("@updatedOn", DateTime.UtcNow);

            if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
            {
                return await GetAddress(address.AddressId, includeInactive: true).ConfigureAwait(false);
            }

            return null;
        }

        #endregion

        #region Get

        public async Task<AddAddressIntenal> GetAddress(Guid addressId, bool includeInactive = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT AddressId, ConnectedId, AddressType, UnitNumber, ApartmentNumber, AddressLine1, AddressLine2,
                       City, Country, Province, PostalCode, IsActive, CreatedAt, UpdatedOn, HomeAddress
                FROM addresses
                WHERE AddressId = @addressId";

            if (!includeInactive)
                cmd.CommandText += " AND IsActive = TRUE";

            cmd.AddParameter("@addressId", addressId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;

            return MapReaderToAddress(reader);
        }

        public async Task<AddAddressIntenal> GetAddressWithConnectedId(Guid connectedId, bool includeInactive = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT AddressId, ConnectedId, AddressType, UnitNumber, ApartmentNumber, AddressLine1, AddressLine2,
                       City, Country, Province, PostalCode, IsActive, CreatedAt, UpdatedOn, HomeAddress
                FROM addresses
                WHERE ConnectedId = @connectedId";

            if (!includeInactive)
                cmd.CommandText += " AND IsActive = TRUE";

            cmd.AddParameter("@connectedId", connectedId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;

            return MapReaderToAddress(reader);
        }

        #endregion

        #region Update

        public async Task<AddAddressIntenal> UpdateAddress(UpdateAddress address, bool? isActive = null)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE addresses
                SET UnitNumber = @unitNumber,
                    ApartmentNumber = @apartmentNumber,
                    AddressLine1 = @addressLine1,
                    AddressLine2 = @addressLine2,
                    City = @city,
                    Country = @country,
                    Province = @province,
                    PostalCode = @postalCode,
                    HomeAddress = @homeAddress,
                    UpdatedOn = @updatedOn";

            if (isActive.HasValue)
                cmd.CommandText += ", IsActive = @isActive";

            cmd.CommandText += " WHERE AddressId = @addressId";

            cmd.AddParameter("@addressId", address.AddressId.ToByteArray());
            cmd.AddParameter("@unitNumber", address.UnitNo);
            cmd.AddParameter("@apartmentNumber", address.ApartmentNo);
            cmd.AddParameter("@addressLine1", address.AddressLine1);
            cmd.AddParameter("@addressLine2", address.AddressLine2);
            cmd.AddParameter("@city", address.City);
            cmd.AddParameter("@country", address.Country);
            cmd.AddParameter("@province", address.Province);
            cmd.AddParameter("@postalCode", address.PostalCode);
            cmd.AddParameter("@homeAddress", address.HomeAddress);
            cmd.AddParameter("@updatedOn", DateTime.UtcNow);

            if (isActive.HasValue)
                cmd.AddParameter("@isActive", isActive.Value);

            if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                return await GetAddress(address.AddressId, includeInactive: true).ConfigureAwait(false);

            return null;
        }

        #endregion

        #region Delete

        public async Task<bool> DeleteAddress(Guid addressId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM addresses WHERE AddressId = @addressId";
            else
                cmd.CommandText = @"UPDATE addresses SET IsActive = FALSE, UpdatedOn = @updatedOn WHERE AddressId = @addressId";

            cmd.AddParameter("@addressId", addressId.ToByteArray());
            if (!hardDelete)
                cmd.AddParameter("@updatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        public async Task<bool> DeleteAddressByConnectedId(Guid connectedId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM addresses WHERE ConnectedId = @connectedId";
            else
                cmd.CommandText = @"UPDATE addresses SET IsActive = FALSE, UpdatedOn = @updatedOn WHERE ConnectedId = @connectedId";

            cmd.AddParameter("@connectedId", connectedId.ToByteArray());
            if (!hardDelete)
                cmd.AddParameter("@updatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        #endregion

        #region Private Helpers

        private AddAddressIntenal MapReaderToAddress(DbDataReader reader)
        {
            return new AddAddressIntenal
            {
                AddressId = reader.GetGuidFromByteArray("AddressId"),
                ConnectedId = reader.GetGuidFromByteArray("ConnectedId"),
                AddressType = (AddressType)reader.GetInt16("AddressType"),
                UnitNo = reader.GetString("UnitNumber"),
                ApartmentNo = reader.GetString("ApartmentNumber"),
                AddressLine1 = reader.GetString("AddressLine1"),
                AddressLine2 = reader.GetString("AddressLine2"),
                City = reader.GetString("City"),
                Country = reader.GetString("Country"),
                Province = reader.GetString("Province"),
                PostalCode = reader.GetString("PostalCode"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                HomeAddress = reader.GetBoolean("HomeAddress")
            };
        }

        #endregion
    }
}
