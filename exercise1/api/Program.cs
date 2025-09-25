using MediatR;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();   // shows in terminal / VS Code
builder.Logging.AddDebug(); 

// ---------- Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core + interceptor (optional, if you added it)
builder.Services.AddScoped<SqlLoggingInterceptor>();
builder.Services.AddDbContext<StargateContext>((sp, opts) =>
{
    var interceptor = sp.GetRequiredService<SqlLoggingInterceptor>();
    opts.UseSqlite(builder.Configuration.GetConnectionString("StarbaseApiDatabase"));
    opts.AddInterceptors(interceptor);
});

// Dev CORS so Swagger can call the API from the same origin (or others if you like)
builder.Services.AddCors(o =>
{
    o.AddPolicy("DevCors", p => p
        .AllowAnyOrigin()   // or .WithOrigins("http://localhost:5204", "https://localhost:7041")
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services.AddMediatR(cfg =>
{
    cfg.AddRequestPreProcessor<CreateAstronautDutyPreProcessor>();
    cfg.AddRequestPreProcessor<CreatePersonPreProcessor>();
    cfg.AddRequestPreProcessor<UpdatePersonPreProcessor>();
    cfg.AddRequestPreProcessor<UpdateAstronautDutyPreProcessor>();
    cfg.AddRequestPreProcessor<UpdatePersonByNamePreProcessor>();
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly);
});

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILogWriter, SqliteLogWriter>();
builder.Services.AddScoped<ILogBuffer, LogBuffer>();


var app = builder.Build();

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Enable CORS for dev
    app.UseCors("DevCors");

    // Optional: root -> Swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    // Only force HTTPS in prod (keeps dev simple)
    app.UseHttpsRedirection();
}

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
