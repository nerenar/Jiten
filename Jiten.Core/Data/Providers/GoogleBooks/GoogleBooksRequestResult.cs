namespace Jiten.Core.Data.Providers.GoogleBooks;

public class GoogleBooksRequestResult
{
    public int TotalItems { get; set; }
    public List<GoogleBooksItem> Items { get; set; }
}