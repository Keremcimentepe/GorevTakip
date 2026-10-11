using FluentValidation;

namespace GorevTakip.Infrastructure;

// Endpoint filtresi: istek endpoint'in kodu çalışmadan ÖNCE buradan geçer.
// T tipindeki istek gövdesini bulur, T için yazılmış validator'ı çalıştırır;
// hata varsa 400 döner, yoksa isteği endpoint'e iletir.
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        var argument = context.Arguments.OfType<T>().FirstOrDefault();

        if (validator is not null && argument is not null)
        {
            var result = await validator.ValidateAsync(argument);

            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
