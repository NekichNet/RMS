using Business.Database;
using Business.Logging.Extensions;
using Business.Models;
using Business.Services;
using Business.Services.Interfaces;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;


namespace Business
{
    public class Program
    {
		private static WebApplication _app;

        public static void Main(string[] args)
        {
            Env.Load(".env");

            var connectionString = Env.GetString("DB_CONNECTION_STRING")
				?? throw new InvalidOperationException("Connection string not configured.");

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy =>
                    {
                        policy.WithOrigins("http://localhost:3000")  // Your frontend URL
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
            });

            builder.Logging.ClearProviders();
			builder.Logging.AddCustomConsole();
			builder.Logging.AddFile();

			string secretKeyString = Env.GetString("JWT_SECRET_STRING");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKeyString));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            /*
            var jwtSecret = Env.GetString("JWT_SECRET")
                ?? builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("JWT Secret is not configured");
			var key = Encoding.ASCII.GetBytes(jwtSecret);
            */

            var jwtIssuer = builder.Configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("'Jwt:Issuer' не настроен в appsettings.json");

            var jwtAudience = builder.Configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("'Jwt:Audience' не настроен в appsettings.json");

            var jwtExpiryString = Env.GetString("JWT_EXPIRY_MINUTES")
                ?? builder.Configuration["Jwt:ExpiryMinutes"]
                ?? "60";
            var jwtExpiryMinutes = int.Parse(jwtExpiryString);

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
                    IssuerSigningKey = new SymmetricSecurityKey(key.Key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddDbContext<AppDbContext>(options =>
				options.UseNpgsql(connectionString));

            builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            // И явная регистрация нетипизированного ILogger
            builder.Services.AddSingleton<ILogger>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("Global"));

            builder.Services.AddTransient<PostService>();
            builder.Services.AddTransient<RankService>();
            builder.Services.AddTransient<RewardService>();
            builder.Services.AddTransient<StructureService>();
            builder.Services.AddTransient<SubdivisionService>();
			builder.Services.AddTransient<UnitService>();
            builder.Services.AddTransient<DocService>();
            builder.Services.AddTransient<UnitPermissionService>();
            builder.Services.AddTransient<RankPermissionService>();

			builder.Services.AddHttpClient();
            builder.Services.AddScoped<IDiscordOAuthService, DiscordOAuthService>();
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.WriteAsString;
                });

            _app = builder.Build();
            _app.UseCors("AllowFrontend");
            _app.UseAuthentication();
            _app.UseAuthorization();


            #region RequestDebugging
            _app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next();
            });
            #endregion RequestDebugging

            _app.UseStaticFiles();

            _app.Use(async (context, next) =>
            {
                bool userExistsInDb = false;

                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var discordIdClaim = context.User.FindFirst("discord_id")?.Value
                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (!string.IsNullOrEmpty(discordIdClaim) && ulong.TryParse(discordIdClaim, out var discordId))
                    {
                        var db = context.RequestServices.GetRequiredService<AppDbContext>();

                        var unit = await db.Units.FindAsync(discordId);

                        if (unit != null)
                        {
                            context.Items["Actor"] = unit;
                            userExistsInDb = true;
                        }
                        else
                        {
                            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogWarning($"Пользователь с Discord ID {discordId} аутентифицирован JWT, но отсутствует в таблице Units.");
                        }
                    }

                    if (!userExistsInDb)
                    {
                        var endpoint = context.GetEndpoint();
                        var authorizeAttribute = endpoint?.Metadata.GetMetadata<AuthorizeAttribute>();
                        var allowAnonymousAttribute = endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>();

                        if (authorizeAttribute != null && allowAnonymousAttribute == null)
                        {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Forbidden",
                                message = "Доступ ограничен. Ваш Discord ID отсутствует в базе данных клана."
                            });
                            return;
                        }
                    }
                }

                await next();
            });

            _app.MapControllers();
            _app.Run();
		}
	}
}
