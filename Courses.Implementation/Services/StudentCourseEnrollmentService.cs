using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using System.Transactions;

namespace Courses.Implementation.Services
{
    public class StudentCourseEnrollmentService : IStudentCourseEnrollmentService
    {
        private readonly IStudentCourseEnrollmentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly ICourseService _courseService;

        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository, IStudentCourseTransactionService studentCourseEnrollmentService, ICourseService courseService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseEnrollmentService;
            _courseService = courseService;
        }

        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {

            var childEnrollment = await GetStudentCourseEnrollment(enrollment.ChildId, enrollment.CourseId).ConfigureAwait(false);

            // If child is not already registered to the same course
            if (childEnrollment is not null && childEnrollment.CourseEnrollmentGroupId != enrollment.CourseEnrollmentGroupId)
            {

                var familyTransactions = await _studentCourseTransactionService.GetCourseTransactionsByFamily(enrollment.CourseId, enrollment.FamilyId).ConfigureAwait(false);

                if (familyTransactions.Any()) // we have to append the transaction and recalculate the discount and fee in transaction
                {
                    // Add enrollment first
                    var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);

                    var transaction = await _studentCourseTransactionService.GetTransaction(familyTransactions.First().StudentCourseTransactionId).ConfigureAwait(false);

                    // Update the transcation
                    // First create the transaction based on course data
                    var transactionToAdd = await CreateTransactionData(enrollment, new AddStudentCourseTransaction
                    {
                        StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                        FamilyId = transaction.FamilyId,
                        PaymentCode = transaction.PaymentCode,
                        AmountDiscounted = transaction.AmountDiscounted,
                        Comments = transaction.Comments,
                        DayCareFee = transaction.DayCareFee,
                        IsActive = transaction.IsActive,
                        IsCompletelyPaid = transaction.IsCompletelyPaid,
                        PayableFee = transaction.PayableFee,
                        StudentCourseEnrollmentIds = new List<Guid> { transaction.StudentCourseEnrollmentId },
                        TotalAmountPaid = transaction.TotalAmountPaid,
                        TotalPayable = transaction.TotalPayable,
                         TransactionStatus = transaction.TransactionStatus
                    }).ConfigureAwait(false);
                    
                    if (addedEnrollment != null)
                    {
                        var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(transaction.StudentCourseTransactionId,
                            addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                    }

                    //Now update the transaction to include the new enrollment

                }
                else //its a brand new registration for the course
                {
                    // Add enrollment first
                    var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);
                    StudentCourseTransactionResponse transaction = null; 
                    
                    // Add student transaction
                    if (addedEnrollment != null)
                    {
                        // First create the transaction based on course data
                        transaction = await _studentCourseTransactionService.AddTransaction(await CreateTransactionData(enrollment, null).ConfigureAwait(false)
                            ).ConfigureAwait(false);
                        
                        if (transaction != null)
                        {
                            var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(transaction.StudentCourseEnrollmentId,
                                addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                        }
                        else//reverting the state
                        {
                            await _repository.DeleteEnrollment(addedEnrollment.StudentCourseEnrollmentId, true).ConfigureAwait(false);
                        }
                    }

                    if (addedEnrollment != null && transaction != null)
                    {
                        var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(familyTransactions.First().StudentCourseTransactionId,
                            addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                    }
                }

                // Now add the transcation
                //if (latestTransaction is not null)// add logic to check the course session here
                //{
                // caclulate the discount if any
                //Calculate the payble fee
                // Get instution polucy for discount

                //     _studentCourseEnrollmentService.UpdateTransaction(new AddStudentCourseTransaction
                //     {
                //         AmountDiscounted = latestTransaction.AmountDiscounted,
                //         StudentCourseTransactionId = latestTransaction.StudentCourseTransactionId,

                //         FamilyId = latestTransaction.FamilyId,
                //         PaymentCode = latestTransaction.PaymentCode,
                //         //       StudentCourseEnrollmentId = latestTransaction.StudentCourseEnrollmentId
                //     });
                ////     latestTransaction.StudentCourseEnrollmentId
                // }

                // add serive to generate payment code and invoice id
                // in case no exisiting transaction found, create a new one
                // enrollment.TransactionId = latestTransaction.Id;

                return await _repository.AddEnrollment(enrollment).ConfigureAwait(false); 
            }

            return null;

        }

        private async Task<AddStudentCourseTransaction> CreateTransactionData(AddStudentCourseEnrollment enrollment, AddStudentCourseTransaction addStudentCourseTransaction)
        {
            var course = await _courseService.GetCourseGroup(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false);

            if (addStudentCourseTransaction == null) // new
            {
                addStudentCourseTransaction = new AddStudentCourseTransaction();
                addStudentCourseTransaction.StudentCourseTransactionId = Guid.NewGuid();
                addStudentCourseTransaction.FamilyId = enrollment.FamilyId;
                addStudentCourseTransaction.PaymentCode = $"GENERATECODE";
                addStudentCourseTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.AwaitingPayment;
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee = course.Fee; // get from course
                addStudentCourseTransaction.AmountDiscounted = 0;
                addStudentCourseTransaction.DayCareFee = course.Fee;//add day care fee in course group
                addStudentCourseTransaction.TotalPayable = addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee - addStudentCourseTransaction.AmountDiscounted;
                addStudentCourseTransaction.Comments = "New Enrollment";
                addStudentCourseTransaction.IsCompletelyPaid = false;
            }
            else //exisiting
            {
                // Add logic to recacluate fee
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee += course.Fee; // get from course
                addStudentCourseTransaction.AmountDiscounted = 0;
                addStudentCourseTransaction.DayCareFee += course.Fee;
                //Get discount on fee
                addStudentCourseTransaction.TotalPayable = addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee - addStudentCourseTransaction.AmountDiscounted;
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
