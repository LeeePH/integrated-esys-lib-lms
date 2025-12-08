using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SIA_IPT.Models.AdminCreateAssessment
{
	public class AdminCreateAssessmentViewModel
	{
		[Required]
		public string Title { get; set; }

		public string Description { get; set; }

		public List<TestBlock> Tests { get; set; } = new();

		public int TotalPoints
		{
			get
			{
				int total = 0;
				foreach (var test in Tests)
					foreach (var q in test.Questions)
						total += (int)q.Points;
				return total;
			}
		}
	}

	public class TestBlock
	{
		public int TestIndex { get; set; }
		public List<Question> Questions { get; set; } = new();
	}

	public class Question
	{
		public string Text { get; set; }
		public string Type { get; set; }
		public double Points { get; set; }

		// Optional type-specific fields
		public string CorrectAnswer { get; set; }
		public List<string> Choices { get; set; }
		public string TrueFalseAnswer { get; set; }
		public List<string> EnumerationAnswers { get; set; }
	}
}
