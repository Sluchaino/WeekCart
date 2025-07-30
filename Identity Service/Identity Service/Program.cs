using Application.Interfaces;
using Application.Validators;
using Domain.Entities;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) DB
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2) Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(o => o.SignIn.RequireConfirmedEmail = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 3) JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var cfg = builder.Configuration.GetSection(JwtOptions.SectionName);
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = cfg["Issuer"],
            ValidAudience = cfg["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Key"]!)),
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.NameIdentifier, // ← стандарт
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// 4) MVC + FV
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

// 5) DI
builder.Services.AddScoped<ITokenService, TokenService>();

// 6) Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Id = "Bearer", Type = ReferenceType.SecurityScheme } },
            Array.Empty<string>()
        }
    });
});

// 7) CORS (при необходимости)
builder.Services.AddCors(p => p.AddPolicy("AllowAll", b => b.AllowAnyOrigin()
                                                           .AllowAnyMethod()
                                                           .AllowAnyHeader()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// роли USER / ADMIN создаём один раз
using (var scope = app.Services.CreateScope())
{
    var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var r in new[] { "USER", "ADMIN" })
        if (!await rm.RoleExistsAsync(r))
            await rm.CreateAsync(new IdentityRole<Guid>(r));
}

app.Run();