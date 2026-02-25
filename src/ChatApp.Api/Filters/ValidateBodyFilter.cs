using ChatApp.Api.Common;
using ChatApp.Application.Common;
using FluentValidation;

namespace ChatApp.Api.Filters;

public sealed class ValidateBodyFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null) return await next(ctx);

        var body = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (body is null) return await next(ctx);

        var result = await validator.ValidateAsync(body, ctx.HttpContext.RequestAborted);
        if (result.IsValid) return await next(ctx);

        var dict = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());

        var error = new AppError(
            Code: "validation.failed",
            Message: "Validation failed.",
            Type: ErrorType.Validation,
            FieldErrors: dict);

        return error.ToHttp(ctx.HttpContext);
    }
}