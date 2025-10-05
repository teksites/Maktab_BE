using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;


namespace Courses.Implementation.Services
{
    public class InstituteService : IInstituteService
    {
        private readonly IInstituteRepository _repository;

        public InstituteService(IInstituteRepository repository)
        {
            _repository = repository;
        }

        public Task<InstituteResponse> AddInstitute(AddInstitute institute)
            => _repository.AddInstitute(institute);

        public Task<InstituteResponse> GetInstitute(Guid instituteId)
            => _repository.GetInstitute(instituteId);

        public Task<IEnumerable<InstituteResponse>> GetAllInstitutes(bool onlyActive = true)
            => _repository.GetAllInstitutes(onlyActive);

        public Task<bool> UpdateInstitute(Guid instituteId, AddInstitute institute)
            => _repository.UpdateInstitute(instituteId, institute);

        public Task<bool> DeleteInstitute(Guid instituteId, bool hardDelete = false)
            => _repository.DeleteInstitute(instituteId, hardDelete);

    }
}
