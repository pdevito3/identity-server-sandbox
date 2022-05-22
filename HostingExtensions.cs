namespace identity_template_example;

using Duende.IdentityServer;
using identity_template_example.Data;
using identity_template_example.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;


internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        
        // DbContext -- Do Not Delete
        // if (env.IsEnvironment(LocalConfig.FunctionalTestingEnvName))
        // {
        //     services.AddDbContext<RecipesDbContext>(options =>
        //         options.UseInMemoryDatabase($"RecipeManagement"));
        // }
        // else
        // {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if(string.IsNullOrEmpty(connectionString))
        {
            // this makes local migrations easier to manage. feel free to refactor if desired.
            connectionString = builder.Environment.IsDevelopment() 
                ? "Host=localhost;Port=33674;Database=dev_recipemanagement;Username=postgres;Password=postgres"
                : throw new Exception("DB_CONNECTION_STRING environment variable is not set.");
        }

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention());
        // }

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
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryClients(Config.Clients)
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

        return builder.Build();
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