using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Net.Mime;
using System.Reflection;
using Api;
using Api.Hubs;

namespace Web;

public class Startup {

    public IConfiguration Configuration { get; }
    private const string DefaultCorsPolicyName = "localhost";

    public Startup(IWebHostEnvironment env)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
#if Development
                .AddJsonFile($"appsettings.Development.json", optional: true)
#endif
            .AddEnvironmentVariables();
        Configuration = builder.Build();
    }

    public void ConfigureServices(IServiceCollection services) {
        services.AddCors(
            options => options.AddPolicy(
                DefaultCorsPolicyName,
                builder => builder
                    .SetIsOriginAllowed(x => x.Length != 0)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
            )
        );

        services.AddControllers().ConfigureApiBehaviorOptions(option => {
            option.InvalidModelStateResponseFactory = context => {
                var result = new BadRequestObjectResult(context.ModelState);
                result.ContentTypes.Add(MediaTypeNames.Application.Json);
                result.ContentTypes.Add(MediaTypeNames.Application.Xml);
                return result;
            };
        }).AddApplicationPart(Assembly.Load("Api"));

        services.AddApi();

        services.AddSwaggerGen(c => {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "APIs", Version = "v1" });
        });

        services.AddSignalR();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
        if (env.IsDevelopment()) {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "APIs v1"));
        }
        app.UseCors(DefaultCorsPolicyName);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
            endpoints.MapHub<PriceHub>("/priceHub");
        });
    }
}