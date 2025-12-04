using Application.Infrastructure;
using Application.Repositories;
using Application.Services;
using Core.Interfaces.Infrastructure;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api;

public static class ApplicationServiceCollections {
    public static IServiceCollection AddApi(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        // Register repositories
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();

        // Register services
        services.AddSingleton<IExternalPriceProvider, BinancePriceProvider>();
        services.AddSingleton<IPriceService, PriceService>();
        services.AddSingleton<IPriceSubscriptionManager, PriceSubscriptionManager>();

        // Register use cases
        services.AddScoped<Application.UseCases.GetInstrumentsUseCase>();
        services.AddScoped<Application.UseCases.GetCurrentPriceUseCase>();

        // Register SignalR hub
        services.AddSignalR();

        // Register background service for price broadcasting
        services.AddHostedService<Hubs.PriceBroadcastService>();

        return services;
    }
}
