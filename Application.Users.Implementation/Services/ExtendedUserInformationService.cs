using Application.Users.Contracts;
using Email;
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
        private readonly IUserRepository _userRepository;

        private readonly ISendEmailService _sendEmailService;

        public ExtendedUserInformationService(IConfiguration configuration, ISendEmailService sendEmailService, IExtendedUserInformationRepository repository, IUserRepository userRepository) 
        {
            _configuration = configuration;
            _sendEmailService = sendEmailService;
            _repository = repository;
            _userRepository = userRepository;
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
                AddressId = addUserInformation.AddressId ?? Guid.Empty,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
                IsActiveTaxCreditRecipient = addUserInformation.IsActiveTaxCreditRecipient
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
                AddressId = userInformation.AddressId,
                IsActive = userInformation.IsActive,
                CreatedAt = userInformation.CreatedAt,
                UpdatedOn = userInformation.UpdatedOn,
                IsActiveTaxCreditRecipient = userInformation.IsActiveTaxCreditRecipient
            };
        }

        public async Task<ExtendedUserInformationResponse> AddExtendedUserInformation(AddExtendedUserInformationInternal userInformation)
        {
            var existingInfo = await _repository.GetFamilyExtendedUserInformation(userInformation.FamilyId).ConfigureAwait(false);
            //User existingUserInfo = null; 
            // reset existing rl24 info to false

            if (existingInfo != null) 
            {
                await _repository.UpdateExtendedUserInformation(new AddExtendedUserInformationInternal { UserId = userInformation.UserId, IsActiveTaxCreditRecipient = false}).ConfigureAwait(false);
            }

            var information = MapToUserInformationResponse(await  _repository.AddExtendedUserInformation(MapToUserInformation(userInformation)).ConfigureAwait(false));
            //await _repository.AddExtendedUserInformation(MapToUserInformation(userInformation)).ConfigureAwait(false);

            var familyUsers = await _userRepository.GetAllFamilyUsersInformation(userInformation.FamilyId).ConfigureAwait(false);

            var targetUser = familyUsers.Single(x => x.UserId == userInformation.UserId);
            var name = targetUser.FirstName + " " + targetUser.LastName;

            foreach(var familyMember in  familyUsers) 
            {
                var success = _sendEmailService.SendEmail(new EmailData
                {
                    To = familyMember.Email,
                    Subject = "ICC Brossard School Portal Account Tax Credit Information Added/Changed",
                    Body = $"<p><strong>Greetings {familyMember.FirstName} {familyMember.LastName}</strong>,</p>" +
                     $"<p>The Tax Credit Information has been added in your family account for the registered user: <strong> {name}</strong> </p>" +
                     "<div>&nbsp;</div>" +
                     $"<div><strong>Important: </strong> Any previous Tax Credit information will be replaced with this new name.</div>" +
                     "<div>Cheers</div>" +
                     "<div>&nbsp;</div>" +
                       "<div><strong>ICC Brossard School Registration Portal</strong></div>"
                });
            }
            return information;
        }

        public async Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(AddExtendedUserInformationInternal userInformation)
        {
            return MapToUserInformationResponse(await _repository.UpdateExtendedUserInformation(userInformation).ConfigureAwait(false));
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
