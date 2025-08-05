using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using System.Text;
using System.Text.Json.Serialization;
using Application.Users.Registry;
using Resiliency.Registry;
using Data.MySql.Regjstry;
using Email.Registry;
using WebMsgSender.Registry;
using Elavon.Registry;
using Maktab.Jobs;
using Maktab.Attributes;

namespace Maktab
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public async void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddJsonOptions(options =>
            { 
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                //options.JsonSerializerOptions.PropertyNamingPolicy = null;
            }
            );

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Maktab", Version = "v1" });
                c.OperationFilter<CustomHeaderSwaggerAttribute>();
                var securityScheme = new OpenApiSecurityScheme()
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT" // Optional
                };
                var securityRequirement = new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "bearerAuth"
                            }
                        },
                        new string[] {}
                    }
                };
                c.AddSecurityDefinition("bearerAuth", securityScheme);
                c.AddSecurityRequirement(securityRequirement);
            });

            services.AddCors(cors =>cors.AddPolicy("corspolicy", build => 
            {
                build.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();

            }));

            services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            }
            ).AddJwtBearer(jwtOptions =>
            {
                var key = Configuration.GetValue<string>("JwtConfig:Key");
                var keyBytes = Encoding.ASCII.GetBytes(key);

                jwtOptions.SaveToken = true;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters()
                {
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ValidateAudience = false,
                    ValidateIssuer = false
                };
            });
   
            services.AddResiliency();
            services.AddMySql();
            services.AddEmail();
            
            services.AddUserServices();
            services.AddWebMsgSender();
            services.AddElavonServices();

            services.AddQuartz(q =>
            {
                // Just use the name of your job that you created in the Jobs folder.
                var jobKey = new JobKey("updatedailyexchangeratesjob");
                q.AddJob<UpdateDailyExchangeRatesJob>(opts => opts.WithIdentity(jobKey));

                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("updatedailexchangeratestrigger")
                    .WithSimpleSchedule(a => a.WithIntervalInHours(4).RepeatForever())
                    //This Cron interval can be described as "run every minute" (when second is zero)
                    //.WithCronSchedule("0 * * ? * *")
                );
            });

            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        }

          // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Maktab v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();
      
            app.UseCors("corspolicy");
      
            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
