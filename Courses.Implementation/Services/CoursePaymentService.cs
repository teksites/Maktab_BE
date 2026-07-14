using Courses.Repository;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using System.Net;
using Users.Services;

namespace Courses.Services.Implementation
{
    public class CoursePaymentService : ICoursePaymentService
    {
        private const string PaymentConfirmationSubject = "ICC Maktab payment confirmation - confirmation de paiement ICC Maktab";
        private const string RefundConfirmationSubject = "ICC Maktab refund confirmation - confirmation de remboursement ICC Maktab";

        private readonly ICoursePaymentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly ICourseService _courseService;
        private readonly IUserService _userService;
        private readonly ISendEmailService _sendEmailService;

        public CoursePaymentService(
            ICoursePaymentRepository repository,
            IStudentCourseTransactionService studentCourseTransactionService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            ICourseService courseService,
            IUserService userService,
            ISendEmailService sendEmailService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseTransactionService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _courseService = courseService;
            _userService = userService;
            _sendEmailService = sendEmailService;
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
                    AppendTransactionComment(
                        transaction.Comments,
                        $"added payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()} on date: {DateTime.UtcNow}",
                        payment.Comments)))
                .ConfigureAwait(false);
            await RecalculateEnrollmentState(transaction).ConfigureAwait(false);
            await SendPaymentNotificationIfApplicableAsync(payment, transaction).ConfigureAwait(false);

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
                    AppendTransactionComment(
                        transaction.Comments,
                        $"updated payment: {payment.AmountPaid} via payment mode: {payment.PaymentMode.ToString()} on date: {DateTime.UtcNow}",
                        payment.Comments)))
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

        private static string AppendTransactionComment(string? existingComments, string summary, string? paymentComment = null)
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(existingComments))
            {
                segments.Add(existingComments.TrimEnd());
            }

            var entry = string.IsNullOrWhiteSpace(paymentComment)
                ? summary
                : $"{summary}. Comment: {paymentComment}";

            segments.Add(entry);
            return string.Join("\n", segments);
        }

        private async Task SendPaymentNotificationIfApplicableAsync(
            AddCoursePayment payment,
            StudentCourseTransactionResponse transaction)
        {
            var notificationType = GetPaymentNotificationType(payment.PaymentType);
            if (notificationType == null)
            {
                return;
            }

            var courseId = transaction.Enrollments.FirstOrDefault()?.CourseId ?? Guid.Empty;
            if (courseId == Guid.Empty)
            {
                return;
            }

            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false);
            if (course == null)
            {
                return;
            }

            var targetEmails = await GetFamilyNotificationEmailAddressesAsync(transaction.FamilyId).ConfigureAwait(false);
            if (!targetEmails.Any())
            {
                return;
            }

            var email = notificationType == PaymentNotificationType.Payment
                ? BuildPaymentConfirmationEmail(payment.AmountPaid, course)
                : BuildRefundConfirmationEmail(payment.AmountPaid, course);

            await _sendEmailService.SendBulkEmail(new MultiUserEmailData
            {
                To = targetEmails,
                Subject = email.Subject,
                Body = email.Body
            }).ConfigureAwait(false);
        }

        private async Task<List<string>> GetFamilyNotificationEmailAddressesAsync(Guid familyId)
        {
            var familyUsers = await _userService.GetAllFamilyUsersInformation(familyId, true).ConfigureAwait(false)
                ?? Enumerable.Empty<MaktabDataContracts.Responses.Users.UserInformationResponse>();

            return familyUsers
                .Where(x => x.Relationship == Relationship.Mother ||
                            x.Relationship == Relationship.Father ||
                            x.Relationship == Relationship.Guardian)
                .Select(x => x.Email?.Trim() ?? string.Empty)
                .Where(emailAddress => !string.IsNullOrWhiteSpace(emailAddress))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static PaymentEmailContent BuildPaymentConfirmationEmail(decimal amount, CourseResponseDetailed course)
        {
            var amountText = amount.ToString("0.00");
            var courseName = WebUtility.HtmlEncode(course.Name ?? string.Empty);
            var courseNameFr = WebUtility.HtmlEncode(course.NameFr ?? course.Name ?? string.Empty);

            return new PaymentEmailContent(
                PaymentConfirmationSubject,
                $"<p>Your payment <strong>{amountText}</strong> has been received for the <strong>{courseName}</strong>.</p>" +
                "<p>Please review your parent portal to see your payment schedule and amount remaining (if any).</p>" +
                "<div>&nbsp;</div>" +
                $"<p>Votre paiement <strong>{amountText}</strong> est confirme pour <strong>{courseNameFr}</strong>.</p>" +
                "<p>Veuillez svp verifier votre portail pour voir votre cedule de paiement et le montant restant, s'il y a lieu.</p>");
        }

        private static PaymentEmailContent BuildRefundConfirmationEmail(decimal amount, CourseResponseDetailed course)
        {
            var amountText = amount.ToString("0.00");
            var courseName = WebUtility.HtmlEncode(course.Name ?? string.Empty);
            var courseNameFr = WebUtility.HtmlEncode(course.NameFr ?? course.Name ?? string.Empty);

            return new PaymentEmailContent(
                RefundConfirmationSubject,
                $"<p>Your refund <strong>{amountText}</strong> has been processed for the <strong>{courseName}</strong>.</p>" +
                "<p>Please review your parent portal to see your payment schedule and amount remaining (if any).</p>" +
                "<div>&nbsp;</div>" +
                $"<p>Votre remboursement de <strong>{amountText}</strong> est confirme pour <strong>{courseNameFr}</strong>.</p>" +
                "<p>Veuillez svp verifier votre portail pour voir votre cedule de paiement et le montant restant, s'il y a lieu.</p>");
        }

        private static PaymentNotificationType? GetPaymentNotificationType(MaktabDataContracts.Enums.PaymentType paymentType)
        {
            return paymentType switch
            {
                MaktabDataContracts.Enums.PaymentType.Credit => PaymentNotificationType.Payment,
                MaktabDataContracts.Enums.PaymentType.Refund => PaymentNotificationType.Refund,
                _ => null
            };
        }

        private sealed record PaymentEmailContent(string Subject, string Body);

        private enum PaymentNotificationType
        {
            Payment,
            Refund
        }
    }
}
