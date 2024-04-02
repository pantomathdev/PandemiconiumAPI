namespace PandemiconiumAPI.DTO
{
	public sealed class UserToLoginDto
	{
		public string email { get; set; } = string.Empty;
		public string password { get; set; } = string.Empty;
	}
}
//Made UserToLoginDto class as sealed, because there is no potential of extension or inheritence for this class.
