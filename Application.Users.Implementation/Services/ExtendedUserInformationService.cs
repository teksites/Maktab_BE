using Application.Users.Contracts;
using MaktabDataContracts.Responses.Users;
using Microsoft.Extensions.Configuration;
using Users.Contracts;
using Users.Repository;
using Users.Services;

namespace Application.Users.Implementation
{
    public class ExtendedUserInformationService : IExtendedUserInformationService
    {
        private readonly IConfiguration _configuration;
        private readonly IExtendedUserInformationRepository _repository;

        public ExtendedUserInformationService(IConfiguration configuration, IExtendedUserInformationRepository repository) 
        {
            _configuration = configuration;
            _repository = repository;
        } 
        
        public async Task<ExtendedUserInformationResponse> GetExtendedUserInformation(Guid userId)
        {
            var userInfo = await _repository.GetExtendedUserInformation(userId).ConfigureAwait(false);
            
            if (userInfo != null)
            {
                var mappedUser = MapToUserInformationResponse(userInfo);
                return mappedUser;
            }
            return null;
        }
        
        private ExtendedUserInformationDetail MapToUserInformation(AddExtendedUserInformationInternal addUserInformation)
        {
            return new ExtendedUserInformationDetail
            {
                UserId = addUserInformation.UserId,
                FamilyId = addUserInformation.FamilyId,
                SIN = addUserInformation.SIN,
                AddressId = addUserInformation.AddressId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
            };
        }

        private ExtendedUserInformationResponse MapToUserInformationResponse(ExtendedUserInformationDetail userInformation)
        {
            if (userInformation == null)
            {
                return null;
            }
            return new ExtendedUserInformationResponse
            {
                UserId = userInformation.UserId,
                FamilyId = userInformation.FamilyId,
                SIN = userInformation.SIN,
                IsActive = userInformation.IsActive,
                CreatedAt = userInformation.CreatedAt,
                UpdatedOn = userInformation.UpdatedOn,
            };
        }

        public async Task<ExtendedUserInformationResponse> AddExtendedUserInformation(AddExtendedUserInformationInternal userInformation)
        {
            return MapToUserInformationResponse(await  _repository.AddExtendedUserInformation(MapToUserInformation(userInformation)).ConfigureAwait(false));
        }

        public async Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(AddExtendedUserInformationInternal userInformation)
        {
            return MapToUserInformationResponse(await _repository.UpdateExtendedUserInformation(MapToUserInformation(userInformation)).ConfigureAwait(false));
        }

        public async Task<bool> DeletExtendedUserInformation(Guid userId, bool ifHardDelete)
        {
            return await _repository.DeleteExtendedUserInformation(userId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfExtendedUserInformationExisit(Guid userId)
        {
            return await _repository.CheckIfExtendedUserInformationExisit(userId).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfExtendedFamilyInformationExisit(Guid familyId)
        {
            return await _repository.CheckIfExtendedFamilyInformationExisit(familyId).ConfigureAwait(false);
        }

        public async Task<bool> DeleteFamilyExtendedUserInformation(Guid familyId, bool ifHardDelete = false)
        {
            return await _repository.DeleteFamilyExtendedUserInformation(familyId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<ExtendedUserInformationResponse> GetFamilyExtendedUserInformation(Guid familyId)
        {
            var userInfo = await _repository.GetFamilyExtendedUserInformation(familyId).ConfigureAwait(false);

            if (userInfo != null)
            {
                var mappedUser = MapToUserInformationResponse(userInfo);
                return mappedUser;
            }
            return null;
        }
    }
}
