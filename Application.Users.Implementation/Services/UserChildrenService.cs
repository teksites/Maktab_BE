using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Models;
using Users.Repository;
using Users.Services;
using MaktabDataContracts.Responses.Children;
using MaktabDataContracts.Requests.Children;

namespace Application.Users.Implementation
{
 
    public class UserChildrenService : IUserChildrenService
    {
        //private readonly ISedatService _sedatService;
        private readonly IConfiguration _configuration;
        private readonly IUserChildrenRepository _repository;

        public UserChildrenService(IConfiguration configuration, IUserChildrenRepository repository) 
        {
            _configuration = configuration;
            _repository = repository;
            //_sedatService = sedatService;
        }

        public async Task<MaktabApiResult<ChildResponse>> AddChild(AddChildRequest child)
        {

            //var phoneVerification = await _sedatService.VerifyPhoneNumber(new VerifyPhoneRequest
            //{
            //    Telephone = child.Phone,
            //}

            //).ConfigureAwait(false);

            //if (phoneVerification == null)
            //{

            //    return new MaktabApiResult<ChildResponse>
            //    {
            //        Errors =
            //        {
            //                    MaktabApiResult.InvalidChildPhoneNumberError(child.FirstName+" "+child.LastName, child.Phone)
            //            //        MaktabApiResult.GenericError("The Sedat Api authorization failed", (int)ErrorTypes.SedatApiAuthorizationFailed)
            //        }
            //    };
            //}

            //if (!phoneVerification.Success)
            //{
            //    return new MaktabApiResult<ChildResponse>
            //    {
            //        Errors =
            //        {
            //                    MaktabApiResult.InvalidChildPhoneNumberError(child.FirstName+" "+child.LastName, child.Phone)
            //        }
            //    };
            //}


            return MapToChildResponse(await _repository.AddChild(MapToChild(child)).ConfigureAwait(false));
        }

        public async Task<bool> CheckIfChildExisit(UserChildToVerify child)
        {
           return await _repository.CheckIfChildExisit(child).ConfigureAwait(false);
        }

        public async Task<bool> DeleteChild(Guid childId, bool ifHardDelete)
        {
            return await _repository.DeleteChild(childId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> DeleteUserChilds(Guid userId, bool ifHardDelete)
        {
            return await _repository.DeleteFamilyChildren(userId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<MaktabApiResult<ChildResponse>> GetChild(Guid childId)
        {
            return MapToChildResponse(await _repository.GetChild(childId).ConfigureAwait(false));
        }

        public async Task<IEnumerable<MaktabApiResult<ChildResponse>>> GetUserChilds(Guid userId)
        {
            return (await _repository.GetFamilyChildren(userId).ConfigureAwait(false)).
                Select(MapToChildResponse).ToList();
        }

        public async Task<MaktabApiResult<ChildResponse>> UpdateChild(UpdateChildRequest child)
        {
            return MapToChildResponse(await _repository.UpdateChild(child).ConfigureAwait(false));
        }

        private Child MapToChild(AddChildRequest child)
        {
            return new Child
            {
                ChildId = Guid.NewGuid(),
                FamilyId = child.FamilyId,
                WillUseDayCareServices = child.WillUseDayCareServices,
                Gender = child.Gender,
                RAMQExpiry = child.RAMQExpiry,
                DateOfBirth = child.DateOfBirth,
                FirstName = child.FirstName,
                LastName = child.LastName,
                RAMQNumber = child.RAMQNumber,
                RAMQSequenceNumber = child.RAMQSequenceNumber,
                Allergies = child.Allergies,
                OtherHealthConditions = child.OtherHealthConditions,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
                IsActive = true
            };
        }

        private MaktabApiResult<ChildResponse> MapToChildResponse(Child child)
        {
            if (child == null)
            {
                return null;
            }
           
            var response =  new ChildResponse
            {
                ChildId = child.ChildId,
                FamilyId = child.FamilyId,
                Allergies = child.Allergies,
                DateOfBirth = child.DateOfBirth,
                RAMQNumber = child.RAMQNumber,
                RAMQSequenceNumber = child.RAMQSequenceNumber,
                RAMQExpiry = child.RAMQExpiry,
                FirstName = child.FirstName,
                LastName = child.LastName,
                Gender = child.Gender,
                OtherHealthConditions = child.OtherHealthConditions,
                WillUseDayCareServices = child.WillUseDayCareServices,
                CreatedAt = child.CreatedAt,
                UpdatedOn = child.UpdatedOn,
                IsActive = child.IsActive
            };

            return new MaktabApiResult<ChildResponse>
            {
                Result = response,
                Errors = new List<PartnerApiError> { }
            };
        }
    }
}
