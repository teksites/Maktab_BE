using Courses.Repository;
using Courses.Services;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using System.Net;
using Users.Services;

namespace Courses.Implementation.Services
{
    public class StudentCourseTransactionService : IStudentCourseTransactionService
    {
        private const string DiscountConfirmationSubject = "ICC Maktab discount confirmation - confirmation de rabais ICC Maktab";

        private readonly IStudentCourseTransactionRepository _repository;
        private readonly ICourseService _courseService;
        private readonly IUserService _userService;
        private readonly ISendEmailService _sendEmailService;

        public StudentCourseTransactionService(
            IStudentCourseTransactionRepository repository,
            ICourseService courseService,
            IUserService userService,
            ISendEmailService sendEmailService)
        {
            _repository = repository;
            _courseService = courseService;
            _userService = userService;
            _sendEmailService = sendEmailService;
        }

        // ----------------------------
        // Transactions
        // ----------------------------
        public Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
            => _repository.AddTransaction(transaction);

        public Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId)
            => _repository.GetTransaction(transactionId);

        public async Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
        {
            var existingTransaction = await _repository.GetTransaction(transactionId).ConfigureAwait(false);
            await NormalizeTransactionTotalsAsync(existingTransaction, transaction).ConfigureAwait(false);
            var updated = await _repository.UpdateTransaction(transactionId, transaction).ConfigureAwait(false);

            if (!updated || existingTransaction == null)
            {
                return updated;
            }

            var discountIncrease = CalculateDiscountAmount(transaction) - CalculateDiscountAmount(existingTransaction);
            if (discountIncrease > 0m)
            {
                await SendDiscountNotificationIfApplicableAsync(existingTransaction, discountIncrease).ConfigureAwait(false);
            }

            return true;
        }

