using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Models;
using Users.Repository;
using Users.Services;
using MaktabDataContracts.Responses.Children;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Enums;

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
           return await _repository.CheckIfChildExist(child).ConfigureAwait(false);
        }

        public async Task<bool> DeleteChild(Guid childId, bool ifHardDelete)
        {
            return await _repository.DeleteChild(childId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> DeleteUserChilds(Guid userId, bool ifHardDelete)
        {
            return await _repository.DeleteFamilyChildren(userId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<MaktabApiResult<ChildResponse>> GetChild(Guid childId, Guid? viewerUserId = null)
        {
            var child = await _repository.GetChild(childId).ConfigureAwait(false);
            if (child == null)
            {
                return null;
            }

            var relationships = viewerUserId.HasValue
                ? await _repository.GetFamilyUserRelationships(child.FamilyId).ConfigureAwait(false)
                : null;
            return MapToChildResponse(child, viewerUserId, relationships);
        }

        public async Task<IEnumerable<MaktabApiResult<ChildResponse>>> GetUserChilds(Guid familyId, bool fetchAdults = false, Guid? viewerUserId = null)
        {
            var children = await _repository.GetFamilyChildren(familyId).ConfigureAwait(false);
            if (!fetchAdults)
            {
                children = children
                    .Where(child => IsIncludedFamilyMemberUserType(child.UserType))
                    .ToList();
            }

            var relationships = viewerUserId.HasValue
                ? await _repository.GetFamilyUserRelationships(familyId).ConfigureAwait(false)
                : null;
            return children.Select(child => MapToChildResponse(child, viewerUserId, relationships)).ToList();
        }

        public async Task<MaktabApiResult<ChildResponse>> UpdateChild(UpdateChildRequest child)
        {
            ArgumentNullException.ThrowIfNull(child);

            var existingChild = await _repository.GetChild(child.ChildId).ConfigureAwait(false);
            if (existingChild == null)
            {
                return null;
            }

            var updatedChild = await _repository.UpdateChild(MergeChild(existingChild, child)).ConfigureAwait(false);
            return MapToChildResponse(updatedChild);
        }

        private Child MapToChild(AddChildRequest child)
        {
            return new Child
            {
                ChildId = Guid.NewGuid(),
                FamilyId = child.FamilyId,
                Gender = child.Gender,
                RAMQExpiry = child.RAMQExpiry,
                DateOfBirth = child.DateOfBirth,
                FirstName = child.FirstName,
                LastName = child.LastName,
                ArabicName = child.ArabicName,
                RAMQNumber = child.RAMQNumber,
                RAMQSequenceNumber = child.RAMQSequenceNumber,
                HasAllergy = child.HasAllergy,
                Allergies = child.Allergies,
                OtherHealthConditions = child.OtherHealthConditions,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
                IsActive = true,
                AcedemicGroup = child.AcedemicGroup,
                Consent = child.Consent,
                UserType = child.UserType,
            };
        }

        private MaktabApiResult<ChildResponse> MapToChildResponse(
            Child child,
            Guid? viewerUserId = null,
            IReadOnlyDictionary<Guid, Relationship>? familyRelationships = null)
        {
            if (child == null)
            {
                return null;
            }
           
            var response =  new ChildResponse
            {
                ChildId = child.ChildId,
                FamilyId = child.FamilyId,
                HasAllergy = child.HasAllergy,
                Allergies = child.Allergies,
                DateOfBirth = child.DateOfBirth,
                RAMQNumber = child.RAMQNumber,
                RAMQSequenceNumber = child.RAMQSequenceNumber,
                RAMQExpiry = child.RAMQExpiry,
                FirstName = child.FirstName,
                LastName = child.LastName,
                ArabicName = child.ArabicName,
                HasSurahCatalogBeenProvided = child.HasSurahCatalogBeenProvided,
                Gender = child.Gender,
                OtherHealthConditions = child.OtherHealthConditions,
                CreatedAt = child.CreatedAt,
                UpdatedOn = child.UpdatedOn,
                IsActive = child.IsActive,
                AcedemicGroup = child.AcedemicGroup,
                RegistrationNumber = child.RegistrationNumber,
                Consent = child.Consent,
                UserType = child.UserType,
                DisplayType = GetDisplayType(child, viewerUserId, familyRelationships)
            };

            return new MaktabApiResult<ChildResponse>
            {
                Result = response,
                Errors = new List<PartnerApiError> { }
            };
        }

        private static Child MergeChild(Child existingChild, UpdateChildRequest child)
        {
            return new Child
            {
                ChildId = existingChild.ChildId,
                FamilyId = existingChild.FamilyId,
                FirstName = child.FirstName ?? existingChild.FirstName,
                LastName = child.LastName ?? existingChild.LastName,
                ArabicName = child.ArabicName ?? existingChild.ArabicName,
                HasSurahCatalogBeenProvided = existingChild.HasSurahCatalogBeenProvided,
                DateOfBirth = child.DateOfBirth ?? existingChild.DateOfBirth,
                Gender = child.Gender ?? existingChild.Gender,
                RAMQNumber = child.RAMQNumber ?? existingChild.RAMQNumber,
                RAMQExpiry = child.RAMQExpiry ?? existingChild.RAMQExpiry,
                RAMQSequenceNumber = child.RAMQSequenceNumber ?? existingChild.RAMQSequenceNumber,
                Allergies = child.Allergies ?? existingChild.Allergies,
                OtherHealthConditions = child.OtherHealthConditions ?? existingChild.OtherHealthConditions,
                IsActive = existingChild.IsActive,
                CreatedAt = existingChild.CreatedAt,
                UpdatedOn = DateTime.UtcNow,
                AcedemicGroup = child.AcedemicGroup ?? existingChild.AcedemicGroup,
                RegistrationNumber = existingChild.RegistrationNumber,
                HasAllergy = child.HasAllergy ?? existingChild.HasAllergy,
                Consent = child.Consent ?? existingChild.Consent,
                UserType = child.UserType ?? existingChild.UserType,
            };
        }

        private static bool IsIncludedFamilyMemberUserType(UserType userType)
        {
            return userType == UserType.Child
                || userType == UserType.Self
                || userType == UserType.Mother
                || userType == UserType.Father
                || userType == UserType.Guardian;
        }

        private static FamilyMemberDisplayType GetDisplayType(
            Child child,
            Guid? viewerUserId,
            IReadOnlyDictionary<Guid, Relationship>? familyRelationships)
        {
            if (child.UserType == UserType.Child)
            {
                return FamilyMemberDisplayType.Child;
            }

            if (!viewerUserId.HasValue || child.UserType != UserType.Self)
            {
                return FamilyMemberDisplayType.OtherAdult;
            }

            if (child.ChildId == viewerUserId.Value)
            {
                return FamilyMemberDisplayType.Self;
            }

            if (familyRelationships != null
                && familyRelationships.TryGetValue(viewerUserId.Value, out var viewerRelationship)
                && familyRelationships.TryGetValue(child.ChildId, out var memberRelationship)
                && AreSpouses(viewerRelationship, memberRelationship))
            {
                return FamilyMemberDisplayType.Spouse;
            }

            return FamilyMemberDisplayType.OtherAdult;
        }

        private static bool AreSpouses(Relationship first, Relationship second)
        {
            return (first == Relationship.Mother && second == Relationship.Father)
                || (first == Relationship.Father && second == Relationship.Mother);
        }
    }
}
