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
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// 1.  DATA: DbContext + PostgreSQL
// -----------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// -----------------------------------------------------------------------------
// 2.  IDENTITY
// -----------------------------------------------------------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(opts =>
        opts.SignIn.RequireConfirmedEmail = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// -----------------------------------------------------------------------------
// 3.  JWT authentication
// -----------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();   // <–– убираем авто-маппинг

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),

            // КРИТИЧЕСКИЕ НАСТРОЙКИ
            NameClaimType = JwtRegisteredClaimNames.Sub, // идентификатор пользователя
            RoleClaimType = ClaimTypes.Role              // роли
        };
    });

builder.Services.AddAuthorization();

// -----------------------------------------------------------------------------
// 4.  MVC + FluentValidation
// -----------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

// -----------------------------------------------------------------------------
// 5.  DI
// -----------------------------------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();

// -----------------------------------------------------------------------------
// 6.  Swagger (с Bearer-авторизацией)
// -----------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity Service API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Введите JWT в формате **Bearer <token>**",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// -----------------------------------------------------------------------------
// 7.  CORS
// -----------------------------------------------------------------------------
builder.Services.AddCors(o =>
{
    o.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// -----------------------------------------------------------------------------
// 8.  HTTP pipeline
// -----------------------------------------------------------------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();          // сначала аутентификация
app.UseAuthorization();           // затем авторизация

app.MapControllers();

// -----------------------------------------------------------------------------
// 9.  Инициализация ролей
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var roles = new[] { "USER", "ADMIN" };

    foreach (var roleName in roles)
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
}
app.Run();