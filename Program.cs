using Bitbucket_PR_Sentinel.Contratos;
using Bitbucket_PR_Sentinel.IoC.Dominio;
using Bitbucket_PR_Sentinel.IoC.Gemini;
using Bitbucket_PR_Sentinel.IoC.Infra;
using Bitbucket_PR_Sentinel.Service.Bitbucket;
using Bitbucket_PR_Sentinel.Service.Notificacao;



var builder = WebApplication.CreateBuilder(args);

// Carrega o appsettings padrão + específicos por ambiente
builder.Configuration
    .AddJsonFile("appsettings.json" , optional: false , reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json" , optional: true , reloadOnChange: true);

// Adiciona o local apenas em desenvolvimento
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.local.json" , optional: true , reloadOnChange: true);
}



builder.Services.AddConfiguraGemini(builder.Configuration);
builder.Services.AddConfiguracaoInfra(builder.Configuration);
builder.Services.AddConfiguracaoDominio(builder.Configuration);
//builder.Services.AddConfiguraOllama(builder.Configuration);
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IBitbucketService, BitbucketService>();
builder.Services.AddHostedService<BitbucketWatcherService>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("ElectronCors" , policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();


app.UseCors("ElectronCors");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificacaoHub>("/hub/notificacoes");
app.Run();
