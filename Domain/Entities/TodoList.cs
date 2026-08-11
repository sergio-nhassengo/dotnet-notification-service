using Domain.Common;

namespace Domain.Entities;

public class TodoList : BaseAuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public IList<TodoItem> Items { get; private set; } = [];
}
