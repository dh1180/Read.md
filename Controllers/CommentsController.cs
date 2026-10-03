using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadMeApp.Data;
using ReadMeApp.Models;
using ReadMeApp.Models.ViewModels;

namespace ReadMeApp.Controllers;

[Authorize]
public class CommentsController : Controller
{
    private readonly ReadmeDbContext _context;

    public CommentsController(ReadmeDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommentRequest? request)
    {
        if (request == null) return Json(ApiResponse<object>.Fail("댓글 요청이 올바르지 않습니다."));

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content)) return Json(ApiResponse<object>.Fail("댓글 내용을 입력해 주세요."));
        if (content.Length > 1000) return Json(ApiResponse<object>.Fail("댓글은 최대 1,000자까지 작성할 수 있습니다."));

        var reviewExists = await _context.UserBooks.AsNoTracking()
            .AnyAsync(ub => ub.Id == request.UserBookId && ub.Status == ReadingStatus.Completed);
        if (!reviewExists) return Json(ApiResponse<object>.Fail("댓글을 작성할 독서록을 찾을 수 없습니다."));

        var authorName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(authorName)) return Unauthorized();
        authorName = authorName.Length > 50 ? authorName[..50] : authorName;

        var comment = new ReviewComment
        {
            UserBookId = request.UserBookId,
            AuthorName = authorName,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReviewComments.Add(comment);
        await _context.SaveChangesAsync();

        return Json(ApiResponse<object>.Ok(new
        {
            comment.Id,
            comment.AuthorName,
            comment.Content,
            CreatedAt = comment.CreatedAt.ToString("yyyy.MM.dd HH:mm")
        }, "댓글을 등록했습니다."));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var comment = await _context.ReviewComments.FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null) return Json(ApiResponse<object>.Fail("댓글을 찾을 수 없습니다."));

        if (!string.Equals(comment.AuthorName, User.Identity?.Name, StringComparison.Ordinal))
            return Forbid();

        _context.ReviewComments.Remove(comment);
        await _context.SaveChangesAsync();
        return Json(ApiResponse<object>.Ok(new { id }, "댓글을 삭제했습니다."));
    }
}
