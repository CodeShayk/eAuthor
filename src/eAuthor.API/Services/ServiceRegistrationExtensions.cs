namespace eAuthor.Services;

public static class ServiceRegistrationExtensions {
    public static IServiceCollection AddTemplatingCore(this IServiceCollection services) {
        services.AddScoped<IRepeaterBlockProcessor, RepeaterBlockProcessor>();
        return services;
    }
}