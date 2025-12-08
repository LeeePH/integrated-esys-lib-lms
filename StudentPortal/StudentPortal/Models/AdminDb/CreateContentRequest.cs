namespace StudentPortal.Models.AdminMaterial
{
    public class CreateContentRequest
    {
        public string ClassId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Deadline { get; set; }
        public string Link { get; set; }
        public int MaxGrade { get; set; } = 100;
    }

}
