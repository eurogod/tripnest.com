using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

internal static class EnumParse
{
    public static T Required<T>(string value, string field) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ValidationException($"Invalid {field}: {value}");
}

public class ExchangeService : IExchangeService
{
    private readonly IRepository<ExchangePost> _postRepository;
    private readonly IRepository<ExchangeReply> _replyRepository;
    private readonly IRepository<User> _userRepository;

    public ExchangeService(
        IRepository<ExchangePost> postRepository,
        IRepository<ExchangeReply> replyRepository,
        IRepository<User> userRepository)
    {
        _postRepository = postRepository;
        _replyRepository = replyRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResult<ExchangePostResponse>> GetPostsAsync(int page, int pageSize)
    {
        var posts = (await _postRepository.GetAllAsync())
            .OrderByDescending(p => p.Pinned)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        var authors = (await _userRepository.FindAsync(u => authorIds.Contains(u.Id)))
            .ToDictionary(u => u.Id, u => u.FullName);
        var replyCounts = (await _replyRepository.GetAllAsync())
            .GroupBy(r => r.PostId)
            .ToDictionary(g => g.Key, g => g.Count());

        var mapped = posts.Select(p => new ExchangePostResponse
        {
            Id = p.Id,
            AuthorId = p.AuthorId,
            AuthorName = authors.GetValueOrDefault(p.AuthorId),
            Title = p.Title,
            Body = p.Body,
            Category = p.Category,
            Pinned = p.Pinned,
            ReplyCount = replyCounts.GetValueOrDefault(p.Id),
            CreatedAt = p.CreatedAt
        }).ToList();

        return Paging.Page(mapped, page, pageSize);
    }

    public async Task<ExchangePostResponse> CreatePostAsync(CreateExchangePostRequest request, string authorId)
    {
        var post = new ExchangePost
        {
            AuthorId = authorId,
            Title = request.Title,
            Body = request.Body,
            Category = EnumParse.Required<ExchangeCategory>(request.Category, "category")
        };
        await _postRepository.AddAsync(post);
        await _postRepository.SaveChangesAsync();

        var author = await _userRepository.GetByIdAsync(authorId);
        return new ExchangePostResponse
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            AuthorName = author?.FullName,
            Title = post.Title,
            Body = post.Body,
            Category = post.Category,
            Pinned = post.Pinned,
            ReplyCount = 0,
            CreatedAt = post.CreatedAt
        };
    }