        public Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
            => _repository.DeleteTransaction(transactionId, hardDelete);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions()
            => _repository.GetAllTransactions();

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId)
            => _repository.GetTransactionByFamily(familyId);

        // ----------------------------
        // Enrollments
        // ----------------------------
        public Task<bool> AddEnrollmentsToTransaction(Guid studentCourseTransactionId, Guid studentCourseEnrollmentId)
            => _repository.AddEnrollmentsToTransaction(studentCourseTransactionId, studentCourseEnrollmentId);

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsForTransaction(Guid transactionId)
            => _repository.GetEnrollmentsForTransaction(transactionId);

        // ----------------------------
        // Payments
        // ----------------------------
        public Task<IEnumerable<StudentCoursePaymentResponse>> GetPaymentsByFamilyAsync(Guid familyId)
            => _repository.GetPaymentsByFamilyAsync(familyId);

        // ----------------------------
        // Pending amounts
        // ----------------------------
        public Task<IEnumerable<PendingAmountResponse>> GetPendingAmountsReportAsync(
            Guid? instituteId = null,
            Guid? courseId = null,
            Guid? courseGroupId = null,
            Guid? familyId = null,
            string? paymentCode = null)
            => _repository.GetPendingAmountsReportAsync(instituteId, courseId, courseGroupId, familyId, paymentCode);

        public Task<decimal> GetPendingAmountByInstitute(Guid instituteId)
            => _repository.GetPendingAmountByInstitute(instituteId);

        public Task<decimal> GetPendingAmountByCourse(Guid courseId)
            => _repository.GetPendingAmountByCourse(courseId);

        public Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId)
            => _repository.GetPendingAmountByCourseGroup(courseGroupId);

        public Task<decimal> GetPendingAmountByFamily(Guid familyId)
            => _repository.GetPendingAmountByFamily(familyId);

        // ----------------------------
        // Additional Queries
        // ----------------------------
        public Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId)
            => _repository.GetTransactionsPerCourseAsync(courseId);

        public Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId)
            => _repository.GetTransactionByFamilyForCurrentSession(familyId, instituteId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetInstituteTransactionsByFamily(Guid familyId, Guid instituteId)
            => _repository.GetInstituteTransactionsByFamily(familyId, instituteId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetCourseTransactionsByFamily(Guid courseId, Guid familyId)
            => _repository.GetCourseTransactionsByFamily(courseId, familyId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId)
            => _repository.GetAllTransactionsByCourse(courseId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByInstitute(Guid instituteId)
            => _repository.GetAllTransactionsByInstitute(instituteId);

        public Task<StudentCourseTransactionResponse?> GetTransactionByPaymentCode(string paymentCode)
        {
            return _repository.GetTransactionByPaymentCode(paymentCode);
        }

        public Task<bool> DeleteStudentCourseTransactionEnrollmentByEnrollmentId(Guid studentCourseEnrollmentId)
           => _repository.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(studentCourseEnrollmentId);

        public Task<bool> DeleteStudentCourseTransactionEnrollmentByTransactionId(Guid transactionId)
           => _repository.DeleteStudentCourseTransactionEnrollmentByTransactionId(transactionId);

        public Task<bool> DeleteStudentCourseTransactionEnrollmentById(Guid id)
           => _repository.DeleteStudentCourseTransactionEnrollmentById(id);

        private async Task SendDiscountNotificationIfApplicableAsync(
            StudentCourseTransactionResponse existingTransaction,
            decimal discountIncrease)
        {
            var courseId = existingTransaction.Enrollments.FirstOrDefault()?.CourseId ?? Guid.Empty;
            if (courseId == Guid.Empty)
            {
                return;
            }

            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false);
            if (course == null)
            {
                return;
            }

            var targetEmails = await GetFamilyNotificationEmailAddressesAsync(existingTransaction.FamilyId).ConfigureAwait(false);
            if (!targetEmails.Any())
            {
                return;
            }

            var email = BuildDiscountConfirmationEmail(discountIncrease, course);
            await _sendEmailService.SendBulkEmail(new MultiUserEmailData
            {
                To = targetEmails,
                Subject = email.Subject,
                Body = email.Body,
                SchoolContacts = new[] { CreateSchoolContact(course) }
            }).ConfigureAwait(false);
        }

        private static EmailSchoolContact CreateSchoolContact(CourseResponseDetailed course)
            => new()
            {
                Name = course.InstituteName,
                NameFr = course.InstituteNameFr,
                Email = course.InstituteEmail,
                Phone = course.InstitutePhone
            };

        private async Task<List<string>> GetFamilyNotificationEmailAddressesAsync(Guid familyId)
        {
            var verifiedEmails = await _userService.GetVerifiedFamilyNotificationEmailAddresses(familyId).ConfigureAwait(false);
            if (verifiedEmails?.Any() == true)
            {
                return verifiedEmails.ToList();
            }

            var familyUsers = await _userService.GetAllFamilyUsersInformation(familyId, true).ConfigureAwait(false)
                ?? Enumerable.Empty<MaktabDataContracts.Responses.Users.UserInformationResponse>();

            return familyUsers
                .Where(x => !x.IfTempUser &&
                            (x.Relationship == Relationship.Mother ||
                             x.Relationship == Relationship.Father ||
                             x.Relationship == Relationship.Guardian))
                .Select(x => x.Email?.Trim() ?? string.Empty)
                .Where(emailAddress => !string.IsNullOrWhiteSpace(emailAddress))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static decimal CalculateDiscountAmount(AddStudentCourseTransaction transaction)
            => transaction.FeeAmountDiscount + transaction.DayCareDiscount;

        private static decimal CalculateDiscountAmount(StudentCourseTransactionResponse transaction)
            => transaction.FeeAmountDiscount + transaction.DayCareDiscount;

        private async Task NormalizeTransactionTotalsAsync(
            StudentCourseTransactionResponse? existingTransaction,
            AddStudentCourseTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            var registrationFee = await GetCourseRegistrationFeeAsync(existingTransaction).ConfigureAwait(false);
            var recalculatedTotalPayable =
                transaction.PayableFee +
                transaction.DayCareFee +
                registrationFee -
                (transaction.FeeAmountDiscount + transaction.DayCareDiscount) +
                Convert.ToDecimal(transaction.Surcharge);

            transaction.TotalPayable = recalculatedTotalPayable < 0m ? 0m : recalculatedTotalPayable;
            transaction.FeeInstallments = NormalizeFeeInstallments(
                transaction.FeeInstallments,
                existingTransaction?.FeeInstallments,
                transaction.TotalPayable);
            transaction.IsCompletelyPaid = transaction.TotalPayable <= transaction.TotalAmountPaid;
        }

        private async Task<decimal> GetCourseRegistrationFeeAsync(StudentCourseTransactionResponse? existingTransaction)
        {
            var courseId = existingTransaction?.Enrollments?.FirstOrDefault()?.CourseId ?? Guid.Empty;
            if (courseId == Guid.Empty)
            {
                return 0m;
            }

            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false);
            return course?.RegistrationFee ?? 0m;
        }

        private static List<FeeInstallment> NormalizeFeeInstallments(
            IReadOnlyCollection<FeeInstallment>? requestedInstallments,
            IReadOnlyCollection<FeeInstallment>? existingInstallments,
            decimal totalPayable)
        {
            if (totalPayable <= 0m)
            {
                return new List<FeeInstallment>();
            }

            var sourceInstallments = (requestedInstallments != null && requestedInstallments.Count > 0
                    ? requestedInstallments
                    : existingInstallments)
                ?.OrderBy(installment => installment.DueDate)
                .ToList();

            if (sourceInstallments == null || sourceInstallments.Count == 0)
            {
                return new List<FeeInstallment>();
            }

            var normalized = new List<FeeInstallment>(sourceInstallments.Count);
            var remainingAmount = totalPayable;

            foreach (var installment in sourceInstallments)
            {
                if (remainingAmount <= 0m)
                {
                    break;
                }

                var amount = installment.Amount <= remainingAmount
                    ? installment.Amount
                    : remainingAmount;

                if (amount <= 0m)
                {
                    continue;
                }

                normalized.Add(new FeeInstallment
                {
                    Description = installment.Description,
                    DescriptionFr = installment.DescriptionFr,
                    DueDate = installment.DueDate,
                    Amount = amount,
                    PaymentStatus = installment.PaymentStatus
                });

                remainingAmount -= amount;
            }

            if (remainingAmount > 0m && normalized.Count > 0)
            {
                normalized[^1].Amount += remainingAmount;
            }
            else if (remainingAmount > 0m)
            {
                normalized.Add(new FeeInstallment
                {
                    Description = "Paiement complet de l'inscription/Complete Registration Payment",
                    DescriptionFr = "Paiement complet de l'inscription",
                    DueDate = DateTime.UtcNow.Date.AddDays(1),
                    Amount = remainingAmount
                });
            }

            return normalized;
        }

        private static DiscountEmailContent BuildDiscountConfirmationEmail(decimal amount, CourseResponseDetailed course)
        {
            var amountText = amount.ToString("0.00");
            var courseName = WebUtility.HtmlEncode(course.Name ?? string.Empty);
            var courseNameFr = WebUtility.HtmlEncode(course.NameFr ?? course.Name ?? string.Empty);

            return new DiscountEmailContent(
                DiscountConfirmationSubject,
                $"<p>A discount of <strong>{amountText}</strong> has been applied to the <strong>{courseName}</strong>.</p>" +
                "<p>Please review your parent portal to see your payment schedule and amount remaining (if any).</p>" +
                "<div>&nbsp;</div>" +
                $"<p>Un rabais de <strong>{amountText}</strong> a ete applique pour <strong>{courseNameFr}</strong>.</p>" +
                "<p>Veuillez svp verifier votre portail pour voir votre cedule de paiement et le montant restant, s'il y a lieu.</p>");
        }

        private sealed record DiscountEmailContent(string Subject, string Body);
    }
}
