using Microsoft.EntityFrameworkCore;
using SessionHandler.Data;
using SessionHandler.Interfaces;
using SessionHandler.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Session persistence (EF Core + SQLite).
builder.Services.AddDbContext<SessionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SessionDb")));
builder.Services.AddScoped<ISessionRepository, SessionRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "SessionHandler v1");
        options.RoutePrefix = "api";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();