using System.Text;
using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Accounts.Queries;
using BankingApi.Application.Interfaces;
using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Transactions.Queries;
using BankingApi.Infrastructure.Persistence;
using BankingApi.Infrastructure.Repositories;
using BankingApi.Infrastructure.Services;
using BankingApi.Infrastructure.Services.Messaging; 
using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Loans.Queries;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Azure.Identity;
using BankingApi.API.Middleware;
using BankingApi.Application.Loans.Services;
using Azure.Messaging.ServiceBus;
using BankingApi.Infrastructure.Services.Blob;
using BankingApi.Application.LoanDocuments.Commands;
using BankingApi.Application.LoanDocuments.Queries;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;

var builder = WebApplication.CreateBuilder(args);
// Application Insights
builder.Services.AddApplicationInsightsTelemetry();
// Key Vault — only in production (Azure)
if (builder.Environment.IsProduction())
{
    var keyVaultUri = new Uri("https://kv-banking-api.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

// Blob Storage
builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
builder.Services.AddScoped<ILoanDocumentRepository, LoanDocumentRepository>();


// Controllers + JSON enum serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Service Bus
var serviceBusNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"];
var serviceBusConnection = builder.Configuration["ServiceBus:ConnectionString"];

if (!string.IsNullOrWhiteSpace(serviceBusNamespace))
{
    // Produção — Managed Identity
    builder.Services.AddSingleton(
        new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential()));
    builder.Services.AddScoped<IMessagePublisher, ServiceBusMessagePublisher>();
}
else if (!string.IsNullOrWhiteSpace(serviceBusConnection))
{
    // Local — connection string
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
    builder.Services.AddScoped<IMessagePublisher, ServiceBusMessagePublisher>();
}
else
{
    // Sem Service Bus configurado — NullMessagePublisher
    builder.Services.AddScoped<IMessagePublisher, NullMessagePublisher>();
}

builder.Services.AddScoped<RetryAiAnalysisHandler>();


// Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Banking API", Version = "v1" });
    c.EnableAnnotations();

    // Allow sending JWT token from Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGci..."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<BankingDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200",
                            "https://brave-bay-07381f20f.6.azurestaticapps.net")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login-policy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(60);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("register-policy", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(60);
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("upload-policy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(60);
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please try again later." }, ct);
    };
});

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    options.AddPolicy("upload-timeout", new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(120)
    });

    options.AddPolicy("query-timeout", new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(15)
    });
});

// Repositories
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();


// Application handlers
builder.Services.AddScoped<LoanProfitabilityService>();
builder.Services.AddScoped<GetLoanApprovalDetailsHandler>();
builder.Services.AddScoped<CreateAccountHandler>();
builder.Services.AddScoped<GetAccountHandler>();
builder.Services.AddScoped<DepositHandler>();
builder.Services.AddScoped<WithdrawHandler>();
builder.Services.AddScoped<TransferHandler>();
builder.Services.AddScoped<GetStatementHandler>();
builder.Services.AddScoped<RequestLoanHandler>();
builder.Services.AddScoped<RequestPayrollLoanHandler>();
builder.Services.AddScoped<ApproveLoanHandler>();
builder.Services.AddScoped<RejectLoanHandler>();
builder.Services.AddScoped<CancelLoanHandler>();
builder.Services.AddScoped<GetLoanHandler>();
builder.Services.AddScoped<GetMyLoansHandler>();
builder.Services.AddScoped<GetPendingLoansHandler>();
builder.Services.AddScoped<GetAccountByOwnerHandler>();
builder.Services.AddScoped<GetDecidedLoansHandler>();
builder.Services.AddScoped<UploadLoanDocumentHandler>();
builder.Services.AddScoped<GetLoanDocumentsHandler>();
builder.Services.AddScoped<GetDocumentDownloadUriHandler>();

builder.Services.AddScoped<IIdentityService, IdentityService>();


var app = builder.Build();

// Auto-run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    db.Database.Migrate();
}

// Seed roles
await SeedRolesAsync(app);

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Seed default roles
static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
 
    string[] roles = ["Client", "Manager", "Supervisor", "CreditCommittee"];
 
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}