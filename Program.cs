using Dapper;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MySql.Data;
using MySql.Data.MySqlClient;
using PandemiconiumAPI.DTO;
using System.Collections;
using MailKit;
using MimeKit;
using Verifalia.Api;
using MailKit.Security;
using MailKit.Net.Smtp;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;
using Org.BouncyCastle.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var corsPolicy = new CorsPolicyBuilder("http://localhost:3000")
	.AllowAnyHeader()
	.AllowAnyOrigin()
	.AllowAnyMethod()
	.Build();
builder.Services.AddCors(options => options.AddPolicy("CustomPolicy", corsPolicy));

var app = builder.Build();
app.UseCors("CustomPolicy");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDeveloperExceptionPage();
app.MapGet("/GetUser", () =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	IEnumerable<UserToGetDto> users;
	using(var connection = new MySqlConnection(connectionString))
	{
		string sql = "SELECT email FROM test.user";
		users = connection.Query<UserToGetDto>(sql);
	}
	return Results.Ok(users);
})
.WithName("GetWeatherForecast")
.WithOpenApi()
.RequireCors("CustomPolicy");


app.MapPost("/SignUp", async (UserToAddDto user) =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	int affectedRows = 0;
	string text = string.Empty;
	using (var connection = new MySqlConnection(connectionString))
	{
		var welcomeMessage = new MimeMessage();
		welcomeMessage.From.Add(new MailboxAddress("","pandemiconium.manager@gmail.com"));
		welcomeMessage.To.Add(new MailboxAddress("", user.email));
		welcomeMessage.Subject = "Welcome to Pandemic-onium Manager!";
		welcomeMessage.Body = new TextPart("html") { Text = "<h2>Thank you for choosing our platform!<br/>We hope this mail finds you well. As we have seen, surviving a pandemic isn't a small deal. During these uncertain times, we hope our platform gives you light and hope to continue thriving. Have a great time exploring our platform.</h2> <br/><br/><h4>Click the link to log in to our website: *Link goes here*</h4><br/><h6>Unsubscribe: *link to unsubscribe from the mail service*</h6>" };
			using (var client = new SmtpClient())
			{
					client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
					client.Authenticate("pandemiconium.manager@gmail.com", "wszm pksz svqq yjva");

					var verifalia = new VerifaliaRestClient(username: "pandemictestuser", password: "ProjectProfile1");
					var job = await verifalia.
									EmailValidations.
									SubmitAsync(user.email);
					var entry = job.Entries[0];
					if (!entry.Status.HasFlag(Verifalia.Api.EmailValidations.Models.ValidationEntryStatus.Success))
						return Results.BadRequest($"{user.email} does not exist."); 

					text = client.Send(welcomeMessage);
					client.Disconnect(true);
			}

		string checkAccount = "SELECT COUNT(*) FROM user WHERE email = @email";
		long count = (long)connection.ExecuteScalar(checkAccount, new { email = user.email})!;
		if (count > 0)
			return Results.BadRequest("User already exists!");

		byte[] passwordSalt = new byte[128 / 8];
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
		{
			rng.GetNonZeroBytes(passwordSalt);
		}

		string passwordSaltString = builder.Configuration.GetSection("Appsettings:PasswordKey").Value! + Convert.ToBase64String(passwordSalt);

		byte[] passwordHash = KeyDerivation.Pbkdf2(
			password: user.password,
			salt: Encoding.ASCII.GetBytes(passwordSaltString),
			prf: KeyDerivationPrf.HMACSHA256,
			iterationCount: 100000,
			numBytesRequested: 256 / 8
		);

		string sql = "INSERT INTO test.user(email, password_hash, password_salt) VALUES(@email, @passwordHash, @passwordSalt)";
		try
		{
			affectedRows = await connection.ExecuteAsync(sql, new { email = user.email, passwordHash = passwordHash, passwordSalt = passwordSalt });
		}
		catch(Exception ex)
		{
			throw new Exception("Couldn't insert the user!" + ex.Message);
		}
	}
	return Results.Ok($"{text}");
})
	.RequireCors("CustomPolicy");

