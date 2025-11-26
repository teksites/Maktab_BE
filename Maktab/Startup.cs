using System.Text;
using System.Text.Json.Serialization;
using Application.Users.Registry;
using Courses.Registry;
using Data.MySql.Regjstry;
using Email.Registry;
using Elavon.Registry;
using Maktab.Attributes;
using Maktab.Jobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using Resiliency.Registry;
using WebMsgSender.Registry;

namespace Maktab
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // NOTE: ConfigureServices should NOT be async/void
        public void ConfigureServices(IServiceCollection services)
        {
            // MVC + JSON
            services
                .AddControllers(options =>
                {
                    // If you ever want global filters, add them here
                    // e.g. options.Filters.Add<SessionInfoFilter>();
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            // Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Maktab",
                    Version = "v1"
                });

                // We no longer use CustomHeaderSwaggerAttribute to add Session_Info
                // per endpoint. Instead we define it as a global security scheme:

                // 1) JWT Bearer
                var jwtScheme = new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization using Bearer header.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "bearerAuth"
                    }
                };

                // 2) X-Api-Key
                var apiKeyScheme = new OpenApiSecurityScheme
                {
                    Description = "API Key via X-Api-Key header.",
                    Name = "X-Api-Key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKey",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                };

                // 3) Session_Info – shown in the Authorize popup
                var sessionInfoScheme = new OpenApiSecurityScheme
                {
                    Description = "Session_Info header used to identify the user session.",
                    Name = "Session_Info",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "SessionInfo",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "SessionInfo"
                    }
                };

                c.AddSecurityDefinition("bearerAuth", jwtScheme);
                c.AddSecurityDefinition("ApiKey", apiKeyScheme);
                c.AddSecurityDefinition("SessionInfo", sessionInfoScheme);

                // All three appear in the "Authorize" dialog.
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { jwtScheme,        new string[] { } },
                    { apiKeyScheme,     new string[] { } },
                    { sessionInfoScheme,new string[] { } }
                });
            });

            // CORS
            services.AddCors(cors =>
            {
                cors.AddPolicy("corspolicy", build =>
                {
                    build.AllowAnyOrigin()
                         .AllowAnyMethod()
                         .AllowAnyHeader();
                });
            });

            // Authentication: JWT + API Key via policy scheme
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "MultiAuth";
                options.DefaultAuthenticateScheme = "MultiAuth";
                options.DefaultChallengeScheme = "MultiAuth";
            })
            .AddPolicyScheme("MultiAuth", "JWT or API Key", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // If X-Api-Key is present, use ApiKey handler; otherwise use JWT
                    if (context.Request.Headers.ContainsKey("X-Api-Key"))
                    {
                        return "ApiKey";
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
            {
                var key = Configuration.GetValue<string>("JwtConfig:Key");
                var keyBytes = Encoding.ASCII.GetBytes(key);

                jwtOptions.SaveToken = true;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ValidateAudience = false,
                    ValidateIssuer = false
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { });

            // Registrations
            services.AddResiliency();
            services.AddMySql();
            services.AddEmail();
            services.AddUserServices();
            services.AddWebMsgSender();
            services.AddElavonServices();
            services.AddCoursesServices();

            // Quartz jobs
            services.AddQuartz(q =>
            {
                var jobKey = new JobKey("updatedailyexchangeratesjob");
                q.AddJob<UpdateDailyExchangeRatesJob>(opts => opts.WithIdentity(jobKey));

                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("updatedailexchangeratestrigger")
                    .WithSimpleSchedule(a => a.WithIntervalInHours(4).RepeatForever()));
            });

            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        }

        // HTTP pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.IsProduction())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Maktab v1");
                });
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
