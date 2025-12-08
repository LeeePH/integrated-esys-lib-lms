using System;

namespace StudentPortal.Models.Studentdb
{
	public class JoinClassResponse
	{
		public bool Success { get; set; }
		public string Message { get; set; } = "";
		public DateTime? RequestedAtUtc { get; set; }
	}
}
