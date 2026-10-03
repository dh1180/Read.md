using System.ComponentModel.DataAnnotations;

namespace ReadMeApp.Models;

public class ReviewComment
{
    public int Id { get; set; }
    public int UserBookId { get; set; }
    public UserBook? UserBook { get; set; }

    [Required, MaxLength(50)]
    public string AuthorName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
