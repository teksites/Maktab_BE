using Courses.Repository;
using Courses.Services;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Policies;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Newtonsoft.Json;
using Users.Services;

namespace Courses.Implementation.Services
{
    public class StudentCourseEnrollmentService : IStudentCourseEnrollmentService
    {
        private readonly IStudentCourseEnrollmentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly ICourseService _courseService;
        private readonly IInstitutePolicyService _policyService;
        private readonly ICourseEnrollmentGroupService _courseEnrollmentGroupService;
        private readonly ISendEmailService _sendEmailService;
        private readonly IUserService _userService;


        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository, IStudentCourseTransactionService studentCourseEnrollmentService, ICourseService courseService, IInstitutePolicyService policyService, 
            ICourseEnrollmentGroupService courseEnrollmentGroupService, ISendEmailService sendEmailService, IUserService userService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseEnrollmentService;
            _courseService = courseService;
            _policyService = policyService;
            _courseEnrollmentGroupService = courseEnrollmentGroupService;
            _sendEmailService = sendEmailService;
            _userService = userService;

        }

        private sealed class EnrollmentUpdateExecutionResult
        {
            public bool Success { get; init; }
            public Guid FamilyId { get; init; }
            public Guid CourseId { get; init; }
            public bool RequiresRecalculation { get; init; }
            public EnrollmentEmailNotification? EmailNotification { get; init; }
        }

        private sealed class EnrollmentEmailNotification
        {
            public Guid FamilyId { get; init; }
            public EnrollmentStatus Status { get; init; }
            public string ChildName { get; init; } = string.Empty;
            public string CourseName { get; init; } = string.Empty;
            public string CourseNameFr { get; init; } = string.Empty;
            public string CourseGroupDetails { get; init; } = string.Empty;
            public string CourseGroupDetailsFr { get; init; } = string.Empty;
        }

        private const string EnrollmentConfirmationSubject = "Confirmation de l'inscription / Confirmation of enrollment";

