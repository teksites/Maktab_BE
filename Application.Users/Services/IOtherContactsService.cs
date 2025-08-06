using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.OtherContacts;
using MaktabDataContracts.Responses.OtherContacts;

namespace Users.Services
{
    public interface IOtherContactsService
    {
        Task<OtherContactResponse> AddOtherContact(AddOtherContact otherContactInformation);
        Task<OtherContactResponse> UpdateOtherContact(UpdateOtherContact otherContactInformation);
        Task<bool> DeleteOtherContact(Guid otherContactId, bool ifHardDelete);
        Task<OtherContactResponse> GetOtherContact(Guid otherContactId);
        Task<OtherContactResponse> GetOtherContactByPhoneNumber(string phoneNumber);
        Task<IEnumerable<OtherContactResponse>> GetFamilyOtherContacts(Guid familyId, IEnumerable<ContactType> contactTypes);
        Task<bool> CheckIfOtherContactExisit(Guid familyId, String phone);
        Task<bool> DeleteFamilyOtherContact(Guid familyId, bool ifHardDelete);
    }
}