using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Policies;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Newtonsoft.Json;

namespace Courses.Implementation.Services
{
    public class StudentCourseEnrollmentService : IStudentCourseEnrollmentService
    {
        private readonly IStudentCourseEnrollmentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly ICourseService _courseService;
        private readonly IInstitutePolicyService _policyService;

        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository, IStudentCourseTransactionService studentCourseEnrollmentService, ICourseService courseService, IInstitutePolicyService policyService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseEnrollmentService;
            _courseService = courseService;
            _policyService = policyService;
        }

        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {

            var familyTransactions = await _studentCourseTransactionService.GetCourseTransactionsByFamily(enrollment.CourseId, enrollment.FamilyId).ConfigureAwait(false);

            var familyCourseTransaction = familyTransactions.Where(x => x.Enrollments.All(y => y.CourseId == enrollment.CourseId));
            var course = await _courseService.GetCourse(enrollment.CourseId).ConfigureAwait(false);
            var selectedCourseEnrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

            if (familyCourseTransaction.Any())// We have the transaction for the same course for same child or other child at the moment
            {
                //var childEnrollments = await GetStudentCourseEnrollment(enrollment.ChildId, enrollment.CourseId).ConfigureAwait(false);
                var childEnrollment = familyCourseTransaction.First().Enrollments.Where(childEnrollment => childEnrollment.ChildId == enrollment.ChildId);
                var enrollmentForExistingChild = childEnrollment.Any(x => x.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                if (enrollmentForExistingChild)
                {
                    throw new Exception("The child is already registered to the selected course group");
                }

                var transaction = familyCourseTransaction.First();

                var newDayCareFee = (enrollment.WillUseDayCare ? selectedCourseEnrollmentGroup.DayCareFee : 0);
                decimal newCourseFee = 0;

                //if there is any existing enrollment for the child in the same course for any course group. if it is the same course group then we will not allow registration
                if (childEnrollment.Any()) // child is already registered for the same course in different course group
                {
                    enrollment.EnrollmentIndex = childEnrollment.Max(e => e.EnrollmentIndex);
                    newCourseFee = selectedCourseEnrollmentGroup.Fee;
                    //Now update the transaction to include the new enrollment

                }
                else // child is not registered for any course group of the course
                {
                    //here we have to add new enrollment and update the existing transaction to include the new enrollment for new child
                    // We will calculate the discount and apply
                    var policies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                    var discountPolicy = policies.Where(p => p.IsActive && p.InstutePolicy == MaktabDataContracts.Enums.InstutePolicyType.SiblingDiscount).First().Details;
                    SiblingDiscountPolicy policy = JsonConvert.DeserializeObject<SiblingDiscountPolicy>(discountPolicy);

                    var distinctChildIds = familyTransactions
                        .SelectMany(t => t.Enrollments)
                        .Select(e => e.ChildId)
                        .Distinct()
                        .ToList();
                    
                    enrollment.EnrollmentIndex = distinctChildIds.Count + 1;

                    decimal discountPercentage = policy.FirstChildFee / 100;

                    if (distinctChildIds.Count == 1)
                    {
                        discountPercentage = policy.SecondChildFee / 100;
                    }
                    else if (distinctChildIds.Count >= 2)
                    {
                        discountPercentage = policy.ThirdAndOnwardChildFee / 100;
                    }

                    newCourseFee = selectedCourseEnrollmentGroup.Fee * discountPercentage;
                }

                // Add enrollment first
                var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);

                //var transaction = await _studentCourseTransactionService.GetTransaction(familyCourseTransaction.First().StudentCourseTransactionId).ConfigureAwait(false);

                // Update the transcation
                // First create the transaction based on course data
                var transactionToUpdate = new AddStudentCourseTransaction
                {
                    StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                    FamilyId = transaction.FamilyId,
                    PaymentCode = transaction.PaymentCode,
                    FeeAmountDiscount = transaction.FeeAmountDiscount,
                    DayCareDiscount = transaction.DayCareDiscount,
                    Comments = transaction.Comments,
                    DayCareFee = transaction.DayCareFee + newDayCareFee,
                    IsActive = transaction.IsActive,
                    IsCompletelyPaid = transaction.IsCompletelyPaid,
                    PayableFee = transaction.PayableFee + newCourseFee,
                    StudentCourseEnrollmentIds = new List<Guid> { transaction.StudentCourseEnrollmentId },
                    TotalAmountPaid = transaction.TotalAmountPaid,
                    TotalPayable = transaction.TotalPayable + newDayCareFee + newCourseFee,
                    TransactionStatus = transaction.TransactionStatus
                };
                var ifTransactionUpdated = await _studentCourseTransactionService.UpdateTransaction(transaction.StudentCourseTransactionId, transactionToUpdate).ConfigureAwait(false);

                if (ifTransactionUpdated)
                {
                    var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(transaction.StudentCourseTransactionId,
                        addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                }
                else
                {
                    await _repository.DeleteEnrollment(addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                }

                return addedEnrollment;
            }
            else // there is no previous transaction for the course for any child of the family. we will calculate new transaction and apply regisrtration fee
            {
                var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);
                StudentCourseTransactionResponse transaction = null;

                // Add student transaction
                var addStudentCourseTransaction = new AddStudentCourseTransaction();
                addStudentCourseTransaction.StudentCourseTransactionId = Guid.NewGuid();
                addStudentCourseTransaction.FamilyId = enrollment.FamilyId;
                addStudentCourseTransaction.PaymentCode = $"GENERATECODE";
                addStudentCourseTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.AwaitingPayment;
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee = selectedCourseEnrollmentGroup.Fee; // get from course
                addStudentCourseTransaction.FeeAmountDiscount = 0;
                addStudentCourseTransaction.DayCareDiscount = 0;
                addStudentCourseTransaction.DayCareFee = enrollment.WillUseDayCare ? selectedCourseEnrollmentGroup.DayCareFee : 0;//add day care fee in course group
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount);
                addStudentCourseTransaction.Comments = "New Enrollment";
                addStudentCourseTransaction.IsCompletelyPaid = false;

                try
                {
                    var addedTransaction = await _studentCourseTransactionService.AddTransaction(addStudentCourseTransaction).ConfigureAwait(false);
                    var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(addedTransaction.StudentCourseEnrollmentId,
                              addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                }
                catch(Exception e)
                {
                        await _repository.DeleteEnrollment(addedEnrollment.StudentCourseEnrollmentId, true).ConfigureAwait(false);
                }
                return addedEnrollment;
            }
        }

        private async Task<AddStudentCourseTransaction> CreateTransactionData(AddStudentCourseEnrollment enrollment, AddStudentCourseTransaction addStudentCourseTransaction)
        {
            //var course = await _courseService.GetCourseGroup(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false);
            var course = await _courseService.GetCourse(enrollment.CourseId).ConfigureAwait(false);
            var enrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);
           
            if (enrollmentGroup == null)
            {
                throw new Exception("Course enrollment group not found");
            }
             
            if (addStudentCourseTransaction == null) // new
            {
                addStudentCourseTransaction = new AddStudentCourseTransaction();
                addStudentCourseTransaction.StudentCourseTransactionId = Guid.NewGuid();
                addStudentCourseTransaction.FamilyId = enrollment.FamilyId;
                addStudentCourseTransaction.PaymentCode = $"GENERATECODE";
                addStudentCourseTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.AwaitingPayment;
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee = enrollmentGroup.Fee; // get from course
                addStudentCourseTransaction.FeeAmountDiscount = 0;
                addStudentCourseTransaction.DayCareDiscount = 0;
                addStudentCourseTransaction.DayCareFee = enrollmentGroup.DayCareFee;//add day care fee in course group
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) - 
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) ;
                addStudentCourseTransaction.Comments = "New Enrollment";
                addStudentCourseTransaction.IsCompletelyPaid = false;
            }
            else //exisiting
            {
                // Add logic to recacluate fee
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee += enrollmentGroup.Fee; // get from course
                addStudentCourseTransaction.FeeAmountDiscount += 0;
                addStudentCourseTransaction.DayCareFee += enrollmentGroup.DayCareFee;
                addStudentCourseTransaction.DayCareDiscount += 0;//Get discount on fee
                addStudentCourseTransaction.TotalPayable += (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee);
                addStudentCourseTransaction.IsCompletelyPaid = false;
            }

            return addStudentCourseTransaction;
        }
        public Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId)
            => _repository.GetEnrollment(enrollmentId);
        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId)
            => _repository.GetAllEnrollmentsByFamily(familyId);

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
            => _repository.GetAllEnrollmentsByCourse(courseId);

        public Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
            => _repository.UpdateEnrollment(enrollmentId, enrollment);

        public Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
            => _repository.DeleteEnrollment(enrollmentId, hardDelete);
        public Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
            => _repository.GetStudentCourseEnrollment(childId, courseId);
    }
}