app.MapPost("/LogIn", async (UserToLoginDto user) =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	using (var connection = new MySqlConnection(connectionString))
	{
		string sql = "SELECT iduser, password_hash, password_salt FROM test.user WHERE email LIKE @email";
		var result = await connection.QueryAsync<UserLoginModel>(sql, new { email = user.email });
		var loginUser = result.FirstOrDefault();
		if(loginUser is not null)
		{
			byte[] passwordHash = GetPasswordHash(user.password, loginUser.password_salt);
			for(int i = 0; i< passwordHash.Length; i++)
			{
				if (passwordHash[i] != loginUser.password_hash[i])
					return Results.BadRequest("Invalid credentials.");
			}
			return Results.Ok(loginUser.iduser);
		}
		return Results.BadRequest("Invalid credentials.");
	}
}
)
.RequireCors("CustomPolicy");

byte[] GetPasswordHash(string password, byte[] passwordSalt)
{
	string passwordSaltString = builder.Configuration.GetSection("AppSettings:PasswordKey").Value + Convert.ToBase64String(passwordSalt);

		return KeyDerivation.Pbkdf2(
			password: password,
			salt: Encoding.ASCII.GetBytes(passwordSaltString),
			prf: KeyDerivationPrf.HMACSHA256,
			iterationCount: 100000,
			numBytesRequested: 256 / 8
		);
}

app.MapPost("/SaveArticle", (StarredArticle starredArticle) =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	using (var connection = new MySqlConnection(connectionString))
	{
		string sql = "INSERT INTO starred_articles(id,articleUrl,imageUrl) VALUES(@id,@article,@image)";
		int rowsAffected = connection.Execute(sql, new { id = starredArticle.id, article = starredArticle.articleUrl, image = starredArticle.imageUrl });
		if(rowsAffected > 0)
			return Results.Ok();
		else
			throw new Exception("Could not save article.");
	}

})
	.RequireCors("CustomPolicy");

app.MapGet("/GetStarredArticles/{id:int}", (int id) =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	using (var connection = new MySqlConnection(connectionString))
	{
		string countSql = "SELECT COUNT(*) FROM starred_articles WHERE id= @id";
		long count = (long)connection.ExecuteScalar(countSql, new { id = id })!;

		string resultSql = "SELECT articleUrl,imageUrl FROM starred_articles WHERE id=@id";

		List<SaveArticleDto> results = new List<SaveArticleDto>();
		results = connection.Query<SaveArticleDto>(resultSql, new { id = id }).AsList();

		return Results.Ok(new ArrayList() { count, results});
	}
}
).RequireCors("CustomPolicy");

app.MapDelete("/DeleteStarredArticle/{id:int}", (int id, string articleUrl) =>
{
	string connectionString = builder.Configuration.GetConnectionString("Default")!;
	using (var connection = new MySqlConnection(connectionString))
	{
		string deleteSql = "DELETE FROM starred_articles WHERE id = @id AND articleUrl = @articleUrl";

		int result = connection.Execute(deleteSql, new {id = id,  articleUrl = articleUrl});

		if (result > 0)
			return Results.Ok();
		else
			throw new Exception("Could not delete the requested row.");
	}
}
).RequireCors("CustomPolicy");
app.Run();

public class User
{
    public int iduser { get; set; }

	public string email { get; set; } = string.Empty;

	public string password { get; set; } = string.Empty;

	public override string ToString()
	{
		return $"{iduser}\t{email}\t{password}";
	}
}


public class UserLoginModel
{
	public int iduser { get; set; }

	public byte[] password_hash { get; set; } = new byte[1000];
	public byte[] password_salt { get; set; } = new byte[1000];
}

public class StarredArticle
{
	public int id { get; set; }
	public string articleUrl { get; set; } = "";
	public string imageUrl { get; set; } = "";
}