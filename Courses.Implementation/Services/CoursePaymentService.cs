using Courses.Repository;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services.Implementation
{
    public class CoursePaymentService : ICoursePaymentService
    {
        private readonly ICoursePaymentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;

        public CoursePaymentService(ICoursePaymentRepository repository, IStudentCourseTransactionService studentCourseTransactionService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseTransactionService;
        }

        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        {
            var transaction = await _studentCourseTransactionService.GetTransaction(payment.StudentCourseTransactionId).ConfigureAwait(false);
            
            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var paymentResponse = await _repository.AddPayment(payment).ConfigureAwait(false);
            var allPayments = await _repository.GetAllPayments(payment.StudentCourseTransactionId).ConfigureAwait(false);
            decimal totalPaid = allPayments.Sum(p => p.AmountPaid);

            //update the transaction for the paid amount

            //transaction.TotalAmountPaid = totalPaid;
            AddStudentCourseTransaction updatedTransaction = new AddStudentCourseTransaction
            {
                FamilyId = transaction.FamilyId,
                FeeAmountDiscount = transaction.FeeAmountDiscount,
                StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                Comments = transaction.Comments + $"\n added payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()} on date: {DateTime.UtcNow}",
                DayCareDiscount = transaction.DayCareDiscount,
                DayCareFee = transaction.DayCareFee,
                TotalAmountPaid = totalPaid,
                PayableFee = transaction.PayableFee,
                TotalPayable = transaction.TotalPayable,
                PaymentCode = transaction.PaymentCode,
                StudentCourseEnrollmentIds = new List<Guid>(),
                FeeInstallments = transaction.FeeInstallments,
                TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.PartiallyPaid,
                RegistrationStatus = transaction.RegistrationStatus,
                IsActive = transaction.IsActive,
            };
            
            updatedTransaction.IsCompletelyPaid = updatedTransaction.TotalPayable <= updatedTransaction.TotalAmountPaid;
            if (updatedTransaction.IsCompletelyPaid)
            {
                updatedTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.FullyPaid;
            }

            await _studentCourseTransactionService.UpdateTransaction(transaction.StudentCourseTransactionId, updatedTransaction).ConfigureAwait(false);

            return paymentResponse;
        }

        public async Task<CoursePaymentResponse> GetPayment(Guid paymentId)
            => await _repository.GetPayment(paymentId);

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId)
            => await _repository.GetAllPayments(transactionId);

        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        {
            var transaction = await _studentCourseTransactionService.GetTransaction(payment.StudentCourseTransactionId).ConfigureAwait(false);

            if (transaction == null)
            {
                throw new Exception("The transaaction doesn't exist");
            }

            var paymentResponse = await _repository.UpdatePayment(paymentId, payment).ConfigureAwait(false); 

            var allPayments = await _repository.GetAllPayments(payment.StudentCourseTransactionId).ConfigureAwait(false);
            decimal totalPaid = allPayments.Sum(p => p.AmountPaid);

            //update the transaction for the paid amount

            transaction.TotalAmountPaid += payment.AmountPaid;

            AddStudentCourseTransaction updatedTransaction = new AddStudentCourseTransaction
            {
                FamilyId = transaction.FamilyId,
                FeeAmountDiscount = transaction.FeeAmountDiscount,
                StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                Comments = transaction.Comments + $"\n added payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()}",
                DayCareDiscount = transaction.DayCareDiscount,
                DayCareFee = transaction.DayCareFee,
                TotalAmountPaid = totalPaid,
                PayableFee = transaction.PayableFee,
                TotalPayable = transaction.TotalPayable,
                PaymentCode = transaction.PaymentCode,
                StudentCourseEnrollmentIds = new List<Guid>(),
                FeeInstallments = transaction.FeeInstallments,
                TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.PartiallyPaid,
                RegistrationStatus = transaction.RegistrationStatus,
                IsActive = transaction.IsActive,
            };

            updatedTransaction.IsCompletelyPaid = updatedTransaction.TotalPayable <= updatedTransaction.TotalAmountPaid;
            if (updatedTransaction.IsCompletelyPaid)
            {
                updatedTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.FullyPaid;
            }

            await _studentCourseTransactionService.UpdateTransaction(transaction.StudentCourseTransactionId, updatedTransaction).ConfigureAwait(false);

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

            var allPayments = await _repository.GetAllPayments(paymentDetails.StudentCourseTransactionId).ConfigureAwait(false);
            decimal totalPaid = allPayments.Sum(p => p.AmountPaid);

            AddStudentCourseTransaction updatedTransaction = new AddStudentCourseTransaction
            {
                FamilyId = transaction.FamilyId,
                FeeAmountDiscount = transaction.FeeAmountDiscount,
                StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                Comments = transaction.Comments + $"\n Removed payment: {paymentId.ToString()}",
                DayCareDiscount = transaction.DayCareDiscount,
                DayCareFee = transaction.DayCareFee,
                TotalAmountPaid = totalPaid,
                PayableFee = transaction.PayableFee,
                TotalPayable = transaction.TotalPayable,
                PaymentCode = transaction.PaymentCode,
                StudentCourseEnrollmentIds = new List<Guid>(),
                FeeInstallments = transaction.FeeInstallments,
                TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.PartiallyPaid,
                RegistrationStatus = transaction.RegistrationStatus, 
                IsActive = transaction.IsActive,
            };

            updatedTransaction.IsCompletelyPaid = updatedTransaction.TotalPayable <= updatedTransaction.TotalAmountPaid;
            if (updatedTransaction.IsCompletelyPaid)
            {
                updatedTransaction.TransactionStatus = MaktabDataContracts.Enums.TransactionStatus.FullyPaid;
            }

            await _studentCourseTransactionService.UpdateTransaction(transaction.StudentCourseTransactionId, updatedTransaction).ConfigureAwait(false);

            return paymentResponse;
            
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByStudentTransactionId(Guid studentTransactionId)
        {
            return await _repository.GetAllPaymentsByStudentTransactionId(studentTransactionId).ConfigureAwait(false);
        }
    }
}
