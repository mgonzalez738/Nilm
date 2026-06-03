using EnergyMetersService.Api.Authentication;
using EnergyMetersService.Api.Services;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Application.Services;
using EnergyMetersService.Application.Validators;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using EnergyMetersService.Infraestructure.Data.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;

namespace EnergyMetersService.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

            builder.Services.AddAuthentication("HeadersScheme")
                            .AddScheme<AuthenticationSchemeOptions, HeadersAuthHandler>("HeadersScheme", null);

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserContext, ApiUserContext>();

            builder.Services.AddScoped<IValidator<CompanyCreateDto>, CompanyCreateDtoValidator>();
            builder.Services.AddScoped<IValidator<CompanyUpdateDto>, CompanyUpdateDtoValidator>();

            builder.Services.AddScoped<ICompanyService, CompanyService>();

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
