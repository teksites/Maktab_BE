using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
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
        
        private ExtendedUserInformationDetail MapToUserInformation(AddExtendedUserInformation addUserInformation)
        {
            return new ExtendedUserInformationDetail
            {
                UserId = addUserInformation.UserId,
                BusinesName = addUserInformation.BusinesName,
                Occupation = addUserInformation.Occupation,
                DateOfBirth = addUserInformation.DateOfBirth,
                IdNumber = addUserInformation.IdNumber,
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
             //   BusinesName = userInformation.BusinesName,
              //  Occupation = userInformation.Occupation,
                DateOfBirth = userInformation.DateOfBirth,
                IdNumber = userInformation.IdNumber,
                IsActive = userInformation.IsActive,
                CreatedAt = userInformation.CreatedAt,
                UpdatedOn = userInformation.UpdatedOn,
            };
        }

        public async Task<ExtendedUserInformationResponse> AddExtendedUserInformation(AddExtendedUserInformation userInformation)
        {
            return MapToUserInformationResponse(await  _repository.AddExtendedUserInformation(MapToUserInformation(userInformation)).ConfigureAwait(false));
        }

        public async Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(AddExtendedUserInformation userInformation)
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
    }
}
