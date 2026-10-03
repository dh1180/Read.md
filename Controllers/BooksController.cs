using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadMeApp.Data;
using ReadMeApp.Models;
using ReadMeApp.Models.ViewModels;

namespace ReadMeApp.Controllers;

public class BooksController : Controller
{
    private const int PageSize = 12;
    private const int MaxReviewContentLength = 10000;
    private readonly ReadmeDbContext _context;
    private readonly ILogger<BooksController> _logger;

    public BooksController(ReadmeDbContext context, ILogger<BooksController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Authorize]
    public async Task<IActionResult> Index(string? sort, string? query, string status = "all", int page = 1)
    {
        var reviewerName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reviewerName)) return Challenge();

        page = Math.Max(page, 1);

        var bookReviewsQuery = _context.UserBooks
            .Include(ub => ub.Book)
            .Where(ub => ub.ReviewerName == reviewerName)
            .AsQueryable();

        if (status.Equals("wishlist", StringComparison.OrdinalIgnoreCase))
        {
            bookReviewsQuery = bookReviewsQuery.Where(ub => ub.Status == ReadingStatus.Wishlist);
        }
        else if (status.Equals("reading", StringComparison.OrdinalIgnoreCase))
        {
            bookReviewsQuery = bookReviewsQuery.Where(ub => ub.Status == ReadingStatus.Reading);
        }
        else if (status.Equals("draft", StringComparison.OrdinalIgnoreCase))
        {
            bookReviewsQuery = bookReviewsQuery.Where(ub => ub.Status == ReadingStatus.Draft);
        }
        else if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            bookReviewsQuery = bookReviewsQuery.Where(ub => ub.Status == ReadingStatus.Completed);
        }
        else
        {
            status = "all";
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            bookReviewsQuery = bookReviewsQuery.Where(ub =>
                (ub.Book != null && (ub.Book.Title.ToLower().Contains(q) || ub.Book.Author.ToLower().Contains(q))) ||
                (ub.Summary != null && ub.Summary.ToLower().Contains(q)) ||
                (ub.Quote != null && ub.Quote.ToLower().Contains(q)) ||
                (ub.Content != null && ub.Content.ToLower().Contains(q)));
        }

        var filteredCount = await bookReviewsQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)PageSize));
        page = Math.Min(page, totalPages);

        bookReviewsQuery = sort switch
        {
            "popular" => bookReviewsQuery.OrderByDescending(ub => ub.LikesCount).ThenByDescending(ub => ub.CreatedAt),
            "rating" => bookReviewsQuery.OrderByDescending(ub => ub.Rating).ThenByDescending(ub => ub.CreatedAt),
            _ => bookReviewsQuery.OrderByDescending(ub => ub.UpdatedAt)
        };

        var list = await bookReviewsQuery
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.CurrentSort = sort ?? "latest";
        ViewBag.SearchQuery = query;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.FilteredCount = filteredCount;

        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userBook = await _context.UserBooks
            .Include(ub => ub.Book)
            .Include(ub => ub.Notes.OrderByDescending(n => n.CreatedAt))
            .FirstOrDefaultAsync(ub => ub.Id == id);

        if (userBook == null) return NotFound();

        if (userBook.Status != ReadingStatus.Completed && !IsOwner(userBook))
        {
            return NotFound();
        }

        return View(userBook);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create(
        int? draftId = null,
        string? isbn = null,
        string? title = null,
        string? author = null,
        string? cover = null)
    {
        var reviewerName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reviewerName)) return Challenge();

        if (draftId.HasValue)
        {
            var draft = await _context.UserBooks
                .Include(ub => ub.Book)
                .FirstOrDefaultAsync(ub => ub.Id == draftId.Value && ub.Status == ReadingStatus.Draft);

            if (draft == null || !IsOwner(draft))
            {
                return NotFound();
            }

            var draftModel = new CreateReviewRequest
            {
                DraftId = draft.Id,
                Isbn = draft.Book?.Isbn ?? string.Empty,
                Title = draft.Book?.Title ?? string.Empty,
                Author = draft.Book?.Author ?? string.Empty,
                Publisher = draft.Book?.Publisher ?? string.Empty,
                CoverImageUrl = draft.Book?.CoverImageUrl ?? string.Empty,
                Description = draft.Book?.Description ?? string.Empty,
                ReviewerName = reviewerName,
                Rating = draft.Rating,
                Summary = draft.Summary,
                Quote = draft.Quote,
                Content = draft.Content ?? string.Empty,
                ReadDate = draft.ReadDate
            };

            return View(draftModel);
        }

        var model = new CreateReviewRequest
        {
            Isbn = isbn ?? string.Empty,
            Title = title ?? string.Empty,
            Author = author ?? string.Empty,
            CoverImageUrl = cover ?? string.Empty,
            ReviewerName = reviewerName,
            Rating = 5,
            ReadDate = DateTime.Today
        };

        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var reviewerName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reviewerName)) return Challenge();

        var userBook = await _context.UserBooks
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.Id == id && ub.Status == ReadingStatus.Completed);

        if (userBook == null || !IsOwner(userBook))
        {
            return NotFound();
        }

        var model = new CreateReviewRequest
        {
            EditId = userBook.Id,
            Isbn = userBook.Book?.Isbn ?? string.Empty,
            Title = userBook.Book?.Title ?? string.Empty,
            Author = userBook.Book?.Author ?? string.Empty,
            Publisher = userBook.Book?.Publisher ?? string.Empty,
            CoverImageUrl = userBook.Book?.CoverImageUrl ?? string.Empty,
            Description = userBook.Book?.Description ?? string.Empty,
            ReviewerName = reviewerName,
            Rating = userBook.Rating,
            Summary = userBook.Summary,
            Quote = userBook.Quote,
            Content = userBook.Content ?? string.Empty,
            ReadDate = userBook.ReadDate
        };

        return View("Create", model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Edit(CreateReviewRequest request)
    {
        if (!request.EditId.HasValue)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError("Title", "도서명을 입력하거나 도서를 검색해 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            ModelState.AddModelError("Content", "독서 감상평 본문을 작성해 주세요.");
        }
        else if (request.Content.Length > MaxReviewContentLength)
        {
            ModelState.AddModelError("Content", "독서록 본문은 최대 10,000자까지 작성할 수 있습니다.");
        }

        if (!ModelState.IsValid)
        {
            return View("Create", request);
        }

        var userBook = await _context.UserBooks
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.Id == request.EditId.Value && ub.Status == ReadingStatus.Completed);

        if (userBook == null || !IsOwner(userBook))
        {
            return NotFound();
        }

        try
        {
            var book = await FindOrCreateBookAsync(request);

            userBook.BookId = book.Id;
            userBook.Rating = Math.Clamp(request.Rating, 1, 5);
            userBook.Summary = TrimTo(request.Summary, 200);
            userBook.Quote = TrimTo(request.Quote, 500);
            userBook.Content = TrimTo(request.Content, MaxReviewContentLength);
            userBook.ReadDate = request.ReadDate ?? userBook.ReadDate;
            userBook.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AuthSuccess"] = $"'{book.Title}' 독서록을 수정했습니다.";
            return RedirectToAction(nameof(Details), new { id = userBook.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "독서록 수정 중 오류 발생");
            ModelState.AddModelError(string.Empty, "독서록 수정 중 오류가 발생했습니다.");
            return View("Create", request);
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CreateReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError("Title", "도서명을 입력하거나 도서를 검색해 선택해 주세요.");
            return View(request);
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            ModelState.AddModelError("Content", "독서 감상평 본문을 작성해 주세요.");
            return View(request);
        }

        if (request.Content.Length > MaxReviewContentLength)
        {
            ModelState.AddModelError("Content", "독서록 본문은 최대 10,000자까지 작성할 수 있습니다.");
            return View(request);
        }

        try
        {
            var book = await FindOrCreateBookAsync(request);
            var reviewer = User.Identity?.Name ?? "독서가";

            var userBook = await CreateOrUpdateReviewAsync(book, reviewer, request);

            TempData["AuthSuccess"] = $"'{book.Title}' 독서록이 등록되었습니다.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "독서록 작성 중 오류 발생");
            ModelState.AddModelError(string.Empty, "독서록 저장 중 오류가 발생했습니다.");
            return View(request);
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SaveDraft([FromBody] CreateReviewRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return Json(ApiResponse<object>.Fail("임시 저장하려면 먼저 책을 선택해 주세요."));
        }

        if (request.Content.Length > MaxReviewContentLength)
        {
            return Json(ApiResponse<object>.Fail("독서록 본문은 최대 10,000자까지 임시 저장할 수 있습니다."));
        }

        try
        {
            var reviewer = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(reviewer)) return Unauthorized();

            var normalizedReviewer = reviewer.Length > 50 ? reviewer[..50] : reviewer;
            var book = await FindOrCreateBookAsync(request);

            UserBook? userBook = null;

            if (request.DraftId.HasValue)
            {
                userBook = await _context.UserBooks.FirstOrDefaultAsync(ub =>
                    ub.Id == request.DraftId.Value &&
                    ub.ReviewerName == normalizedReviewer &&
                    ub.Status == ReadingStatus.Draft);

                if (userBook == null)
                {
                    return Json(ApiResponse<object>.Fail("임시 저장된 독서록을 찾을 수 없습니다."));
                }
            }
            else
            {
                userBook = await _context.UserBooks.FirstOrDefaultAsync(ub =>
                    ub.BookId == book.Id &&
                    ub.ReviewerName == normalizedReviewer &&
                    (ub.Status == ReadingStatus.Draft || ub.Status == ReadingStatus.Wishlist));
            }

            if (userBook == null)
            {
                userBook = new UserBook
                {
                    BookId = book.Id,
                    ReviewerName = normalizedReviewer,
                    LikesCount = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.UserBooks.Add(userBook);
            }

            userBook.BookId = book.Id;
            userBook.Rating = Math.Clamp(request.Rating, 1, 5);
            userBook.Summary = TrimTo(request.Summary, 200);
            userBook.Quote = TrimTo(request.Quote, 500);
            userBook.Content = TrimTo(request.Content, MaxReviewContentLength);
            userBook.ReadDate = request.ReadDate ?? DateTime.Today;
            userBook.Status = ReadingStatus.Draft;
            userBook.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(ApiResponse<object>.Ok(new
            {
                draftId = userBook.Id,
                updatedAt = userBook.UpdatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm")
            }, "임시 저장했습니다."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "독서록 임시 저장 중 오류 발생");
            return Json(ApiResponse<object>.Fail("임시 저장 중 오류가 발생했습니다."));
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return Json(ApiResponse<object>.Fail("도서 정보가 올바르지 않습니다."));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Json(ApiResponse<object>.Fail("독서록 본문을 작성해 주세요."));
        }

        if (request.Content.Length > MaxReviewContentLength)
        {
            return Json(ApiResponse<object>.Fail("독서록 본문은 최대 10,000자까지 작성할 수 있습니다."));
        }

        try
        {
            var book = await FindOrCreateBookAsync(request);
            var reviewer = User.Identity?.Name ?? "독서가";

            var userBook = await CreateOrUpdateReviewAsync(book, reviewer, request);

            return Json(ApiResponse<object>.Ok(new { id = userBook.Id }, $"'{book.Title}' 독서록이 등록되었습니다."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "독서록 등록 중 오류 발생");
            return Json(ApiResponse<object>.Fail("독서록 등록 중 오류가 발생했습니다."));
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Like(int id)
    {
        var userBook = await _context.UserBooks.FindAsync(id);
        if (userBook == null)
        {
            return Json(ApiResponse<object>.Fail("독서록을 찾을 수 없습니다."));
        }

        var cookieName = $"readme_like_{id}";
        if (Request.Cookies.ContainsKey(cookieName))
        {
            return Json(ApiResponse<object>.Fail("이미 공감한 독서록입니다."));
        }

        userBook.LikesCount++;
        await _context.SaveChangesAsync();

        Response.Cookies.Append(cookieName, "1", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(180)
        });

        return Json(ApiResponse<object>.Ok(new { likes = userBook.LikesCount }, "공감을 남겼습니다."));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var userBook = await _context.UserBooks.FindAsync(id);
        if (userBook == null) return NotFound();
        if (!IsOwner(userBook)) return Forbid();

        _context.UserBooks.Remove(userBook);
        await _context.SaveChangesAsync();
        TempData["AuthSuccess"] = "독서 기록을 삭제했습니다.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsOwner(UserBook userBook)
    {
        return User.Identity?.IsAuthenticated == true &&
               !string.IsNullOrWhiteSpace(User.Identity.Name) &&
               string.Equals(userBook.ReviewerName, User.Identity.Name, StringComparison.Ordinal);
    }

    private async Task<UserBook> CreateOrUpdateReviewAsync(Book book, string reviewer, CreateReviewRequest request)
    {
        var normalizedReviewer = reviewer.Length > 50 ? reviewer[..50] : reviewer;
        UserBook? userBook = null;

        if (request.DraftId.HasValue)
        {
            userBook = await _context.UserBooks.FirstOrDefaultAsync(ub =>
                ub.Id == request.DraftId.Value &&
                ub.ReviewerName == normalizedReviewer &&
                ub.Status == ReadingStatus.Draft);

            if (userBook == null)
            {
                throw new InvalidOperationException("임시 저장된 독서록을 찾을 수 없습니다.");
            }
        }
        else
        {
            userBook = await _context.UserBooks.FirstOrDefaultAsync(ub =>
                ub.BookId == book.Id &&
                ub.ReviewerName == normalizedReviewer &&
                ub.Status != ReadingStatus.Completed);
        }

        if (userBook == null)
        {
            userBook = new UserBook
            {
                BookId = book.Id,
                ReviewerName = normalizedReviewer,
                LikesCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserBooks.Add(userBook);
        }

        userBook.BookId = book.Id;
        userBook.Rating = Math.Clamp(request.Rating, 1, 5);
        userBook.Summary = TrimTo(request.Summary, 200);
        userBook.Quote = TrimTo(request.Quote, 500);
        userBook.Content = TrimTo(request.Content, MaxReviewContentLength);
        userBook.ReadDate = request.ReadDate ?? DateTime.UtcNow;
        userBook.Status = ReadingStatus.Completed;
        userBook.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return userBook;
    }

    private async Task<Book> FindOrCreateBookAsync(CreateReviewRequest request)
    {
        var isbn = string.IsNullOrWhiteSpace(request.Isbn)
            ? Guid.NewGuid().ToString("N")[..13]
            : request.Isbn.Trim();
        isbn = isbn.Length > 50 ? isbn[..50] : isbn;

        var title = request.Title.Trim();
        title = title.Length > 200 ? title[..200] : title;

        var author = string.IsNullOrWhiteSpace(request.Author) ? "저자 미상" : request.Author.Trim();
        author = author.Length > 100 ? author[..100] : author;

        var publisher = request.Publisher?.Trim() ?? string.Empty;
        publisher = publisher.Length > 100 ? publisher[..100] : publisher;

        var cover = request.CoverImageUrl?.Trim() ?? string.Empty;
        cover = cover.Length > 500 ? cover[..500] : cover;

        var description = request.Description?.Trim() ?? string.Empty;
        description = description.Length > 2000 ? description[..2000] : description;

        var book = await _context.Books.FirstOrDefaultAsync(b => b.Isbn == isbn);
        if (book != null) return book;

        book = new Book
        {
            Isbn = isbn,
            Title = title,
            Author = author,
            Publisher = publisher,
            CoverImageUrl = cover,
            TotalPages = 300,
            Description = description
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return book;
    }

    private static string? TrimTo(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