    public async Task<List<ExchangeReplyResponse>> GetRepliesAsync(string postId)
    {
        _ = await _postRepository.GetByIdAsync(postId) ?? throw new NotFoundException("Post");
        var replies = (await _replyRepository.FindAsync(r => r.PostId == postId))
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var authorIds = replies.Select(r => r.AuthorId).Distinct().ToList();
        var authors = (await _userRepository.FindAsync(u => authorIds.Contains(u.Id)))
            .ToDictionary(u => u.Id, u => u.FullName);

        return replies.Select(r => new ExchangeReplyResponse
        {
            Id = r.Id,
            AuthorId = r.AuthorId,
            AuthorName = authors.GetValueOrDefault(r.AuthorId),
            Body = r.Body,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<ExchangeReplyResponse> AddReplyAsync(string postId, CreateExchangeReplyRequest request, string authorId)
    {
        _ = await _postRepository.GetByIdAsync(postId) ?? throw new NotFoundException("Post");
        var reply = new ExchangeReply { PostId = postId, AuthorId = authorId, Body = request.Body };
        await _replyRepository.AddAsync(reply);
        await _replyRepository.SaveChangesAsync();

        var author = await _userRepository.GetByIdAsync(authorId);
        return new ExchangeReplyResponse
        {
            Id = reply.Id,
            AuthorId = reply.AuthorId,
            AuthorName = author?.FullName,
            Body = reply.Body,
            CreatedAt = reply.CreatedAt
        };
    }
}

public class ResourceService : IResourceService
{
    private readonly IRepository<ResourceItem> _repository;

    public ResourceService(IRepository<ResourceItem> repository) => _repository = repository;

    public async Task<List<ResourceResponse>> GetAllAsync()
    {
        var items = (await _repository.GetAllAsync()).OrderByDescending(r => r.CreatedAt);
        return items.Select(Map).ToList();
    }

    public async Task<ResourceResponse> CreateAsync(CreateResourceRequest request)
    {
        var item = new ResourceItem
        {
            Title = request.Title,
            Description = request.Description,
            Category = EnumParse.Required<ResourceCategory>(request.Category, "category"),
            Format = request.Format,
            Url = request.Url
        };
        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();
        return Map(item);
    }

    private static ResourceResponse Map(ResourceItem r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        Category = r.Category,
        Format = r.Format,
        Url = r.Url,
        CreatedAt = r.CreatedAt
    };
}

public class HostTaskService : IHostTaskService
{
    private readonly IRepository<HostTask> _repository;

    public HostTaskService(IRepository<HostTask> repository) => _repository = repository;

    public async Task<PagedResult<HostTaskResponse>> GetMineAsync(string landlordId, int page, int pageSize)
    {
        var tasks = (await _repository.FindAsync(t => t.LandlordId == landlordId))
            .OrderByDescending(t => t.CreatedAt)
            .Select(Map)
            .ToList();
        return Paging.Page(tasks, page, pageSize);
    }

    public async Task<HostTaskResponse> CreateAsync(CreateHostTaskRequest request, string landlordId)
    {
        var task = new HostTask
        {
            LandlordId = landlordId,
            Title = request.Title,
            PropertyId = string.IsNullOrWhiteSpace(request.PropertyId) ? null : request.PropertyId,
            Type = EnumParse.Required<HostTaskType>(request.Type, "type"),
            Priority = EnumParse.Required<HostTaskPriority>(request.Priority, "priority"),
            DueDate = request.DueDate,
            Assignee = request.Assignee
        };
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();
        return Map(task);
    }

    public async Task<HostTaskResponse> UpdateAsync(string id, UpdateHostTaskRequest request, string landlordId)
    {
        var task = await LoadOwnedAsync(id, landlordId);

        if (request.Title is not null) task.Title = request.Title;
        if (request.Type is not null) task.Type = EnumParse.Required<HostTaskType>(request.Type, "type");
        if (request.Priority is not null) task.Priority = EnumParse.Required<HostTaskPriority>(request.Priority, "priority");
        if (request.Status is not null) task.Status = EnumParse.Required<HostTaskStatus>(request.Status, "status");
        if (request.DueDate is not null) task.DueDate = request.DueDate;
        if (request.Assignee is not null) task.Assignee = request.Assignee;

        await _repository.UpdateAsync(task);
        await _repository.SaveChangesAsync();
        return Map(task);
    }

    public async Task DeleteAsync(string id, string landlordId)
    {
        var task = await LoadOwnedAsync(id, landlordId);
        await _repository.DeleteAsync(task);
        await _repository.SaveChangesAsync();
    }

    private async Task<HostTask> LoadOwnedAsync(string id, string landlordId)
    {
        var task = await _repository.GetByIdAsync(id) ?? throw new NotFoundException("Task");
        if (task.LandlordId != landlordId)
            throw new ForbiddenException("This task is not yours");
        return task;
    }

    private static HostTaskResponse Map(HostTask t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        PropertyId = t.PropertyId,
        Type = t.Type,
        Priority = t.Priority,
        Status = t.Status,
        DueDate = t.DueDate,
        Assignee = t.Assignee,
        CreatedAt = t.CreatedAt
    };
}

public class TeamService : ITeamService
{
    private readonly IRepository<TeamMember> _repository;

    public TeamService(IRepository<TeamMember> repository) => _repository = repository;

    public async Task<List<TeamMemberResponse>> GetMineAsync(string landlordId)
    {
        var members = (await _repository.FindAsync(m => m.LandlordId == landlordId))
            .OrderByDescending(m => m.CreatedAt);
        return members.Select(Map).ToList();
    }

    public async Task<TeamMemberResponse> InviteAsync(InviteTeamMemberRequest request, string landlordId)
    {
        var member = new TeamMember
        {
            LandlordId = landlordId,
            Name = request.Name,
            Email = request.Email,
            Role = EnumParse.Required<TeamMemberRole>(request.Role, "role"),
            Status = TeamMemberStatus.Invited,
            PropertiesCount = request.PropertiesCount
        };
        await _repository.AddAsync(member);
        await _repository.SaveChangesAsync();
        return Map(member);
    }

    public async Task<TeamMemberResponse> UpdateAsync(string id, UpdateTeamMemberRequest request, string landlordId)
    {
        var member = await LoadOwnedAsync(id, landlordId);

        if (request.Role is not null) member.Role = EnumParse.Required<TeamMemberRole>(request.Role, "role");
        if (request.Status is not null) member.Status = EnumParse.Required<TeamMemberStatus>(request.Status, "status");
        if (request.PropertiesCount is not null) member.PropertiesCount = request.PropertiesCount.Value;

        await _repository.UpdateAsync(member);
        await _repository.SaveChangesAsync();
        return Map(member);
    }

    public async Task RemoveAsync(string id, string landlordId)
    {
        var member = await LoadOwnedAsync(id, landlordId);
        await _repository.DeleteAsync(member);
        await _repository.SaveChangesAsync();
    }

    private async Task<TeamMember> LoadOwnedAsync(string id, string landlordId)
    {
        var member = await _repository.GetByIdAsync(id) ?? throw new NotFoundException("Team member");
        if (member.LandlordId != landlordId)
            throw new ForbiddenException("This team member is not yours");
        return member;
    }

    private static TeamMemberResponse Map(TeamMember m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Email = m.Email,
        Role = m.Role,
        Status = m.Status,
        PropertiesCount = m.PropertiesCount,
        LastActiveAt = m.LastActiveAt,
        CreatedAt = m.CreatedAt
    };
}
