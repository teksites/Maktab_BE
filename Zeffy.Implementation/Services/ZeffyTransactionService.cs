using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Zeffy;
using Zeffy.Repository;
using Zeffy.Services;

namespace Zeffy.Implementation.Services
{
    public class ZeffyTransactionService : IZeffyTransactionService
    {
        private IZeffyTransactionRepository _repository;
        private readonly IStudentCourseTransactionService _studentTransactionService;
        private readonly ICoursePaymentService _coursePaymentService;

        public ZeffyTransactionService(
            IZeffyTransactionRepository repository,
            IStudentCourseTransactionService studentTransactionService,
            ICoursePaymentService coursePaymentService)
        {
            _repository = repository;
            _studentTransactionService = studentTransactionService;
            _coursePaymentService = coursePaymentService;
        }

        public async Task SaveZeffyTransaction(ZeffyRequest zeffy)
        {
            if (zeffy == null || string.IsNullOrWhiteSpace(zeffy.Email))
                return;

            var paymentCode = GetCustomFieldValue(zeffy.CustomFields, "Payment Code");

            if (!string.IsNullOrWhiteSpace(paymentCode))
            {
                var transaction = await _studentTransactionService.GetTransactionByPaymentCode(paymentCode).ConfigureAwait(false);

                if (transaction == null)
                    return;

                var addPayment = new AddCoursePayment
                {
                    AmountPaid = Convert.ToDecimal(zeffy.Amount),
                    Comments = $"Payment from: {zeffy.Firstname} {zeffy.Lastname}, {zeffy.Email}",
                    FamilyId = transaction.FamilyId,
                    IsActive = true,
                    PaymentType = ResolvePaymentType(zeffy),
                    PaymentMode = PaymentMode.Zeffy,
                    StudentCourseTransactionId = transaction.StudentCourseTransactionId,
                };

                await _coursePaymentService.AddPayment(addPayment).ConfigureAwait(false);
                await _repository.Add(MapToAddRequest(zeffy, paymentCode, Guid.NewGuid(), transaction.FamilyId, transaction.StudentCourseTransactionId)).ConfigureAwait(false);
            }
        }

        public Task<List<ZeffyResponse>> GetAllZeffyDonations()
        {
            return _repository.GetAllZeffyDonations();
        }

        public Task<IEnumerable<ZeffyResponse>> GetByStudentCourseTransactionId(Guid studentCourseTransactionId)
        {
            return _repository.GetByStudentCourseTransactionId(studentCourseTransactionId);
        }

        public Task<IEnumerable<ZeffyResponse>> GetByFamilyId(Guid familyId)
        {
            return _repository.GetByFamilyId(familyId);
        }

        public Task<IEnumerable<ZeffyResponse>> GetByPaymentCode(string paymentCode)
        {
            return _repository.GetByPaymentCode(paymentCode);
        }

        public Task<IEnumerable<ZeffyResponse>> GetByFamilyAndPaymentCode(Guid familyId, string paymentCode)
        {
            return _repository.GetByFamilyAndPaymentCode(familyId, paymentCode);
        }

        public Task<ZeffyResponse?> GetByZeffyId(Guid zeffyId)
        {
            return _repository.GetByZeffyId(zeffyId);
        }

        public Task<bool> Delete(Guid zeffyId, bool hardDelete = false)
        {
            return _repository.Delete(zeffyId, hardDelete);
        }

        public Task<bool> Update(ZeffyResponse zeffy)
        {
            return _repository.Update(zeffy);
        }

        private AddZeffyRequest MapToAddRequest(
            ZeffyRequest request,
            string paymentCode,
            Guid zeffyId,
            Guid familyId,
            Guid studentCourseTransactionId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return new AddZeffyRequest
            {
                ZeffyId = zeffyId,
                FamilyId = familyId,
                StudentCourseTransactionId = studentCourseTransactionId,
                PaymentCode = paymentCode,
                Id = request.Id,
                Address = request.Address,
                Amount = request.Amount,
                Birthdate = request.Birthdate,
                CampaignId = request.CampaignId,
                City = request.City,
                Company = request.Company,
                Country = request.Country,
                CreationDate = request.CreationDate,
                CustomFields = request.CustomFields,
                DonationFormId = request.DonationFormId,
                DonationId = request.DonationId,
                Email = request.Email,
                Firstname = request.Firstname,
                Form_Name = request.Form_Name,
                InHonourName = request.InHonourName,
                Language = request.Language,
                Lastname = request.Lastname,
                OrganizationId = request.OrganizationId,
                PaymentMethod = request.PaymentMethod,
                Pdf = request.Pdf,
                Postal = request.Postal,
                Province = request.Province,
                ReceiptNumber = request.ReceiptNumber,
                Recurrent = request.Recurrent,
                TeamId = request.TeamId,
                IsActive = true
            };
        }

        private static string GetCustomFieldValue(List<CustomField> fields, string question)
        {
            if (fields == null || string.IsNullOrWhiteSpace(question))
                return null;

            var item = fields.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Question) &&
                string.Equals(x.Question.Trim(), question.Trim(), StringComparison.OrdinalIgnoreCase));

            return item?.Answer;
        }

        private static PaymentType ResolvePaymentType(ZeffyRequest zeffy)
        {
            if (zeffy == null)
            {
                return PaymentType.Credit;
            }

            if (decimal.TryParse(zeffy.Amount, out var amount) && amount < 0)
            {
                return PaymentType.Refund;
            }

            return PaymentType.Credit;
        }
    }
}
