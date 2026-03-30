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
var serviceBusConnection = builder.Configuration["ServiceBus:ConnectionString"];
if (!string.IsNullOrWhiteSpace(serviceBusConnection))
{
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
    builder.Services.AddScoped<IMessagePublisher, ServiceBusMessagePublisher>();
}
else
{
    // Fallback for local development — no Service Bus configured
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowFrontend");
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