        private static IReadOnlyList<StudentCourseEnrollmentResponse> GetActiveEnrollments(
            StudentCourseTransactionResponse? transaction)
        {
            if (transaction?.Enrollments == null)
            {
                return Array.Empty<StudentCourseEnrollmentResponse>();
            }

            return transaction.Enrollments
                .Where(enrollment => enrollment.IsActive)
                .ToList();
        }

        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment, bool ifAddedByAdmin = false)
        {

            var familyTransactions = await _studentCourseTransactionService.GetCourseTransactionsByFamily(enrollment.CourseId, enrollment.FamilyId).ConfigureAwait(false);

            var familyCourseTransaction = familyTransactions.Where(x => x.Enrollments.All(y => y.CourseId == enrollment.CourseId));
            var course = await _courseService.GetCourse(enrollment.CourseId).ConfigureAwait(false);

            await EnsureFamilyHasRequiredParentsForEnrollmentAsync(enrollment.FamilyId, course).ConfigureAwait(false);

            if (!course.IsRegistrationOpened && !ifAddedByAdmin)
            {
                throw new Exception("The registration is closed. Contact Admin please");
            }

            var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false);
            if (enrollmentGroupState == null)
            {
                throw new Exception("Course enrollment group not found");
            }

            if (!enrollmentGroupState.IfRegistrationOpen)
            {
                throw new Exception("The registration is closed. Contact Admin please");
            }

            var occupiedSeatCount = GetOccupiedSeatCount(enrollmentGroupState.EnrollmentStatusCount);

            var canRegister = !course.IsManualEnrollment &&
                occupiedSeatCount < enrollmentGroupState.MaxStudents;
            enrollment.EnrollmentStatus = canRegister ? EnrollmentStatus.Enrolled : EnrollmentStatus.Awaiting;

            var selectedCourseEnrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

            if (familyCourseTransaction.Any())// We have the transaction for the same course for same child or other child at the moment
            {
                //var childEnrollments = await GetStudentCourseEnrollment(enrollment.ChildId, enrollment.CourseId).ConfigureAwait(false);
                var transaction = familyCourseTransaction.First();
                var activeTransactionEnrollments = GetActiveEnrollments(transaction);
                var childEnrollment = activeTransactionEnrollments.Where(childEnrollment => childEnrollment.ChildId == enrollment.ChildId);
                var enrollmentForExistingChild = childEnrollment.Any(x => x.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                if (enrollmentForExistingChild)
                {
                    throw new Exception("The child is already registered to the selected course group");
                }

                //var newDayCareFee = (enrollment.WillUseDayCare ? selectedCourseEnrollmentGroup.DayCareFee : 0);
                //decimal newCourseFee = 0;

                //if there is any existing enrollment for the child in the same course for any course group. if it is the same course group then we will not allow registration
                if (activeTransactionEnrollments.Any(existingEnrollment => existingEnrollment.ChildId == enrollment.ChildId)) // child is already registered for the same course in different course group
                {
                    enrollment.EnrollmentIndex = activeTransactionEnrollments
                        .Where(existingEnrollment => existingEnrollment.ChildId == enrollment.ChildId)
                        .Max(existingEnrollment => existingEnrollment.EnrollmentIndex);
                    //newCourseFee = selectedCourseEnrollmentGroup.Fee;
                    //Now update the transaction to include the new enrollment

                }
                else // child is not registered for any course group of the course
                {
                    //here we have to add new enrollment and update the existing transaction to include the new enrollment for new child
                    //// We will calculate the discount and apply
                    //var policies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                    //var discountPolicy = policies.Where(p => p.IsActive && p.PolicyType == MaktabDataContracts.Enums.PolicyType.SiblingDiscount).First().Details;
                    //SiblingDiscountPolicy policy = JsonConvert.DeserializeObject<SiblingDiscountPolicy>(discountPolicy);

                    enrollment.EnrollmentIndex = familyTransactions
                        .SelectMany(GetActiveEnrollments)
                        .Select(e => e.EnrollmentIndex)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                }

                // Add enrollment first
                var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);

                var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(transaction.StudentCourseTransactionId,
                    addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);

                if (!await RecalculateCourseFee(course.CourseId, enrollment.FamilyId).ConfigureAwait(false))//revert if transaction failed
                {
                    await _studentCourseTransactionService.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                    await _repository.DeleteEnrollment(addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
                }
                else if (course.IsManualEnrollment && enrollment.ShouldTriggerEmail)
                {
                    await SendManualEnrollmentAwaitingEmailAsync(
                        addedEnrollment,
                        course,
                        selectedCourseEnrollmentGroup).ConfigureAwait(false);
                }
                else if (enrollment.ShouldTriggerEmail)
                {
                    await SendEnrollmentCreatedEmailAsync(
                        addedEnrollment,
                        course,
                        selectedCourseEnrollmentGroup).ConfigureAwait(false);
                }

                return addedEnrollment;
            }
            else // there is no previous transaction for the course for any child of the family. we will calculate new transaction and apply regisrtration fee
            {
                enrollment.EnrollmentIndex = 1;
                var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);
                StudentCourseTransactionResponse transaction = null;

                // Add student transaction
                var addStudentCourseTransaction = new AddStudentCourseTransaction();
                addStudentCourseTransaction.StudentCourseTransactionId = Guid.NewGuid();
                addStudentCourseTransaction.FamilyId = enrollment.FamilyId;
                addStudentCourseTransaction.PaymentCode = $"GENERATECODE";
                addStudentCourseTransaction.TransactionStatus = TransactionStatus.AwaitingPayment;
                addStudentCourseTransaction.RegistrationStatus = RegistrationStatus.Pending;               
                addStudentCourseTransaction.IsActive = true;
                // Bill immediately when the child has a confirmed seat.
                if (IsFeeBearingEnrollmentStatus(enrollment.EnrollmentStatus))
                {
                    addStudentCourseTransaction.PayableFee = (decimal)selectedCourseEnrollmentGroup.Fee;
                    addStudentCourseTransaction.DayCareFee = enrollment.WillUseDayCare ? selectedCourseEnrollmentGroup.DayCareFee : 0;
                }
                else
                {
                    addStudentCourseTransaction.PayableFee = 0;
                    addStudentCourseTransaction.DayCareFee = 0;
                }
                addStudentCourseTransaction.FeeAmountDiscount = 0;
                addStudentCourseTransaction.DayCareDiscount = 0;
                addStudentCourseTransaction.Surcharge = 0;
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) +
                    Convert.ToDecimal(addStudentCourseTransaction.Surcharge);
                var activePolicies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                var activeFeePaymentPolicy = activePolicies.FirstOrDefault(
                    p => p.IsActive &&
                         p.PolicyType == PolicyType.CourseFeePayment &&
                         p.CourseId == course.CourseId);
                var (feePolicy, feePaymentPolicyFound) = ParseValidatedFeePaymentPolicy(activeFeePaymentPolicy?.Details);
                addStudentCourseTransaction.FeeInstallments = BuildFeeInstallments(
                    addStudentCourseTransaction.TotalPayable - course.RegistrationFee,// exclude registration fee from installments
                    course.RegistrationFee,
                    feePaymentPolicyFound,
                    feePolicy,
                    new[] { 1 },
                    course.CourseEnrollmentGroups.Count);
                addStudentCourseTransaction.Comments = $"New Enrollment on {DateTime.UtcNow.ToString()}";
                addStudentCourseTransaction.IsCompletelyPaid = false;

                try
                {
                    var addedTransaction = await _studentCourseTransactionService.AddTransaction(addStudentCourseTransaction).ConfigureAwait(false);
                    var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(addedTransaction.StudentCourseTransactionId,
                              addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);

                    if (course.IsManualEnrollment && enrollment.ShouldTriggerEmail)
                    {
                        await SendManualEnrollmentAwaitingEmailAsync(
                            addedEnrollment,
                            course,
                            selectedCourseEnrollmentGroup).ConfigureAwait(false);
                    }
                    else if (enrollment.ShouldTriggerEmail)
                    {
                        await SendEnrollmentCreatedEmailAsync(
                            addedEnrollment,
                            course,
                            selectedCourseEnrollmentGroup).ConfigureAwait(false);
                    }
                }
                catch(Exception e)
                {
                        await _repository.DeleteEnrollment(addedEnrollment.StudentCourseEnrollmentId, true).ConfigureAwait(false);
                }
                return addedEnrollment;
            }
        }

        public async Task<bool> RecalculateCourseFee(Guid courseId, Guid familyId)
        {
            var familyTransaction = (await _studentCourseTransactionService.GetCourseTransactionsByFamily(courseId, familyId).ConfigureAwait(false)).FirstOrDefault();
            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false);

            if (familyTransaction == null || course == null)
            {
                throw new Exception("Transaction or Course not found");
            }
            
            var feeAmountDiscount = familyTransaction.FeeAmountDiscount;
            var dayCareDiscount = familyTransaction.DayCareDiscount;
            var paymentCode = familyTransaction.PaymentCode;
            var transactionStatus = familyTransaction.TransactionStatus;
            var registrationtionStatus = familyTransaction.RegistrationStatus;
            var isActive = familyTransaction.IsActive;
            var totalAmountPaid = familyTransaction.TotalAmountPaid;

            decimal dayCareFee = 0m;
            decimal courseFee = 0m;

            var activeEnrollments = GetActiveEnrollments(familyTransaction);
            var effectiveEnrollments = activeEnrollments
            .GroupBy(e => new { e.ChildId, e.CourseEnrollmentGroupId })
            .Select(g => g
                .OrderByDescending(e => e.UpdatedOn)
                .ThenByDescending(e => e.CreatedAt)
                .First())
            .OrderBy(e => e.EnrollmentIndex)
            .ThenBy(e => e.CreatedAt)
            .ToList();

            var feeApplicableEnrollments = effectiveEnrollments
            .Where(e => IsFeeBearingEnrollmentStatus(e.EnrollmentStatus))
            .ToList();

            var groupedByChild = feeApplicableEnrollments
            .GroupBy(e => e.ChildId)
            .Select(g =>
            {
                var orderedEnrollments = g
                    .OrderBy(e => e.CreatedAt)
                    .ThenBy(e => e.UpdatedOn)
                    .ToList();

                return new
                {
                    ChildId = g.Key,
                    EnrollmentIndex = orderedEnrollments.Min(e => e.EnrollmentIndex),
                    FirstEnrollmentCreatedAt = orderedEnrollments.Min(e => e.CreatedAt),
                    Enrollments = orderedEnrollments
                };
            })
            .OrderByDescending(g => g.Enrollments.Count(e => IsFeeBearingEnrollmentStatus(e.EnrollmentStatus)))
            .ThenBy(g => g.EnrollmentIndex)
            .ThenBy(g => g.FirstEnrollmentCreatedAt)
            .ThenBy(g => g.ChildId)
            .ToList();
            
            var enrollmentGroupCountsByChild = groupedByChild.Select(g => g.Enrollments.Count).ToList();

            int i = 1;

            SiblingDiscountPolicy policy = new SiblingDiscountPolicy();
            List<FeePaymentPolicy> feePolicy = new List<FeePaymentPolicy>();
            var policyFound = false;
            var feePaymentPolicyFound = false;

            //if (groupedByChild.Count > 1) //We need to get two policies now. therefore we will check if we have more than
            ////1 child and then get the policies. if we have only one child then we dont need to get the policies as there will be no discount
            {
                var policies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                if (!policies.Any())
                {
                    policyFound = false;
                    feePaymentPolicyFound = false;
                }
                else
                {
                    var activeSiblingPolicy = policies.FirstOrDefault(p => p.IsActive && p.PolicyType == PolicyType.SiblingDiscount && p.CourseId == course.CourseId && p.IsActive);
                    var discountPolicy = activeSiblingPolicy?.Details;
                    if (!string.IsNullOrEmpty(discountPolicy))
                    {
                        try
                        {
                            policy = JsonConvert.DeserializeObject<SiblingDiscountPolicy>(discountPolicy);
                            policyFound = true;
                        }
                        catch
                        {
                            policyFound = false;
                        }
                    }

                    var activeFeePaymentPolicy = policies.FirstOrDefault(p => p.IsActive && p.PolicyType == PolicyType.CourseFeePayment && p.CourseId == course.CourseId && p.IsActive);
                    (feePolicy, feePaymentPolicyFound) = ParseValidatedFeePaymentPolicy(activeFeePaymentPolicy?.Details);
                }
            }

           foreach (var childGroup in groupedByChild)
            {
                var childEnrollments = childGroup.Enrollments;
                decimal childFee = 0m;
                decimal childDayCareFee = 0m;
                decimal applicableDiscountPercentage = 1m;
                decimal applicableFee = 0m;// Convert.ToDecimal(enrollmentGroup.Fee);
               
                if (policyFound)
                {
                    if (i == 1 && policy.IsFeeAbsolute)
                    {
                        applicableFee = policy.FirstChildFee;
                    }
                    else if (i == 1 && !policy.IsFeeAbsolute)
                    {
                        applicableDiscountPercentage = policy.FirstChildFee / 100m;
                    }
                    else if (i == 2 && policy.IsFeeAbsolute)
                    {
                        applicableFee = policy.SecondChildFee;
                    }
                    else if (i == 2 && !policy.IsFeeAbsolute)
                    {
                        applicableDiscountPercentage = policy.SecondChildFee/100m;
                    }
                    else if (i == 3 && policy.IsFeeAbsolute)
                    {
                        applicableFee = policy.ThirdChildFee;
                    }
                    else if (i == 3 && !policy.IsFeeAbsolute)
                    {
                        applicableDiscountPercentage = policy.ThirdChildFee / 100m;
                    }
                    else if (i > 3 && policy.IsFeeAbsolute)
                    {
                        applicableFee = policy.FourthAndOnwardChildFee;
                    }
                    else if (i > 3 && !policy.IsFeeAbsolute)
                    {
                        applicableDiscountPercentage = policy.FourthAndOnwardChildFee / 100m;
                    }
                }
                foreach (var enrollment in childEnrollments)
                {
                    var enrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                    if (enrollmentGroup != null && IsFeeBearingEnrollmentStatus(enrollment.EnrollmentStatus))// Only allowed billable ones
                    {
                        if (!policyFound)
                        {
                            applicableFee = Convert.ToDecimal(enrollmentGroup.Fee);
                            childFee += applicableFee;
                        }
                        else if (policyFound && policy.IsFeeAbsolute) 
                        {
                            childFee += applicableFee;
                        }
                        else
                        {
                            childFee += (Convert.ToDecimal(enrollmentGroup.Fee) * applicableDiscountPercentage);
                        }
                        
                        childDayCareFee += enrollment.WillUseDayCare ? Convert.ToDecimal(enrollmentGroup.DayCareFee) : 0m;
                    }
                }

                courseFee += childFee;
                dayCareFee += childDayCareFee;
                i++;
            }

            var addStudentCourseTransaction = new AddStudentCourseTransaction();
            addStudentCourseTransaction.StudentCourseTransactionId = familyTransaction.StudentCourseTransactionId;
            addStudentCourseTransaction.FamilyId = familyTransaction.FamilyId;
            addStudentCourseTransaction.PaymentCode = familyTransaction.PaymentCode;
            addStudentCourseTransaction.TransactionStatus = familyTransaction.TransactionStatus;
            // Check if any enrollment is Awaiting, set RegistrationStatus accordingly
            var hasAwaiting = effectiveEnrollments.Any(e => e.EnrollmentStatus == EnrollmentStatus.Awaiting);
            addStudentCourseTransaction.RegistrationStatus = hasAwaiting ? RegistrationStatus.Pending : RegistrationStatus.Completed;
            addStudentCourseTransaction.IsActive = familyTransaction.IsActive;
            addStudentCourseTransaction.FeeAmountDiscount = familyTransaction.FeeAmountDiscount;
            addStudentCourseTransaction.DayCareDiscount = familyTransaction.DayCareDiscount;
            addStudentCourseTransaction.Surcharge = familyTransaction.Surcharge;
            addStudentCourseTransaction.DayCareFee = dayCareFee;
            addStudentCourseTransaction.PayableFee = courseFee;
            addStudentCourseTransaction.TotalAmountPaid = familyTransaction.TotalAmountPaid;
            var registrationFeeToApply = activeEnrollments.Count > 0 ? course.RegistrationFee : 0m;
            var recalculatedTotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + registrationFeeToApply) -
                (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) +
                Convert.ToDecimal(addStudentCourseTransaction.Surcharge);
            addStudentCourseTransaction.TotalPayable = recalculatedTotalPayable < 0m ? 0m : recalculatedTotalPayable;
            addStudentCourseTransaction.FeeInstallments = BuildFeeInstallments(
                Math.Max(addStudentCourseTransaction.TotalPayable - registrationFeeToApply, 0m),
                registrationFeeToApply,
                feePaymentPolicyFound,
                feePolicy,
                enrollmentGroupCountsByChild,
                course.CourseEnrollmentGroups.Count);
            ApplyInstallmentPaymentStatuses(
                addStudentCourseTransaction.FeeInstallments,
                addStudentCourseTransaction.TotalAmountPaid);

            addStudentCourseTransaction.Comments = familyTransaction.Comments +$"\n Updated the transaction on {DateTime.UtcNow.ToString()}";
            addStudentCourseTransaction.IsCompletelyPaid = recalculatedTotalPayable <= addStudentCourseTransaction.TotalAmountPaid;

            var promotedNotifications = new List<EnrollmentEmailNotification>();
            if (addStudentCourseTransaction.IsCompletelyPaid)
            {
                foreach (var enrollment in effectiveEnrollments.Where(e => e.EnrollmentStatus == EnrollmentStatus.Enrolled))
                {
                    var updateSucceeded = await _repository
                        .UpdateEnrollmentStatus(enrollment.StudentCourseEnrollmentId, EnrollmentStatus.Registered)
                        .ConfigureAwait(false);

                    if (!updateSucceeded)
                    {
                        continue;
                    }

                    var enrollmentGroup = course.CourseEnrollmentGroups
                        .FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                    var notification = CreateEnrollmentEmailNotification(
                        statusChanged: true,
                        enrollmentStatus: EnrollmentStatus.Registered,
                        enrollmentDetails: enrollment,
                        courseDetails: course,
                        enrollmentGroup: enrollmentGroup);

                    if (notification != null)
                    {
                        promotedNotifications.Add(notification);
                    }
                }
            }

            var transactionUpdated = await _studentCourseTransactionService
                .UpdateTransaction(familyTransaction.StudentCourseTransactionId, addStudentCourseTransaction)
                .ConfigureAwait(false);

            if (transactionUpdated && promotedNotifications.Count > 0)
            {
                await SendEnrollmentStatusEmailsAsync(promotedNotifications).ConfigureAwait(false);
            }

            return transactionUpdated;
        }

        private static (List<FeePaymentPolicy> FeePolicy, bool FeePaymentPolicyFound) ParseValidatedFeePaymentPolicy(string? feePaymentPolicyDetails)
        {
            if (string.IsNullOrWhiteSpace(feePaymentPolicyDetails))
            {
                return (new List<FeePaymentPolicy>(), false);
            }

            try
            {
                var feePolicy = JsonConvert.DeserializeObject<List<FeePaymentPolicy>>(feePaymentPolicyDetails) ?? new List<FeePaymentPolicy>();
                return (feePolicy, feePolicy.Count > 0);
            }
            catch
            {
                return (new List<FeePaymentPolicy>(), false);
            }
        }

        private static List<FeeInstallment> BuildFeeInstallments(
            decimal totalPayable,
            decimal registrationFee,
            bool feePaymentPolicyFound,
            IReadOnlyList<FeePaymentPolicy> feePolicy,
            IReadOnlyList<int> enrollmentGroupCountsByChild,
            int totalEnrollmentGroups)
        {
            var sortedPolicy = feePolicy
                .OrderBy(policy => policy.PaymentDate)
                .ToList();

            if (totalPayable <= 0m && registrationFee <= 0m)
            {
                return new List<FeeInstallment>();
            }

            if (!feePaymentPolicyFound || sortedPolicy.Count == 0 || totalEnrollmentGroups <= 1)
            {
                return new List<FeeInstallment> { CreateDefaultInstallment(totalPayable + registrationFee) };
            }

            if (totalPayable <= 0m) // only registration fee
            {
                return new List<FeeInstallment> { CreatePolicyInstallment(sortedPolicy[0], registrationFee) };
            }

            if (!sortedPolicy[0].ShouldApplyEnrollmentToCover)
            {
                return BuildPercentageDrivenInstallments(
                    totalPayable,
                    registrationFee,
                    sortedPolicy,
                    enrollmentGroupCountsByChild.Count);
            }

            return BuildEnrollmentCountDrivenInstallments(
                totalPayable,
                registrationFee,
                sortedPolicy,
                enrollmentGroupCountsByChild,
                totalEnrollmentGroups);
        }

        private static List<FeeInstallment> BuildPercentageDrivenInstallments(
            decimal totalPayable,
            decimal registrationFee,
            IReadOnlyList<FeePaymentPolicy> sortedPolicy,
            int enrolledChildrenCount)
        {
            var firstPolicy = sortedPolicy[0];
            var totalPercentageToCover = sortedPolicy.Sum(policy => policy.PercentageToCover);

            if (totalPercentageToCover != 100
                || firstPolicy.MinimalChildrenToApply == 0
                || enrolledChildrenCount < firstPolicy.MinimalChildrenToApply)
            {
                return new List<FeeInstallment>
                {
                    CreatePolicyInstallment(firstPolicy, totalPayable + registrationFee)
                };
            }

            var installmentAmounts = new List<decimal>(sortedPolicy.Count);
            var remainingAmount = totalPayable;

            for (var index = 0; index < sortedPolicy.Count; index++)
            {
                var amount = remainingAmount;
                if (index < sortedPolicy.Count - 1)
                {
                    var percentageAmount = totalPayable * sortedPolicy[index].PercentageToCover / 100m;
                    amount = RoundToNearestFive(percentageAmount);
                    if (amount < 0m)
                    {
                        amount = 0m;
                    }

                    if (amount > remainingAmount)
                    {
                        amount = remainingAmount;
                    }
                }

                installmentAmounts.Add(amount);
                remainingAmount -= amount;
            }

            if (installmentAmounts.Count == 0)
            {
                return new List<FeeInstallment>
                {
                    CreatePolicyInstallment(firstPolicy, totalPayable + registrationFee)
                };
            }

            installmentAmounts[0] += registrationFee;
            return BuildInstallmentsFromPolicy(sortedPolicy, installmentAmounts);
        }

        private static List<FeeInstallment> BuildEnrollmentCountDrivenInstallments(
            decimal totalPayable,
            decimal registrationFee,
            IReadOnlyList<FeePaymentPolicy> sortedPolicy,
            IReadOnlyList<int> enrollmentGroupCountsByChild,
            int totalEnrollmentGroups)
        {
            var firstPolicy = sortedPolicy[0];
            if (enrollmentGroupCountsByChild.Count == 0 || enrollmentGroupCountsByChild[0] <= 0)
            {
                return new List<FeeInstallment>
                {
                    CreatePolicyInstallment(firstPolicy, totalPayable + registrationFee)
                };
            }

            var maxEnrollmentCount = enrollmentGroupCountsByChild[0];
            var unitPrice = totalPayable / maxEnrollmentCount;
            var installmentAmounts = new List<decimal>(sortedPolicy.Count);
            var applicablePolicy = new List<FeePaymentPolicy>(sortedPolicy.Count);
            var remainingAmount = totalPayable;
            var remainingEnrollments = maxEnrollmentCount;

            for (var index = 0; index < sortedPolicy.Count && remainingEnrollments > 0 && remainingAmount > 0m; index++)
            {
                var isLastPolicyItem = index == sortedPolicy.Count - 1;
                var enrollmentsToCover = isLastPolicyItem
                    ? remainingEnrollments
                    : Math.Max(sortedPolicy[index].EnrollmentsToCover, 0);

                if (enrollmentsToCover > remainingEnrollments)
                {
                    enrollmentsToCover = remainingEnrollments;
                }

                if (enrollmentsToCover <= 0)
                {
                    continue;
                }

                var amount = remainingAmount;
                if (!isLastPolicyItem)
                {
                    amount = RoundToNearestFive(enrollmentsToCover * unitPrice);
                    if (amount < 0m)
                    {
                        amount = 0m;
                    }

                    if (amount > remainingAmount)
                    {
                        amount = remainingAmount;
                    }
                }

                applicablePolicy.Add(sortedPolicy[index]);
                installmentAmounts.Add(amount);
                remainingAmount -= amount;
                remainingEnrollments -= enrollmentsToCover;
            }

            if (installmentAmounts.Count == 0 || applicablePolicy.Count == 0)
            {
                return new List<FeeInstallment>
                {
                    CreatePolicyInstallment(firstPolicy, totalPayable + registrationFee)
                };
            }

            if (remainingAmount > 0m)
            {
                installmentAmounts[^1] += remainingAmount;
            }

            installmentAmounts[0] += registrationFee;
            return BuildInstallmentsFromPolicy(applicablePolicy, installmentAmounts);
        }

        private static void ApplyInstallmentPaymentStatuses(
            IReadOnlyList<FeeInstallment> feeInstallments,
            decimal totalAmountPaid)
        {
            var remainingPaidAmount = totalAmountPaid;

            foreach (var installment in feeInstallments.OrderBy(installment => installment.DueDate))
            {
                if (remainingPaidAmount <= 0m)
                {
                    installment.PaymentStatus = PaymentStatus.Unpaid;
                    continue;
                }

                if (remainingPaidAmount >= installment.Amount)
                {
                    installment.PaymentStatus = PaymentStatus.Paid;
                    remainingPaidAmount -= installment.Amount;
                    continue;
                }

                installment.PaymentStatus = PaymentStatus.PartiallyPaid;
                remainingPaidAmount = 0m;
            }
        }

        private static List<FeeInstallment> BuildInstallmentsFromPolicy(
            IReadOnlyList<FeePaymentPolicy> sortedPolicy,
            IReadOnlyList<decimal> installmentAmounts)
        {
            var installments = new List<FeeInstallment>(Math.Min(sortedPolicy.Count, installmentAmounts.Count));

            for (var index = 0; index < sortedPolicy.Count && index < installmentAmounts.Count; index++)
            {
                if (installmentAmounts[index] <= 0m)
                {
                    continue;
                }

                installments.Add(CreatePolicyInstallment(sortedPolicy[index], installmentAmounts[index]));
            }

            return installments;
        }

        private static FeeInstallment CreatePolicyInstallment(FeePaymentPolicy policy, decimal amount)
            => new FeeInstallment
            {
                Description = policy.Name,
                DescriptionFr = policy.NameFr,
                DueDate = policy.PaymentDate,
                Amount = amount
            };

        private static FeeInstallment CreateDefaultInstallment(decimal amount)
            => new FeeInstallment
            {
                Description = "Paiement complet de l'inscription/Complete Registration Payment",
                DescriptionFr = "Paiement complet de l'inscription",
                DueDate = DateTime.UtcNow.Date.AddDays(1),
                Amount = amount
            };

        private static decimal RoundToNearestFive(decimal amount)
            => Math.Round(amount / 5m, 0, MidpointRounding.AwayFromZero) * 5m;

        private static int GetOccupiedSeatCount(IReadOnlyDictionary<EnrollmentStatus, int> enrollmentStatusCount)
        {
            var enrolledCount = enrollmentStatusCount.TryGetValue(EnrollmentStatus.Enrolled, out var enrolled)
                ? enrolled
                : 0;
            var registeredCount = enrollmentStatusCount.TryGetValue(EnrollmentStatus.Registered, out var registered)
                ? registered
                : 0;

            return enrolledCount + registeredCount;
        }

        private async Task EnsureFamilyHasRequiredParentsForEnrollmentAsync(Guid familyId, CourseResponseDetailed course)
        {
            if (course == null || course.IsCourseAnEvent)
            {
                return;
            }

            var familyUsers = await _userService.GetAllFamilyUsersInformation(familyId, true).ConfigureAwait(false)
                ?? Enumerable.Empty<MaktabDataContracts.Responses.Users.UserInformationResponse>();

            var hasMother = familyUsers.Any(user => user.Relationship == Relationship.Mother);
            var hasFather = familyUsers.Any(user => user.Relationship == Relationship.Father);

            if (hasMother && hasFather)
            {
                return;
            }

            throw new InvalidOperationException("Both mother and father must be registered before enrolling the child in this course.");
        }

        private static bool IsFeeBearingEnrollmentStatus(EnrollmentStatus status)
            => status == EnrollmentStatus.Enrolled
            || status == EnrollmentStatus.Registered
            || status == EnrollmentStatus.Awaiting;

        private static bool HoldsSeat(EnrollmentStatus status)
            => status == EnrollmentStatus.Enrolled || status == EnrollmentStatus.Registered;

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
                addStudentCourseTransaction.RegistrationStatus = RegistrationStatus.Completed;
                addStudentCourseTransaction.IsActive = true;
                addStudentCourseTransaction.PayableFee = enrollmentGroup.Fee; // get from course
                addStudentCourseTransaction.FeeAmountDiscount = 0;
                addStudentCourseTransaction.DayCareDiscount = 0;
                addStudentCourseTransaction.Surcharge = 0;
                addStudentCourseTransaction.DayCareFee = enrollmentGroup.DayCareFee;//add day care fee in course group
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) - 
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) +
                    Convert.ToDecimal(addStudentCourseTransaction.Surcharge);
                addStudentCourseTransaction.Comments = $"New Enrollment on {DateTime.UtcNow.ToString()}";
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
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) +
                    Convert.ToDecimal(addStudentCourseTransaction.Surcharge);
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

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsByGroup(Guid courseEnrollmentGroupId)
            => _repository.GetAllEnrollmentsByGroup(courseEnrollmentGroupId);

        public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment, bool ifUpdatedByAdmin = false)
        {
            var result = await UpdateEnrollmentInternal(
                enrollmentId,
                enrollment,
                ifUpdatedByAdmin,
                shouldRecalculate: true,
                shouldQueueEmailNotification: enrollment.ShouldTriggerEmail,
                shouldSendEmailImmediately: enrollment.ShouldTriggerEmail).ConfigureAwait(false);

            return result.Success;
        }

        public async Task<bool> UpdateEnrollmentsBatch(UpdateStudentCourseEnrollmentsBatchRequest request, bool ifUpdatedByAdmin = false)
        {
            if (request?.Enrollments == null || !request.Enrollments.Any())
            {
                return false;
            }

            var updateResults = new List<EnrollmentUpdateExecutionResult>(request.Enrollments.Count);

            foreach (var item in request.Enrollments)
            {
                if (item == null || item.EnrollmentId == Guid.Empty || item.Enrollment == null)
                {
                    return false;
                }

                var updateResult = await UpdateEnrollmentInternal(
                    item.EnrollmentId,
                    item.Enrollment,
                    ifUpdatedByAdmin,
                    shouldRecalculate: false,
                    shouldQueueEmailNotification: item.Enrollment.ShouldTriggerEmail,
                    shouldSendEmailImmediately: false).ConfigureAwait(false);

                if (!updateResult.Success)
                {
                    return false;
                }

                updateResults.Add(updateResult);
            }

            var recalculations = updateResults
                .Where(result => result.RequiresRecalculation)
                .Select(result => new { result.CourseId, result.FamilyId })
                .Distinct()
                .ToList();

            foreach (var recalculation in recalculations)
            {
                var recalculated = await RecalculateCourseFee(recalculation.CourseId, recalculation.FamilyId).ConfigureAwait(false);
                if (!recalculated)
                {
                    return false;
                }
            }

            var notifications = updateResults
                .Where(result => result.EmailNotification != null)
                .Select(result => result.EmailNotification!)
                .ToList();

            await SendEnrollmentStatusEmailsAsync(notifications).ConfigureAwait(false);
            return true;
        }

        private async Task<EnrollmentUpdateExecutionResult> UpdateEnrollmentInternal(
            Guid enrollmentId,
            AddStudentCourseEnrollment enrollment,
            bool ifUpdatedByAdmin,
            bool shouldRecalculate,
            bool shouldQueueEmailNotification,
            bool shouldSendEmailImmediately)
        {
            var enrollmentDetails = await _repository.GetEnrollment(enrollmentId).ConfigureAwait(false);

            if (enrollmentDetails == null)
            {
                return new EnrollmentUpdateExecutionResult { Success = false };
            }

            var enrollmentStatus = enrollmentDetails.EnrollmentStatus;
            var courseDetails = await _courseService.GetCourse(enrollmentDetails.CourseId).ConfigureAwait(false);

            if (!courseDetails.IsRegistrationOpened && !ifUpdatedByAdmin)
            {
                return new EnrollmentUpdateExecutionResult { Success = false };
            }

            if (enrollment.EnrollmentStatus == EnrollmentStatus.Unknown)
            {
                enrollment.EnrollmentStatus = enrollmentStatus;
            }

            var enrollmentGroup = courseDetails.CourseEnrollmentGroups.FirstOrDefault(x => x.CourseEnrollmentGroupId == enrollmentDetails.CourseEnrollmentGroupId);
            var isMovingIntoSeatHoldingStatus =
                !HoldsSeat(enrollmentStatus) &&
                HoldsSeat(enrollment.EnrollmentStatus) &&
                enrollment.EnrollmentStatus != enrollmentStatus;

            if (isMovingIntoSeatHoldingStatus && !ifUpdatedByAdmin)
            {
                var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollmentDetails.CourseEnrollmentGroupId).ConfigureAwait(false);
                if (enrollmentGroupState == null)
                {
                    return new EnrollmentUpdateExecutionResult { Success = false };
                }

                var occupiedSeatCount = GetOccupiedSeatCount(enrollmentGroupState.EnrollmentStatusCount);
                if (!enrollmentGroupState.IfRegistrationOpen || occupiedSeatCount >= enrollmentGroupState.MaxStudents)
                {
                    return new EnrollmentUpdateExecutionResult { Success = false };
                }
            }

            var response = await _repository.UpdateEnrollment(enrollmentId, enrollment).ConfigureAwait(false);
            if (!response)
            {
                return new EnrollmentUpdateExecutionResult { Success = false };
            }

            var statusChanged = enrollment.EnrollmentStatus != enrollmentStatus;
            var result = new EnrollmentUpdateExecutionResult
            {
                Success = true,
                FamilyId = enrollmentDetails.FamilyId,
                CourseId = enrollmentDetails.CourseId,
                RequiresRecalculation = statusChanged && enrollment.EnrollmentStatus != EnrollmentStatus.Refunded,
                EmailNotification = shouldQueueEmailNotification
                    ? CreateEnrollmentEmailNotification(
                        statusChanged,
                        enrollment.EnrollmentStatus,
                        enrollmentDetails,
                        courseDetails,
                        enrollmentGroup)
                    : null
            };

            if (shouldRecalculate && result.RequiresRecalculation)
            {
                await RecalculateCourseFee(enrollmentDetails.CourseId, enrollmentDetails.FamilyId).ConfigureAwait(false);
            }

            if (shouldSendEmailImmediately && result.EmailNotification != null)
            {
                await SendEnrollmentStatusEmailsAsync(new[] { result.EmailNotification }).ConfigureAwait(false);
            }

            return result;
        }

        private static EnrollmentEmailNotification? CreateEnrollmentEmailNotification(
            bool statusChanged,
            EnrollmentStatus enrollmentStatus,
            StudentCourseEnrollmentResponse enrollmentDetails,
            CourseResponseDetailed courseDetails,
            CourseEnrollmentGroupResponse? enrollmentGroup)
        {
            if (!statusChanged)
            {
                return null;
            }

            return enrollmentStatus switch
            {
                EnrollmentStatus.Enrolled or EnrollmentStatus.Awaiting or EnrollmentStatus.Registered or EnrollmentStatus.Cancelled => new EnrollmentEmailNotification
                {
                    FamilyId = enrollmentDetails.FamilyId,
                    Status = enrollmentStatus,
                    ChildName = enrollmentDetails.ChildName ?? string.Empty,
                    CourseName = courseDetails.Name ?? string.Empty,
                    CourseNameFr = courseDetails.NameFr ?? string.Empty,
                    CourseGroupDetails = enrollmentGroup?.Details ?? string.Empty,
                    CourseGroupDetailsFr = enrollmentGroup?.DetailsFr ?? string.Empty
                },
                _ => null
            };
        }

        private async Task SendEnrollmentStatusEmailsAsync(IEnumerable<EnrollmentEmailNotification> notifications)
        {
            foreach (var familyNotifications in notifications.GroupBy(notification => notification.FamilyId))
            {
                var email = BuildEnrollmentStatusEmail(familyNotifications.ToList());
                if (email == null)
                {
                    continue;
                }

                var targetEmails = await GetFamilyNotificationEmailAddressesAsync(familyNotifications.Key).ConfigureAwait(false);

                if (!targetEmails.Any())
                {
                    continue;
                }

                await _sendEmailService.SendBulkEmail(new MultiUserEmailData
                {
                    To = targetEmails,
                    Subject = email.Value.Subject,
                    Body = email.Value.Body
                }).ConfigureAwait(false);
            }
        }

        private async Task SendEnrollmentCreatedEmailAsync(
            StudentCourseEnrollmentResponse enrollment,
            CourseResponseDetailed courseDetails,
            CourseEnrollmentGroupResponse? enrollmentGroup)
        {
            if (courseDetails.IsManualEnrollment && enrollment.EnrollmentStatus == EnrollmentStatus.Awaiting)
            {
                await SendManualEnrollmentAwaitingEmailAsync(enrollment, courseDetails, enrollmentGroup).ConfigureAwait(false);
                return;
            }

            var notification = CreateEnrollmentEmailNotification(
                statusChanged: true,
                enrollmentStatus: enrollment.EnrollmentStatus,
                enrollmentDetails: enrollment,
                courseDetails: courseDetails,
                enrollmentGroup: enrollmentGroup);

            if (notification == null)
            {
                return;
            }

            await SendEnrollmentStatusEmailsAsync(new[] { notification }).ConfigureAwait(false);
        }

        private async Task SendManualEnrollmentAwaitingEmailAsync(
            StudentCourseEnrollmentResponse enrollment,
            CourseResponseDetailed courseDetails,
            CourseEnrollmentGroupResponse? enrollmentGroup)
        {
            var targetEmails = await GetFamilyNotificationEmailAddressesAsync(enrollment.FamilyId).ConfigureAwait(false);
            if (!targetEmails.Any())
            {
                return;
            }

            var email = BuildManualEnrollmentAwaitingEmail(enrollment, courseDetails, enrollmentGroup);
            await _sendEmailService.SendBulkEmail(new MultiUserEmailData
            {
                To = targetEmails,
                Subject = email.Subject,
                Body = email.Body
            }).ConfigureAwait(false);
        }

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

        private static (string Subject, string Body)? BuildEnrollmentStatusEmail(IReadOnlyList<EnrollmentEmailNotification> notifications)
        {
            if (notifications.Count == 0)
            {
                return null;
            }

            if (notifications.Count == 1)
            {
                return BuildSingleEnrollmentStatusEmail(notifications[0]);
            }

            var frenchItems = string.Join(string.Empty, notifications.Select(BuildFrenchEnrollmentStatusListItem));
            var englishItems = string.Join(string.Empty, notifications.Select(BuildEnglishEnrollmentStatusListItem));

            return (
                "Mise a jour des inscriptions / Enrollment Update",
                $"<p><strong>Cher parent,</strong></p>" +
                $"<p>Voici les dernieres mises a jour pour les inscriptions de votre famille :</p>" +
                $"<ul>{frenchItems}</ul>" +
                $"<div>&nbsp;</div>" +
                $"<p><strong>Dear parent,</strong></p>" +
                $"<p>Here are the latest updates for your family's enrollments:</p>" +
                $"<ul>{englishItems}</ul>" +
                $"<div>&nbsp;</div>" +
                $"<div><strong>ICC Brossard School / Activities Registration Portal - Portail de l'inscription ecoles / activites</strong></div>");
        }

        private static (string Subject, string Body)? BuildSingleEnrollmentStatusEmail(EnrollmentEmailNotification notification)
        {
            return notification.Status switch
            {
                EnrollmentStatus.Enrolled => (
                    EnrollmentConfirmationSubject,
                    $"<p><strong>Chèr parent,</strong></p>" +
                    $"<p>Merci d'avoir inscrit votre enfant au <strong>{notification.CourseNameFr}-</strong><strong>{notification.CourseGroupDetailsFr}</strong>. L'inscription sera complétée seulement après réception du paiement, conformément à la politique du {{school / camp}}. Veuillez vous connecter au portail et payer les frais d'inscription.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<p><strong>Dear parent,</strong></p>" +
                    $"<p>Thank you for enrolling your child in the <strong>{notification.CourseName}-</strong><strong>{notification.CourseGroupDetails}</strong>. Registration is only complete when payment is made based on the policy of the {{school / camp}}. Please login to the portal and pay the fee to register your child.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<div>&nbsp;</div>" +
                    $"<div><strong>ICC Brossard School Registration Portal</strong></div>"),
                EnrollmentStatus.Awaiting => (
                    $"{notification.CourseNameFr} Liste d'attente / {notification.CourseName} Waiting List",
                    $"<p><strong>Cher parent,</strong></p>" +
                    $"<p>Votre enfant {notification.ChildName} a été ajouté(e) à la liste d'attente pour le cours / l'activité <strong>{notification.CourseNameFr}</strong>{FormatGroupSuffix(notification.CourseGroupDetailsFr)}. Nous communiquerons avec vous lorsqu'une place se libérera ou lorsqu'une prochaine étape sera requise.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<p><strong>Dear parent,</strong></p>" +
                    $"<p>Your child {notification.ChildName} has been added to the waiting list for the course / activity <strong>{notification.CourseName}</strong>{FormatGroupSuffix(notification.CourseGroupDetails)}. We will contact you if a seat becomes available or if any next step is required.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<div>&nbsp;</div>" +
                    $"<div><strong>ICC Brossard School / Activities Registration Portal - Portail de l'inscription écoles / activités</strong></div>"),
                EnrollmentStatus.Registered => (
                    $"{notification.CourseNameFr} Confirmation d'inscription / {notification.CourseName} Registration Confirmation",
                    $"<p><strong>Cher parent,</strong></p>" +
                    $"<p>Votre enfant {notification.ChildName} est inscrit(e) au cours / à l'activité <strong>{notification.CourseNameFr}</strong>. Tous les frais sont réglés.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<p><strong>Dear parent,</strong></p>" +
                    $"<p>Your child {notification.ChildName} has been registered for the course / activity <strong>{notification.CourseName}</strong>. All fees are fully paid.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<div>&nbsp;</div>" +
                    $"<div><strong>ICC Brossard School / Activities Registration Portal - Portail de l'inscription écoles / activités</strong></div>"),
                EnrollmentStatus.Cancelled => (
                    "Annulation de l'inscription / Cancellation of Registration",
                    $"<p><strong>Chèr parent,</strong></p>" +
                    $"<p>L'inscription de votre enfant a été annulée à cause que les frais requis n'ayant pas été réglés dans les délais précédemment communiqués.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<p><strong>Dear parent,</strong></p>" +
                    $"<p>Due to the required fees being unpaid by the deadline previously given, your child's registration has been cancelled.</p>" +
                    $"<div>&nbsp;</div>" +
                    $"<div>&nbsp;</div>" +
                    $"<div><strong>ICC Brossard School Registration Portal</strong></div>"),
                _ => null
            };
        }

        private static (string Subject, string Body) BuildManualEnrollmentAwaitingEmail(
            StudentCourseEnrollmentResponse enrollment,
            CourseResponseDetailed courseDetails,
            CourseEnrollmentGroupResponse? enrollmentGroup)
        {
            var childName = System.Net.WebUtility.HtmlEncode(enrollment.ChildName ?? string.Empty);
            var courseName = System.Net.WebUtility.HtmlEncode(courseDetails.Name ?? string.Empty);
            var courseNameFr = System.Net.WebUtility.HtmlEncode(courseDetails.NameFr ?? string.Empty);
            var courseGroupDetails = System.Net.WebUtility.HtmlEncode(enrollmentGroup?.Details ?? string.Empty);
            var courseGroupDetailsFr = System.Net.WebUtility.HtmlEncode(enrollmentGroup?.DetailsFr ?? string.Empty);
            var formattedCourseName = string.IsNullOrWhiteSpace(courseGroupDetails)
                ? courseName
                : $"{courseName} ({courseGroupDetails})";
            var formattedCourseNameFr = string.IsNullOrWhiteSpace(courseGroupDetailsFr)
                ? courseNameFr
                : $"{courseNameFr} ({courseGroupDetailsFr})";

            var body =
                $"<p>Votre enfant {childName} est inscrit au cours coranique de l'ecole <strong>{formattedCourseNameFr}</strong> et son statut est actuellement <strong>Inscrit</strong> et <strong>En attente</strong>.</p>" +
                $"<p>Si votre enfant a deja frequente l'ecole, l'administration l'affectera au cours du meme jour que precedemment. Si le paiement complet a ete effectue, son inscription sera confirmee et son statut changera a <strong>Enregistre</strong>. Cela sera fait au cours des prochains jours.</p>" +
                $"<p>Si vous souhaitez modifier le jour de cours de votre enfant, veuillez envoyer un courriel a l'administration de l'ecole a l'adresse <strong>rattel.ecole@gmail.com</strong>. Sous reserve des places disponibles, l'ecole fera de son mieux pour effectuer ce changement.</p>" +
                $"<p>S'il s'agit de la premiere inscription de votre enfant a l'ecole, l'administration communiquera avec vous afin de finaliser le processus d'inscription et d'enregistrement.</p>" +
                $"<div>&nbsp;</div>" +
                $"<p>Your child {childName} has been enrolled in the <strong>{formattedCourseName}</strong> and is currently in <strong>Enrolled</strong> and <strong>Waiting</strong> status.</p>" +
                $"<p>If your child has previously attended the school, the administration will assign your child to the class on the same day as before. If full payment has been made, your child's registration will then be confirmed and the status will change to <strong>Registered</strong>. This will be done within the next few days.</p>" +
                $"<p>If you would like to change your child's class day, please email the school administration at <strong>rattel.ecole@gmail.com</strong>. Subject to availability, the school will do its best to make the change.</p>" +
                $"<p>If this is your child's first time enrolling at the school, the administration will contact you to finalize the enrollment and registration process.</p>" +
                $"<div>&nbsp;</div>" +
                $"<div><strong>ICC Brossard School Registration Portal</strong></div>";

            return (EnrollmentConfirmationSubject, body);
        }

        private static string BuildFrenchEnrollmentStatusListItem(EnrollmentEmailNotification notification)
        {
            return notification.Status switch
            {
                EnrollmentStatus.Enrolled => $"<li>{notification.ChildName} - <strong>{notification.CourseNameFr}</strong>{FormatGroupSuffix(notification.CourseGroupDetailsFr)} : inscription recue, paiement requis.</li>",
                EnrollmentStatus.Awaiting => $"<li>{notification.ChildName} - <strong>{notification.CourseNameFr}</strong>{FormatGroupSuffix(notification.CourseGroupDetailsFr)} : ajoute(e) a la liste d'attente.</li>",
                EnrollmentStatus.Registered => $"<li>{notification.ChildName} - <strong>{notification.CourseNameFr}</strong> : inscription confirmee, tous les frais sont regles.</li>",
                EnrollmentStatus.Cancelled => $"<li>{notification.ChildName} - <strong>{notification.CourseNameFr}</strong> : inscription annulee.</li>",
                _ => string.Empty
            };
        }

        private static string BuildEnglishEnrollmentStatusListItem(EnrollmentEmailNotification notification)
        {
            return notification.Status switch
            {
                EnrollmentStatus.Enrolled => $"<li>{notification.ChildName} - <strong>{notification.CourseName}</strong>{FormatGroupSuffix(notification.CourseGroupDetails)}: enrollment received, payment required.</li>",
                EnrollmentStatus.Awaiting => $"<li>{notification.ChildName} - <strong>{notification.CourseName}</strong>{FormatGroupSuffix(notification.CourseGroupDetails)}: added to the waiting list.</li>",
                EnrollmentStatus.Registered => $"<li>{notification.ChildName} - <strong>{notification.CourseName}</strong>: registration confirmed, all fees are paid.</li>",
                EnrollmentStatus.Cancelled => $"<li>{notification.ChildName} - <strong>{notification.CourseName}</strong>: registration cancelled.</li>",
                _ => string.Empty
            };
        }

        private static string FormatGroupSuffix(string details)
            => string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : $" - <strong>{details}</strong>";

        public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false, bool ifDeletedByAdmin = false)
        {
            var enrollmentDetails = await _repository.GetEnrollment(enrollmentId).ConfigureAwait(false);

            if (enrollmentDetails == null)
            {
                return false;
            }

            var previousEnrollmentStatus = enrollmentDetails.EnrollmentStatus;
          
            var courseDetails = await _courseService.GetCourse(enrollmentDetails.CourseId).ConfigureAwait(false);
            
            if (!courseDetails.IsRegistrationOpened && !ifDeletedByAdmin)
            {
                return false;
            }

            var enrollmentGroup = courseDetails.CourseEnrollmentGroups.FirstOrDefault(x => x.CourseEnrollmentGroupId == enrollmentDetails.CourseEnrollmentGroupId);

            var courseId = enrollmentDetails.CourseId;
            var familyId = enrollmentDetails.FamilyId;
            var familyTransaction = (await _studentCourseTransactionService.GetCourseTransactionsByFamily(courseId, familyId).ConfigureAwait(false)).FirstOrDefault();

            var activeEnrollmentCount = GetActiveEnrollments(familyTransaction).Count;
            var shouldPreserveLastEnrollmentLink = !hardDelete && activeEnrollmentCount <= 1;

            var ifDeleted = shouldPreserveLastEnrollmentLink ||
                await _studentCourseTransactionService.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(enrollmentId).ConfigureAwait(false);
            var ifEnrollmentDeleted = false;

            if (ifDeleted)
            {
                ifEnrollmentDeleted = await _repository.DeleteEnrollment(enrollmentId, hardDelete).ConfigureAwait(false);
            }

            if (!ifDeleted || !ifEnrollmentDeleted)
            {
                return false;
            }

            if (familyTransaction == null)
            {
                return true;
            }

            return await RecalculateCourseFee(courseId, familyId).ConfigureAwait(false);

        }
        public Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
            => _repository.GetStudentCourseEnrollment(childId, courseId);

        public Task<IEnumerable<CourseEnrollmentGroupInformationResponse>> GetCourseEnrollmentGroupsInformation(Guid courseId)
            => _repository.GetCourseEnrollmentGroupsInformation(courseId);

        public Task<CourseEnrollmentGroupInformationResponse?> GetCourseEnrollmentGroupInformation(Guid courseGroupId)
            => _repository.GetCourseEnrollmentGroupInformation(courseGroupId);
    }
}
