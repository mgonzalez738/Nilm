using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;

namespace EnergyMetersService.Api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (contextFeature != null)
                {
                    // 1. Forzamos el estándar Problem Details
                    context.Response.ContentType = "application/problem+json";
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    var exception = contextFeature.Error;

                    var problemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                        Title = "Internal Server Error",
                        Instance = context.Request.Path,

                        Detail = env.IsDevelopment()
                            ? exception.ToString() 
                            : "An unexpected error occurred while processing the request." 
                    };

                    await context.Response.WriteAsJsonAsync(problemDetails);
                }
            });
        });

        return app;
    }
}