using Application.Users.Contracts;
using MaktabDataContracts.Enums;

namespace Users.Repository
{
    public interface IOtherContactsRepository
    {
        Task<OtherContactInformation> AddOtherContact(OtherContactInformation otherContactInformation);
        Task<bool> CheckIfOtherContactExisit(Guid familyId, string phone);
        Task<bool> DeleteOtherContact(Guid otherContactId, bool ifHardDelete = false);
        Task<bool> DeleteFamilyOtherContact(Guid familyId, bool ifHardDelete = false);
        Task<OtherContactInformation> GetOtherContact(Guid otherContact);
        Task<OtherContactInformation> GetOtherContactByPhoneNumber(string phoneNumber);
        Task<IEnumerable<OtherContactInformation>> GetFamilyOtherContacts(Guid familyId, IEnumerable<ContactType> contactTypes);
        Task<OtherContactInformation> UpdateOtherContact(OtherContactInformation otherContactInformation);
    }
}
