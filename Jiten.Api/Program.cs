using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Jiten.Api.Helpers;
using Jiten.Api.Jobs;
using Jiten.Api.Services;
using Jiten.Core;
using Jiten.Core.Data.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine(Environment.CurrentDirectory, "..", "Shared", "sharedsettings.json"), optional: true,
                                  reloadOnChange: true);
builder.Configuration.AddJsonFile("sharedsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization
                                                         .JsonNumberHandling.AllowNamedFloatingPointLiterals;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

builder.Services.AddDbContext<JitenDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("JitenDatabase"),
                                                                           o =>
                                                                           {
                                                                               o.UseQuerySplittingBehavior(QuerySplittingBehavior
                                                                                   .SplitQuery);
                                                                           }));

// Authentication

builder.Services.AddDbContext<UserDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("JitenDatabase"),
                                                                          o =>
                                                                          {
                                                                              o.UseQuerySplittingBehavior(QuerySplittingBehavior
                                                                                  .SplitQuery);
                                                                          }));

builder.Services.AddIdentity<User, IdentityRole>(options =>
       {
           // Password settings
           options.Password.RequireDigit = true;
           options.Password.RequireLowercase = true;
           options.Password.RequireUppercase = true;
           options.Password.RequiredLength = 10;

           // Lockout settings
           options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
           options.Lockout.MaxFailedAccessAttempts = 3;
           options.Lockout.AllowedForNewUsers = true;

           // User settings
           options.User.RequireUniqueEmail = true;
           options.SignIn.RequireConfirmedEmail = true;

           // Token Provider for 2FA
           // options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
       })
       .AddEntityFrameworkStores<UserDbContext>()
       .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"];
if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
{
    throw new
        InvalidOperationException("JWT Secret Key is not configured or is too short. It must be at least 32 characters long for HS256.");
}

var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
       {
           options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
           options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
       })
       .AddJwtBearer(options =>
       {
           options.SaveToken = true;
           options.RequireHttpsMetadata = builder.Environment.IsProduction();
           options.TokenValidationParameters = new TokenValidationParameters
                                               {
                                                   ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                                                   ValidateIssuerSigningKey = true, ValidIssuer = jwtSettings["Issuer"],
                                                   ValidAudience = jwtSettings["Audience"],
                                                   IssuerSigningKey = new SymmetricSecurityKey(key), ClockSkew = TimeSpan.Zero
                                               };
       });
// TODO: Later, for Google Login:
// .AddGoogle(options =>
// {
//     options.ClientId = configuration["Authentication:Google:ClientId"];
//     options.ClientSecret = configuration["Authentication:Google:ClientSecret"];
// });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequiresAdmin", policy => policy.RequireRole(nameof(UserRole.Administrator)));
});

builder.Services.AddScoped<TokenService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("fixed", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
                                                        context.Connection.RemoteIpAddress?.ToString() ??
                                                        context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                                                        "unknown",
                                                        _ => new FixedWindowRateLimiterOptions
                                                             {
                                                                 PermitLimit = 120, Window = TimeSpan.FromSeconds(60),
                                                                 QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 3,
                                                                 AutoReplenishment = true
                                                             });
    });

    options.AddPolicy("download", context =>
    {
        return RateLimitPartition.GetSlidingWindowLimiter(
                                                          context.Connection.RemoteIpAddress?.ToString() ??
                                                          context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                                                          "unknown",
                                                          _ => new SlidingWindowRateLimiterOptions
                                                               {
                                                                   PermitLimit = 5, Window = TimeSpan.FromSeconds(60),
                                                                   SegmentsPerWindow = 10,
                                                                   QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 2,
                                                                   AutoReplenishment = true
                                                               });
    });

    options.OnRejected = async (context, token) =>
    {
        var origin = context.HttpContext.Request.Headers.Origin.FirstOrDefault();
        var allowedOrigins = new[] { "https://localhost:3000", "https://jiten.moe" };

        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
        {
            context.HttpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        }

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});

builder.Services.AddResponseCaching();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowSpecificOrigin",
                      policy =>
                      {
                          policy.WithOrigins("https://localhost:3000",
                                             "https://jiten.moe")
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// Hangfire jobs
builder.Services.AddScoped<ParseJob>();
builder.Services.AddScoped<ReparseJob>();
builder.Services.AddScoped<ComputationJob>();

builder.Services.AddHangfire(configuration =>
                                 configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                                              .UseSimpleAssemblyNameTypeSerializer()
                                              .UseRecommendedSerializerSettings()
                                              .UsePostgreSqlStorage((options) =>
                                                                        options.UseNpgsqlConnection(() => builder.Configuration
                                                                            .GetConnectionString("JitenDatabase"))));

builder.Services.AddHangfireServer((options) => { options.WorkerCount = Environment.ProcessorCount / 4; });


var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
                        {
                            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                            KnownNetworks = { }, // clears default for reverse proxy
                            KnownProxies = { }
                        });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
        c.RoutePrefix = "";
    });
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("AllowSpecificOrigin");

app.UseResponseCaching();

app.UseRateLimiter();

app.UseStaticFiles();

bool.TryParse(app.Configuration["UseBunnyCdn"], out var useBunnyCdn);
if (useBunnyCdn)
{
    //
}
else
{
    app.UseStaticFiles(new StaticFileOptions
                       {
                           FileProvider =
                               new PhysicalFileProvider(app.Configuration["StaticFilesPath"] ??
                                                        throw new Exception("Please set the StaticFilesPath in appsettings.json")),
                           RequestPath = "/static"
                       });
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions() { Authorization = [new HangfireAuthorizationFilter(app.Configuration)] });

app.MapSwagger();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHangfireDashboard();

app.Run();