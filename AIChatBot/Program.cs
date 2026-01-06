using AIChatBot.Data;
using AIChatBot.Models;
using AIChatBot.Services;
using AIChatBot.SignalR;
using AIChatBot.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'AppDbContextConnection' not found.");
ConfigurationManager configuration = builder.Configuration;
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
//builder.Services.AddSingleton<PythonServiceManager>();
builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ChatSearchService>();
builder.Services.AddSingleton<ChatSentimentService>();
builder.Services.AddSingleton<ChatRecommendationService>();
builder.Services.AddScoped<ChatHybridService>();
builder.Services.AddSingleton<ChatForecastService>();
builder.Services.AddSingleton<ChatGenAIService>();
builder.Services.AddScoped<AgentOrchestratorService>(); // <-- new
builder.Services.AddControllers();
builder.Services.AddHttpClient("ChatAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7201/"); // Your API endpoint
});
var app = builder.Build();
//var pythonManager = app.Services.GetRequiredService<PythonServiceManager>();
//pythonManager.StartPythonServer();
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    var faqs = db.FAQs.ToList();

//    if (faqs.Any())
//    {
//        FAQModelTrainer.TrainModel(faqs);
//        Console.WriteLine("✅ FAQ ML model trained and saved.");
//    }
//    else
//    {
//        Console.WriteLine("⚠️ No FAQ data found to train ML model.");
//    }
//}
//using (var scope = app.Services.CreateScope())
//{
//    var updater = scope.ServiceProvider.GetRequiredService<FAQEmbeddingUpdater>();
//    await updater.UpdateEmbeddingsAsync();
//}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHub<ChatHub>("/chathub");

app.Run();
