namespace Business.Models.Dto.Acts.Abstraction
{
    public class ActDto
    {
        public int? DocId { get; set; } = null;
        public HashSet<string> UnitIds { get; set; } = new HashSet<string>();
    }
}
