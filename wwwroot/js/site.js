// Read.me - Modern Reading Journal (ASP.NET Core MVC + jQuery Ajax)
$(document).ready(function () {


    // Send ASP.NET Core antiforgery token with jQuery Ajax requests.
    var antiForgeryToken = $('input[name="__RequestVerificationToken"]').first().val();
    if (antiForgeryToken) {
        $.ajaxSetup({
            headers: { 'RequestVerificationToken': antiForgeryToken }
        });
    }

    // --- Toast Notification Helper ---
    function showToast(message, isSuccess = true) {
        var toastEl = $('#appToast');
        var toastBody = $('#appToastBody');
        toastBody.text(message);

        toastEl.removeClass('bg-danger bg-primary bg-success');
        if (isSuccess) {
            toastEl.addClass('bg-success');
        } else {
            toastEl.addClass('bg-danger');
        }

        var toast = new bootstrap.Toast(toastEl[0]);
        toast.show();
    }

    // --- 1. Global Book Search (20 items per page + load more) ---
    var currentSearchResults = [];
    var currentSearchQuery = '';
    var currentSearchPage = 1;
    var currentSearchIsEnd = true;
    var currentSearchPageableCount = 0;

    $('#btnExecuteSearch').on('click', function () {
        performSearch();
    });

    $('#inputBookSearch').on('keypress', function (e) {
        if (e.which === 13) {
            e.preventDefault();
            performSearch();
        }
    });

    $('#btnLoadMoreBooks').on('click', function () {
        if (!currentSearchQuery || currentSearchIsEnd) return;
        loadSearchPage(currentSearchPage + 1, true);
    });

    function performSearch() {
        var query = $('#inputBookSearch').val().trim();
        if (!query) {
            alert('검색어를 입력해 주세요.');
            return;
        }

        currentSearchQuery = query;
        currentSearchPage = 1;
        currentSearchIsEnd = true;
        currentSearchPageableCount = 0;
        currentSearchResults = [];

        $('#searchResultsList').empty();
        $('#bookSearchMoreWrap, #bookSearchEndMessage').addClass('d-none');
        loadSearchPage(1, false);
    }

    function loadSearchPage(page, append) {
        var loadMoreButton = $('#btnLoadMoreBooks');

        if (append) {
            loadMoreButton.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-1"></span> 불러오는 중...');
        } else {
            $('#searchLoading').removeClass('d-none');
        }

        $.ajax({
            url: '/Search/Query',
            type: 'GET',
            data: { q: currentSearchQuery, page: page, size: 20 },
            dataType: 'json',
            success: function (response) {
                $('#searchLoading').addClass('d-none');

                var pageData = response && response.data ? response.data : null;
                var books = pageData && Array.isArray(pageData.items) ? pageData.items : [];

                if (!response.success || (!append && books.length === 0)) {
                    currentSearchResults = [];
                    $('#bookSearchMoreWrap, #bookSearchEndMessage').addClass('d-none');
                    $('#searchResultsList').html(`
                        <div class="col-12 text-center text-muted py-5">
                            <i class="bi bi-search fs-1 text-muted opacity-50"></i>
                            <h6 class="fw-bold mt-3 text-secondary">검색 결과가 없습니다</h6>
                            <p class="small text-muted mb-0">다른 검색어나 키워드로 다시 검색해 보세요.</p>
                        </div>
                    `);
                    return;
                }

                currentSearchPage = pageData.page || page;
                currentSearchIsEnd = pageData.isEnd === true;
                currentSearchPageableCount = pageData.pageableCount || 0;

                var startIndex = currentSearchResults.length;
                currentSearchResults = currentSearchResults.concat(books);
                renderGlobalSearchBooks(books, startIndex, append);
                updateGlobalSearchPagination();
            },
            error: function () {
                $('#searchLoading').addClass('d-none');
                loadMoreButton.prop('disabled', false)
                    .html('<i class="bi bi-chevron-down me-1"></i> 더보기');
                showToast('도서 검색 중 오류가 발생했습니다.', false);
            }
        });
    }

    function renderGlobalSearchBooks(books, startIndex, append) {
        var html = '';

        $.each(books, function (offset, book) {
            var index = startIndex + offset;
            var coverImg = book.coverImageUrl ?
                `<img src="${book.coverImageUrl}" alt="${escapeHtml(book.title)}" class="search-book-cover" />` :
                `<div class="search-book-cover bg-light d-flex align-items-center justify-content-center text-secondary"><i class="bi bi-book fs-3"></i></div>`;

            var publisherBadge = book.publisher ?
                `<span class="badge bg-light text-secondary border small me-1">${escapeHtml(book.publisher)}</span>` : '';
            var dateBadge = book.publishedDate ?
                `<span class="text-muted small" style="font-size: 0.78rem;"><i class="bi bi-calendar3 me-1"></i>${escapeHtml(book.publishedDate)}</span>` : '';

            html += `
                <div class="col-12">
                    <div class="search-book-card">
                        ${coverImg}
                        <div class="search-book-info">
                            <div class="d-flex align-items-center gap-1 mb-1">
                                ${publisherBadge}
                                ${dateBadge}
                            </div>
                            <div class="search-book-title" title="${escapeHtml(book.title)}">${escapeHtml(book.title)}</div>
                            <div class="search-book-meta"><i class="bi bi-person me-1"></i>${escapeHtml(book.author || '저자 미상')}</div>
                            <p class="search-book-desc">${escapeHtml(book.description || '책 소개 정보가 없습니다.')}</p>
                        </div>
                        <div class="d-flex flex-column gap-2 flex-shrink-0 ms-2">
                            <button type="button" class="btn btn-primary-gradient btn-sm rounded-pill px-3 py-2 btn-write-review-from-search" data-index="${index}">
                                <i class="bi bi-pencil-square me-1"></i> 독서록 쓰기
                            </button>
                            <button type="button" class="btn btn-add-shelf btn-sm py-1" data-index="${index}">
                                <i class="bi bi-bookmark-plus me-1"></i> 서재 보관
                            </button>
                        </div>
                    </div>
                </div>
            `;
        });

        if (append) {
            $('#searchResultsList').append(html);
        } else {
            $('#searchResultsList').html(html);
        }
    }

    function updateGlobalSearchPagination() {
        var loadMoreButton = $('#btnLoadMoreBooks');
        loadMoreButton.prop('disabled', false)
            .html('<i class="bi bi-chevron-down me-1"></i> 더보기');

        var countText = currentSearchPageableCount > 0
            ? `${currentSearchResults.length}개 표시 중 · 검색 가능 ${currentSearchPageableCount}개`
            : `${currentSearchResults.length}개 표시 중`;

        $('#bookSearchResultMeta').text(countText);

        if (currentSearchIsEnd) {
            $('#bookSearchMoreWrap').addClass('d-none');
            if (currentSearchResults.length > 20) {
                $('#bookSearchEndMessage').removeClass('d-none');
            } else {
                $('#bookSearchEndMessage').addClass('d-none');
            }
        } else {
            $('#bookSearchEndMessage').addClass('d-none');
            $('#bookSearchMoreWrap').removeClass('d-none');
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // Add to library via Ajax (Safely using array cache, avoiding HTML attribute breakages)
    $(document).on('click', '.btn-add-shelf', function () {
        var btn = $(this);
        var index = parseInt(btn.data('index'));
        var book = currentSearchResults[index];

        if (!book) {
            showToast('도서 정보를 읽을 수 없습니다.', false);
            return;
        }

        var bookData = {
            isbn: book.isbn || '',
            title: book.title || '제목 없음',
            author: book.author || '',
            publisher: book.publisher || '',
            coverImageUrl: book.coverImageUrl || '',
            totalPages: parseInt(book.totalPages) || 300,
            description: book.description || '',
            status: 1 // Reading (읽는 중)
        };

        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span> 담는 중...');

        $.ajax({
            url: '/Search/AddToLibrary',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(bookData),
            success: function (res) {
                if (res.success) {
                    btn.removeClass('btn-add-shelf').addClass('btn btn-success rounded-pill px-3')
                       .html('<i class="bi bi-check2"></i> 담기 완료');
                    showToast(res.message, true);
                    setTimeout(function () {
                        location.reload();
                    }, 1000);
                } else {
                    btn.prop('disabled', false).html('<i class="bi bi-plus-lg"></i> 서재에 담기');
                    showToast(res.message, false);
                }
            },
            error: function (xhr) {
                btn.prop('disabled', false).html('<i class="bi bi-plus-lg"></i> 서재에 담기');
                var errMsg = '서재에 추가하지 못했습니다.';
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errMsg = xhr.responseJSON.message;
                }
                showToast(errMsg, false);
            }
        });
    });

    // Write review directly from Search Modal -> Navigate to /Books/Create page
    $(document).on('click', '.btn-write-review-from-search', function () {
        var index = parseInt($(this).data('index'));
        var book = currentSearchResults[index];
        if (!book) return;

        // Hide search modal
        var searchModalEl = document.getElementById('searchModal');
        var searchModal = bootstrap.Modal.getInstance(searchModalEl);
        if (searchModal) searchModal.hide();

        // Redirect directly to /Books/Create page with query params
        var params = new URLSearchParams({
            isbn: book.isbn || '',
            title: book.title || '',
            author: book.author || '',
            cover: book.coverImageUrl || ''
        });
        window.location.href = '/Books/Create?' + params.toString();
    });

    // --- 2. Write Review Modal: Book Search & Selection ---
    var reviewSearchResults = [];
    var reviewSearchQuery = '';
    var reviewSearchPage = 1;
    var reviewSearchIsEnd = true;

    $('#btnExecuteReviewSearch').on('click', function () {
        performReviewBookSearch();
    });

    $('#inputReviewBookSearch').on('keypress', function (e) {
        if (e.which === 13) {
            e.preventDefault();
            performReviewBookSearch();
        }
    });

    function performReviewBookSearch() {
        var query = $('#inputReviewBookSearch').val().trim();
        if (!query) {
            alert('검색할 도서명을 입력해 주세요.');
            return;
        }

        reviewSearchQuery = query;
        reviewSearchPage = 1;
        reviewSearchIsEnd = true;
        reviewSearchResults = [];
        $('#reviewSearchResultsList').empty();

        loadReviewSearchPage(1, false);
    }

    $(document).on('click', '#btnLoadMoreReviewBooks', function () {
        if (!reviewSearchQuery || reviewSearchIsEnd) return;
        loadReviewSearchPage(reviewSearchPage + 1, true);
    });

    function loadReviewSearchPage(page, append) {
        $('#reviewSearchLoading').removeClass('d-none');
        $('#btnLoadMoreReviewBooks').prop('disabled', true);

        $.ajax({
            url: '/Search/Query',
            type: 'GET',
            data: { q: reviewSearchQuery, page: page, size: 20 },
            dataType: 'json',
            success: function (response) {
                $('#reviewSearchLoading').addClass('d-none');

                var pageData = response && response.data ? response.data : null;
                var books = pageData && Array.isArray(pageData.items) ? pageData.items : [];

                if (!response.success || (!append && books.length === 0)) {
                    $('#reviewSearchResultsList').html(`
                        <div class="text-center text-muted py-3">
                            <span class="small">검색 결과가 없습니다. 다른 책 제목을 입력해 보세요.</span>
                        </div>
                    `);
                    return;
                }

                reviewSearchPage = pageData.page || page;
                reviewSearchIsEnd = pageData.isEnd === true;

                var startIndex = reviewSearchResults.length;
                reviewSearchResults = reviewSearchResults.concat(books);

                var html = '';
                $.each(books, function (offset, book) {
                    var index = startIndex + offset;
                    var coverImg = book.coverImageUrl ?
                        `<img src="${book.coverImageUrl}" alt="${escapeHtml(book.title)}" class="rounded shadow-sm" style="width: 36px; height: 50px; object-fit: cover;" />` :
                        `<div class="bg-light rounded d-flex align-items-center justify-content-center text-secondary" style="width: 36px; height: 50px;"><i class="bi bi-book"></i></div>`;

                    html += `
                        <div class="review-book-select-item d-flex align-items-center justify-content-between p-2" data-index="${index}">
                            <div class="d-flex align-items-center gap-2 min-w-0">
                                ${coverImg}
                                <div class="text-truncate">
                                    <div class="fw-bold small text-dark text-truncate">${escapeHtml(book.title)}</div>
                                    <div class="text-muted text-truncate" style="font-size: 0.78rem;">${escapeHtml(book.author || '저자 미상')} · ${escapeHtml(book.publisher || '')}</div>
                                </div>
                            </div>
                            <button type="button" class="btn btn-sm btn-primary rounded-pill px-3 py-1 flex-shrink-0 ms-2">선택</button>
                        </div>
                    `;
                });

                if (append) {
                    $('#reviewSearchResultsList .review-search-more-wrap').remove();
                    $('#reviewSearchResultsList').append(html);
                } else {
                    $('#reviewSearchResultsList').html(html);
                }

                if (!reviewSearchIsEnd) {
                    $('#reviewSearchResultsList').append(`
                        <div class="review-search-more-wrap text-center pt-3">
                            <button type="button" id="btnLoadMoreReviewBooks" class="btn btn-sm btn-outline-primary rounded-pill px-4">
                                <i class="bi bi-chevron-down me-1"></i> 더보기
                            </button>
                        </div>
                    `);
                }
            },
            error: function () {
                $('#reviewSearchLoading').addClass('d-none');
                $('#btnLoadMoreReviewBooks').prop('disabled', false);
                showToast('도서 검색 중 오류가 발생했습니다.', false);
            }
        });
    }

    // Select book in review modal
    $(document).on('click', '.review-book-select-item', function () {
        var index = parseInt($(this).data('index'));
        var book = reviewSearchResults[index];
        if (!book) return;

        selectBookForReview(book);
    });

    function selectBookForReview(book) {
        $('#hiddenIsbn').val(book.isbn || '');
        $('#hiddenTitle').val(book.title || '');
        $('#hiddenAuthor').val(book.author || '');
        $('#hiddenPublisher').val(book.publisher || '');
        $('#hiddenCoverUrl').val(book.coverImageUrl || '');
        $('#hiddenDesc').val(book.description || '');

        $('#selectedBookCover').attr('src', book.coverImageUrl || '');
        $('#selectedBookTitle').text(book.title || '제목 없음');
        $('#selectedBookMeta').text((book.author || '저자 미상') + (book.publisher ? ' · ' + book.publisher : ''));

        $('#sectionBookSearch').addClass('d-none');
        $('#selectedBookCard').removeClass('d-none');

        // Suggest summary title if empty
        if (!$('#inputReviewSummary').val().trim()) {
            $('#inputReviewSummary').val(`《${book.title}》을 읽고`);
        }
    }

    // Change selected book
    $('#btnChangeSelectedBook').on('click', function () {
        $('#selectedBookCard').addClass('d-none');
        $('#sectionBookSearch').removeClass('d-none');
        $('#hiddenTitle').val('');
    });

    // --- 3. Star Rating Picker in Review Modal ---
    var ratingDescriptions = {
        1: '1.0점 (추천하지 않아요)',
        2: '2.0점 (조금 아쉬워요)',
        3: '3.0점 (보통이에요)',
        4: '4.0점 (좋아요)',
        5: '5.0점 (최고예요!)'
    };

    $('.star-pick').on('mouseenter', function () {
        var hoverVal = parseInt($(this).data('value'));
        updateStarDisplay(hoverVal);
    });

    $('#starRatingSelector').on('mouseleave', function () {
        var currentVal = parseInt($('#inputReviewRating').val()) || 5;
        updateStarDisplay(currentVal);
    });

    $('.star-pick').on('click', function () {
        var selectedVal = parseInt($(this).data('value'));
        $('#inputReviewRating').val(selectedVal);
        $('#lblRatingDescription').text(ratingDescriptions[selectedVal] || `${selectedVal}.0점`);
        updateStarDisplay(selectedVal);
    });

    function updateStarDisplay(rating) {
        $('.star-pick').each(function () {
            var starVal = parseInt($(this).data('value'));
            if (starVal <= rating) {
                $(this).addClass('active bi-star-fill').removeClass('bi-star text-black-50');
            } else {
                $(this).removeClass('active bi-star-fill').addClass('bi-star text-black-50');
            }
        });
    }

    // --- 4. Submit Review via Ajax ---
    $('#btnSubmitReview').on('click', function () {
        var title = $('#hiddenTitle').val();
        if (!title) {
            alert('독서록을 작성할 책을 1단계에서 먼저 검색하고 선택해 주세요.');
            $('#inputReviewBookSearch').focus();
            return;
        }

        var content = $('#inputReviewContent').val().trim();
        if (!content) {
            alert('독서 감상평 내용을 입력해 주세요.');
            $('#inputReviewContent').focus();
            return;
        }

        var btn = $(this);
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> 등록 중...');

        var reviewData = {
            isbn: $('#hiddenIsbn').val(),
            title: title,
            author: $('#hiddenAuthor').val(),
            publisher: $('#hiddenPublisher').val(),
            coverImageUrl: $('#hiddenCoverUrl').val(),
            description: $('#hiddenDesc').val(),
            reviewerName: $('#inputReviewer').val().trim() || '익명의 독서가',
            rating: parseInt($('#inputReviewRating').val()) || 5,
            quote: $('#inputReviewQuote').val().trim(),
            summary: $('#inputReviewSummary').val().trim(),
            content: content
        };

        $.ajax({
            url: '/Books/CreateReview',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(reviewData),
            success: function (res) {
                btn.prop('disabled', false).html('<i class="bi bi-pencil-square me-1"></i> 독서록 등록하기');
                if (res.success) {
                    showToast(res.message, true);
                    var modalEl = document.getElementById('writeReviewModal');
                    var modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();

                    setTimeout(function () {
                        location.reload();
                    }, 800);
                } else {
                    showToast(res.message, false);
                }
            },
            error: function (xhr) {
                btn.prop('disabled', false).html('<i class="bi bi-pencil-square me-1"></i> 독서록 등록하기');
                var errMsg = '독서록 등록에 실패했습니다.';
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errMsg = xhr.responseJSON.message;
                }
                showToast(errMsg, false);
            }
        });
    });

    // --- 5. Community Like Button (Ajax) ---
    $(document).on('click', '.btn-like-review', function () {
        var btn = $(this);
        var id = btn.data('id');
        var heartIcon = btn.find('i');
        var likeCountEl = btn.find('.like-count');

        $.ajax({
            url: `/Books/Like/${id}`,
            type: 'POST',
            success: function (res) {
                if (res.success && res.data) {
                    likeCountEl.text(res.data.likes);
                    btn.prop('disabled', true).addClass('liked');
                    heartIcon.removeClass('bi-heart').addClass('bi-heart-fill');
                    heartIcon.css('transform', 'scale(1.5)');
                    setTimeout(function () {
                        heartIcon.css('transform', 'scale(1)');
                    }, 200);
                    showToast('이 독서록에 공감했습니다! ❤️', true);
                } else if (res.message) {
                    showToast(res.message, false);
                }
            },
            error: function (xhr) {
                if (xhr.status === 401 || xhr.status === 302) {
                    showToast('공감하려면 로그인이 필요합니다.', false);
                    return;
                }
                showToast('좋아요 처리 중 오류가 발생했습니다.', false);
            }
        });
    });


    // --- 4. Live Markdown Note Editor & Ajax Save ---
    var inputNoteThought = $('#inputNoteThought');
    var noteLivePreview = $('#noteLivePreview');

    if (inputNoteThought.length) {
        inputNoteThought.on('input', function () {
            var text = $(this).val();
            if (!text.trim()) {
                noteLivePreview.html('<em class="text-muted">입력한 내용이 실시간으로 여기에 렌더링됩니다.</em>');
                return;
            }

            var safeText = escapeHtml(text);
            var html = safeText
                .replace(/^### (.*$)/gim, '<h6>$1</h6>')
                .replace(/^## (.*$)/gim, '<h5>$1</h5>')
                .replace(/^# (.*$)/gim, '<h4>$1</h4>')
                .replace(/^&gt;\s+(.*$)/gim, '<blockquote class="blockquote small ps-2 border-start border-2">$1</blockquote>')
                .replace(/\*\*(.*?)\*\*/gim, '<strong>$1</strong>')
                .replace(/\*(.*?)\*/gim, '<em>$1</em>')
                .replace(/\n/gim, '<br />');

            noteLivePreview.html(html);
        });

        $('#btnSaveNoteAjax').on('click', function () {
            var userBookId = parseInt($('#noteUserBookId').val());
            var page = parseInt($('#inputNotePage').val()) || 1;
            var quote = $('#inputNoteQuote').val();
            var thought = $('#inputNoteThought').val();

            if (!thought.trim()) {
                alert('생각 & 감상 내용을 입력해 주세요.');
                return;
            }

            var btn = $(this);
            btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span> 저장 중...');

            $.ajax({
                url: '/Notes/Create',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    userBookId: userBookId,
                    pageNumber: page,
                    quote: quote,
                    thought: thought
                }),
                success: function (res) {
                    btn.prop('disabled', false).html('<i class="bi bi-save"></i> 메모 저장 (Ajax)');
                    if (res.success) {
                        showToast(res.message, true);
                        $('#inputNoteQuote').val('');
                        $('#inputNoteThought').val('');
                        noteLivePreview.html('<em class="text-muted">입력한 내용이 실시간으로 여기에 렌더링됩니다.</em>');
                        $('#emptyNotesPlaceholder').remove();

                        var quoteHtml = res.data.quote ? 
                            `<blockquote class="blockquote fs-6 text-muted ps-3 border-start border-3 my-2 fst-italic">"${res.data.quote}"</blockquote>` : '';

                        var newNoteHtml = `
                            <div class="card border-0 shadow-sm p-3 border-start border-4 border-info" id="note-item-${res.data.id}">
                                <div class="d-flex justify-content-between align-items-center mb-2">
                                    <span class="badge bg-primary">p.${res.data.pageNumber}</span>
                                    <div class="d-flex align-items-center gap-2">
                                        <span class="text-muted small">${res.data.createdAt}</span>
                                        <button type="button" class="btn btn-outline-danger btn-sm py-0 px-1 btn-delete-note" data-id="${res.data.id}" title="메모 삭제">
                                            <i class="bi bi-x"></i>
                                        </button>
                                    </div>
                                </div>
                                ${quoteHtml}
                                <div class="markdown-body small text-dark mt-2">
                                    ${res.data.thoughtHtml}
                                </div>
                            </div>
                        `;

                        $('#notesContainer').prepend(newNoteHtml);
                        var countEl = $('#notesCount');
                        countEl.text(parseInt(countEl.text()) + 1);
                    } else {
                        showToast(res.message, false);
                    }
                },
                error: function () {
                    btn.prop('disabled', false).html('<i class="bi bi-save"></i> 메모 저장 (Ajax)');
                    showToast('메모 저장 중 오류가 발생했습니다.', false);
                }
            });
        });
    }

    // Delete Note via Ajax
    $(document).on('click', '.btn-delete-note', function () {
        if (!confirm('이 독서 메모를 삭제하시겠습니까?')) return;

        var btn = $(this);
        var id = btn.data('id');

        $.ajax({
            url: `/Notes/Delete?id=${id}`,
            type: 'POST',
            success: function (res) {
                if (res.success) {
                    $(`#note-item-${id}`).fadeOut(300, function () {
                        $(this).remove();
                        var countEl = $('#notesCount');
                        var nextCount = Math.max(0, parseInt(countEl.text()) - 1);
                        countEl.text(nextCount);
                    });
                    showToast(res.message, true);
                } else {
                    showToast(res.message, false);
                }
            },
            error: function () {
                showToast('메모 삭제에 실패했습니다.', false);
            }
        });
    });

    // --- 6. Review Comments (Ajax) ---
    var inputCommentContent = $('#inputCommentContent');
    var commentCharCount = $('#commentCharCount');

    if (inputCommentContent.length) {
        inputCommentContent.on('input', function () {
            commentCharCount.text($(this).val().length.toLocaleString());
        });

        $('#btnSaveComment').on('click', function () {
            var userBookId = parseInt($('#commentUserBookId').val());
            var content = inputCommentContent.val().trim();

            if (!content) {
                showToast('댓글 내용을 입력해 주세요.', false);
                inputCommentContent.focus();
                return;
            }

            var btn = $(this);
            btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span> 등록 중...');

            $.ajax({
                url: '/Comments/Create',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ userBookId: userBookId, content: content }),
                success: function (res) {
                    btn.prop('disabled', false).html('<i class="bi bi-send me-1"></i> 댓글 등록');
                    if (!res.success || !res.data) {
                        showToast(res.message || '댓글 등록에 실패했습니다.', false);
                        return;
                    }

                    inputCommentContent.val('');
                    commentCharCount.text('0');
                    $('#emptyCommentsPlaceholder').remove();

                    var initial = res.data.authorName ? escapeHtml(res.data.authorName.substring(0, 1)) : '독';
                    var newCommentHtml = `
                        <div class="comment-item border rounded-3 p-3" id="comment-item-${res.data.id}">
                            <div class="d-flex justify-content-between align-items-start gap-3">
                                <div class="d-flex align-items-center gap-2">
                                    <div class="comment-avatar">${initial}</div>
                                    <div>
                                        <div class="fw-semibold small">${escapeHtml(res.data.authorName)}</div>
                                        <div class="text-muted" style="font-size: .75rem;">${escapeHtml(res.data.createdAt)}</div>
                                    </div>
                                </div>
                                <button type="button" class="btn btn-sm btn-link text-danger p-1 btn-delete-comment"
                                        data-id="${res.data.id}" title="댓글 삭제"><i class="bi bi-trash"></i></button>
                            </div>
                            <div class="comment-content mt-3">${escapeHtml(res.data.content).replace(/\n/g, '<br />')}</div>
                        </div>`;

                    $('#commentsContainer').prepend(newCommentHtml);
                    $('#commentsCount').text((parseInt($('#commentsCount').text()) || 0) + 1);
                    showToast(res.message, true);
                },
                error: function (xhr) {
                    btn.prop('disabled', false).html('<i class="bi bi-send me-1"></i> 댓글 등록');
                    showToast(xhr.status === 401 ? '댓글을 작성하려면 로그인이 필요합니다.' : '댓글 등록 중 오류가 발생했습니다.', false);
                }
            });
        });
    }

    $(document).on('click', '.btn-delete-comment', function () {
        if (!confirm('이 댓글을 삭제하시겠습니까?')) return;
        var id = $(this).data('id');

        $.ajax({
            url: `/Comments/Delete?id=${id}`,
            type: 'POST',
            success: function (res) {
                if (!res.success) {
                    showToast(res.message || '댓글 삭제에 실패했습니다.', false);
                    return;
                }
                $(`#comment-item-${id}`).fadeOut(200, function () {
                    $(this).remove();
                    var countEl = $('#commentsCount');
                    var nextCount = Math.max(0, (parseInt(countEl.text()) || 0) - 1);
                    countEl.text(nextCount);
                    if (nextCount === 0) {
                        $('#commentsContainer').html('<div id="emptyCommentsPlaceholder" class="text-center text-muted py-5"><i class="bi bi-chat-square-text fs-2 opacity-50"></i><p class="small mt-2 mb-0">아직 댓글이 없습니다. 첫 댓글을 남겨보세요.</p></div>');
                    }
                });
                showToast(res.message, true);
            },
            error: function () { showToast('댓글 삭제 중 오류가 발생했습니다.', false); }
        });
    });

    // --- 5. Export to README.md (Ajax) ---
    function triggerExportReadme() {
        $('#txtReadmeContent').val('README.md 마크다운을 생성하고 있습니다...');
        var modal = new bootstrap.Modal($('#exportReadmeModal')[0]);
        modal.show();

        $.ajax({
            url: '/Home/ExportReadme',
            type: 'GET',
            success: function (res) {
                if (res.success) {
                    $('#txtReadmeContent').val(res.data);
                } else {
                    $('#txtReadmeContent').val('마크다운 생성에 실패했습니다.');
                }
            },
            error: function () {
                $('#txtReadmeContent').val('서버와 통신 중 오류가 발생했습니다.');
            }
        });
    }

    $('#btnExportReadmeHeader, #btnExportReadmeHero').on('click', function () {
        triggerExportReadme();
    });

    $('#btnCopyReadmeMarkdown').on('click', function () {
        var content = $('#txtReadmeContent').val();
        if (!content) return;

        navigator.clipboard.writeText(content).then(function () {
            showToast('README 마크다운 코드가 클립보드에 복사되었습니다!', true);
        }).catch(function () {
            showToast('클립보드 복사에 실패했습니다.', false);
        });
    });

});
