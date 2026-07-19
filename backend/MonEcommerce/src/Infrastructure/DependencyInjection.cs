using System.Text;
using CloudinaryDotNet;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Data.Interceptors;
using MonEcommerce.Infrastructure.ExternalServices;
using MonEcommerce.Infrastructure.Identity;
using AppIdentityService = MonEcommerce.Infrastructure.Identity.IdentityService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SendGrid;
using StackExchange.Redis;
using Stripe;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        // JWT
        var jwtSecret = builder.Configuration["Jwt:Secret"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Every validator and client-side form in this app only ever promises "at least 8
        // characters" — relax Identity's compiled-in defaults (which otherwise silently
        // require a digit, upper/lowercase, and a symbol) to match what's actually advertised.
        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
        });

        // AC: reset-password tokens are valid for 1 hour.
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, AppIdentityService>();
        builder.Services.AddTransient<IJwtService, JwtService>();
        builder.Services.AddTransient<IAuthService, AuthService>();

        // Cloudinary
        var cloudinaryUrl = builder.Configuration["Cloudinary:Url"];
        if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
        {
            builder.Services.AddSingleton(new Cloudinary(cloudinaryUrl) { Api = { Secure = true } });
            builder.Services.AddTransient<IFileStorageService, CloudinaryFileStorageService>();
        }

        // Redis
        var redisConnection = builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
            builder.Services.AddTransient<ICacheService, RedisCacheService>();
        }

        // SendGrid
        var sendGridKey = builder.Configuration["SendGrid:ApiKey"];
        if (!string.IsNullOrWhiteSpace(sendGridKey))
        {
            builder.Services.AddTransient<ISendGridClient>(_ => new SendGridClient(sendGridKey));
            builder.Services.AddTransient<IEmailService, SendGridEmailService>();
        }

        // Stripe
        var stripeKey = builder.Configuration["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(stripeKey))
        {
            StripeConfiguration.ApiKey = stripeKey;
            builder.Services.AddTransient<PaymentIntentService>();
            builder.Services.AddTransient<RefundService>();
            builder.Services.AddTransient<IPaymentService, StripePaymentService>();
        }
    }
}
