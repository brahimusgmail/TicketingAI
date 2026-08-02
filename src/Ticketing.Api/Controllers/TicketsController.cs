namespace Ticketing.Api.Controllers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Entities;
using Ticketing.Shared.Contracts.Tickets;

//[Authorize]
[Route("api/tickets")]
[ApiController]
public class TicketsController : ControllerBase
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUserRepository _userRepository;

    public TicketsController(ITicketRepository ticketRepository, ICategoryRepository categoryRepository, IUserRepository userRepository)
    {
        _ticketRepository = ticketRepository;
        _categoryRepository = categoryRepository;
        _userRepository = userRepository;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TicketResponse>> GetById(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        var item = await _ticketRepository.GetByIdAsync(id, ct);

        if (item is null)
        {
            return NotFound();
        }

        var user = await _userRepository.GetByIdAsync(item.AuthorId, ct);
        var category = await _categoryRepository.GetByIdAsync(item.CategoryId, ct);

        var commentAuthorIds = item.Comments
            .Select(c => c.AuthorId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var commentUsers = await _userRepository.GetByIdsAsync(commentAuthorIds, ct);
        var commentUsersById = commentUsers.ToDictionary(x => x.Id);

        return Ok(ToResponse(item, user, category, commentUsersById));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (page < 1)
        {
            return BadRequest(Resource.PageMustGreaterThanOne);
        }

        if (pageSize is < 1 or > 100)
        {
            return BadRequest(Resource.PageBetweenOneAndHundred);
        }

        var items = await _ticketRepository.GetPagedAsync(page, pageSize, ct);

        // charge toutes les categories
        var categories = await _categoryRepository.GetAllAsync(ct);
        var categoryById = categories.ToDictionary(c => c.Id);

        // charge uniquement les users concernés
        var authorIds = items
            .Select(t => t.AuthorId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var users = await _userRepository.GetByIdsAsync(authorIds!, ct);
        var userById = users.ToDictionary(u => u.Id);

        var result = items.Select(t =>
        {
            userById.TryGetValue(t.AuthorId, out var u);
            categoryById.TryGetValue(t.CategoryId, out var c);
            return ToResponse(t, u, c, null);
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create([FromBody] TicketCreateRequest request, CancellationToken ct)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(Resource.TicketMustHaveTitleMessage);
        }

        if (!request.CategoryId.HasValue)
        {
            return BadRequest("CategoryId is required.");
        }

        if (!await CategoryExistAsync(request.CategoryId.Value, ct))
        {
            return BadRequest(Resource.TicketCategoryNotFoundMessage);
        }

        var authorId = GetUserId();

        var created = await _ticketRepository.InsertAsync(new Ticket { Title = title, CategoryId = request.CategoryId.Value, AuthorId = authorId }, ct);

        var user = authorId != null ? await _userRepository.GetByIdAsync(authorId, ct) : null;

        var category = request.CategoryId != null ? await _categoryRepository.GetByIdAsync(request.CategoryId.Value, ct) : null;
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponse(created, user, category, null));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TicketUpdateRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(Resource.TicketMustHaveTitleMessage);
        }

        if (request.CategoryId == Guid.Empty)
        {
            return BadRequest(Resource.CategoryIdRequiredMessage);
        }

        if (!await CategoryExistAsync(request.CategoryId.Value, ct))
        {
            return BadRequest(Resource.TicketCategoryNotFoundMessage);
        }

        var existing = await _ticketRepository.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        var currentUserId = GetUserId();
        if (existing.AuthorId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        existing.Title = title;
        existing.CategoryId = request.CategoryId.Value;
        var result = await _ticketRepository.UpdateAsync(existing, ct);

        return result ? NoContent() : NotFound();
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TicketResponse>> Patch(
    Guid id,
    [FromBody] TicketUpdateRequest request,
    CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        var ticket = await _ticketRepository.GetByIdAsync(id, ct);

        if (ticket is null)
        {
            return NotFound();
        }

        // update fields
        ticket.Title = request.Title;
        ticket.CategoryId = request.CategoryId.Value;
        ticket.UpdatedAtUtc = DateTime.UtcNow;

        await _ticketRepository.UpdateAsync(ticket, ct);

        var response = new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.AuthorId,
            null,
            ticket.CategoryId,
            null,
            ticket.Status,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.Comments
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new TicketCommentResponse(
                c.Id,
                c.Message,
                c.AuthorId,
                null,
                c.CreatedAtUtc))
            .ToList());

        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        var existing = await _ticketRepository.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        var currentUserId = GetUserId();
        if (existing.AuthorId != currentUserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var result = await _ticketRepository.DeleteAsync(id, ct);

        return result ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddTicketCommentRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(Resource.MessageRequiredMessage);
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userIdClaim)
            || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = id,
            Message = request.Message.Trim(),
            AuthorId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var ok = await _ticketRepository.AddCommentAsync(id, comment, ct);
        if (!ok)
        {
            return NotFound(Resource.TicketNotFoundMessage);
        }

        return NoContent();
    }

    [HttpPatch("{id}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Resource.TicketIdRequiredMessage);
        }

        var ok = await _ticketRepository.CloseAsync(id, ct);

        if (!ok)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<bool> CategoryExistAsync(Guid id, CancellationToken ct)
    {
        var categoryExist = await _categoryRepository.GetByIdAsync(id, ct);

        return categoryExist is not null;
    }

    private TicketResponse ToResponse(
    Ticket item,
    User? user,
    Category? category,
    Dictionary<Guid, User>? commentUsersById = null)
    {
        commentUsersById ??= new Dictionary<Guid, User>();

        TicketAuthorResponse? author = null;
        TicketCategoryResponse? categoryItem = null;

        if (user is not null)
        {
            author = new TicketAuthorResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Roles);
        }

        if (category is not null)
        {
            categoryItem = new TicketCategoryResponse(
                category.Id,
                category.Name);
        }

        List<TicketCommentResponse> comments = item.Comments
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c =>
            {
                commentUsersById.TryGetValue(c.AuthorId, out var commentUser);

                TicketAuthorResponse? commentAuthor = null;

                if (commentUser is not null)
                {
                    commentAuthor = new TicketAuthorResponse(
                        commentUser.Id,
                        commentUser.Email,
                        commentUser.FullName,
                        commentUser.Roles);
                }

                return new TicketCommentResponse(
                    c.Id,
                    c.Message,
                    c.AuthorId,
                    commentAuthor,
                    c.CreatedAtUtc);
            })
            .ToList();

        return new TicketResponse(
            Id: item.Id,
            Title: item.Title,
            AuthorId: item.AuthorId,
            Author: author,
            CategoryId: item.CategoryId,
            Category: categoryItem,
            Statut: item.Status,
            CreatedAtUtc: item.CreatedAtUtc,
            UpdatedAtUtc: item.UpdatedAtUtc,
            Comments: comments);
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
        {
            throw new UnauthorizedAccessException("User id claim not found or invalid.");
        }

        return guid;
    }
}
