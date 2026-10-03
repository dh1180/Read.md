using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadMeApp.Data;
using ReadMeApp.Models;
using ReadMeApp.Models.ViewModels;
using ReadMeApp.Utilities;

namespace ReadMeApp.Controllers;

[Authorize]
public class NotesController : Controller
{
    private readonly ReadmeDbContext _context;

    public NotesController(ReadmeDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Thought))
        {
            return Json(ApiResponse<object>.Fail("메모 또는 감상 내용을 입력해 주세요."));
        }

        var userBook = await _context.UserBooks.FindAsync(request.UserBookId);
        if (userBook == null)
        {
            return Json(ApiResponse<object>.Fail("독서 기록을 찾을 수 없습니다."));
        }

        if (!IsOwner(userBook)) return Forbid();

        var note = new ReadingNote
        {
            UserBookId = request.UserBookId,
            PageNumber = Math.Max(1, request.PageNumber),
            Quote = TrimTo(request.Quote, 2000) ?? string.Empty,
            Thought = TrimTo(request.Thought, 4000) ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReadingNotes.Add(note);
        await _context.SaveChangesAsync();

        return Json(ApiResponse<object>.Ok(new
        {
            note.Id,
            note.PageNumber,
            note.Quote,
            note.Thought,
            ThoughtHtml = note.ThoughtHtml,
            CreatedAt = KoreaTime.FromUtc(note.CreatedAt).ToString("yyyy-MM-dd HH:mm")
        }, "독서 메모가 저장되었습니다."));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _context.ReadingNotes
            .Include(n => n.UserBook)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note == null)
        {
            return Json(ApiResponse<object>.Fail("메모를 찾을 수 없습니다."));
        }

        if (note.UserBook == null || !IsOwner(note.UserBook)) return Forbid();

        _context.ReadingNotes.Remove(note);
        await _context.SaveChangesAsync();
        return Json(ApiResponse<object>.Ok(new { id }, "메모가 삭제되었습니다."));
    }

    private bool IsOwner(UserBook userBook)
    {
        return User.Identity?.IsAuthenticated == true &&
               !string.IsNullOrWhiteSpace(User.Identity.Name) &&
               string.Equals(userBook.ReviewerName, User.Identity.Name, StringComparison.Ordinal);
    }

    private static string? TrimTo(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
