using Domain;

namespace Api.MiddleWares;

public class TransactionMiddleware(RequestDelegate next)
{
    readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IUnitOfWork unitOfWork)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method))

        {
            await unitOfWork.BeginTransaction();

            try
            {
                await _next(context);
                await unitOfWork.CommitTransaction();
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransaction();
                throw;
            }
        }
        else
        {
            await _next(context);
        }
    }
}
