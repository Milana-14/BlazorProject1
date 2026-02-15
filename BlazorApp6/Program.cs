using BlazorApp6;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using MudBlazor.Services;
using System.Data;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<StudentManager>();
builder.Services.AddScoped<AvatarManager>();
builder.Services.AddScoped<SubjectsManager>();
builder.Services.AddScoped<SwapManager>();
builder.Services.AddScoped<RateHelpManager>();
builder.Services.AddScoped<ChatManager>();
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<AiChatManager>();
builder.Services.AddSingleton<OnlineUsersService>();


builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7117");
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024;
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

var culture = new CultureInfo("bg-BG");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ChatMessages>("/chathub");
app.MapHub<AiChatHub>("/aichat");
app.MapHub<OnlineHub>("/onlineHub");
app.MapHub<SwapHub>("/swapHub");


app.MapControllers();

app.Run();
