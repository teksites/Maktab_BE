using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using System.Net;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class AddressRepository : DbRepository, IAddressRepository
    {
        public AddressRepository(IDatabase database) : base(database)
        {
        }

        public async Task<AddAddressIntenal> AddAddress(AddAddressIntenal address)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into addresses (`AddressId`, `ConnectedId`, `AddressType`, `UnitNumber`, `ApartmentNumber`, `AddressLine1`, `AddressLine2`, " +
                        "`City`, `Country`, `province`, `PostalCode`, `IsActive`, `CreatedAt`, `UpdatedOn`, `HomeAddress`)"
                        + " Values(@addressId, @connectedId, @addressType, @unitNumber, @apartmentNumber, @addressLine1, @addressLine2," +
                        " @city, @country, @province, @postalCode, @isActive, @createdAt, @updatedOn, @homeAddress)";

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
                    cmd.AddParameter("@createdAt", address.CreatedAt);
                    cmd.AddParameter("@updatedOn", address.UpdatedOn);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return new AddAddressIntenal
                        {
                            AddressId = address.AddressId,
                            AddressType = address.AddressType,
                            AddressLine1 = address.AddressLine1,
                            AddressLine2 = address.AddressLine2,
                            City = address.City,
                            Country = address.Country,
                            Province = address.Province,
                            PostalCode = address.PostalCode,
                            IsActive = address.IsActive,
                            CreatedAt = address.CreatedAt,
                            UpdatedOn = address.UpdatedOn,
                            ApartmentNo = address.ApartmentNo,
                            ConnectedId = address.ConnectedId,
                            UnitNo = address.UnitNo,
                            HomeAddress = address.HomeAddress
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> DeleteAddressByConnectedId(Guid id, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from addresses where ConnectedId = @id";
                    }
                    else
                    {
                        cmd.CommandText = @"Update addresses SET IsActive = false where ConnectedId = @id";
                    }

                    cmd.AddParameter("@id", id.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from addresses where AddressId = @addressId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update addresses SET IsActive = false where AddressId = @addressId";
                    }

                    cmd.AddParameter("@addressId", addressId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<AddAddressIntenal> GetAddress(Guid addressId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `AddressId`, `ConnectedId`, `AddressType`, `UnitNumber`, `ApartmentNumber`, `AddressLine1`, `AddressLine2`, "  +
                        "`City`, `Country`, `Province`, `PostalCode`, `IsActive`, `CreatedAt`, `UpdatedOn` , `HomeAddress` from addresses " +
                        " where AddressId = @addressId";

                    cmd.AddParameter("@addressId", addressId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var connectedid = reader.GetGuidFromByteArray(1);
                    var addressType = reader.GetInt16(2);
                    var unitNumber = reader.GetString(3);
                    var apartmentNumber = reader.GetString(4);
                    var addressLine1 = reader.GetString(5);
                    var addressLine2 = reader.GetString(6);
                    var City = reader.GetString(7);
                    var country = reader.GetString(8);
                    var province = reader.GetString(9);
                    var postalCode = reader.GetString(10);
                    var isActive = reader.GetBoolean(11);
                    var CreatedAt = reader.GetDateTime(12);
                    var UpdatedOn = reader.GetDateTime(13);
                    var homeAddress = reader.GetBoolean(14);//HomeAddress);


                    return new AddAddressIntenal
                    {
                        AddressId = id,
                        ConnectedId  = connectedid,
                        AddressType = (AddressType)addressType,
                        ApartmentNo = apartmentNumber,
                        AddressLine1 = addressLine1,
                        AddressLine2 = addressLine2,
                        City = City,
                        Country = country,
                        PostalCode= postalCode,
                        Province = province,
                        UnitNo = unitNumber,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        HomeAddress = homeAddress
                    };
                }
            }
        }

        public async Task<AddAddressIntenal> GetAddressWithConnectedId(Guid connectedId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `AddressId`, `ConnectedId`, `AddressType`, `UnitNumber`, `ApartmentNumber`, `AddressLine1`, `AddressLine2`, " +
                        "`City`, `Country`, `Province`, `PostalCode`, `IsActive`, `CreatedAt`, `UpdatedOn`, `HomeAddress` from addresses " +
                        " where ConnectedId = @connectedId";

                    cmd.AddParameter("@connectedId", connectedId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var connectedid = reader.GetGuidFromByteArray(1);
                    var addressType = reader.GetInt16(2);
                    var unitNumber = reader.GetString(3);
                    var apartmentNumber = reader.GetString(4);
                    var addressLine1 = reader.GetString(5);
                    var addressLine2 = reader.GetString(6);
                    var City = reader.GetString(7);
                    var country = reader.GetString(8);
                    var province = reader.GetString(9);
                    var postalCode = reader.GetString(10);
                    var isActive = reader.GetBoolean(11);
                    var CreatedAt = reader.GetDateTime(12);
                    var UpdatedOn = reader.GetDateTime(13);
                    var homeAddress = reader.GetBoolean(14);//HomeAddress);

                    return new AddAddressIntenal
                    {
                        AddressId = id,
                        ConnectedId = connectedid,
                        AddressType = (AddressType)addressType,
                        ApartmentNo = apartmentNumber,
                        AddressLine1 = addressLine1,
                        AddressLine2 = addressLine2,
                        City = City,
                        Country = country,
                        PostalCode = postalCode,
                        Province = province,
                        UnitNo = unitNumber,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        HomeAddress = homeAddress
                    };
                }
            }

        }

        public async Task<AddAddressIntenal> UpdateAddress(UpdateAddress address)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update addresses SET AddressId = @addressId, "+
                        "UnitNumber = @unitNumber, " +
                        "ApartmentNumber = @apartmentNumber, " +
                        "AddressLine1 = @addressLine1, " +
                        "AddressLine2 = @addressLine2, " +
                        "City = @city, " +
                        "HomeAddress = @homeAddress, "+
                        "Country = @country, " +
                        "Province = @province, " +
                        "PostalCode = @postalCode, " +
                        "UpdatedOn = @updatedOn where AddressId = @addressId";

                    cmd.AddParameter("@addressId", address.AddressId.ToByteArray());
                    cmd.AddParameter("@unitNumber", address.UnitNo);
                    cmd.AddParameter("@apartmentNumber", address.ApartmentNo);
                    cmd.AddParameter("@addressLine1", address.AddressLine1);
                    cmd.AddParameter("@addressLine2", address.AddressLine2);
                    cmd.AddParameter("@city", address.City);
                    cmd.AddParameter("@country", address.Country);
                    cmd.AddParameter("@province", address.Province);
                    cmd.AddParameter("@homeAddress", address.HomeAddress);
                    cmd.AddParameter("@postalCode", address.PostalCode);
                    cmd.AddParameter("@updatedOn", DateTime.Now);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetAddress(address.AddressId).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }
    }
}
