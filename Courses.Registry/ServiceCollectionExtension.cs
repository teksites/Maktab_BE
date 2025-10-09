using Courses.Repository.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Courses.Repository;
using Courses.Services;
using Courses.Implementation.Services;
using Courses.Services.Implementation;
using Courses.Implementation;

namespace Courses.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddCoursesServices(this IServiceCollection services)
        {
            services.AddScoped<ICourseEnrollmentGroupService, CourseEnrollmentGroupService>();
            services.AddScoped<ICourseEnrollmentGroupRepository, CourseEnrollmentGroupRepository>();
          
            services.AddScoped<ICoursePaymentService, CoursePaymentService>();
            services.AddScoped<ICoursePaymentRepository, CoursePaymentRepository>();

            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<ICourseRepository, CourseRepository>();

            services.AddScoped<IInstitutePolicyService, InstitutePolicyService>();
            services.AddScoped<IInstitutePolicyRepository, InstitutePolicyRepository>();

            services.AddScoped<IInstituteService, InstituteService>();
            services.AddScoped<IInstituteRepository, InstituteRepository>();

            services.AddScoped<IStudentCourseEnrollmentService, StudentCourseEnrollmentService>();
            services.AddScoped<IStudentCourseEnrollmentRepository, StudentCourseEnrollmentRepository>();

            services.AddScoped<IStudentCourseTransactionService, StudentCourseTransactionService>();
            services.AddScoped<IStudentCourseTransactionRepository, StudentCourseTransactionRepository>();

            services.AddScoped<ICourseReportingService, CourseReportingService>();
            services.AddScoped<IStudentCourseTransactionReportRepository, StudentCourseTransactionReportRepository>();

            return services;
        }    
    }
}
