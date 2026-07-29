using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Notifications;

namespace ReeTrack.Infrastructure.Notifications;

/// <summary>
/// Registers all <see cref="IDomainEventHandler{TEvent}"/> implementations found in an assembly.
/// </summary>
public static class DomainEventHandlerRegistration
{
    public static IServiceCollection AddDomainEventHandlers(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerOpenType = typeof(IDomainEventHandler<>);

        var implementations = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => new
            {
                Implementation = t,
                HandlerInterfaces = t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerOpenType)
                    .ToArray()
            })
            .Where(x => x.HandlerInterfaces.Length > 0);

        foreach (var item in implementations)
        {
            services.AddTransient(item.Implementation);

            foreach (var handlerInterface in item.HandlerInterfaces)
                services.AddTransient(handlerInterface, item.Implementation);
        }

        return services;
    }
}
