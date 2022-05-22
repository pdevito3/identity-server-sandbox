namespace identity_template_example;

using Duende.IdentityServer;
using Duende.IdentityServer.EntityFramework.DbContexts;
using identity_template_example.Data;
using identity_template_example.Models;
using is_with_ef.Pages.Admin.ApiScopes;
using is_with_ef.Pages.Admin.Clients;
using is_with_ef.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serilog;


internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            var identityConnectionString = GetIdentityConnectionString(builder);
            options.UseNpgsql(identityConnectionString,
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention();
        });
        
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/
                options.EmitStaticAudienceClaim = true;
            })
            // this adds the config data from DB (clients, resources, CORS)
            .AddConfigurationStore(options =>
            {
                var configConnectionString = GetConfigConnectionString(builder);
                options.ConfigureDbContext = configBuilder =>
                    configBuilder.UseNpgsql(configConnectionString,
                    configOptions => configOptions.MigrationsAssembly(typeof(ConfigurationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention();
            })
            // caching reduces load on and requests to the DB
            .AddConfigurationStoreCache()
            // this adds the operational data from DB (codes, tokens, consents)
            .AddOperationalStore(options =>
            {
                var grantsConnectionString = GetGrantsConnectionString(builder);
                options.ConfigureDbContext = grantsBuilder =>
                    grantsBuilder.UseNpgsql(grantsConnectionString,
                            grantsOptions => grantsOptions.MigrationsAssembly(typeof(PersistedGrantDbContext).Assembly.FullName))
                        .UseSnakeCaseNamingConvention();

                // this enables automatic token cleanup. this is optional.
                options.EnableTokenCleanup = true;
                options.RemoveConsumedTokens = true;
            })
            .AddAspNetIdentity<ApplicationUser>();
        
        builder.Services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                // register your IdentityServer with Google at https://console.developers.google.com
                // enable the Google+ API
                // set the redirect URI to https://localhost:5001/signin-google
                options.ClientId = "copy client ID from Google here";
                options.ClientSecret = "copy client secret from Google here";
            });

        // TODO update with heimguard
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("admin",
                policy => policy.RequireClaim("sub", "1"))
        );
        
        builder.Services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AuthorizeFolder("/Admin", "admin"));

        builder.Services.AddTransient<ClientRepository>();
        builder.Services.AddTransient<IdentityScopeRepository>();
        builder.Services.AddTransient<ApiScopeRepository>();
        
        return builder.Build();
    }

    private static string GetIdentityConnectionString(WebApplicationBuilder builder)
    {
        var identityConnectionString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION_STRING");
        if (string.IsNullOrEmpty(identityConnectionString))
        {
            // this makes local migrations easier to manage. feel free to refactor if desired.
            identityConnectionString = builder.Environment.IsDevelopment()
                ? "Host=localhost;Port=33674;Database=auth-identity-db;Username=postgres;Password=postgres"
                : throw new Exception("IDENTITY_DB_CONNECTION_STRING environment variable is not set.");
        }

        return identityConnectionString;
    }

    private static string GetConfigConnectionString(WebApplicationBuilder builder)
    {
        var configConnectionString = Environment.GetEnvironmentVariable("CONFIG_DB_CONNECTION_STRING");
        if (string.IsNullOrEmpty(configConnectionString))
        {
            // this makes local migrations easier to manage. feel free to refactor if desired.
            configConnectionString = builder.Environment.IsDevelopment()
                ? "Host=localhost;Port=33674;Database=auth-config-db;Username=postgres;Password=postgres"
                : throw new Exception("CONFIG_DB_CONNECTION_STRING environment variable is not set.");
        }

        return configConnectionString;
    }

    private static string GetGrantsConnectionString(WebApplicationBuilder builder)
    {
        var grantsConnectionString = Environment.GetEnvironmentVariable("GRANTS_DB_CONNECTION_STRING");
        if (string.IsNullOrEmpty(grantsConnectionString))
        {
            // this makes local migrations easier to manage. feel free to refactor if desired.
            grantsConnectionString = builder.Environment.IsDevelopment()
                ? "Host=localhost;Port=33674;Database=auth-grants-db;Username=postgres;Password=postgres"
                : throw new Exception("GRANTS_DB_CONNECTION_STRING environment variable is not set.");
        }

        return grantsConnectionString;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    { 
        app.UseSerilogRequestLogging();
    
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();
        
        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}