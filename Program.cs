using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var useLocalDb = ConnectionStringFactory.IsLocalMode(builder.Configuration);
var connectionString = ConnectionStringFactory.Build(builder.Configuration);
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Logging.AddConsole();
Console.WriteLine(useLocalDb
    ? "Database mode: Local (database/rushmybookings_crms.sql via Docker or setup-database.ps1)"
    : "Database mode: Remote (AWS MySQL)");

builder.Services.AddDbContext<CrmDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
        mySqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

builder.Services.AddScoped<ICrmDataService, CrmDataService>();
builder.Services.AddScoped<IBookingFileService, BookingFileService>();
builder.Services.AddHostedService<DatabaseWarmupHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
