using Courses.Repository;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Services.Implementation
{
    public class CoursePaymentService : ICoursePaymentService
    {
        private readonly ICoursePaymentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;

        public CoursePaymentService(
            ICoursePaymentRepository repository,
            IStudentCourseTransactionService studentCourseTransactionService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseTransactionService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
        }

        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        {
            NormalizePayment(payment);
            var transaction = await _studentCourseTransactionService.GetTransaction(payment.StudentCourseTransactionId).ConfigureAwait(false);
            
            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var result = await TryAddPayment(payment).ConfigureAwait(false);
            return result.Payment;
        }

        public async Task<(CoursePaymentResponse Payment, bool Created)> TryAddPayment(AddCoursePayment payment)
        {
            NormalizePayment(payment);
            var transaction = await _studentCourseTransactionService.GetTransaction(payment.StudentCourseTransactionId).ConfigureAwait(false);

            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var result = await _repository.TryAddPayment(payment).ConfigureAwait(false);
            if (!result.Created)
            {
                return result;
            }

            var allPayments = (await _repository.GetAllPayments(payment.StudentCourseTransactionId).ConfigureAwait(false)).ToList();
            await _studentCourseTransactionService.UpdateTransaction(
                transaction.StudentCourseTransactionId,
                BuildUpdatedTransaction(
                    transaction,
                    allPayments,
                    transaction.Comments + $"\n added payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()} on date: {DateTime.UtcNow}"))
                .ConfigureAwait(false);
            await RecalculateEnrollmentState(transaction).ConfigureAwait(false);

            return result;
        }

        public async Task<CoursePaymentResponse> GetPayment(Guid paymentId)
            => await _repository.GetPayment(paymentId);

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId)
            => await _repository.GetAllPayments(transactionId);

        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        {
            NormalizePayment(payment);
            var transaction = await _studentCourseTransactionService.GetTransaction(payment.StudentCourseTransactionId).ConfigureAwait(false);

            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var paymentResponse = await _repository.UpdatePayment(paymentId, payment).ConfigureAwait(false); 

            var allPayments = (await _repository.GetAllPayments(payment.StudentCourseTransactionId).ConfigureAwait(false)).ToList();
            await _studentCourseTransactionService.UpdateTransaction(
                transaction.StudentCourseTransactionId,
                BuildUpdatedTransaction(
                    transaction,
                    allPayments,
                    transaction.Comments + $"\n added payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()}"))
                .ConfigureAwait(false);
            await RecalculateEnrollmentState(transaction).ConfigureAwait(false);

            return paymentResponse;
        }

        public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
        {
            var paymentDetails = await _repository.GetPayment(paymentId).ConfigureAwait(false);

            if (paymentDetails == null)
            {
                return false;
            }

            var transaction = await _studentCourseTransactionService.GetTransaction(paymentDetails.StudentCourseTransactionId).ConfigureAwait(false);

            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var paymentResponse = await _repository.DeletePayment(paymentId, hardDelete).ConfigureAwait(false);

            var allPayments = (await _repository.GetAllPayments(paymentDetails.StudentCourseTransactionId).ConfigureAwait(false)).ToList();
            await _studentCourseTransactionService.UpdateTransaction(
                transaction.StudentCourseTransactionId,
                BuildUpdatedTransaction(
                    transaction,
                    allPayments,
                    transaction.Comments + $"\n Removed payment: {paymentId.ToString()}"))
                .ConfigureAwait(false);
            await RecalculateEnrollmentState(transaction).ConfigureAwait(false);

            return paymentResponse;
            
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByStudentTransactionId(Guid studentTransactionId)
        {
            return await _repository.GetAllPaymentsByStudentTransactionId(studentTransactionId).ConfigureAwait(false);
        }

        private async Task RecalculateEnrollmentState(StudentCourseTransactionResponse transaction)
        {
            var courseId = transaction.Enrollments.FirstOrDefault()?.CourseId ?? Guid.Empty;
            if (courseId == Guid.Empty)
            {
                return;
            }

            await _studentCourseEnrollmentService
                .RecalculateCourseFee(courseId, transaction.FamilyId)
                .ConfigureAwait(false);
        }

        private static void NormalizePayment(AddCoursePayment payment)
        {
            ArgumentNullException.ThrowIfNull(payment);

            if (!Enum.IsDefined(typeof(MaktabDataContracts.Enums.PaymentType), payment.PaymentType))
            {
                payment.PaymentType = MaktabDataContracts.Enums.PaymentType.Credit;
            }
        }

        private static AddStudentCourseTransaction BuildUpdatedTransaction(
            StudentCourseTransactionResponse transaction,
            IReadOnlyCollection<CoursePaymentResponse> currentPayments,
            string comments)
        {
            var updatedTransaction = new AddStudentCourseTransaction
            {
                FamilyId = transaction.FamilyId,
                FeeAmountDiscount = transaction.FeeAmountDiscount,
                StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                Comments = comments,
                DayCareDiscount = transaction.DayCareDiscount,
                DayCareFee = transaction.DayCareFee,
                TotalAmountPaid = CalculateTotalAmountPaid(currentPayments),
                PayableFee = transaction.PayableFee,
                TotalPayable = transaction.TotalPayable,
                PaymentCode = transaction.PaymentCode,
                StudentCourseEnrollmentIds = new List<Guid>(),
                FeeInstallments = transaction.FeeInstallments,
                TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.PartiallyPaid,
                RegistrationStatus = transaction.RegistrationStatus,
                IsActive = transaction.IsActive,
                Surcharge = transaction.Surcharge,
            };

            updatedTransaction.IsCompletelyPaid = updatedTransaction.TotalPayable <= updatedTransaction.TotalAmountPaid;
            if (updatedTransaction.IsCompletelyPaid)
            {
                updatedTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.FullyPaid;
            }

            return updatedTransaction;
        }

        private static decimal CalculateTotalAmountPaid(IEnumerable<CoursePaymentResponse> payments)
        {
            return payments.Sum(payment => payment.PaymentType switch
            {
                MaktabDataContracts.Enums.PaymentType.Refund => -payment.AmountPaid,
                MaktabDataContracts.Enums.PaymentType.Debit => -payment.AmountPaid,
                MaktabDataContracts.Enums.PaymentType.Surcharge => 0m,
                _ => payment.AmountPaid
            });
        }
    }
}
