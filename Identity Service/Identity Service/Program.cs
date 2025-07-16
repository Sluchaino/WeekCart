using Application.Interfaces;
using Application.Validators;
using Domain.Entities;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// 1) DB
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2) Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(o =>
       o.SignIn.RequireConfirmedEmail = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 3) JWT
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication("Bearer")
   .AddJwtBearer("Bearer", o =>
   {
       var cfg = builder.Configuration.GetSection("Jwt");
       o.TokenValidationParameters = new()
       {
           ValidIssuer = cfg["Issuer"],
           ValidAudience = cfg["Audience"],
           IssuerSigningKey = new SymmetricSecurityKey(
               Encoding.UTF8.GetBytes(cfg["Key"]!)),
           ClockSkew = TimeSpan.Zero,
           ValidateLifetime = true,
           NameClaimType = ClaimTypes.NameIdentifier
       };
   });

builder.Services.AddControllers()
                .AddFluentValidation(cfg =>
                    cfg.RegisterValidatorsFromAssemblyContaining<RegisterValidator>());

// либо новая API 11-й версии:
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

builder.Services.AddScoped<ITokenService, TokenService>();



builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity Service API", Version = "v1" });

    // 1. Добавляем определение схемы безопасности
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Добавляем требование безопасности
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});


var app = builder.Build();
app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
