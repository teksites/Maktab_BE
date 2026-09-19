using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;

using MaktabDataContracts.Enums;

namespace Courses.Services
{
    public interface IInstituteService
    {
        Task<InstituteResponse> AddInstitute(AddInstitute institute);
        Task<bool> DeleteInstitute(Guid instituteId, bool hardDelete = false);
        Task<IEnumerable<InstituteResponse>> GetAllInstitutes(bool onlyActive = true, InstituteType? instituteType = null);
        Task<InstituteResponse> GetInstitute(Guid instituteId);
        Task<bool> UpdateInstitute(Guid instituteId, AddInstitute institute);
    }
}
