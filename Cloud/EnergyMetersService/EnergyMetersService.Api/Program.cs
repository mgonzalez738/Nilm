using EnergyMetersService.Api.Authentication;
using EnergyMetersService.Api.Filters;
using EnergyMetersService.Api.Services;
using EnergyMetersService.Application.Constants;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Application.Mappings;
using EnergyMetersService.Application.Services;
using EnergyMetersService.Application.Validators;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using EnergyMetersService.Infraestructure.Data.Repositories;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using MongoDB.Bson.Serialization;
using System.Reflection;
using System.Text.Json.Serialization;
using EnergyMetersService.Api.Extensions;

namespace EnergyMetersService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddProblemDetails();

            builder.Services.AddControllers(options =>
            {
                //options.Filters.Add<ODataProblemDetailsFilter>();
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            }).AddOData(options =>
            {
                options.Select()
                       .Expand()
                       .Filter()
                       .OrderBy()
                       .Count()
                       .SetMaxTop(100);
                var modelBuilder = new ODataConventionModelBuilder();
                modelBuilder.EntitySet<CompanyDto>(nameof(Company));
                modelBuilder.EntitySet<ProjectDto>(nameof(Project));
                options.AddRouteComponents("api", modelBuilder.GetEdmModel());
            });

            var config = TypeAdapterConfig.GlobalSettings;
            config.Default.IgnoreNullValues(true);
            config.Scan(Assembly.GetAssembly(typeof(MappingConfig))!);
            builder.Services.AddSingleton(config);
            builder.Services.AddScoped<IMapper, ServiceMapper>();

            builder.Services.AddAuthentication("HeadersScheme")
                            .AddScheme<AuthenticationSchemeOptions, HeadersAuthHandler>("HeadersScheme", null);

            builder.Services.AddAuthorizationBuilder()
                            .AddPolicy("RequireAccessRoles", policy =>
                                   policy.RequireRole(AppRoles.Super, AppRoles.Admin, AppRoles.User, AppRoles.Viewer)
                            );

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserContext, ApiUserContext>();

            builder.Services.AddScoped<IValidator<CompanyCreateDto>, CompanyCreateDtoValidator>();
            builder.Services.AddScoped<IValidator<CompanyUpdateDto>, CompanyUpdateDtoValidator>();
            builder.Services.AddScoped<IValidator<ProjectCreateDto>, ProjectCreateDtoValidator>();
            builder.Services.AddScoped<IValidator<ProjectUpdateDto>, ProjectUpdateDtoValidator>();

            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();

            builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
            builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

            builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
            builder.Services.AddSingleton<MongoDbContext>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseGlobalExceptionHandler();

            app.UseStatusCodePages();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
