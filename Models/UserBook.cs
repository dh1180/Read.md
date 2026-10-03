using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Markdig;

namespace ReadMeApp.Models;

public class UserBook
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book? Book { get; set; }

    [MaxLength(50)]
    public string ReviewerName { get; set; } = "익명의 독서가";

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [MaxLength(200)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? Quote { get; set; }

    [MaxLength(10000)]
    public string? Content { get; set; }

    [NotMapped]
    public string ContentHtml
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Content)) return string.Empty;
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()
                .Build();
            return Markdown.ToHtml(Content, pipeline);
        }
    }

    public DateTime ReadDate { get; set; } = DateTime.UtcNow;
    public int LikesCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ReadingStatus Status { get; set; } = ReadingStatus.Completed;
    public ICollection<ReadingNote> Notes { get; set; } = new List<ReadingNote>();
}
