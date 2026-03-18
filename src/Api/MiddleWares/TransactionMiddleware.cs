using Api.BackgroundServices;
using Domain;

namespace Api.MiddleWares;

public class TransactionMiddleware(RequestDelegate next)
{
    readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IUnitOfWork unitOfWork,
        IBackgroundJobTrigger backgroundJobTrigger)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method))

        {
            await unitOfWork.BeginTransaction();

            try
            {
                await _next(context);

                var shouldTrigger = unitOfWork.HasBackgroundJobs();

                await unitOfWork.CommitTransaction();

                if (shouldTrigger)
                {
                    // Signal the background service to process new jobs immediately.
                    backgroundJobTrigger.Trigger();
                }
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
