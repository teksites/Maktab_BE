using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Requests.OtherContacts;
using MaktabDataContracts.Responses.OtherContacts;
using Users.Repository;
using Users.Services;
using MaktabDataContracts.Enums;

namespace Application.Users.Implementation
{
    public class OtherContactsService : IOtherContactsService
    {
        private readonly IConfiguration _configuration;
        private readonly IOtherContactsRepository _repository;

        public OtherContactsService(IConfiguration configuration, IOtherContactsRepository repository) 
        {
            _configuration = configuration;
            _repository = repository;
        
        }

        public async Task<OtherContactResponse> AddOtherContact(AddOtherContact otherContactInformation)
        {
           return MaptToOtherContactResonse( await _repository.AddOtherContact(MapToOtherContactInformation(otherContactInformation)).ConfigureAwait(false));
        }

        private OtherContactResponse MaptToOtherContactResonse(OtherContactInformation clientCardInformation)
        {
            if (clientCardInformation == null)
            {
                return null;
            }
            
            return new OtherContactResponse
            {
                ContactId = clientCardInformation.ContactId,
                FamilyId = clientCardInformation.FamilyId,
                FirstName = clientCardInformation.FirstName,
                LastName = clientCardInformation.LastName,
                ContactType = clientCardInformation.ContactType,
                Relationship = clientCardInformation.Relationship,
                IsActive = clientCardInformation.IsActive,
                CreatedAt = clientCardInformation.CreatedAt,
                UpdatedOn = clientCardInformation.UpdatedOn,
                Phone = clientCardInformation.Phone,
            };
        }

        private OtherContactInformation MapToOtherContactInformation(AddOtherContact otherContactInformation)
        {
            return new OtherContactInformation
            {
                ContactId = Guid.NewGuid(),
                ContactType = otherContactInformation.ContactType,
                FamilyId = otherContactInformation.FamilyId,
                FirstName = otherContactInformation.FirstName,
                LastName = otherContactInformation.LastName,
                Phone = otherContactInformation.Phone,
                Relationship = otherContactInformation.Relationship,
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
                IsActive = true,
            };
        }

        public async Task<bool> CheckIfOtherContactExisit(Guid familyId, String phone)
        {
            return await _repository.CheckIfOtherContactExisit(familyId, phone).ConfigureAwait(false);
        }

        public async Task<bool> DeleteOtherContact(Guid otherContactId, bool ifHardDelete)
        {
            return await _repository.DeleteOtherContact(otherContactId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> DeleteFamilyOtherContact(Guid familyId, bool ifHardDelete)
        {
            return await _repository.DeleteFamilyOtherContact(familyId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<OtherContactResponse> GetOtherContact(Guid otherContactId)
        {
            return MaptToOtherContactResonse(await _repository.GetOtherContact(otherContactId).ConfigureAwait(false));
        }
        
        public async Task<IEnumerable<OtherContactResponse>> GetFamilyOtherContacts(Guid familyId, IEnumerable<ContactType> contactTypes)
        {
            var types = (contactTypes ?? Enumerable.Empty<ContactType>()).Distinct().ToList();

            if (!types.Any())
            {
                var contacts = await _repository.GetFamilyOtherContacts(familyId).ConfigureAwait(false);
                return (contacts ?? Enumerable.Empty<OtherContactInformation>())
                    .Select(MaptToOtherContactResonse)
                    .ToList();
            }

            var contactTasks = types.Select(type => _repository.GetFamilyOtherContacts(familyId, type));
            var contactsByType = await Task.WhenAll(contactTasks).ConfigureAwait(false);

            return contactsByType
                .SelectMany(contacts => contacts ?? Enumerable.Empty<OtherContactInformation>())
                .Select(MaptToOtherContactResonse)
                .ToList();
        }

        public async Task<OtherContactResponse> GetOtherContactByPhoneNumber(string phoneNumber)
        {
            return MaptToOtherContactResonse(await _repository.GetOtherContactByPhoneNumber(phoneNumber).ConfigureAwait(false));
        }

        public async Task<OtherContactResponse> UpdateOtherContact(UpdateOtherContact otherContactInformation)
        {
            if (otherContactInformation == null)
            {
                throw new ArgumentNullException(nameof(otherContactInformation));
            }

            var contactIdProperty = otherContactInformation.GetType().GetProperty("ContactId");
            if (contactIdProperty == null || contactIdProperty.PropertyType != typeof(Guid))
            {
                throw new ArgumentException("Update payload must include ContactId.", nameof(otherContactInformation));
            }

            var contactId = (Guid)(contactIdProperty.GetValue(otherContactInformation) ?? Guid.Empty);
            if (contactId == Guid.Empty)
            {
                throw new ArgumentException("ContactId is required.", nameof(otherContactInformation));
            }

            var existing = await _repository.GetOtherContact(contactId).ConfigureAwait(false);
            if (existing == null)
            {
                return null;
            }

            var merged = new OtherContactInformation
            {
                ContactId = existing.ContactId,
                FamilyId = existing.FamilyId,
                FirstName = existing.FirstName,
                LastName = existing.LastName,
                Phone = existing.Phone,
                ContactType = existing.ContactType,
                Relationship = existing.Relationship,
                IsActive = existing.IsActive,
                CreatedAt = existing.CreatedAt,
                UpdatedOn = DateTime.Now
            };

            var destinationProperties = typeof(OtherContactInformation).GetProperties();
            foreach (var destination in destinationProperties)
            {
                if (destination.Name == nameof(OtherContactInformation.ContactId)
                    || destination.Name == nameof(OtherContactInformation.CreatedAt)
                    || destination.Name == nameof(OtherContactInformation.UpdatedOn))
                {
                    continue;
                }

                var source = otherContactInformation.GetType().GetProperty(destination.Name);
                if (source == null)
                {
                    continue;
                }

                var value = source.GetValue(otherContactInformation);
                if (value == null)
                {
                    continue;
                }

                destination.SetValue(merged, value);
            }

            var updated = await _repository.UpdateOtherContact(merged).ConfigureAwait(false);
            return MaptToOtherContactResonse(updated);
        }
    }
}
