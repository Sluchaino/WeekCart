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
           ClockSkew = TimeSpan.Zero
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
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
