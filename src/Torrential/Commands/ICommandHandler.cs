using Microsoft.Extensions.DependencyInjection;

namespace Torrential.Commands
{
    public interface ICommandHandler<in TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        Task<TResult> Execute(TCommand command);
    }

    public static class CommandHandlerServiceCollectionExtensions
    {
        public static IServiceCollection AddCommandHandler<TCommand, TResult, TCommandHandler>(this IServiceCollection services)
            where TCommand : ICommand<TResult>
            where TCommandHandler : class, ICommandHandler<TCommand, TResult>
        {
            services.AddScoped<ICommandHandler<TCommand, TResult>, TCommandHandler>();
            return services;
        }
    }

}
