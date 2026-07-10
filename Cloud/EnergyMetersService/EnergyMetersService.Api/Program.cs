using EnergyMetersService.Api.Authentication;
using EnergyMetersService.Api.Services;
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

namespace EnergyMetersService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            if (!BsonClassMap.IsClassMapRegistered(typeof(SmartPlugSensorDto)))
            {
                BsonClassMap.RegisterClassMap<SmartPlugSensorDto>(cm =>
                {
                    cm.AutoMap(); // Le dice a Mongo que mapee las propiedades por nombre automáticamente
                    cm.SetIgnoreExtraElements(true); // Evita errores si Mongo trae campos que el DTO no tiene
                });
            }

            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetAssembly(typeof(MappingConfig))!);
            builder.Services.AddSingleton(config);
            builder.Services.AddScoped<IMapper, ServiceMapper>();

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                })
                .AddOData(options =>
                {
                    options.Select()
                            .Filter()
                            .OrderBy()
                            .SetMaxTop(100)
                            .Count()
                            .Expand();
                    var modelBuilder = new ODataConventionModelBuilder();
                    modelBuilder.EntitySet<CompanyDto>(nameof(Company));
                    modelBuilder.EntitySet<SmartPlugSensorDto>(nameof(SmartPlugSensor));
                    options.AddRouteComponents("api", modelBuilder.GetEdmModel());
                });

            builder.Services.AddAuthentication("HeadersScheme")
                            .AddScheme<AuthenticationSchemeOptions, HeadersAuthHandler>("HeadersScheme", null);

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserContext, ApiUserContext>();

            builder.Services.AddScoped<IValidator<CompanyCreateDto>, CompanyCreateDtoValidator>();
            builder.Services.AddScoped<IValidator<CompanyUpdateDto>, CompanyUpdateDtoValidator>();
            builder.Services.AddScoped<IValidator<SmartPlugSensorCreateDto>, SmartPlugSensorCreateDtoValidator>();
            builder.Services.AddScoped<IValidator<SmartPlugSensorUpdateDto>, SmartPlugSensorUpdateDtoValidator>();

            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<ISmartPlugSensorService, SmartPlugSensorService>();

            builder.Services.AddScoped(typeof(IEntityRepository<>), typeof(MongoDbEntityRepository<>));

            builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
            builder.Services.AddSingleton<MongoDbContext>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
