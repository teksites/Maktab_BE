using Courses.Services;
using Courses.Repository;
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Implementation
{

    public class CourseReportingService : ICourseReportingService
    {
        private readonly IStudentCourseTransactionRepository _transactionRepo;
        private readonly ICoursePaymentRepository _paymentRepo;
        private readonly IStudentCourseEnrollmentRepository _enrollmentRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly ICourseEnrollmentGroupRepository _groupRepo;
        private readonly IInstituteRepository _instituteRepo;

        public CourseReportingService(
            IStudentCourseTransactionRepository transactionRepo,
            ICoursePaymentRepository paymentRepo,
            IStudentCourseEnrollmentRepository enrollmentRepo,
            ICourseRepository courseRepo,
            ICourseEnrollmentGroupRepository groupRepo,
            IInstituteRepository instituteRepo)
        {
            _transactionRepo = transactionRepo;
            _paymentRepo = paymentRepo;
            _enrollmentRepo = enrollmentRepo;
            _courseRepo = courseRepo;
            _groupRepo = groupRepo;
            _instituteRepo = instituteRepo;
        }

        // ----------------------------
        // Pending Amounts per Family
        // ----------------------------
        public async Task<IEnumerable<PendingAmountPerFamily>> GetPendingAmountsPerFamily()
        {
            var transactions = await _transactionRepo.GetAllTransactionsByFamily(Guid.Empty); // Fetch all
            return transactions
                .GroupBy(t => t.FamilyId)
                .Select(g => new PendingAmountPerFamily
                {
                    FamilyId = g.Key,
                    FamilyName = "", // Optional: fetch from family repo if available
                    PendingAmount = g.Sum(t => t.TotalPayable - t.TotalAmountPaid)
                });
        }

        // ----------------------------
        // Pending Amounts per Institute
        // ----------------------------
        public async Task<IEnumerable<PendingAmountPerInstitute>> GetPendingAmountsPerInstitute()
        {
            var transactions = await _transactionRepo.GetAllTransactionsByFamily(Guid.Empty);
            var result = new List<PendingAmountPerInstitute>();

            foreach (var t in transactions)
            {
                var enrollment = await _enrollmentRepo.GetEnrollment(t.StudentCourseEnrollmentId);
                var course = await _courseRepo.GetCourse(enrollment.CourseId);

                result.Add(new PendingAmountPerInstitute
                {
                    InstituteId = course.InstituteId,
                    InstituteName = "", // Fetch from institute repo if needed
                    PendingAmount = t.TotalPayable - t.TotalAmountPaid
                });
            }

            return result
                .GroupBy(r => r.InstituteId)
                .Select(g => new PendingAmountPerInstitute
                {
                    InstituteId = g.Key,
                    InstituteName = g.First().InstituteName,
                    PendingAmount = g.Sum(x => x.PendingAmount)
                });
        }

        // ----------------------------
        // Pending Amounts per Course
        // ----------------------------
        public async Task<IEnumerable<PendingAmountPerCourse>> GetPendingAmountsPerCourse()
        {
            var transactions = await _transactionRepo.GetAllTransactionsByFamily(Guid.Empty);
            var result = new List<PendingAmountPerCourse>();

            foreach (var t in transactions)
            {
                var enrollment = await _enrollmentRepo.GetEnrollment(t.StudentCourseEnrollmentId);
                var course = await _courseRepo.GetCourse(enrollment.CourseId);

                result.Add(new PendingAmountPerCourse
                {
                    CourseId = course.CourseId,
                    CourseName = course.Name,
                    PendingAmount = t.TotalPayable - t.TotalAmountPaid
                });
            }

            return result
                .GroupBy(r => r.CourseId)
                .Select(g => new PendingAmountPerCourse
                {
                    CourseId = g.Key,
                    CourseName = g.First().CourseName,
                    PendingAmount = g.Sum(x => x.PendingAmount)
                });
        }

        // ----------------------------
        // Pending Amounts per Course Group
        // ----------------------------
        public async Task<IEnumerable<PendingAmountPerCourseGroup>> GetPendingAmountsPerCourseGroup()
        {
            var transactions = await _transactionRepo.GetAllTransactionsByFamily(Guid.Empty);
            var result = new List<PendingAmountPerCourseGroup>();

            foreach (var t in transactions)
            {
                var enrollment = await _enrollmentRepo.GetEnrollment(t.StudentCourseEnrollmentId);
                var group = await _groupRepo.GetGroup(enrollment.CourseEnrollmentGroupId);

                result.Add(new PendingAmountPerCourseGroup
                {
                    CourseEnrollmentGroupId = group.CourseEnrollmentGroupId,
                    GroupTitle = group.GroupTitle,
                    PendingAmount = t.TotalPayable - t.TotalAmountPaid
                });
            }

            return result
                .GroupBy(r => r.CourseEnrollmentGroupId)
                .Select(g => new PendingAmountPerCourseGroup
                {
                    CourseEnrollmentGroupId = g.Key,
                    GroupTitle = g.First().GroupTitle,
                    PendingAmount = g.Sum(x => x.PendingAmount)
                });
        }

        // ----------------------------
        // Total Collected per Institute
        // ----------------------------
        public async Task<IEnumerable<TotalCollectedPerInstitute>> GetTotalPaymentsCollectedPerInstitute()
        {
            var payments = await _paymentRepo.GetAllPayments(Guid.Empty);
            var result = new List<TotalCollectedPerInstitute>();

            foreach (var p in payments)
            {
                var transaction = await _transactionRepo.GetTransaction(p.StudentCourseTransactionId);
                var enrollment = await _enrollmentRepo.GetEnrollment(transaction.StudentCourseEnrollmentId);
                var course = await _courseRepo.GetCourse(enrollment.CourseId);

                result.Add(new TotalCollectedPerInstitute
                {
                    InstituteId = course.InstituteId,
                    InstituteName = "", // Fetch from institute repo if needed
                    TotalCollected = p.AmountPaid
                });
            }

            return result
                .GroupBy(r => r.InstituteId)
                .Select(g => new TotalCollectedPerInstitute
                {
                    InstituteId = g.Key,
                    InstituteName = g.First().InstituteName,
                    TotalCollected = g.Sum(x => x.TotalCollected)
                });
        }
    }
}
