using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PandemiconiumAPI
{
	public class JWTmanager
	{
		private IConfiguration _builder {  get; }
		public JWTmanager(IConfiguration builder) 
		{
			_builder = builder;
		}
		public string CreateToken(long id)
		{
			Claim[] claims = new Claim[]
			{
				new("userId", id.ToString())
			};

			SymmetricSecurityKey securityKey = new
				(Encoding.UTF8.GetBytes
				(_builder?.GetRequiredSection("TokenKey").Value!)
				);

			SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha512);

			SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor()
			{
				Subject = new ClaimsIdentity(claims),
				SigningCredentials = credentials,
				Expires = DateTime.UtcNow.AddDays(1)
			};

			JwtSecurityTokenHandler jwtSecurityToken = new();

			SecurityToken token = jwtSecurityToken.CreateToken(tokenDescriptor);

			return jwtSecurityToken.WriteToken(token);
		}
	}
}
