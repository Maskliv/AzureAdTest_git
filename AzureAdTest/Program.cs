using AzureAdTest.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using System.Text;
namespace AzureAdTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            
            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddDistributedMemoryCache();


            //Cookies policy config
            builder.Services.Configure<CookiePolicyOptions>(options =>
            {
                // SameSiteMode.None is required to support SAML SSO.
                options.MinimumSameSitePolicy = SameSiteMode.None;

                options.CheckConsentNeeded = context => false;

                // Some older browsers don't support SameSiteMode.None.
                options.OnAppendCookie = cookieContext => SameSite.CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
                options.OnDeleteCookie = cookieContext => SameSite.CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
            });

            //Cors config
            builder.Services.AddCors(x =>
            {
                x.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                });
            });

            //Sustainsys.Saml2 config
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultScheme = ApplicationSamlConstants.Application;
                o.DefaultSignInScheme = ApplicationSamlConstants.External;
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(Convert.ToDouble(builder.Configuration["Jwt:ExpireInMinutes"])),
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new Exception("No appsettings.json config for Jwt:Key"))),
                };
            })
            .AddCookie(ApplicationSamlConstants.Application)
            .AddCookie(ApplicationSamlConstants.External)
            .AddSaml2(options =>
            {
                options.SPOptions.EntityId = new EntityId(builder.Configuration["Saml2:EntityId"]);
                /*
                 * Sustainsys.SAML2 Always return to https://yourApp/Saml2/Acs (so that's the Return Url you have to add in Azure AD Configuration to allow it) 
                 * Sustainsys do this to process the SAML response and stores the authentication data in the cookies session and give it to your endpoint. https://localhost:5001/auth/callback in this case
                 * 
                 */
                options.SPOptions.ReturnUrl = new Uri(builder.Configuration["Saml2:ReturnUrl"]?? string.Empty); 
                options.IdentityProviders.Add(
                    new IdentityProvider(
                        new EntityId(builder.Configuration["Saml2:IdentityProvider:MicrosoftEntraIdentifier"]), options.SPOptions)
                    {
                        MetadataLocation = builder.Configuration["Saml2:IdentityProvider:MetadataLocation"],
                        LoadMetadata = true
                    });
            });

            //Session config 
            builder.Services.AddSession(options =>
            {
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.None;
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

            

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();


            app.MapControllers();

            app.Run();
        }
    }
}
