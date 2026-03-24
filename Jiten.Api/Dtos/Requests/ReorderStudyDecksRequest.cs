namespace Jiten.Api.Dtos.Requests;

public class ReorderStudyDecksRequest
{
    public List<ReorderItem> Items { get; set; } = new();

    public class ReorderItem
    {
        public int UserStudyDeckId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
