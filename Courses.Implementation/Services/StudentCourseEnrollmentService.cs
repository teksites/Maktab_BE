using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
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
        private readonly ICourseEnrollmentGroupService _courseEnrollmentGroupService;

        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository, IStudentCourseTransactionService studentCourseEnrollmentService, ICourseService courseService, IInstitutePolicyService policyService, ICourseEnrollmentGroupService courseEnrollmentGroupService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseEnrollmentService;
            _courseService = courseService;
            _policyService = policyService;
            _courseEnrollmentGroupService = courseEnrollmentGroupService;
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

            //var enrollmentGroupsState = await GetCourseEnrollmentGroupsInformation(enrollment.CourseId).ConfigureAwait(false);
            var enrollmentGroupState = await GetCourseEnrollmentGroupInformation(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false);

            enrollment.EnrollmentStatus = enrollmentGroupState?.MaxStudents > enrollmentGroupState?.EnrollmentStatusCount[EnrollmentStatus.Registered] && 
                enrollmentGroupState.IfRegistrationOpen?
                EnrollmentStatus.Registered : EnrollmentStatus.Awaiting ;

            if (enrollmentGroupState?.MaxStudents <= enrollmentGroupState?.EnrollmentStatusCount[EnrollmentStatus.Registered] + 1  &&
                enrollmentGroupState.IfRegistrationOpen)
            {
                await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroupState.CourseEnrollmentGroupId, false).ConfigureAwait(false);

            }

            //if count of entrollment in registered is now equal to max students, we have to set the insregisteration opened to false for the group

            // Here check count of the existing enrollments for the group. if they are exceeding the count enrollments for the group, we will keep status waiting
            //otherwise we will keep it successful

            //we will do further change in update enrollment api to update the status of the enrollment based on the count of the enrollments in the group and max allowed students in the group. if the count is exceeding the max allowed students, we will keep status waiting otherwise we will keep it successful

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

                    //decimal discountPercentage = policy.FirstChildFee / 100;

                    //if (distinctChildIds.Count == 1)
                    //{
                    //    discountPercentage = policy.SecondChildFee / 100;
                    //}
                    //else if (distinctChildIds.Count >= 2)
                    //{
                    //    discountPercentage = policy.ThirdAndOnwardChildFee / 100;
                    //}

                    //newCourseFee = selectedCourseEnrollmentGroup.Fee * discountPercentage;
                }

                // Add enrollment first
                var addedEnrollment = await _repository.AddEnrollment(enrollment).ConfigureAwait(false);

                //var transaction = await _studentCourseTransactionService.GetTransaction(familyCourseTransaction.First().StudentCourseTransactionId).ConfigureAwait(false);

                // Update the transcation
                // First create the transaction based on course data
                /*var transactionToUpdate = new AddStudentCourseTransaction
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
                */
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
                // Only calculate fees if enrollment status is Registered
                if (enrollment.EnrollmentStatus == EnrollmentStatus.Registered)
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

            var groupedByChild = familyTransaction.Enrollments
            .GroupBy(e => e.ChildId)
            .Select(g => new
            {
                ChildId = g.Key,
                Enrollments = g.ToList()
            })
            .ToList();

            int i = 1;

            SiblingDiscountPolicy policy = new SiblingDiscountPolicy();
            var policyFound = false;

            if (groupedByChild.Count > 1)
            {
                var policies = await _policyService.GetAllPolicies(course.InstituteId).ConfigureAwait(false);
                if (!policies.Any())
                {
                    policyFound = false;
                }
                else
                {
                    var activeSiblingPolicy = policies.FirstOrDefault(p => p.IsActive && p.InstutePolicy == InstutePolicyType.SiblingDiscount);
                    var discountPolicy = activeSiblingPolicy?.Details;
                    if (!string.IsNullOrEmpty(discountPolicy))
                    {
                        try
                        {
                            policy = JsonConvert.DeserializeObject<SiblingDiscountPolicy>(discountPolicy);
                            policyFound = true;
                        }
                        catch (Exception e)
                        {
                            policyFound = false;
                        }
                    }
                }
            }

            decimal applicableDiscountPercentage = 1m;

            foreach (var childGroup in groupedByChild)
            {
                var enrollments = childGroup.Enrollments;
                decimal childFee = 0m;
                decimal childDayCareFee = 0m;

                if (i == 2 && policyFound)// we have multiple children
                {
                    applicableDiscountPercentage = policy.SecondChildFee / 100m;
                }
                else if (i == 3 && policyFound)// we have multiple children
                {
                    applicableDiscountPercentage = policy.ThirdChildFee / 100m;
                }
                else if (i > 3 && policyFound)// we have multiple children
                {
                    applicableDiscountPercentage = policy.FourthAndOnwardChildFee / 100m;
                }

                foreach (var enrollment in enrollments)
                {
                    var enrollmentGroup = course.CourseEnrollmentGroups.FirstOrDefault(g => g.CourseEnrollmentGroupId == enrollment.CourseEnrollmentGroupId);

                    if (enrollmentGroup != null && enrollment.EnrollmentStatus == EnrollmentStatus.Registered)// Only allowed registered ones
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
            addStudentCourseTransaction.RegistrationStatus = familyTransaction.RegistrationStatus;
            addStudentCourseTransaction.IsActive = familyTransaction.IsActive;
            addStudentCourseTransaction.FeeAmountDiscount = familyTransaction.FeeAmountDiscount;
            addStudentCourseTransaction.DayCareDiscount = familyTransaction.DayCareDiscount;
            addStudentCourseTransaction.DayCareFee = dayCareFee;
            addStudentCourseTransaction.PayableFee = courseFee;
            addStudentCourseTransaction.TotalAmountPaid = familyTransaction.TotalAmountPaid;
            var recalculatedTotalPayable = (addStudentCourseTransaction.PayableFee + addStudentCourseTransaction.DayCareFee + course.RegistrationFee) -
                (addStudentCourseTransaction.FeeAmountDiscount + addStudentCourseTransaction.DayCareDiscount);
            addStudentCourseTransaction.TotalPayable = recalculatedTotalPayable < 0m ? 0m : recalculatedTotalPayable;
            addStudentCourseTransaction.Comments = familyTransaction.Comments +$"\n Updated the transaction on {DateTime.UtcNow.ToString()}";
            addStudentCourseTransaction.IsCompletelyPaid = addStudentCourseTransaction.TotalPayable <= addStudentCourseTransaction.TotalAmountPaid;

            return await _studentCourseTransactionService.UpdateTransaction(familyTransaction.StudentCourseTransactionId, addStudentCourseTransaction).ConfigureAwait(false);
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
                addStudentCourseTransaction.RegistrationStatus = RegistrationStatus.Pending;
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

            var response = await _repository.UpdateEnrollment(enrollmentId, enrollment).ConfigureAwait(false);

            //if previous state was registered and its deleted, then we have to set the isregistered to true if the isregistered is false 
            // it means we have a room now
            if (enrollmentDetails != null && enrollmentDetails.EnrollmentStatus == EnrollmentStatus.Registered)
            {
                var enrollmentGroup = courseDetails.CourseEnrollmentGroups.FirstOrDefault(x => x.CourseEnrollmentGroupId == enrollmentDetails.CourseEnrollmentGroupId);
                if (!enrollmentGroup.IfRegistrationOpen)
                {
                    await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroup.CourseEnrollmentGroupId, true).ConfigureAwait(false);
                }
            }

            if (response && enrollment.EnrollmentStatus != enrollmentStatus)
            {
                await RecalculateCourseFee(enrollmentDetails.CourseId, enrollmentDetails.FamilyId).ConfigureAwait(false);
            }

            return response;
        }
        public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false, bool ifDeletedByAdmin = false)
        {
            var enrollmentDetails = await _repository.GetEnrollment(enrollmentId).ConfigureAwait(false);
            enrollmentDetails.EnrollmentStatus = EnrollmentStatus.Cancelled;

            if (enrollmentDetails == null)
            {
                return false;
            }
          
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

            if (ifDeleted && ifEnrollmentDeleted)
            {
                if (familyTransaction.Enrollments.Count <= 1)
                {
                    var ifFeePaid = familyTransaction.TotalAmountPaid > 0;
                    return await _studentCourseTransactionService.DeleteTransaction(familyTransaction.StudentCourseTransactionId, !ifFeePaid).ConfigureAwait(false);
                }
                else
                {
                    return await RecalculateCourseFee(courseId, familyId).ConfigureAwait(false);
                }
            }

            //if previous state was registered and its deleted, then we have to set the isregistered to true if the isregistered is false 
            // it means we have a room now
            if (enrollmentGroup != null && enrollmentDetails.EnrollmentStatus == EnrollmentStatus.Registered && !enrollmentGroup.IfRegistrationOpen)
            {
                await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(enrollmentGroup.CourseEnrollmentGroupId, true).ConfigureAwait(false);
            }

            if (ifDeleted)
            {
                ifEnrollmentDeleted = await _repository.DeleteEnrollment(enrollmentId, hardDelete).ConfigureAwait(false);
            }

            return false;

        }
        public Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
            => _repository.GetStudentCourseEnrollment(childId, courseId);

        public Task<IEnumerable<CourseEnrollmentGroupInformationResponse>> GetCourseEnrollmentGroupsInformation(Guid courseId)
            => _repository.GetCourseEnrollmentGroupsInformation(courseId);

        public Task<CourseEnrollmentGroupInformationResponse?> GetCourseEnrollmentGroupInformation(Guid courseGroupId)
            => _repository.GetCourseEnrollmentGroupInformation(courseGroupId);
    }
}
