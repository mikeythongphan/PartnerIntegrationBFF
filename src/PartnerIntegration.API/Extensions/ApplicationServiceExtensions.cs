using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PartnerIntegration.Application.Behaviors;
using PartnerIntegration.Application.Commands.SubmitTransaction;

namespace PartnerIntegration.API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(SubmitTransactionCommand).Assembly;

        // MediatR: auto-registers all IRequestHandler<,> in Application assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
        });

        // MediatR Pipeline Behaviors (order matters: first registered = outermost)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // FluentValidation: auto-registers all validators in Application assembly
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }
}
