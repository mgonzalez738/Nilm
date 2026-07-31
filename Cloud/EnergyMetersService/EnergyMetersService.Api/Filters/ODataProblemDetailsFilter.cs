using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace EnergyMetersService.Api.Filters;

public class ODataProblemDetailsFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult result && result.StatusCode == 400)
        {
            var jsonString = JsonSerializer.Serialize(result.Value);

            if (jsonString.Contains("ODataException") || jsonString.Contains("OData"))
            {
                string detailMessage = "There was a syntax error in your OData query.";

                try
                {
                    using var document = JsonDocument.Parse(jsonString);
                    if (document.RootElement.TryGetProperty("ExceptionMessage", out var excMsg))
                    {
                        detailMessage = excMsg.GetString() ?? detailMessage;
                    }
                    else if (document.RootElement.TryGetProperty("message", out var msg))
                    {
                        detailMessage = msg.GetString() ?? detailMessage;
                    }
                }
                catch { }

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                    Title = "Invalid OData Query",
                    Detail = detailMessage,
                    Instance = context.HttpContext.Request.Path
                };

                context.Result = new ObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" },
                    StatusCode = 400
                };
            }
        }
    }
}