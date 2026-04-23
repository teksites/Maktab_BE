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

    public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment, bool ifAddedByAdmin = false)
        {

            var familyTransactions = await _studentCourseTransactionService.GetCourseTransactionsByFamily(enrollment.CourseId, enrollment.FamilyId).ConfigureAwait(false);

            var familyCourseTransaction = familyTransactions.Where(x => x.Enrollments.All(y => y.CourseId == enrollment.CourseId));
            var course = await _courseService.GetCourse(enrollment.CourseId).ConfigureAwait(false);

            if (!course.IsRegistrationOpened && !ifAddedByAdmin)
            {
                throw new Exception("The registration is closed. Contact Admin please");
            }

            var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false);
            if (enrollmentGroupState == null)
            {
                throw new Exception("Course enrollment group not found");
            }

            var occupiedSeatCount = GetOccupiedSeatCount(enrollmentGroupState.EnrollmentStatusCount);

            var canRegister = enrollmentGroupState.IfRegistrationOpen && occupiedSeatCount < enrollmentGroupState.MaxStudents;
            enrollment.EnrollmentStatus = canRegister ? EnrollmentStatus.Enrolled : EnrollmentStatus.Awaiting;

            if (enrollmentGroupState.IfRegistrationOpen && occupiedSeatCount + 1 >= enrollmentGroupState.MaxStudents)
            {
                await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroupState.CourseEnrollmentGroupId, false).ConfigureAwait(false);
            }

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

                //var newDayCareFee = (enrollment.WillUseDayCare ? selectedCourseEnrollmentGroup.DayCareFee : 0);
                //decimal newCourseFee = 0;

                //if there is any existing enrollment for the child in the same course for any course group. if it is the same course group then we will not allow registration
                if (childEnrollment.Any()) // child is already registered for the same course in different course group
                {
                    enrollment.EnrollmentIndex = childEnrollment.Max(e => e.EnrollmentIndex);
                    //newCourseFee = selectedCourseEnrollmentGroup.Fee;
                    //Now update the transaction to include the new enrollment

                }
                else // child is not registered for any course group of the course
                {
                    //here we have to add new enrollment and update the existing transaction to include the new enrollment for new child
                    //// We will calculate the discount and apply
                    //var policies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                    //var discountPolicy = policies.Where(p => p.IsActive && p.InstutePolicy == MaktabDataContracts.Enums.InstutePolicyType.SiblingDiscount).First().Details;
                    //SiblingDiscountPolicy policy = JsonConvert.DeserializeObject<SiblingDiscountPolicy>(discountPolicy);

                    var distinctChildIds = familyTransactions
                        .SelectMany(t => t.Enrollments)
                        .Select(e => e.ChildId)
                        .Distinct()
                        .ToList();
                    
                    enrollment.EnrollmentIndex = distinctChildIds.Count + 1;

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
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount);
                var activePolicies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                var activeFeePaymentPolicy = activePolicies.FirstOrDefault(p => p.IsActive && p.InstutePolicy == PolicyType.CourseFeePayment);
                var (feePolicy, feePaymentPolicyFound) = ParseValidatedFeePaymentPolicy(activeFeePaymentPolicy?.Details);
                addStudentCourseTransaction.FeeInstallments = BuildFeeInstallments(
                    addStudentCourseTransaction.TotalPayable,
                    feePaymentPolicyFound,
                    feePolicy,
                    new[] { 1 });
                addStudentCourseTransaction.Comments = $"New Enrollment on {DateTime.UtcNow.ToString()}";
                addStudentCourseTransaction.IsCompletelyPaid = false;

                try
                {
                    var addedTransaction = await _studentCourseTransactionService.AddTransaction(addStudentCourseTransaction).ConfigureAwait(false);
                    var studenEnrollmentTransaction = await _studentCourseTransactionService.AddEnrollmentsToTransaction(addedTransaction.StudentCourseTransactionId,
                              addedEnrollment.StudentCourseEnrollmentId).ConfigureAwait(false);
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

            var effectiveEnrollments = familyTransaction.Enrollments
            .GroupBy(e => new { e.EnrollmentIndex, e.CourseEnrollmentGroupId })
            .Select(g => g
                .OrderByDescending(e => e.UpdatedOn)
                .ThenByDescending(e => e.CreatedAt)
                .First())
            .OrderBy(e => e.EnrollmentIndex)
            .ThenBy(e => e.CreatedAt)
            .ToList();

            var groupedByChild = effectiveEnrollments
            .GroupBy(e => e.EnrollmentIndex)
            .Select(g =>
            {
                var orderedEnrollments = g
                    .OrderBy(e => e.CreatedAt)
                    .ThenBy(e => e.UpdatedOn)
                    .ToList();

                return new
                {
                    EnrollmentIndex = g.Key,
                    ChildId = orderedEnrollments[0].ChildId,
                    Enrollments = orderedEnrollments
                };
            })
            .OrderBy(g => g.EnrollmentIndex)
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
                    var activeSiblingPolicy = policies.FirstOrDefault(p => p.IsActive && p.InstutePolicy == PolicyType.SiblingDiscount);
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

                    var activeFeePaymentPolicy = policies.FirstOrDefault(p => p.IsActive && p.InstutePolicy == PolicyType.CourseFeePayment);
                    (feePolicy, feePaymentPolicyFound) = ParseValidatedFeePaymentPolicy(activeFeePaymentPolicy?.Details);
                }
            }

           foreach (var childGroup in groupedByChild)
            {
                var childEnrollments = childGroup.Enrollments;
                decimal childFee = 0m;
                decimal childDayCareFee = 0m;
                decimal applicableDiscountPercentage = 1m;

                if (i == 2 && policyFound)
                {
                    applicableDiscountPercentage = policy.SecondChildFee / 100m;
                }
                else if (i == 3 && policyFound)
                {
                    applicableDiscountPercentage = policy.ThirdChildFee / 100m;
                }
                else if (i > 3 && policyFound)
                {
                    applicableDiscountPercentage = policy.FourthAndOnwardChildFee / 100m;
                }

                foreach (var enrollment in childEnrollments)
                {
                    var enrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                    if (enrollmentGroup != null && IsFeeBearingEnrollmentStatus(enrollment.EnrollmentStatus))// Only allowed billable ones
                    {
                        childFee += (Convert.ToDecimal(enrollmentGroup.Fee) * applicableDiscountPercentage);
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
            addStudentCourseTransaction.DayCareFee = dayCareFee;
            addStudentCourseTransaction.PayableFee = courseFee;
            addStudentCourseTransaction.TotalAmountPaid = familyTransaction.TotalAmountPaid;
            var recalculatedTotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount);
            addStudentCourseTransaction.TotalPayable = recalculatedTotalPayable < 0m ? 0m : recalculatedTotalPayable + (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount);
            addStudentCourseTransaction.FeeInstallments = BuildFeeInstallments(
                addStudentCourseTransaction.TotalPayable,
                feePaymentPolicyFound,
                feePolicy,
                enrollmentGroupCountsByChild);
            addStudentCourseTransaction.Comments = familyTransaction.Comments +$"\n Updated the transaction on {DateTime.UtcNow.ToString()}";
            addStudentCourseTransaction.IsCompletelyPaid = addStudentCourseTransaction.TotalPayable <= addStudentCourseTransaction.TotalAmountPaid;

            return await _studentCourseTransactionService.UpdateTransaction(familyTransaction.StudentCourseTransactionId, addStudentCourseTransaction).ConfigureAwait(false);
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
                var totalMinimumAmountDue = feePolicy.Sum(x => x.MinimumAmountDue ?? 0m);

                return (feePolicy, feePolicy.Count > 0);
            }
            catch
            {
                return (new List<FeePaymentPolicy>(), false);
            }
        }

        private static List<FeeInstallment> BuildFeeInstallments(
            decimal totalPayable,
            bool feePaymentPolicyFound,
            IReadOnlyList<FeePaymentPolicy> feePolicy,
            IReadOnlyList<int> enrollmentGroupCountsByChild)
        {
            if (totalPayable <= 0m)
            {
                return new List<FeeInstallment>();
            }

            if (!feePaymentPolicyFound || feePolicy.Count == 0)
            {
                return new List<FeeInstallment>
                {
                    new FeeInstallment
                    {
                        Description = "Paiement complet de l'inscription/Complete Registration Payment",
                        DueDate = DateTime.UtcNow.Date.AddDays(1),
                        Amount = totalPayable
                    }
                };
            }

            var installmentCount = DetermineInstallmentCount(feePolicy.Count, enrollmentGroupCountsByChild);
            if (installmentCount <= 1)
            {
                return new List<FeeInstallment>
                {
                    new FeeInstallment
                    {
                        Description = feePolicy[0].Name,
                        DueDate = feePolicy[0].PaymentDate,
                        Amount = totalPayable
                    }
                };
            }

            var installmentAmounts = SplitInstallmentAmounts(totalPayable, installmentCount);
            var installments = new List<FeeInstallment>(installmentCount);

            for (var index = 0; index < installmentCount; index++)
            {
                installments.Add(new FeeInstallment
                {
                    Description = feePolicy[index].Name,
                    DueDate = feePolicy[index].PaymentDate,
                    Amount = installmentAmounts[index]
                });
            }

            return installments;
        }

        private static int DetermineInstallmentCount(int policyItemCount, IReadOnlyList<int> enrollmentGroupCountsByChild)
        {
            if (policyItemCount <= 0 || enrollmentGroupCountsByChild.Count == 0)
            {
                return 0;
            }

            if (enrollmentGroupCountsByChild.Count == 1)
            {
                var singleChildEnrollmentGroupCount = enrollmentGroupCountsByChild[0];
                if (singleChildEnrollmentGroupCount <= 2)
                {
                    return 1;
                }

                return singleChildEnrollmentGroupCount <= policyItemCount
                    ? singleChildEnrollmentGroupCount - 1
                    : policyItemCount;
            }

            if (enrollmentGroupCountsByChild.All(count => count <= 1))
            {
                return 1;
            }

            var maxEnrollmentGroupCount = enrollmentGroupCountsByChild.Max();
            return maxEnrollmentGroupCount <= policyItemCount
                ? maxEnrollmentGroupCount
                : policyItemCount;
        }

        private static List<decimal> SplitInstallmentAmounts(decimal totalPayable, int installmentCount)
        {
            if (installmentCount <= 1)
            {
                return new List<decimal> { totalPayable };
            }

            var installmentAmounts = new List<decimal>(installmentCount);
            var remainingAmount = totalPayable;

            for (var index = 0; index < installmentCount - 1; index++)
            {
                var remainingInstallmentCount = installmentCount - index;
                var roundedInstallmentAmount = RoundToNearestFive(remainingAmount / remainingInstallmentCount);

                if (roundedInstallmentAmount > remainingAmount)
                {
                    roundedInstallmentAmount = remainingAmount;
                }

                installmentAmounts.Add(roundedInstallmentAmount);
                remainingAmount -= roundedInstallmentAmount;
            }

            installmentAmounts.Add(remainingAmount);
            return installmentAmounts;
        }

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
                addStudentCourseTransaction.DayCareFee = enrollmentGroup.DayCareFee;//add day care fee in course group
                addStudentCourseTransaction.TotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) - 
                    (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount) ;
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

        public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment, bool ifUpdatedByAdmin = false)
        {
            var enrollmentDetails = await _repository.GetEnrollment(enrollmentId).ConfigureAwait(false);

            if (enrollmentDetails == null)
            {
                return false;
            }

            var enrollmentStatus = enrollmentDetails.EnrollmentStatus;

            var courseDetails = await _courseService.GetCourse(enrollmentDetails.CourseId).ConfigureAwait(false);
            //var enrollmentGroup = courseDetails.CourseEnrollmentGroups.FirstOrDefault(x => x.CourseEnrollmentGroupId == enrollmentDetails.CourseEnrollmentGroupId);

            if (!courseDetails.IsRegistrationOpened && !ifUpdatedByAdmin)
            {
                return false;
            }

            // Preserve the existing status when the caller does not explicitly send one.
            if (enrollment.EnrollmentStatus == EnrollmentStatus.Unknown)
            {
                enrollment.EnrollmentStatus = enrollmentStatus;
            }

            var enrollmentGroup = courseDetails.CourseEnrollmentGroups.FirstOrDefault(x => x.CourseEnrollmentGroupId == enrollmentDetails.CourseEnrollmentGroupId);
            var isMovingIntoSeatHoldingStatus =
                !HoldsSeat(enrollmentStatus) &&
                HoldsSeat(enrollment.EnrollmentStatus) &&
                enrollment.EnrollmentStatus != enrollmentStatus;

            if (isMovingIntoSeatHoldingStatus)
            {
                var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollmentDetails.CourseEnrollmentGroupId).ConfigureAwait(false);
                if (enrollmentGroupState == null)
                {
                    return false;
                }

                var occupiedSeatCount = GetOccupiedSeatCount(enrollmentGroupState.EnrollmentStatusCount);
                if (!enrollmentGroupState.IfRegistrationOpen || occupiedSeatCount >= enrollmentGroupState.MaxStudents)
                {
                    return false;
                }
            }

            var response = await _repository.UpdateEnrollment(enrollmentId, enrollment).ConfigureAwait(false);

            if (response &&
                isMovingIntoSeatHoldingStatus &&
                enrollmentGroup != null)
            {
                var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollmentDetails.CourseEnrollmentGroupId).ConfigureAwait(false);
                if (enrollmentGroupState != null)
                {
                    var occupiedSeatCount = GetOccupiedSeatCount(enrollmentGroupState.EnrollmentStatusCount);
                    if (enrollmentGroupState.IfRegistrationOpen && occupiedSeatCount >= enrollmentGroupState.MaxStudents)
                    {
                        await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroup.CourseEnrollmentGroupId, false).ConfigureAwait(false);
                    }
                }
            }

            //if previous state was registered and its deleted, then we have to set the isregistered to true if the isregistered is false 
            // it means we have a room now
            if (enrollmentDetails != null &&
                HoldsSeat(enrollmentStatus) &&
                !HoldsSeat(enrollment.EnrollmentStatus) &&
                enrollment.EnrollmentStatus != enrollmentStatus)
            {
                if (!enrollmentGroup.IfRegistrationOpen)
                {
                    await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroup.CourseEnrollmentGroupId, true).ConfigureAwait(false);
                }
            }

            if (response && enrollment.EnrollmentStatus != enrollmentStatus && enrollment.EnrollmentStatus != EnrollmentStatus.Refunded)//No need calc fee on refund
            {
                await RecalculateCourseFee(enrollmentDetails.CourseId, enrollmentDetails.FamilyId).ConfigureAwait(false);
            }


            var familyUsers = await _userService.GetAllFamilyUsersInformation(enrollment.FamilyId).ConfigureAwait(false);

            var targetEmails = familyUsers
            .Where(x => x.Relationship == Relationship.Mother ||
                        x.Relationship == Relationship.Father ||
                        x.Relationship == Relationship.Guardian)
            .Select(x => x.Email)
            .ToList();

            var emailBody = string.Empty;
            var emailSubject = string.Empty;
            //now emails
            if (response && enrollment.EnrollmentStatus != enrollmentStatus && (enrollment.EnrollmentStatus == EnrollmentStatus.Registered || enrollment.EnrollmentStatus == EnrollmentStatus.Enrolled))//No need calc fee on refund
            {
                emailSubject = "Confirmation de l'inscription / Confirmation of enrollment";
                emailBody = $"<p><strong>Chèr parent,</strong></p>" +
                       $"<p>Merci d'avoir inscrit votre enfant au <strong>{courseDetails.NameFr}-</strong><strong>{enrollmentGroup.DetailsFr}</strong>. L'inscription sera complétée seulement après réception du paiement, conformément à la politique du {{school / camp}}. Veuillez vous connecter au portail et payer les frais d'inscription.</p>" +
                       $"<div>&nbsp;</div>" +
                       $"<p><strong>Dear parent,</strong></p>" +
                       $"<p>Thank you for enrolling your child in the <strong>{courseDetails.Name}-</strong><strong>{enrollmentGroup.Details}</strong>. Registration is only complete when payment is made based on the policy of the {{school / camp}}. Please login to the portal and pay the fee to register your child.</p>" +
                       $"<div>&nbsp;</div>" +
                       $"<div>Cheers</div>" +
                       $"<div>&nbsp;</div>" +
                       $"<div><strong>ICC Brossard School Registration Portal</strong></div>";

            }
            else if (response && enrollment.EnrollmentStatus != enrollmentStatus && enrollment.EnrollmentStatus == EnrollmentStatus.Cancelled)//No need calc fee on refund

            {
                emailSubject = $"Annulation de l'inscription / Cancellation of Registration";
                emailBody = $"<p><strong>Chèr parent,</strong></p>" +
               $"<p>L'inscription de votre enfant a été annulée à cause que les frais requis n'ayant pas été réglés dans les délais précédemment communiqués.</p>" +
               $"<div>&nbsp;</div>" +
               $"<p><strong>Dear parent,</strong></p>" +
               $"<p>Due to the required fees being unpaid by the deadline previously given, your child's registration has been cancelled.</p>" +
               $"<div>&nbsp;</div>" +
               $"<div>Cheers</div>" +
               $"<div>&nbsp;</div>" +
               $"<div><strong>ICC Brossard School Registration Portal</strong></div>";

            }

            if (!string.IsNullOrEmpty(emailBody) && targetEmails.Any())
            {
               // _sendEmailService.
                 var success = _sendEmailService.SendBulkEmail(new MultiUserEmailData
                {
                    To = targetEmails.ToList(),
                    Subject = emailSubject,
                    Body = emailBody
                });
        }

            return response;
        }
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


            var ifDeleted = await _studentCourseTransactionService.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(enrollmentId).ConfigureAwait(false);
            var ifEnrollmentDeleted = false;

            if (ifDeleted)
            {
                ifEnrollmentDeleted = await _repository.DeleteEnrollment(enrollmentId, hardDelete).ConfigureAwait(false);
            }

            if (!ifDeleted || !ifEnrollmentDeleted)
            {
                return false;
            }

            // If a confirmed-seat enrollment is removed, reopen the group when it had been closed for capacity.
            if (enrollmentGroup != null &&
                HoldsSeat(previousEnrollmentStatus) &&
                !enrollmentGroup.IfRegistrationOpen)
            {
                await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroup.CourseEnrollmentGroupId, true).ConfigureAwait(false);
            }

            if (familyTransaction.Enrollments.Count <= 1)
            {
                var ifFeePaid = familyTransaction.TotalAmountPaid > 0;
                return await _studentCourseTransactionService.DeleteTransaction(familyTransaction.StudentCourseTransactionId, !ifFeePaid).ConfigureAwait(false);
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
