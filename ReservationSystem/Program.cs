using ReservationSystem.Models;
using ReservationSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http; 
using Microsoft.AspNetCore.Authentication.Cookies; 
using Microsoft.AspNetCore.Hosting; 
using System.IO; 

//AI PROMPT:"Build a full ASP.NET Core app configuration (Program.cs) with Razor Pages, session support, cookie-based login, scoped services, Google Calendar API integration via file, and environment-based error handling."

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorPages();

// HTTP Context erişimi için gerekli
builder.Services.AddHttpContextAccessor();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDistributedMemoryCache(); // Oturum için gereklidir
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Oturum süresi
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; 
});


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) 
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => 
    {
        options.Cookie.Name = "UserLoginCookie"; 
        options.LoginPath = "/Login";           
        options.AccessDeniedPath = "/AccessDenied"; 
        options.ExpireTimeSpan = TimeSpan.FromDays(7); 
        options.SlidingExpiration = true; 
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("InstructorOnly", policy => policy.RequireRole("instructor"));
    
});


// Servislerin Yaşam Sürelerini Scoped Olarak Ayarla
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<LoggingService>();


builder.Services.AddScoped(provider =>
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
   
    var jsonPath = Path.Combine(env.WebRootPath, "API.json"); 
    if (!File.Exists(jsonPath))
    {
        
        Console.WriteLine($"UYARI: GoogleCalendarService için API.json dosyası bulunamadı: {jsonPath}");
        
    }
    return new GoogleCalendarService(jsonPath); 
});


var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error"); 
    app.UseHsts(); 
}


app.UseHttpsRedirection(); 
app.UseStaticFiles();      

app.UseRouting();          


app.UseSession();


app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
