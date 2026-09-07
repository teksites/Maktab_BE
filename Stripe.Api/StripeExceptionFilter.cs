using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Stripe.Contracts;

namespace Stripe.Api;

public sealed class StripeExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not StripeIntegrationException exception) return;

        context.Result = new ObjectResult(new StripeProviderErrorResponse
        {
            Success = false,
            Error = new StripeProviderErrorDetail
            {
                Code = exception.Code ?? "stripe_error",
                Message = exception.Message,
                Type = exception.ErrorType,
                DeclineCode = exception.DeclineCode,
                Retryable = exception.IsUpstreamFailure
            }
        }) { StatusCode = exception.IsUpstreamFailure ? 502 : 400 };
        context.ExceptionHandled = true;
    }
}
